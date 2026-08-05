#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public enum E_GRAPHICS_BAKE_DEPENDENCY_KIND
	{
		LIGHTMAP_SCENE,
		REFLECTION_PROBE,
		ADAPTIVE_PROBE_VOLUME
	}

	public sealed class UnityGraphicsMcpBakeTargetInput
	{
		public string scenePath { get; set; }
		public string[] dependencyKinds { get; set; }
		public string[] reflectionProbeObjectIds { get; set; }
	}

	internal sealed class UnityGraphicsMcpDirtyDependencyRecord
	{
		public string ScenePath { get; set; }
		public int SceneHandle { get; set; }
		public long DirtySerial { get; set; }
		public DateTime LastDirtyUtc { get; set; }
		public HashSet<string> Kinds { get; } = new HashSet<string>(StringComparer.Ordinal);
		public HashSet<string> ReflectionProbeObjectIds { get; } =
			new HashSet<string>(StringComparer.Ordinal);
	}

	internal sealed class UnityGraphicsMcpBakeSceneBaseline
	{
		public int SceneHandle { get; set; }
		public string ScenePath { get; set; }
		public bool WasDirty { get; set; }
		public string ContentDigest { get; set; }
	}

	internal sealed class UnityGraphicsMcpPreparedBakeDependency
	{
		public string DependencyId { get; set; }
		public string Kind { get; set; }
		public string ScenePath { get; set; }
		public int SceneHandle { get; set; }
		public string ObjectId { get; set; }
		public string OutputAssetPath { get; set; }
		public string BaselineDigest { get; set; }
		public string Backend { get; set; }
	}

	internal sealed class UnityGraphicsMcpExecutableBakePlan
	{
		public string PlanId { get; set; }
		public long Revision { get; set; }
		public long DirtySetSerial { get; set; }
		public DateTime CreatedUtc { get; set; }
		public DateTime ExpiresUtc { get; set; }
		public string ApprovalTokenHash { get; set; }
		public string DiffDigest { get; set; }
		public bool Consumed { get; set; }
		public List<UnityGraphicsMcpBakeSceneBaseline> ContributingScenes { get; set; } =
			new List<UnityGraphicsMcpBakeSceneBaseline>();
		public List<UnityGraphicsMcpPreparedBakeDependency> Dependencies { get; set; } =
			new List<UnityGraphicsMcpPreparedBakeDependency>();
	}

	[InitializeOnLoad]
	internal static class UnityGraphicsMcpDependencyBakeSession
	{
		private const int MAX_BAKE_PLAN_COUNT = 8;
		private static readonly TimeSpan BAKE_PLAN_LIFETIME = TimeSpan.FromMinutes(10.0);
		private static readonly Dictionary<string, UnityGraphicsMcpExecutableBakePlan> _plans =
			new Dictionary<string, UnityGraphicsMcpExecutableBakePlan>();
		private static readonly Dictionary<string, UnityGraphicsMcpDirtyDependencyRecord>
			_dirtyDependencies =
				new Dictionary<string, UnityGraphicsMcpDirtyDependencyRecord>(
					StringComparer.Ordinal);

		private static long _dirtySerial = 1;
		private static int _ownedBakeDepth;

		internal static Func<Scene, bool> SceneBakeOverrideForTests { get; set; }
		internal static Func<ReflectionProbe, string, bool>
			ReflectionProbeBakeOverrideForTests { get; set; }

		static UnityGraphicsMcpDependencyBakeSession()
		{
			EditorSceneManager.sceneDirtied += TrackDirtyScene;
			EditorSceneManager.sceneClosed += RemoveClosedScene;
			EditorSceneManager.sceneOpened += OnSceneOpened;
			EditorApplication.playModeStateChanged += state => Clear();
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			CompilationPipeline.compilationStarted += context => Clear();
			EditorApplication.quitting += Clear;
		}

		public static long DirtySerial => _dirtySerial;

		public static string StorePlan(UnityGraphicsMcpExecutableBakePlan plan)
		{
			RemoveExpiredPlans();
			RemoveOldestPlanWhenFull();

			plan.PlanId = UnityGraphicsMcpSession.SessionId +
				":bake-plan:" + Guid.NewGuid().ToString("N");
			plan.CreatedUtc = DateTime.UtcNow;
			plan.ExpiresUtc = plan.CreatedUtc + BAKE_PLAN_LIFETIME;
			_plans[plan.PlanId] = plan;
			return plan.PlanId;
		}

		public static bool TryGetPlan(
			string planId,
			long expectedRevision,
			string approvalToken,
			out UnityGraphicsMcpExecutableBakePlan plan,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			plan = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;
			RemoveExpiredPlans();

			if (string.IsNullOrWhiteSpace(planId) ||
				!planId.StartsWith(
					UnityGraphicsMcpSession.SessionId + ":bake-plan:",
					StringComparison.Ordinal) ||
				!_plans.TryGetValue(planId, out plan))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Bake Planが現在のEditor Sessionに存在しないか有効期限切れです。";
				return false;
			}

			if (plan.Consumed)
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "Bake Planは既に使用済みです。";
				return false;
			}

			if (expectedRevision != UnityGraphicsMcpSession.Revision ||
				plan.Revision != UnityGraphicsMcpSession.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Bake Plan作成後にEditor Revisionが変更されました。";
				return false;
			}

			if (string.IsNullOrWhiteSpace(approvalToken) ||
				!string.Equals(
					plan.ApprovalTokenHash,
					UnityGraphicsMcpSaveEvaluationSession.HashText(approvalToken),
					StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "Bake承認Tokenが不足しているか一致しません。";
				return false;
			}

			return true;
		}

		public static void ConsumePlan(UnityGraphicsMcpExecutableBakePlan plan)
		{
			if (plan != null)
			{
				plan.Consumed = true;
			}
		}

		public static void EnsureCurrentlyDirtyScenesTracked()
		{
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (scene.IsValid() && scene.isLoaded && scene.isDirty)
				{
					TrackDirtyScene(scene);
				}
			}
		}

		public static bool HasDirtyDependency(
			string scenePath,
			string dependencyKind,
			string objectId)
		{
			UnityGraphicsMcpDirtyDependencyRecord record;
			if (!_dirtyDependencies.TryGetValue(scenePath, out record) ||
				!record.Kinds.Contains(dependencyKind))
			{
				return false;
			}

			return dependencyKind !=
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.REFLECTION_PROBE.ToString() ||
				(!string.IsNullOrWhiteSpace(objectId) &&
				 record.ReflectionProbeObjectIds.Contains(objectId));
		}

		public static bool TryGetDirtyRecord(
			string scenePath,
			out UnityGraphicsMcpDirtyDependencyRecord record)
		{
			return _dirtyDependencies.TryGetValue(scenePath, out record);
		}

		public static void ClearCompletedDependency(
			UnityGraphicsMcpPreparedBakeDependency dependency)
		{
			UnityGraphicsMcpDirtyDependencyRecord record;
			if (dependency == null ||
				!_dirtyDependencies.TryGetValue(dependency.ScenePath, out record))
			{
				return;
			}

			if (dependency.Kind ==
				E_GRAPHICS_BAKE_DEPENDENCY_KIND.REFLECTION_PROBE.ToString())
			{
				record.ReflectionProbeObjectIds.Remove(dependency.ObjectId);
				if (record.ReflectionProbeObjectIds.Count == 0)
				{
					record.Kinds.Remove(dependency.Kind);
				}
			}
			else
			{
				record.Kinds.Remove(dependency.Kind);
			}

			if (record.Kinds.Count == 0)
			{
				_dirtyDependencies.Remove(dependency.ScenePath);
			}

			_dirtySerial++;
		}

		public static void BeginOwnedBake()
		{
			_ownedBakeDepth++;
		}

		public static void EndOwnedBake()
		{
			_ownedBakeDepth = Math.Max(0, _ownedBakeDepth - 1);
		}

		public static void ClearForTests()
		{
			Clear();
		}

		public static void TrackDirtySceneForTests(Scene scene)
		{
			TrackDirtyScene(scene);
		}

		public static bool HasDirtySceneForTests(string scenePath)
		{
			return _dirtyDependencies.ContainsKey(scenePath);
		}

		private static void TrackDirtyScene(Scene scene)
		{
			if (_ownedBakeDepth > 0 ||
				!scene.IsValid() ||
				!scene.isLoaded ||
				string.IsNullOrWhiteSpace(scene.path))
			{
				return;
			}

			bool dependencySetChanged = false;
			UnityGraphicsMcpDirtyDependencyRecord record;
			if (!_dirtyDependencies.TryGetValue(scene.path, out record))
			{
				record = new UnityGraphicsMcpDirtyDependencyRecord
				{
					ScenePath = scene.path
				};
				_dirtyDependencies[scene.path] = record;
				dependencySetChanged = true;
			}

			if (record.SceneHandle != scene.handle)
			{
				record.SceneHandle = scene.handle;
				dependencySetChanged = true;
			}

			record.LastDirtyUtc = DateTime.UtcNow;
			dependencySetChanged |= record.Kinds.Add(
				E_GRAPHICS_BAKE_DEPENDENCY_KIND.LIGHTMAP_SCENE.ToString());

			foreach (ReflectionProbe probe in scene
				.GetRootGameObjects()
				.SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>(true)))
			{
				if (probe.mode != ReflectionProbeMode.Baked)
				{
					continue;
				}

				string objectId = GlobalObjectId.GetGlobalObjectIdSlow(probe).ToString();
				if (!string.IsNullOrWhiteSpace(objectId))
				{
					dependencySetChanged |= record.Kinds.Add(
						E_GRAPHICS_BAKE_DEPENDENCY_KIND.REFLECTION_PROBE.ToString());
					dependencySetChanged |=
						record.ReflectionProbeObjectIds.Add(objectId);
				}
			}

			if (SceneContainsAdaptiveProbeVolume(scene))
			{
				dependencySetChanged |= record.Kinds.Add(
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.ADAPTIVE_PROBE_VOLUME.ToString());
			}

			if (dependencySetChanged)
			{
				record.DirtySerial = ++_dirtySerial;
			}
		}

		private static bool SceneContainsAdaptiveProbeVolume(Scene scene)
		{
			foreach (Component component in scene
				.GetRootGameObjects()
				.SelectMany(root => root.GetComponentsInChildren<Component>(true)))
			{
				if (component == null)
				{
					continue;
				}

				string fullName = component.GetType().FullName ?? string.Empty;
				if (fullName.IndexOf("ProbeVolume", StringComparison.OrdinalIgnoreCase) >= 0 &&
					fullName.IndexOf("ReflectionProbe", StringComparison.OrdinalIgnoreCase) < 0)
				{
					return true;
				}
			}

			return false;
		}

		private static void RemoveClosedScene(Scene scene)
		{
			if (!string.IsNullOrWhiteSpace(scene.path) &&
				_dirtyDependencies.Remove(scene.path))
			{
				_dirtySerial++;
			}
		}

		private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
		{
			if (scene.isDirty)
			{
				TrackDirtyScene(scene);
			}
		}

		private static void RemoveExpiredPlans()
		{
			DateTime now = DateTime.UtcNow;
			foreach (string id in _plans
				.Where(pair => pair.Value.ExpiresUtc <= now)
				.Select(pair => pair.Key)
				.ToArray())
			{
				_plans.Remove(id);
			}
		}

		private static void RemoveOldestPlanWhenFull()
		{
			while (_plans.Count >= MAX_BAKE_PLAN_COUNT)
			{
				string oldestId = _plans
					.OrderBy(pair => pair.Value.CreatedUtc)
					.First()
					.Key;
				_plans.Remove(oldestId);
			}
		}

		private static void Clear()
		{
			_plans.Clear();
			_dirtyDependencies.Clear();
			SceneBakeOverrideForTests = null;
			ReflectionProbeBakeOverrideForTests = null;
			_ownedBakeDepth = 0;
			_dirtySerial++;
		}
	}

	/// <summary>
	/// Dirty Dependency Setと承認済みDependency限定Bakeを所有します。
	/// </summary>
	public static partial class UnityGraphicsMcpInspection
	{
		private const string BAKE_MODE_EXPLICIT_DEPENDENCIES =
			"EXPLICIT_DEPENDENCIES";
		private const int MAX_BAKE_TARGET_COUNT = 8;
		private const int MAX_BAKE_DEPENDENCY_COUNT = 32;

		public static UnityGraphicsMcpToolResult PrepareBakePlan(
			string requestId,
			long? expectedRevision,
			UnityGraphicsMcpBakeTargetInput[] targets)
		{
			return ExecuteReadOnly(
				"graphics.prepare_bake_plan",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.prepare_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					if (expectedRevision.Value != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult(
							"graphics.prepare_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"expectedRevisionが現在のEditor Revisionと一致しません。",
							null);
					}

					if (targets == null ||
						targets.Length == 0 ||
						targets.Length > MAX_BAKE_TARGET_COUNT)
					{
						return CreateResult(
							"graphics.prepare_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Bake Targetは1～8件で明示指定してください。",
							null);
					}

					UnityGraphicsMcpDependencyBakeSession.EnsureCurrentlyDirtyScenesTracked();

					List<UnityGraphicsMcpBakeSceneBaseline> contributingScenes;
					UnityGraphicsMcpToolResult sceneSetFailure;
					if (!TryCaptureDependencyBakeContributingScenes(
						requestId,
						out contributingScenes,
						out sceneSetFailure))
					{
						return sceneSetFailure;
					}

					List<UnityGraphicsMcpPreparedBakeDependency> dependencies =
						new List<UnityGraphicsMcpPreparedBakeDependency>();
					HashSet<string> dependencyKeys =
						new HashSet<string>(StringComparer.Ordinal);

					foreach (UnityGraphicsMcpBakeTargetInput target in targets)
					{
						UnityGraphicsMcpToolResult targetFailure;
						if (!TryPrepareDependencyBakeTarget(
							requestId,
							target,
							dependencies,
							dependencyKeys,
							out targetFailure))
						{
							return targetFailure;
						}
					}

					if (dependencies.Count == 0 ||
						dependencies.Count > MAX_BAKE_DEPENDENCY_COUNT)
					{
						return CreateResult(
							"graphics.prepare_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"ExecutableなBake Dependencyは1～32件である必要があります。",
							new Dictionary<string, object>
							{
								{ "dependencyCount", dependencies.Count }
							});
					}

					string approvalToken = Guid.NewGuid().ToString("N") +
						Guid.NewGuid().ToString("N");
					UnityGraphicsMcpExecutableBakePlan plan =
						new UnityGraphicsMcpExecutableBakePlan
						{
							Revision = expectedRevision.Value,
							DirtySetSerial =
								UnityGraphicsMcpDependencyBakeSession.DirtySerial,
							ApprovalTokenHash =
								UnityGraphicsMcpSaveEvaluationSession.HashText(approvalToken),
							ContributingScenes = contributingScenes,
							Dependencies = dependencies
						};
					plan.DiffDigest = BuildDependencyBakePlanDigest(plan);
					UnityGraphicsMcpDependencyBakeSession.StorePlan(plan);

					return CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Dirty Dependency SetをRead-onlyで固定し、別承認のDependency限定Bake Planを作成しました。",
						new Dictionary<string, object>
						{
							{ "planId", plan.PlanId },
							{ "expectedRevision", plan.Revision },
							{ "approvalToken", approvalToken },
							{
								"approvalTokenExpiresUtc",
								plan.ExpiresUtc.ToString("O", CultureInfo.InvariantCulture)
							},
							{ "diffDigest", plan.DiffDigest },
							{ "dirtySetSerial", plan.DirtySetSerial },
							{
								"contributingScenes",
								BuildDependencyBakeScenePreviews(plan.ContributingScenes)
							},
							{
								"dependencies",
								BuildDependencyBakeDependencyPreviews(plan.Dependencies)
							},
							{
								"bakeMode",
								BAKE_MODE_EXPLICIT_DEPENDENCIES
							},
							{ "bakePerformed", false },
							{ "savePerformed", false },
							{ "undoAvailable", false },
							{ "automaticRollback", false }
						});
				});
		}

		public static UnityGraphicsMcpToolResult BakeDependencies(
			string requestId,
			string planId,
			long? expectedRevision,
			string approvalToken,
			string bakeMode)
		{
			return ExecuteSaveEvaluationPersistentOperation(
				"graphics.bake_dependencies",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.bake_dependencies",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					if (!string.Equals(
						string.IsNullOrWhiteSpace(bakeMode)
							? string.Empty
							: bakeMode.Trim(),
						BAKE_MODE_EXPLICIT_DEPENDENCIES,
						StringComparison.OrdinalIgnoreCase))
					{
						return CreateResult(
							"graphics.bake_dependencies",
							requestId,
							E_MCP_TOOL_STATUS.UNSUPPORTED,
							"利用できるbakeModeはEXPLICIT_DEPENDENCIESだけです。",
							null);
					}

					if (EditorApplication.isPlayingOrWillChangePlaymode ||
						Application.isPlaying)
					{
						return CreateResult(
							"graphics.bake_dependencies",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"BakeはEdit Modeでのみ実行できます。",
							null);
					}

					if (Lightmapping.isRunning)
					{
						return CreateResult(
							"graphics.bake_dependencies",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"別のLightmapping Bakeが実行中です。",
							null);
					}

					UnityGraphicsMcpExecutableBakePlan plan;
					E_MCP_TOOL_STATUS failureStatus;
					string failureMessage;
					if (!UnityGraphicsMcpDependencyBakeSession.TryGetPlan(
						planId,
						expectedRevision.Value,
						approvalToken,
						out plan,
						out failureStatus,
						out failureMessage))
					{
						return CreateResult(
							"graphics.bake_dependencies",
							requestId,
							failureStatus,
							failureMessage,
							new Dictionary<string, object>
							{
								{ "planId", planId },
								{ "currentRevision", UnityGraphicsMcpSession.Revision }
							});
					}

					List<UnityGraphicsMcpIssue> staleIssues;
					if (!ValidateDependencyBakePlan(plan, out staleIssues) ||
						!string.Equals(
							plan.DiffDigest,
							BuildDependencyBakePlanDigest(plan),
							StringComparison.Ordinal))
					{
						UnityGraphicsMcpToolResult staleResult = CreateResult(
							"graphics.bake_dependencies",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"Bake Plan作成後にLoaded Scene、DependencyまたはDirty Setが変更されました。",
							null);
						staleResult.issues.AddRange(staleIssues);
						return staleResult;
					}

					List<UnityGraphicsMcpIssue> preflightIssues;
					if (!PreflightDependencyBakeDependencies(
						plan.Dependencies,
						out preflightIssues))
					{
						UnityGraphicsMcpToolResult unsupportedResult = CreateResult(
							"graphics.bake_dependencies",
							requestId,
							E_MCP_TOOL_STATUS.BACKEND_NOT_IMPLEMENTED,
							"全Dependencyを安全に実行できるNative Bake Backendが揃っていません。",
							null);
						unsupportedResult.issues.AddRange(preflightIssues);
						return unsupportedResult;
					}

					return ExecuteDependencyBakePlan(requestId, plan);
				});
		}

		private static UnityGraphicsMcpToolResult ExecuteDependencyBakePlan(
			string requestId,
			UnityGraphicsMcpExecutableBakePlan plan)
		{
			List<string> completedDependencyIds = new List<string>();
			Dictionary<string, object> failedDependency = null;
			long startRevision = UnityGraphicsMcpSession.Revision;

			UnityGraphicsMcpDependencyBakeSession.ConsumePlan(plan);
			UnityGraphicsMcpDependencyBakeSession.BeginOwnedBake();

			try
			{
				foreach (UnityGraphicsMcpPreparedBakeDependency dependency
					in plan.Dependencies)
				{
					bool succeeded;
					string failureMessage;
					if (!TryExecuteDependencyBakeDependency(
						dependency,
						out succeeded,
						out failureMessage) ||
						!succeeded)
					{
						failedDependency = new Dictionary<string, object>
						{
							{ "dependencyId", dependency.DependencyId },
							{ "kind", dependency.Kind },
							{ "scenePath", dependency.ScenePath },
							{ "objectId", dependency.ObjectId },
							{ "message", failureMessage }
						};
						break;
					}

					completedDependencyIds.Add(dependency.DependencyId);
					UnityGraphicsMcpDependencyBakeSession.ClearCompletedDependency(
						dependency);
				}
			}
			finally
			{
				UnityGraphicsMcpDependencyBakeSession.EndOwnedBake();
			}

			if (completedDependencyIds.Count > 0 &&
				startRevision == UnityGraphicsMcpSession.Revision)
			{
				UnityGraphicsMcpSession.NotifyMutationApplied();
			}

			E_MCP_TOOL_STATUS status = failedDependency == null
				? E_MCP_TOOL_STATUS.SUCCESS
				: completedDependencyIds.Count > 0
					? E_MCP_TOOL_STATUS.PARTIAL
					: E_MCP_TOOL_STATUS.FAILED;

			string summary = failedDependency == null
				? "明示承認されたDependencyだけを同期Bakeしました。"
				: "Bakeの一部または全部が失敗しました。完了済みBakeは自動Rollbackされません。";

			return CreateResult(
				"graphics.bake_dependencies",
				requestId,
				status,
				summary,
				new Dictionary<string, object>
				{
					{ "planId", plan.PlanId },
					{ "bakeMode", BAKE_MODE_EXPLICIT_DEPENDENCIES },
					{ "requestedDependencyCount", plan.Dependencies.Count },
					{ "completedDependencyIds", completedDependencyIds },
					{ "failedDependency", failedDependency },
					{ "bakePerformed", completedDependencyIds.Count > 0 },
					{ "savePerformed", false },
					{ "undoAvailable", false },
					{ "automaticRollback", false },
					{ "revision", UnityGraphicsMcpSession.Revision }
				});
		}

		private static bool TryCaptureDependencyBakeContributingScenes(
			string requestId,
			out List<UnityGraphicsMcpBakeSceneBaseline> scenes,
			out UnityGraphicsMcpToolResult failure)
		{
			scenes = new List<UnityGraphicsMcpBakeSceneBaseline>();
			failure = null;

			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (!scene.IsValid() || !scene.isLoaded)
				{
					continue;
				}

				string path = NormalizeSaveEvaluationSceneAssetPath(scene.path);
				if (!IsSupportedSaveEvaluationSceneAssetPath(path))
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"全てのLoaded SceneがAssets配下の保存済みSceneである必要があります。",
						new Dictionary<string, object>
						{
							{ "sceneHandle", scene.handle },
							{ "scenePath", path }
						});
					return false;
				}

				scenes.Add(
					new UnityGraphicsMcpBakeSceneBaseline
					{
						SceneHandle = scene.handle,
						ScenePath = scene.path,
						WasDirty = scene.isDirty,
						ContentDigest = BuildSaveEvaluationSceneContentDigest(scene)
					});
			}

			if (scenes.Count == 0)
			{
				failure = CreateResult(
					"graphics.prepare_bake_plan",
					requestId,
					E_MCP_TOOL_STATUS.INVALID_REQUEST,
					"Loaded Sceneが存在しません。",
					null);
				return false;
			}

			scenes = scenes
				.OrderBy(scene => scene.ScenePath, StringComparer.Ordinal)
				.ToList();
			return true;
		}

		private static bool TryPrepareDependencyBakeTarget(
			string requestId,
			UnityGraphicsMcpBakeTargetInput target,
			List<UnityGraphicsMcpPreparedBakeDependency> dependencies,
			HashSet<string> dependencyKeys,
			out UnityGraphicsMcpToolResult failure)
		{
			failure = null;
			if (target == null ||
				string.IsNullOrWhiteSpace(target.scenePath) ||
				target.dependencyKinds == null ||
				target.dependencyKinds.Length == 0)
			{
				failure = CreateResult(
					"graphics.prepare_bake_plan",
					requestId,
					E_MCP_TOOL_STATUS.INVALID_REQUEST,
					"各Bake TargetへscenePathとdependencyKindsを指定してください。",
					null);
				return false;
			}

			string scenePath = NormalizeSaveEvaluationSceneAssetPath(target.scenePath);
			Scene scene;
			if (!IsSupportedSaveEvaluationSceneAssetPath(scenePath) ||
				!TryResolveSaveEvaluationLoadedScene(scenePath, out scene))
			{
				failure = CreateResult(
					"graphics.prepare_bake_plan",
					requestId,
					E_MCP_TOOL_STATUS.INVALID_REQUEST,
					"scenePathはAssets配下の既存Loaded Sceneを指定してください。",
					new Dictionary<string, object>
					{
						{ "scenePath", scenePath }
					});
				return false;
			}

			UnityGraphicsMcpDirtyDependencyRecord dirtyRecord;
			if (!UnityGraphicsMcpDependencyBakeSession.TryGetDirtyRecord(
				scenePath,
				out dirtyRecord))
			{
				failure = CreateResult(
					"graphics.prepare_bake_plan",
					requestId,
					E_MCP_TOOL_STATUS.INVALID_REQUEST,
					"指定Sceneは現在SessionのDirty Dependency Setに存在しません。",
					new Dictionary<string, object>
					{
						{ "scenePath", scenePath },
						{ "sceneDirty", scene.isDirty }
					});
				return false;
			}

			HashSet<string> normalizedKinds = new HashSet<string>(
				target.dependencyKinds
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Select(value => value.Trim().ToUpperInvariant()),
				StringComparer.Ordinal);

			if (normalizedKinds.Count == 0)
			{
				failure = CreateResult(
					"graphics.prepare_bake_plan",
					requestId,
					E_MCP_TOOL_STATUS.INVALID_REQUEST,
					"dependencyKindsへ明示的なBake種別を指定してください。",
					null);
				return false;
			}

			foreach (string kind in normalizedKinds)
			{
				E_GRAPHICS_BAKE_DEPENDENCY_KIND parsedKind;
				if (!Enum.TryParse(kind, true, out parsedKind))
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"未対応のBake Dependency Kindです。",
						new Dictionary<string, object>
						{
							{ "kind", kind }
						});
					return false;
				}

				if (parsedKind ==
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.ADAPTIVE_PROBE_VOLUME)
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.BACKEND_NOT_IMPLEMENTED,
						"APVはBaking SetとLighting ScenarioのPipeline固有契約が必要なため、Dependency Bakeでは実行しません。",
						new Dictionary<string, object>
						{
							{ "scenePath", scenePath },
							{ "kind", parsedKind.ToString() },
							{
								"dirtyDependencyDetected",
								dirtyRecord.Kinds.Contains(parsedKind.ToString())
							}
						});
					return false;
				}

				if (!dirtyRecord.Kinds.Contains(parsedKind.ToString()))
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"指定Dependencyは現在SessionのDirty Dependency Setに存在しません。",
						new Dictionary<string, object>
						{
							{ "scenePath", scenePath },
							{ "kind", parsedKind.ToString() }
						});
					return false;
				}

				if (parsedKind ==
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.LIGHTMAP_SCENE)
				{
					string key = parsedKind + "|" + scenePath;
					if (!dependencyKeys.Add(key))
					{
						failure = CreateResult(
							"graphics.prepare_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"同一Scene Lightmap Dependencyが重複しています。",
							new Dictionary<string, object>
							{
								{ "scenePath", scenePath }
							});
						return false;
					}

					dependencies.Add(
						new UnityGraphicsMcpPreparedBakeDependency
						{
							DependencyId = "BAKE-" + Guid.NewGuid().ToString("N"),
							Kind = parsedKind.ToString(),
							ScenePath = scenePath,
							SceneHandle = scene.handle,
							BaselineDigest = BuildSaveEvaluationSceneContentDigest(scene),
							Backend = "UNITY_LIGHTMAPPING_SCENE"
						});
					continue;
				}

				if (!TryPrepareSaveEvaluationReflectionProbeDependencies(
					requestId,
					scene,
					target.reflectionProbeObjectIds,
					dependencies,
					dependencyKeys,
					out failure))
				{
					return false;
				}
			}

			return true;
		}

		private static bool TryPrepareSaveEvaluationReflectionProbeDependencies(
			string requestId,
			Scene scene,
			string[] objectIds,
			List<UnityGraphicsMcpPreparedBakeDependency> dependencies,
			HashSet<string> dependencyKeys,
			out UnityGraphicsMcpToolResult failure)
		{
			failure = null;
			List<string> normalizedIds = objectIds == null
				? new List<string>()
				: objectIds
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Select(value => value.Trim())
					.Distinct(StringComparer.Ordinal)
					.ToList();

			if (normalizedIds.Count == 0)
			{
				failure = CreateResult(
					"graphics.prepare_bake_plan",
					requestId,
					E_MCP_TOOL_STATUS.INVALID_REQUEST,
					"REFLECTION_PROBEではreflectionProbeObjectIdsを明示指定してください。",
					null);
				return false;
			}

			foreach (string objectId in normalizedIds)
			{
				ReflectionProbe probe;
				if (!TryResolveSaveEvaluationReflectionProbe(objectId, out probe) ||
					probe.gameObject.scene.handle != scene.handle)
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"reflectionProbeObjectIdを対象SceneのBaked Reflection Probeとして解決できません。",
						new Dictionary<string, object>
						{
							{ "objectId", objectId },
							{ "scenePath", scene.path }
						});
					return false;
				}

				if (probe.mode != ReflectionProbeMode.Baked)
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"Reflection ProbeのModeはBakedである必要があります。",
						new Dictionary<string, object>
						{
							{ "objectId", objectId },
							{ "mode", probe.mode.ToString() }
						});
					return false;
				}

				if (!UnityGraphicsMcpDependencyBakeSession.HasDirtyDependency(
					scene.path,
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.REFLECTION_PROBE.ToString(),
					objectId))
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"指定Reflection ProbeはDirty Dependency Setに存在しません。",
						new Dictionary<string, object>
						{
							{ "objectId", objectId }
						});
					return false;
				}

				string outputAssetPath = probe.bakedTexture == null
					? string.Empty
					: AssetDatabase.GetAssetPath(probe.bakedTexture);
				if (string.IsNullOrWhiteSpace(outputAssetPath) ||
					!outputAssetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
					AssetDatabase.LoadAssetAtPath<Cubemap>(outputAssetPath) == null)
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"既存Cubemap Assetを持つBaked Reflection Probeだけを上書きできます。新規Asset生成は行いません。",
						new Dictionary<string, object>
						{
							{ "objectId", objectId },
							{ "outputAssetPath", outputAssetPath }
						});
					return false;
				}

				string key = E_GRAPHICS_BAKE_DEPENDENCY_KIND.REFLECTION_PROBE +
					"|" + objectId;
				if (!dependencyKeys.Add(key))
				{
					failure = CreateResult(
						"graphics.prepare_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"同一Reflection Probe Dependencyが重複しています。",
						new Dictionary<string, object>
						{
							{ "objectId", objectId }
						});
					return false;
				}

				dependencies.Add(
					new UnityGraphicsMcpPreparedBakeDependency
					{
						DependencyId = "BAKE-" + Guid.NewGuid().ToString("N"),
						Kind =
							E_GRAPHICS_BAKE_DEPENDENCY_KIND.REFLECTION_PROBE.ToString(),
						ScenePath = scene.path,
						SceneHandle = scene.handle,
						ObjectId = objectId,
						OutputAssetPath = outputAssetPath,
						BaselineDigest =
							BuildSaveEvaluationReflectionProbeDigest(probe, outputAssetPath),
						Backend = "UNITY_LIGHTMAPPING_REFLECTION_PROBE"
					});
			}

			return true;
		}

		private static bool ValidateDependencyBakePlan(
			UnityGraphicsMcpExecutableBakePlan plan,
			out List<UnityGraphicsMcpIssue> issues)
		{
			issues = new List<UnityGraphicsMcpIssue>();
			if (plan == null ||
				plan.Dependencies == null ||
				plan.Dependencies.Count == 0)
			{
				issues.Add(CreateDependencyBakeIssue(
					"BAKE_PLAN_EMPTY",
					"Bake PlanにDependencyがありません。",
					null));
				return false;
			}

			if (plan.DirtySetSerial !=
				UnityGraphicsMcpDependencyBakeSession.DirtySerial)
			{
				issues.Add(CreateDependencyBakeIssue(
					"DIRTY_DEPENDENCY_SET_CHANGED",
					"Prepare後にDirty Dependency Setが変更されました。",
					new Dictionary<string, object>
					{
						{ "planSerial", plan.DirtySetSerial },
						{
							"currentSerial",
							UnityGraphicsMcpDependencyBakeSession.DirtySerial
						}
					}));
			}

			foreach (UnityGraphicsMcpBakeSceneBaseline baseline
				in plan.ContributingScenes)
			{
				Scene scene;
				if (!TryResolveSaveEvaluationLoadedSceneByHandleAndPath(
					baseline.SceneHandle,
					baseline.ScenePath,
					out scene) ||
					baseline.WasDirty != scene.isDirty ||
					!string.Equals(
						baseline.ContentDigest,
						BuildSaveEvaluationSceneContentDigest(scene),
						StringComparison.Ordinal))
				{
					issues.Add(CreateDependencyBakeIssue(
						"BAKE_CONTRIBUTING_SCENE_CHANGED",
						"Bakeへ寄与するLoaded SceneがPrepare時の状態と一致しません。",
						new Dictionary<string, object>
						{
							{ "scenePath", baseline.ScenePath },
							{ "sceneHandle", baseline.SceneHandle }
						}));
				}
			}

			if (plan.ContributingScenes.Count != SceneManager.sceneCount)
			{
				issues.Add(CreateDependencyBakeIssue(
					"BAKE_LOADED_SCENE_SET_CHANGED",
					"Loaded Scene数がPrepare時から変更されました。",
					new Dictionary<string, object>
					{
						{ "before", plan.ContributingScenes.Count },
						{ "after", SceneManager.sceneCount }
					}));
			}

			foreach (UnityGraphicsMcpPreparedBakeDependency dependency
				in plan.Dependencies)
			{
				if (!UnityGraphicsMcpDependencyBakeSession.HasDirtyDependency(
					dependency.ScenePath,
					dependency.Kind,
					dependency.ObjectId))
				{
					issues.Add(CreateDependencyBakeIssue(
						"BAKE_DEPENDENCY_NO_LONGER_DIRTY",
						"Dependencyが現在のDirty Dependency Setに存在しません。",
						BuildDependencyBakeDependencyPreview(dependency)));
					continue;
				}

				Scene scene;
				if (!TryResolveSaveEvaluationLoadedSceneByHandleAndPath(
					dependency.SceneHandle,
					dependency.ScenePath,
					out scene))
				{
					issues.Add(CreateDependencyBakeIssue(
						"BAKE_TARGET_SCENE_CHANGED",
						"Bake対象SceneをHandleとPathで再解決できません。",
						BuildDependencyBakeDependencyPreview(dependency)));
					continue;
				}

				if (dependency.Kind ==
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.LIGHTMAP_SCENE.ToString())
				{
					if (!string.Equals(
						dependency.BaselineDigest,
						BuildSaveEvaluationSceneContentDigest(scene),
						StringComparison.Ordinal))
					{
						issues.Add(CreateDependencyBakeIssue(
							"LIGHTMAP_SCENE_BASELINE_CHANGED",
							"Lightmap対象SceneがPrepare時の状態と一致しません。",
							BuildDependencyBakeDependencyPreview(dependency)));
					}
					continue;
				}

				ReflectionProbe probe;
				if (!TryResolveSaveEvaluationReflectionProbe(
					dependency.ObjectId,
					out probe) ||
					!string.Equals(
						dependency.BaselineDigest,
						BuildSaveEvaluationReflectionProbeDigest(
							probe,
							dependency.OutputAssetPath),
						StringComparison.Ordinal))
				{
					issues.Add(CreateDependencyBakeIssue(
						"REFLECTION_PROBE_BASELINE_CHANGED",
						"Reflection ProbeがPrepare時の状態と一致しません。",
						BuildDependencyBakeDependencyPreview(dependency)));
				}
			}

			return issues.Count == 0;
		}

		private static bool PreflightDependencyBakeDependencies(
			IEnumerable<UnityGraphicsMcpPreparedBakeDependency> dependencies,
			out List<UnityGraphicsMcpIssue> issues)
		{
			issues = new List<UnityGraphicsMcpIssue>();
			foreach (UnityGraphicsMcpPreparedBakeDependency dependency
				in dependencies)
			{
				if (dependency.Kind ==
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.LIGHTMAP_SCENE.ToString())
				{
					Scene scene;
					if (!TryResolveSaveEvaluationLoadedSceneByHandleAndPath(
						dependency.SceneHandle,
						dependency.ScenePath,
						out scene) ||
						!CanExecuteSaveEvaluationSceneBake(scene))
					{
						issues.Add(CreateDependencyBakeIssue(
							"SCENE_BAKE_BACKEND_NOT_IMPLEMENTED",
							"対象Unity VersionでScene限定Bake APIを解決できません。全Loaded Scene BakeへのFallbackは行いません。",
							BuildDependencyBakeDependencyPreview(dependency)));
					}
					continue;
				}

				if (dependency.Kind ==
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.REFLECTION_PROBE.ToString())
				{
					ReflectionProbe probe;
					if (!TryResolveSaveEvaluationReflectionProbe(
						dependency.ObjectId,
						out probe))
					{
						issues.Add(CreateDependencyBakeIssue(
							"REFLECTION_PROBE_BACKEND_UNAVAILABLE",
							"Reflection Probeを再解決できません。",
							BuildDependencyBakeDependencyPreview(dependency)));
					}
					continue;
				}

				issues.Add(CreateDependencyBakeIssue(
					"APV_BAKE_BACKEND_NOT_IMPLEMENTED",
					"APV Bake BackendはDependency Bakeでは未実装です。",
					BuildDependencyBakeDependencyPreview(dependency)));
			}

			return issues.Count == 0;
		}

		private static bool TryExecuteDependencyBakeDependency(
			UnityGraphicsMcpPreparedBakeDependency dependency,
			out bool succeeded,
			out string failureMessage)
		{
			succeeded = false;
			failureMessage = null;

			if (dependency.Kind ==
				E_GRAPHICS_BAKE_DEPENDENCY_KIND.LIGHTMAP_SCENE.ToString())
			{
				Scene scene;
				if (!TryResolveSaveEvaluationLoadedSceneByHandleAndPath(
					dependency.SceneHandle,
					dependency.ScenePath,
					out scene))
				{
					failureMessage = "Sceneを再解決できません。";
					return false;
				}

				succeeded = ExecuteSaveEvaluationSceneBake(scene);
				if (!succeeded)
				{
					failureMessage = "Unity Lightmapping Scene Bakeがfalseを返しました。";
				}
				return true;
			}

			if (dependency.Kind ==
				E_GRAPHICS_BAKE_DEPENDENCY_KIND.REFLECTION_PROBE.ToString())
			{
				ReflectionProbe probe;
				if (!TryResolveSaveEvaluationReflectionProbe(
					dependency.ObjectId,
					out probe))
				{
					failureMessage = "Reflection Probeを再解決できません。";
					return false;
				}

				Func<ReflectionProbe, string, bool> testOverride =
					UnityGraphicsMcpDependencyBakeSession
						.ReflectionProbeBakeOverrideForTests;
				succeeded = testOverride != null
					? testOverride(probe, dependency.OutputAssetPath)
					: Lightmapping.BakeReflectionProbe(
						probe,
						dependency.OutputAssetPath);
				if (!succeeded)
				{
					failureMessage =
						"Unity Lightmapping Reflection Probe Bakeがfalseを返しました。";
				}
				return true;
			}

			failureMessage = "APV Bake Backendは未実装です。";
			return false;
		}

		private static bool CanExecuteSaveEvaluationSceneBake(Scene scene)
		{
			if (UnityGraphicsMcpDependencyBakeSession.SceneBakeOverrideForTests != null)
			{
				return true;
			}

			return ResolveSaveEvaluationSceneBakeMethod() != null ||
				SceneManager.sceneCount == 1;
		}

		private static bool ExecuteSaveEvaluationSceneBake(Scene scene)
		{
			Func<Scene, bool> testOverride =
				UnityGraphicsMcpDependencyBakeSession.SceneBakeOverrideForTests;
			if (testOverride != null)
			{
				return testOverride(scene);
			}

			MethodInfo method = ResolveSaveEvaluationSceneBakeMethod();
			if (method != null)
			{
				object result = method.Invoke(null, new object[] { scene });
				return result is bool && (bool)result;
			}

			return SceneManager.sceneCount == 1 && Lightmapping.Bake();
		}

		private static MethodInfo ResolveSaveEvaluationSceneBakeMethod()
		{
			MethodInfo method = typeof(Lightmapping).GetMethod(
				"Bake",
				BindingFlags.Public | BindingFlags.Static,
				null,
				new[] { typeof(Scene) },
				null);
			if (method != null && method.ReturnType == typeof(bool))
			{
				return method;
			}

			Type experimentalType = Type.GetType(
				"UnityEditor.Experimental.Lightmapping, UnityEditor");
			if (experimentalType == null)
			{
				return null;
			}

			method = experimentalType.GetMethod(
				"Bake",
				BindingFlags.Public | BindingFlags.Static,
				null,
				new[] { typeof(Scene) },
				null);
			return method != null && method.ReturnType == typeof(bool)
				? method
				: null;
		}

		private static bool TryResolveSaveEvaluationReflectionProbe(
			string objectId,
			out ReflectionProbe probe)
		{
			probe = null;
			GlobalObjectId globalObjectId;
			if (string.IsNullOrWhiteSpace(objectId) ||
				!GlobalObjectId.TryParse(objectId, out globalObjectId))
			{
				return false;
			}

			Object target =
				GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
			probe = target as ReflectionProbe;
			if (probe == null)
			{
				GameObject gameObject = target as GameObject;
				if (gameObject != null)
				{
					probe = gameObject.GetComponent<ReflectionProbe>();
				}
			}

			return probe != null &&
				probe.gameObject.scene.IsValid() &&
				probe.gameObject.scene.isLoaded;
		}

		private static string BuildSaveEvaluationReflectionProbeDigest(
			ReflectionProbe probe,
			string outputAssetPath)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(
				GlobalObjectId.GetGlobalObjectIdSlow(probe).ToString()).Append('|');
			builder.Append(probe.gameObject.scene.handle).Append('|');
			builder.Append(probe.gameObject.scene.path).Append('|');
			builder.Append(outputAssetPath).Append('|');
			builder.Append(EditorJsonUtility.ToJson(probe, false));
			return UnityGraphicsMcpSaveEvaluationSession.HashText(builder.ToString());
		}

		private static string BuildDependencyBakePlanDigest(
			UnityGraphicsMcpExecutableBakePlan plan)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(plan.Revision).Append('|');
			builder.Append(plan.DirtySetSerial).Append('|');

			foreach (UnityGraphicsMcpBakeSceneBaseline scene
				in plan.ContributingScenes
					.OrderBy(item => item.ScenePath, StringComparer.Ordinal))
			{
				builder.Append(scene.SceneHandle).Append('|');
				builder.Append(scene.ScenePath).Append('|');
				builder.Append(scene.WasDirty).Append('|');
				builder.Append(scene.ContentDigest).Append('|');
			}

			foreach (UnityGraphicsMcpPreparedBakeDependency dependency
				in plan.Dependencies
					.OrderBy(item => item.DependencyId, StringComparer.Ordinal))
			{
				builder.Append(dependency.DependencyId).Append('|');
				builder.Append(dependency.Kind).Append('|');
				builder.Append(dependency.SceneHandle).Append('|');
				builder.Append(dependency.ScenePath).Append('|');
				builder.Append(dependency.ObjectId).Append('|');
				builder.Append(dependency.OutputAssetPath).Append('|');
				builder.Append(dependency.BaselineDigest).Append('|');
				builder.Append(dependency.Backend).Append('|');
			}

			return UnityGraphicsMcpSaveEvaluationSession.HashText(builder.ToString());
		}

		private static List<Dictionary<string, object>>
			BuildDependencyBakeScenePreviews(
				IEnumerable<UnityGraphicsMcpBakeSceneBaseline> scenes)
		{
			return scenes
				.Select(scene => new Dictionary<string, object>
				{
					{ "sceneHandle", scene.SceneHandle },
					{ "scenePath", scene.ScenePath },
					{ "wasDirty", scene.WasDirty },
					{ "contentDigest", scene.ContentDigest }
				})
				.ToList();
		}

		private static List<Dictionary<string, object>>
			BuildDependencyBakeDependencyPreviews(
				IEnumerable<UnityGraphicsMcpPreparedBakeDependency> dependencies)
		{
			return dependencies
				.Select(BuildDependencyBakeDependencyPreview)
				.ToList();
		}

		private static Dictionary<string, object>
			BuildDependencyBakeDependencyPreview(
				UnityGraphicsMcpPreparedBakeDependency dependency)
		{
			return new Dictionary<string, object>
			{
				{ "dependencyId", dependency.DependencyId },
				{ "kind", dependency.Kind },
				{ "scenePath", dependency.ScenePath },
				{ "sceneHandle", dependency.SceneHandle },
				{ "objectId", dependency.ObjectId },
				{ "outputAssetPath", dependency.OutputAssetPath },
				{ "baselineDigest", dependency.BaselineDigest },
				{ "backend", dependency.Backend }
			};
		}

		private static UnityGraphicsMcpIssue CreateDependencyBakeIssue(
			string code,
			string message,
			object evidence)
		{
			return new UnityGraphicsMcpIssue
			{
				code = code,
				message = message,
				evidence = evidence
			};
		}
	}
}

#endif
