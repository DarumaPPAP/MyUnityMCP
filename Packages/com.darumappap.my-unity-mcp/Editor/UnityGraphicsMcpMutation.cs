#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpVector3Input
	{
		public float x { get; set; }
		public float y { get; set; }
		public float z { get; set; }
	}

	public sealed class UnityGraphicsMcpColorInput
	{
		public float r { get; set; }
		public float g { get; set; }
		public float b { get; set; }
		public float a { get; set; } = 1.0f;
	}

	public sealed class UnityGraphicsMcpLightOperationInput
	{
		public string operationId { get; set; }
		public string operation { get; set; }
		public string targetObjectId { get; set; }
		public string targetScenePath { get; set; }
		public string name { get; set; }
		public string lightType { get; set; }
		public UnityGraphicsMcpColorInput color { get; set; }
		public float? intensity { get; set; }
		public float? range { get; set; }
		public float? spotAngle { get; set; }
		public string shadows { get; set; }
		public UnityGraphicsMcpVector3Input position { get; set; }
		public UnityGraphicsMcpVector3Input eulerAngles { get; set; }
		public bool? enabled { get; set; }
	}

	internal sealed class UnityGraphicsMcpPreparedLightOperation
	{
		public string OperationId { get; set; }
		public string Operation { get; set; }
		public string TargetObjectId { get; set; }
		public int TargetInstanceId { get; set; }
		public int TargetSceneHandle { get; set; }
		public string TargetScenePath { get; set; }
		public string Name { get; set; }
		public LightType? LightType { get; set; }
		public Color? Color { get; set; }
		public float? Intensity { get; set; }
		public float? Range { get; set; }
		public float? SpotAngle { get; set; }
		public LightShadows? Shadows { get; set; }
		public Vector3? Position { get; set; }
		public Vector3? EulerAngles { get; set; }
		public bool? Enabled { get; set; }
		public UnityGraphicsMcpLightState BaselineState { get; set; }
	}

	internal sealed class UnityGraphicsMcpExecutableLightPlan
	{
		public string PlanId { get; set; }
		public string DirectionPlanId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public DateTime ExpiresUtc { get; set; }
		public string ApprovalTokenHash { get; set; }
		public string DiffDigest { get; set; }
		public bool Consumed { get; set; }
		public List<UnityGraphicsMcpPreparedLightOperation> Operations { get; set; } =
			new List<UnityGraphicsMcpPreparedLightOperation>();
	}

	internal sealed class UnityGraphicsMcpLightState
	{
		public int InstanceId { get; set; }
		public string ObjectId { get; set; }
		public string Name { get; set; }
		public string ScenePath { get; set; }
		public string LightType { get; set; }
		public Color Color { get; set; }
		public float Intensity { get; set; }
		public float Range { get; set; }
		public float SpotAngle { get; set; }
		public string Shadows { get; set; }
		public Vector3 Position { get; set; }
		public Vector3 EulerAngles { get; set; }
		public bool Enabled { get; set; }
	}

	internal sealed class UnityGraphicsMcpMutationTransaction
	{
		public string TransactionId { get; set; }
		public string PlanId { get; set; }
		public int UndoGroup { get; set; }
		public long PostRevision { get; set; }
		public bool AwaitingOwnedHierarchyChange { get; set; }
		public bool Invalidated { get; set; }
		public bool Undone { get; set; }
		public List<int> CreatedInstanceIds { get; set; } = new List<int>();
		public Dictionary<int, UnityGraphicsMcpLightState> BeforeStates { get; set; } =
			new Dictionary<int, UnityGraphicsMcpLightState>();
		public Dictionary<int, UnityGraphicsMcpLightState> AfterStates { get; set; } =
			new Dictionary<int, UnityGraphicsMcpLightState>();
	}

	/// <summary>
	/// Executable Planと直近TransactionをEditor Session内だけで保持します。
	/// </summary>
	internal static class UnityGraphicsMcpMutationSession
	{
		private const int MAX_PLAN_COUNT = 8;
		private static readonly TimeSpan PLAN_LIFETIME = TimeSpan.FromMinutes(10.0);
		private static readonly Dictionary<string, UnityGraphicsMcpExecutableLightPlan> _plans =
			new Dictionary<string, UnityGraphicsMcpExecutableLightPlan>();
		private static UnityGraphicsMcpMutationTransaction _latestTransaction;
		private static bool _isPerformingOwnedUndo;

		static UnityGraphicsMcpMutationSession()
		{
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
			EditorApplication.projectChanged += InvalidateTransaction;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			CompilationPipeline.compilationStarted += OnCompilationStarted;
			EditorApplication.quitting += Clear;
		}

		public static string StorePlan(UnityGraphicsMcpExecutableLightPlan plan)
		{
			RemoveExpiredPlans();
			RemoveOldestPlanWhenFull();

			plan.PlanId = UnityGraphicsMcpSession.SessionId +
				":mutation-plan:" + Guid.NewGuid().ToString("N");
			plan.CreatedUtc = DateTime.UtcNow;
			plan.ExpiresUtc = plan.CreatedUtc + PLAN_LIFETIME;
			_plans[plan.PlanId] = plan;
			return plan.PlanId;
		}

		public static bool TryGetPlan(
			string planId,
			long expectedRevision,
			string approvalToken,
			out UnityGraphicsMcpExecutableLightPlan plan,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			plan = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;

			if (string.IsNullOrWhiteSpace(planId) ||
				!planId.StartsWith(
					UnityGraphicsMcpSession.SessionId + ":mutation-plan:",
					StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Executable Planは現在のEditor Sessionに属していません。";
				return false;
			}

			RemoveExpiredPlans();
			if (!_plans.TryGetValue(planId, out plan))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Executable Planが存在しないか有効期限切れです。";
				return false;
			}

			if (plan.Consumed)
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "Executable Planは既に使用済みです。";
				return false;
			}

			if (expectedRevision != UnityGraphicsMcpSession.Revision ||
				plan.Revision != UnityGraphicsMcpSession.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Executable Plan作成後にEditor Revisionが変更されました。";
				return false;
			}

			if (string.IsNullOrWhiteSpace(approvalToken) ||
				!string.Equals(
					plan.ApprovalTokenHash,
					HashText(approvalToken),
					StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "承認Tokenが不足しているか一致しません。";
				return false;
			}

			return true;
		}

		public static void ConsumePlan(UnityGraphicsMcpExecutableLightPlan plan)
		{
			if (plan != null)
			{
				plan.Consumed = true;
			}
		}

		public static void SetLatestTransaction(
			UnityGraphicsMcpMutationTransaction transaction)
		{
			_latestTransaction = transaction;
		}

		public static bool TryGetLatestTransaction(
			string transactionId,
			out UnityGraphicsMcpMutationTransaction transaction,
			out string failureMessage)
		{
			transaction = _latestTransaction;
			failureMessage = null;

			if (transaction == null || transaction.Undone)
			{
				failureMessage = "Undo可能なMyUnityMCP Transactionがありません。";
				return false;
			}

			if (!string.Equals(
				transaction.TransactionId,
				transactionId,
				StringComparison.Ordinal))
			{
				failureMessage = "transactionIdが直近Transactionと一致しません。";
				return false;
			}

			if (transaction.Invalidated)
			{
				failureMessage = "Transaction後に外部変更が検出されたため、安全なUndoを実行できません。";
				return false;
			}

			return true;
		}

		public static void BeginOwnedUndo()
		{
			_isPerformingOwnedUndo = true;
		}

		public static void EndOwnedUndo(bool succeeded)
		{
			_isPerformingOwnedUndo = false;
			if (succeeded && _latestTransaction != null)
			{
				_latestTransaction.Undone = true;
			}
		}

		public static void ClearForTests()
		{
			Clear();
		}

		private static void OnHierarchyChanged()
		{
			if (_latestTransaction == null || _latestTransaction.Undone)
			{
				return;
			}

			if (_latestTransaction.AwaitingOwnedHierarchyChange)
			{
				_latestTransaction.PostRevision = UnityGraphicsMcpSession.Revision;
				_latestTransaction.AwaitingOwnedHierarchyChange = false;
				return;
			}

			_latestTransaction.Invalidated = true;
		}

		private static void OnUndoRedoPerformed()
		{
			if (!_isPerformingOwnedUndo)
			{
				InvalidateTransaction();
			}
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			Clear();
		}

		private static void OnCompilationStarted(object context)
		{
			Clear();
		}

		private static void InvalidateTransaction()
		{
			if (_latestTransaction != null)
			{
				_latestTransaction.Invalidated = true;
			}
		}

		private static void Clear()
		{
			_plans.Clear();
			_latestTransaction = null;
			_isPerformingOwnedUndo = false;
		}

		private static void RemoveExpiredPlans()
		{
			DateTime now = DateTime.UtcNow;
			List<string> expiredIds = _plans
				.Where(pair => pair.Value.ExpiresUtc <= now)
				.Select(pair => pair.Key)
				.ToList();

			foreach (string expiredId in expiredIds)
			{
				_plans.Remove(expiredId);
			}
		}

		private static void RemoveOldestPlanWhenFull()
		{
			while (_plans.Count >= MAX_PLAN_COUNT)
			{
				KeyValuePair<string, UnityGraphicsMcpExecutableLightPlan> oldest =
					_plans.OrderBy(pair => pair.Value.CreatedUtc).First();
				_plans.Remove(oldest.Key);
			}
		}

		public static string HashText(string value)
		{
			using (SHA256 sha256 = SHA256.Create())
			{
				byte[] bytes = sha256.ComputeHash(
					Encoding.UTF8.GetBytes(value ?? string.Empty));
				StringBuilder builder = new StringBuilder(bytes.Length * 2);
				foreach (byte item in bytes)
				{
					builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
				}
				return builder.ToString();
			}
		}
	}

	/// <summary>
	/// Owner: UnityGraphicsMCP。
	/// Responsibility: 明示的なLight差分をPreviewし、承認済みPlanをUndo Transactionとして適用します。
	/// </summary>
	public static partial class UnityGraphicsMcpInspection
	{
		private const int MAX_LIGHT_OPERATION_COUNT = 32;
		private const string SAVE_MODE_NONE = "NONE";

		public static UnityGraphicsMcpToolResult PrepareLightPlan(
			string requestId,
			string directionPlanId,
			long? expectedRevision,
			UnityGraphicsMcpLightOperationInput[] operations)
		{
			return ExecuteReadOnly(
				"graphics.prepare_light_plan",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.prepare_light_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					UnityGraphicsMcpDirectionPlan directionPlan;
					E_MCP_TOOL_STATUS failureStatus;
					if (!UnityGraphicsMcpSession.TryGetPlan(
						directionPlanId,
						expectedRevision.Value,
						out directionPlan,
						out failureStatus))
					{
						return CreateResult(
							"graphics.prepare_light_plan",
							requestId,
							failureStatus,
							"Direction Planは現在のEditor SessionまたはRevisionでは利用できません。",
							new Dictionary<string, object>
							{
								{ "directionPlanId", directionPlanId },
								{ "expectedRevision", expectedRevision.Value },
								{ "currentRevision", UnityGraphicsMcpSession.Revision }
							});
					}

					if (!directionPlan.Recommendations.Any(item => item.section == "LIGHTING"))
					{
						return CreateResult(
							"graphics.prepare_light_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Direction PlanにLIGHTING Sectionがありません。",
							null);
					}

					List<UnityGraphicsMcpPreparedLightOperation> preparedOperations;
					List<UnityGraphicsMcpIssue> issues;
					if (!TryPrepareLightOperations(
						operations,
						out preparedOperations,
						out issues))
					{
						UnityGraphicsMcpToolResult invalidResult = CreateResult(
							"graphics.prepare_light_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Light OperationをExecutable Planへ変換できませんでした。",
							null);
						invalidResult.issues.AddRange(issues);
						return invalidResult;
					}

					string approvalToken = Guid.NewGuid().ToString("N") +
						Guid.NewGuid().ToString("N");
					UnityGraphicsMcpExecutableLightPlan executablePlan =
						new UnityGraphicsMcpExecutableLightPlan
						{
							DirectionPlanId = directionPlanId,
							Revision = expectedRevision.Value,
							ApprovalTokenHash =
								UnityGraphicsMcpMutationSession.HashText(approvalToken),
							Operations = preparedOperations
						};

					executablePlan.DiffDigest = BuildPlanDigest(executablePlan);
					UnityGraphicsMcpMutationSession.StorePlan(executablePlan);

					return CreateResult(
						"graphics.prepare_light_plan",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"明示的なLight操作をRead-onlyで検証し、承認待ちExecutable Planを作成しました。",
						new Dictionary<string, object>
						{
							{ "directionPlanId", directionPlanId },
							{ "planId", executablePlan.PlanId },
							{ "expectedRevision", executablePlan.Revision },
							{ "approvalToken", approvalToken },
							{ "approvalTokenExpiresUtc", executablePlan.ExpiresUtc.ToString("O") },
							{ "diffDigest", executablePlan.DiffDigest },
							{ "operations", BuildOperationPreviews(executablePlan.Operations) },
							{ "saveMode", SAVE_MODE_NONE },
							{ "mutationApplied", false },
							{ "savePerformed", false },
							{ "bakePerformed", false }
						});
				});
		}

		public static UnityGraphicsMcpToolResult ApplyPlan(
			string requestId,
			string planId,
			long? expectedRevision,
			string approvalToken,
			string saveMode)
		{
			return ExecuteMutation(
				"graphics.apply_plan",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.apply_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					if (!string.Equals(
						string.IsNullOrWhiteSpace(saveMode) ? SAVE_MODE_NONE : saveMode.Trim(),
						SAVE_MODE_NONE,
						StringComparison.OrdinalIgnoreCase))
					{
						return CreateResult(
							"graphics.apply_plan",
							requestId,
							E_MCP_TOOL_STATUS.UNSUPPORTED,
							"Phase 3Aで利用できるsaveModeはNONEだけです。",
							null);
					}

					UnityGraphicsMcpExecutableLightPlan plan;
					E_MCP_TOOL_STATUS failureStatus;
					string failureMessage;
					if (!UnityGraphicsMcpMutationSession.TryGetPlan(
						planId,
						expectedRevision.Value,
						approvalToken,
						out plan,
						out failureStatus,
						out failureMessage))
					{
						return CreateResult(
							"graphics.apply_plan",
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
					if (!ValidatePreparedPlanStillMatches(plan, out staleIssues) ||
						!string.Equals(
							plan.DiffDigest,
							BuildPlanDigest(plan),
							StringComparison.Ordinal))
					{
						UnityGraphicsMcpToolResult staleResult = CreateResult(
							"graphics.apply_plan",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"Preview後に対象LightまたはScene状態が変化したため適用を中止しました。",
							null);
						staleResult.issues.AddRange(staleIssues);
						return staleResult;
					}

					return ApplyPreparedLightPlan(requestId, plan);
				});
		}

		public static UnityGraphicsMcpToolResult UndoLastTransaction(
			string requestId,
			string transactionId,
			long? expectedRevision)
		{
			return ExecuteMutation(
				"graphics.undo_last_transaction",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.undo_last_transaction",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					UnityGraphicsMcpMutationTransaction transaction;
					string failureMessage;
					if (!UnityGraphicsMcpMutationSession.TryGetLatestTransaction(
						transactionId,
						out transaction,
						out failureMessage))
					{
						return CreateResult(
							"graphics.undo_last_transaction",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							failureMessage,
							null);
					}

					if (expectedRevision.Value != UnityGraphicsMcpSession.Revision ||
						transaction.PostRevision != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult(
							"graphics.undo_last_transaction",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"Transaction後にEditor Revisionが変更されたためUndoを拒否しました。",
							new Dictionary<string, object>
							{
								{ "transactionRevision", transaction.PostRevision },
								{ "currentRevision", UnityGraphicsMcpSession.Revision }
							});
					}

					if (Undo.GetCurrentGroup() != transaction.UndoGroup)
					{
						return CreateResult(
							"graphics.undo_last_transaction",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"MyUnityMCP TransactionがUndo Stackの最新Groupではありません。",
							null);
					}

					List<UnityGraphicsMcpIssue> verificationIssues;
					if (!VerifyTransactionPostState(transaction, out verificationIssues))
					{
						UnityGraphicsMcpToolResult invalidResult = CreateResult(
							"graphics.undo_last_transaction",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"Transaction対象が適用直後の状態と一致しないためUndoを拒否しました。",
							null);
						invalidResult.issues.AddRange(verificationIssues);
						return invalidResult;
					}

					long revisionBeforeUndo = UnityGraphicsMcpSession.Revision;
					bool undoSucceeded = false;
					UnityGraphicsMcpMutationSession.BeginOwnedUndo();

					try
					{
						Undo.PerformUndo();
						undoSucceeded = VerifyTransactionRestored(transaction, out verificationIssues);
					}
					finally
					{
						UnityGraphicsMcpMutationSession.EndOwnedUndo(undoSucceeded);
					}

					if (!undoSucceeded)
					{
						UnityGraphicsMcpToolResult failedResult = CreateResult(
							"graphics.undo_last_transaction",
							requestId,
							E_MCP_TOOL_STATUS.FAILED,
							"Unity Undo後の復元検証に失敗しました。",
							null);
						failedResult.issues.AddRange(verificationIssues);
						return failedResult;
					}

					if (revisionBeforeUndo == UnityGraphicsMcpSession.Revision)
					{
						UnityGraphicsMcpSession.NotifyMutationApplied();
					}

					return CreateResult(
						"graphics.undo_last_transaction",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"直近のMyUnityMCP Light TransactionをUnity Undoで復元しました。",
						new Dictionary<string, object>
						{
							{ "transactionId", transaction.TransactionId },
							{ "undoPerformed", true },
							{ "savePerformed", false },
							{ "bakePerformed", false },
							{ "revision", UnityGraphicsMcpSession.Revision }
						});
				});
		}

		private static UnityGraphicsMcpToolResult ApplyPreparedLightPlan(
			string requestId,
			UnityGraphicsMcpExecutableLightPlan plan)
		{
			Undo.IncrementCurrentGroup();
			int undoGroup = Undo.GetCurrentGroup();
			string undoName = "MyUnityMCP Apply Light Plan";
			Undo.SetCurrentGroupName(undoName);

			UnityGraphicsMcpMutationTransaction transaction =
				new UnityGraphicsMcpMutationTransaction
				{
					TransactionId = UnityGraphicsMcpSession.SessionId +
						":transaction:" + Guid.NewGuid().ToString("N"),
					PlanId = plan.PlanId,
					UndoGroup = undoGroup
				};

			List<string> createdIds = new List<string>();
			List<string> modifiedIds = new List<string>();
			HashSet<string> dirtyScenes = new HashSet<string>(StringComparer.Ordinal);
			long startRevision = UnityGraphicsMcpSession.Revision;

			try
			{
				foreach (UnityGraphicsMcpPreparedLightOperation operation in plan.Operations)
				{
					if (operation.Operation == "LIGHT_CREATE")
					{
						Scene scene;
						if (!TryResolveLoadedSceneByHandle(operation.TargetSceneHandle, out scene))
						{
							throw new InvalidOperationException(
								"LIGHT_CREATE対象SceneがLoaded Sceneとして解決できません。");
						}

						GameObject gameObject = new GameObject(operation.Name);
						if (gameObject.scene.handle != scene.handle)
						{
							SceneManager.MoveGameObjectToScene(gameObject, scene);
						}

						Undo.RegisterCreatedObjectUndo(gameObject, undoName);
						Light light = Undo.AddComponent<Light>(gameObject);
						ApplyLightOperation(light, operation);
						EditorSceneManager.MarkSceneDirty(scene);

						UnityGraphicsMcpLightState afterState = CaptureLightState(light);
						transaction.CreatedInstanceIds.Add(light.GetInstanceID());
						transaction.AfterStates[light.GetInstanceID()] = afterState;
						createdIds.Add(afterState.ObjectId);
						dirtyScenes.Add(NormalizeSceneLabel(scene));
					}
					else
					{
						Light light = ResolveLightByInstanceId(operation.TargetInstanceId);
						transaction.BeforeStates[light.GetInstanceID()] = CaptureLightState(light);
						Undo.RecordObject(light.gameObject, undoName);
						Undo.RecordObject(light.transform, undoName);
						Undo.RecordObject(light, undoName);
						ApplyLightOperation(light, operation);
						EditorSceneManager.MarkSceneDirty(light.gameObject.scene);

						UnityGraphicsMcpLightState afterState = CaptureLightState(light);
						transaction.AfterStates[light.GetInstanceID()] = afterState;
						modifiedIds.Add(afterState.ObjectId);
						dirtyScenes.Add(NormalizeSceneLabel(light.gameObject.scene));
					}
				}

				Undo.CollapseUndoOperations(undoGroup);
				bool hierarchyEventAlreadyObserved =
					UnityGraphicsMcpSession.Revision != startRevision;
				UnityGraphicsMcpSession.NotifyMutationApplied();
				transaction.PostRevision = UnityGraphicsMcpSession.Revision;
				transaction.AwaitingOwnedHierarchyChange =
					transaction.CreatedInstanceIds.Count > 0 &&
					!hierarchyEventAlreadyObserved;
				UnityGraphicsMcpMutationSession.SetLatestTransaction(transaction);
				UnityGraphicsMcpMutationSession.ConsumePlan(plan);

				return CreateResult(
					"graphics.apply_plan",
					requestId,
					E_MCP_TOOL_STATUS.SUCCESS,
					"承認済みLight Planを一つのUnity Undo Transactionとして適用しました。",
					new Dictionary<string, object>
					{
						{ "planId", plan.PlanId },
						{ "transactionId", transaction.TransactionId },
						{ "createdObjectIds", createdIds },
						{ "modifiedObjectIds", modifiedIds },
						{ "dirtyScenes", dirtyScenes.ToList() },
						{ "saveMode", SAVE_MODE_NONE },
						{ "savePerformed", false },
						{ "bakePerformed", false },
						{ "undoAvailable", true },
						{ "revision", transaction.PostRevision }
					});
			}
			catch
			{
				Undo.RevertAllDownToGroup(undoGroup);
				throw;
			}
		}

		private static UnityGraphicsMcpToolResult ExecuteMutation(
			string toolName,
			string requestId,
			Func<UnityGraphicsMcpToolResult> operation)
		{
			string normalizedRequestId = string.IsNullOrWhiteSpace(requestId)
				? Guid.NewGuid().ToString("N")
				: requestId;

			if (!UnityGraphicsMcpSession.IsMainThread)
			{
				return CreateResult(
					toolName,
					normalizedRequestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Unity Editor APIはMain Threadで実行する必要があります。",
					null);
			}

			if (UnityGraphicsMcpSession.IsReloading)
			{
				return CreateResult(
					toolName,
					normalizedRequestId,
					E_MCP_TOOL_STATUS.EDITOR_RELOADING,
					"Unity EditorがCompileまたはDomain Reload中です。",
					null);
			}

			try
			{
				return operation();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return CreateResult(
					toolName,
					normalizedRequestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Graphics Mutation中に例外が発生し、Undo GroupをRollbackしました。",
					new Dictionary<string, object>
					{
						{ "exceptionType", exception.GetType().FullName },
						{ "message", exception.Message }
					});
			}
		}

		private static bool TryPrepareLightOperations(
			UnityGraphicsMcpLightOperationInput[] inputs,
			out List<UnityGraphicsMcpPreparedLightOperation> operations,
			out List<UnityGraphicsMcpIssue> issues)
		{
			operations = new List<UnityGraphicsMcpPreparedLightOperation>();
			issues = new List<UnityGraphicsMcpIssue>();

			if (inputs == null || inputs.Length == 0 ||
				inputs.Length > MAX_LIGHT_OPERATION_COUNT)
			{
				issues.Add(CreateMutationIssue(
					"LIGHT_OPERATION_COUNT_INVALID",
					"Light Operationは1～32件で指定してください。",
					new Dictionary<string, object>
					{
						{ "count", inputs == null ? 0 : inputs.Length }
					}));
				return false;
			}

			HashSet<string> operationIds = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> updateTargets = new HashSet<string>(StringComparer.Ordinal);

			foreach (UnityGraphicsMcpLightOperationInput input in inputs)
			{
				UnityGraphicsMcpPreparedLightOperation operation;
				UnityGraphicsMcpIssue issue;
				if (!TryPrepareLightOperation(input, out operation, out issue))
				{
					issues.Add(issue);
					continue;
				}

				if (!operationIds.Add(operation.OperationId))
				{
					issues.Add(CreateMutationIssue(
						"DUPLICATE_OPERATION_ID",
						"operationIdはPlan内で一意である必要があります。",
						new Dictionary<string, object>
						{
							{ "operationId", operation.OperationId }
						}));
					continue;
				}

				if (operation.Operation == "LIGHT_UPDATE" &&
					!updateTargets.Add(operation.TargetObjectId))
				{
					issues.Add(CreateMutationIssue(
						"DUPLICATE_LIGHT_UPDATE_TARGET",
						"同一Lightを一つのPlan内で複数回更新できません。",
						new Dictionary<string, object>
						{
							{ "targetObjectId", operation.TargetObjectId }
						}));
					continue;
				}

				operations.Add(operation);
			}

			return issues.Count == 0 && operations.Count == inputs.Length;
		}

		private static bool TryPrepareLightOperation(
			UnityGraphicsMcpLightOperationInput input,
			out UnityGraphicsMcpPreparedLightOperation operation,
			out UnityGraphicsMcpIssue issue)
		{
			operation = null;
			issue = null;

			if (input == null || string.IsNullOrWhiteSpace(input.operationId) ||
				string.IsNullOrWhiteSpace(input.operation))
			{
				issue = CreateMutationIssue(
					"LIGHT_OPERATION_ID_OR_KIND_REQUIRED",
					"operationIdとoperationを指定してください。",
					null);
				return false;
			}

			string operationKind = input.operation.Trim().ToUpperInvariant();
			if (operationKind != "LIGHT_CREATE" && operationKind != "LIGHT_UPDATE")
			{
				issue = CreateMutationIssue(
					"LIGHT_OPERATION_UNSUPPORTED",
					"Phase 3Aで利用できるoperationはLIGHT_CREATEとLIGHT_UPDATEだけです。",
					new Dictionary<string, object>
					{
						{ "operation", input.operation }
					});
				return false;
			}

			operation = new UnityGraphicsMcpPreparedLightOperation
			{
				OperationId = input.operationId.Trim(),
				Operation = operationKind,
				TargetObjectId = string.IsNullOrWhiteSpace(input.targetObjectId)
					? null
					: input.targetObjectId.Trim(),
				Name = input.name == null ? null : input.name.Trim(),
				Intensity = input.intensity,
				Range = input.range,
				SpotAngle = input.spotAngle,
				Enabled = input.enabled
			};

			if (!TryParseLightType(input.lightType, out LightType? lightType) ||
				!TryParseShadows(input.shadows, out LightShadows? shadows) ||
				!TryParseColor(input.color, out Color? color) ||
				!TryParseVector(input.position, out Vector3? position) ||
				!TryParseVector(input.eulerAngles, out Vector3? eulerAngles))
			{
				issue = CreateMutationIssue(
					"LIGHT_OPERATION_VALUE_INVALID",
					"Light Type、Color、Shadow、Transformのいずれかが不正です。",
					new Dictionary<string, object>
					{
						{ "operationId", operation.OperationId }
					});
				return false;
			}

			operation.LightType = lightType;
			operation.Shadows = shadows;
			operation.Color = color;
			operation.Position = position;
			operation.EulerAngles = eulerAngles;

			if (!ValidateNumericValues(operation, out issue))
			{
				return false;
			}

			if (operationKind == "LIGHT_CREATE")
			{
				return PrepareCreateOperation(input, operation, out issue);
			}

			return PrepareUpdateOperation(operation, out issue);
		}

		private static bool PrepareCreateOperation(
			UnityGraphicsMcpLightOperationInput input,
			UnityGraphicsMcpPreparedLightOperation operation,
			out UnityGraphicsMcpIssue issue)
		{
			issue = null;
			if (string.IsNullOrWhiteSpace(operation.Name) ||
				!operation.LightType.HasValue ||
				!operation.Color.HasValue ||
				!operation.Intensity.HasValue ||
				!operation.Shadows.HasValue ||
				!operation.Position.HasValue ||
				!operation.EulerAngles.HasValue ||
				!operation.Enabled.HasValue)
			{
				issue = CreateMutationIssue(
					"LIGHT_CREATE_EXPLICIT_VALUES_REQUIRED",
					"LIGHT_CREATEではname、lightType、color、intensity、shadows、position、eulerAngles、enabledを明示してください。",
					new Dictionary<string, object>
					{
						{ "operationId", operation.OperationId }
					});
				return false;
			}

			if ((operation.LightType == LightType.Point ||
				operation.LightType == LightType.Spot) && !operation.Range.HasValue)
			{
				issue = CreateMutationIssue(
					"LIGHT_RANGE_REQUIRED",
					"PointまたはSpotのLIGHT_CREATEではrangeを明示してください。",
					null);
				return false;
			}

			if (operation.LightType == LightType.Spot && !operation.SpotAngle.HasValue)
			{
				issue = CreateMutationIssue(
					"SPOT_ANGLE_REQUIRED",
					"SpotのLIGHT_CREATEではspotAngleを明示してください。",
					null);
				return false;
			}

			Scene scene;
			if (!TryResolveLoadedScene(input.targetScenePath, out scene))
			{
				issue = CreateMutationIssue(
					"TARGET_SCENE_NOT_LOADED",
					"LIGHT_CREATEのtargetScenePathがLoaded Sceneとして解決できません。",
					new Dictionary<string, object>
					{
						{ "targetScenePath", input.targetScenePath }
					});
				return false;
			}

			operation.TargetSceneHandle = scene.handle;
			operation.TargetScenePath = NormalizeSceneLabel(scene);
			return true;
		}

		private static bool PrepareUpdateOperation(
			UnityGraphicsMcpPreparedLightOperation operation,
			out UnityGraphicsMcpIssue issue)
		{
			issue = null;
			if (string.IsNullOrWhiteSpace(operation.TargetObjectId))
			{
				issue = CreateMutationIssue(
					"LIGHT_UPDATE_TARGET_REQUIRED",
					"LIGHT_UPDATEではinspect_sceneが返した安定Object IDを指定してください。",
					null);
				return false;
			}

			Light light;
			if (!TryResolveLightByObjectId(operation.TargetObjectId, out light))
			{
				issue = CreateMutationIssue(
					"LIGHT_UPDATE_TARGET_NOT_FOUND",
					"targetObjectIdからLoaded SceneのLightを解決できません。",
					new Dictionary<string, object>
					{
						{ "targetObjectId", operation.TargetObjectId }
					});
				return false;
			}

			if (!HasAnyUpdateValue(operation))
			{
				issue = CreateMutationIssue(
					"LIGHT_UPDATE_VALUE_REQUIRED",
					"LIGHT_UPDATEでは一つ以上の変更値を指定してください。",
					null);
				return false;
			}

			operation.TargetInstanceId = light.GetInstanceID();
			operation.TargetSceneHandle = light.gameObject.scene.handle;
			operation.TargetScenePath = NormalizeSceneLabel(light.gameObject.scene);
			operation.BaselineState = CaptureLightState(light);
			return true;
		}

		private static bool ValidateNumericValues(
			UnityGraphicsMcpPreparedLightOperation operation,
			out UnityGraphicsMcpIssue issue)
		{
			issue = null;
			if ((operation.Intensity.HasValue &&
				(!IsFinite(operation.Intensity.Value) || operation.Intensity.Value < 0.0f)) ||
				(operation.Range.HasValue &&
				(!IsFinite(operation.Range.Value) || operation.Range.Value <= 0.0f)) ||
				(operation.SpotAngle.HasValue &&
				(!IsFinite(operation.SpotAngle.Value) ||
					operation.SpotAngle.Value < 1.0f ||
					operation.SpotAngle.Value > 179.0f)))
			{
				issue = CreateMutationIssue(
					"LIGHT_NUMERIC_VALUE_OUT_OF_RANGE",
					"intensityは0以上、rangeは0より大きく、spotAngleは1～179で指定してください。",
					new Dictionary<string, object>
					{
						{ "operationId", operation.OperationId }
					});
				return false;
			}

			return true;
		}

		private static bool ValidatePreparedPlanStillMatches(
			UnityGraphicsMcpExecutableLightPlan plan,
			out List<UnityGraphicsMcpIssue> issues)
		{
			issues = new List<UnityGraphicsMcpIssue>();

			foreach (UnityGraphicsMcpPreparedLightOperation operation in plan.Operations)
			{
				if (operation.Operation == "LIGHT_CREATE")
				{
					Scene scene;
					if (!TryResolveLoadedSceneByHandle(operation.TargetSceneHandle, out scene) ||
						NormalizeSceneLabel(scene) != operation.TargetScenePath)
					{
						issues.Add(CreateMutationIssue(
							"TARGET_SCENE_CHANGED",
							"LIGHT_CREATE対象SceneがPreview時と一致しません。",
							new Dictionary<string, object>
							{
								{ "operationId", operation.OperationId },
								{ "targetScene", operation.TargetScenePath }
							}));
					}
					continue;
				}

				Light light = ResolveLightByInstanceId(operation.TargetInstanceId);
				if (light == null ||
					!string.Equals(
						Fingerprint(CaptureLightState(light)),
						Fingerprint(operation.BaselineState),
						StringComparison.Ordinal))
				{
					issues.Add(CreateMutationIssue(
						"LIGHT_BASELINE_CHANGED",
						"LIGHT_UPDATE対象がPreview時の状態と一致しません。",
						new Dictionary<string, object>
						{
							{ "operationId", operation.OperationId },
							{ "targetObjectId", operation.TargetObjectId }
						}));
				}
			}

			return issues.Count == 0;
		}

		private static bool VerifyTransactionPostState(
			UnityGraphicsMcpMutationTransaction transaction,
			out List<UnityGraphicsMcpIssue> issues)
		{
			issues = new List<UnityGraphicsMcpIssue>();
			foreach (KeyValuePair<int, UnityGraphicsMcpLightState> pair in transaction.AfterStates)
			{
				Light light = ResolveLightByInstanceId(pair.Key);
				if (light == null ||
					Fingerprint(CaptureLightState(light)) != Fingerprint(pair.Value))
				{
					issues.Add(CreateMutationIssue(
						"TRANSACTION_POST_STATE_CHANGED",
						"LightがTransaction適用直後の状態から変更されています。",
						new Dictionary<string, object>
						{
							{ "instanceId", pair.Key }
						}));
				}
			}
			return issues.Count == 0;
		}

		private static bool VerifyTransactionRestored(
			UnityGraphicsMcpMutationTransaction transaction,
			out List<UnityGraphicsMcpIssue> issues)
		{
			issues = new List<UnityGraphicsMcpIssue>();

			foreach (int instanceId in transaction.CreatedInstanceIds)
			{
				if (ResolveLightByInstanceId(instanceId) != null)
				{
					issues.Add(CreateMutationIssue(
						"CREATED_LIGHT_NOT_REMOVED",
						"Undo後も作成Lightが残っています。",
						new Dictionary<string, object> { { "instanceId", instanceId } }));
				}
			}

			foreach (KeyValuePair<int, UnityGraphicsMcpLightState> pair in transaction.BeforeStates)
			{
				Light light = ResolveLightByInstanceId(pair.Key);
				if (light == null ||
					Fingerprint(CaptureLightState(light)) != Fingerprint(pair.Value))
				{
					issues.Add(CreateMutationIssue(
						"MODIFIED_LIGHT_NOT_RESTORED",
						"Undo後のLightがTransaction前の状態と一致しません。",
						new Dictionary<string, object> { { "instanceId", pair.Key } }));
				}
			}

			return issues.Count == 0;
		}

		private static void ApplyLightOperation(
			Light light,
			UnityGraphicsMcpPreparedLightOperation operation)
		{
			if (operation.Name != null)
			{
				light.gameObject.name = operation.Name;
			}
			if (operation.LightType.HasValue)
			{
				light.type = operation.LightType.Value;
			}
			if (operation.Color.HasValue)
			{
				light.color = operation.Color.Value;
			}
			if (operation.Intensity.HasValue)
			{
				light.intensity = operation.Intensity.Value;
			}
			if (operation.Range.HasValue)
			{
				light.range = operation.Range.Value;
			}
			if (operation.SpotAngle.HasValue)
			{
				light.spotAngle = operation.SpotAngle.Value;
			}
			if (operation.Shadows.HasValue)
			{
				light.shadows = operation.Shadows.Value;
			}
			if (operation.Position.HasValue)
			{
				light.transform.position = operation.Position.Value;
			}
			if (operation.EulerAngles.HasValue)
			{
				light.transform.eulerAngles = operation.EulerAngles.Value;
			}
			if (operation.Enabled.HasValue)
			{
				light.enabled = operation.Enabled.Value;
			}
		}

		private static List<Dictionary<string, object>> BuildOperationPreviews(
			List<UnityGraphicsMcpPreparedLightOperation> operations)
		{
			return operations.Select(operation =>
				new Dictionary<string, object>
				{
					{ "operationId", operation.OperationId },
					{ "operation", operation.Operation },
					{ "targetObjectId", operation.TargetObjectId },
					{ "targetScene", operation.TargetScenePath },
					{ "before", operation.BaselineState == null ? null : ToDictionary(operation.BaselineState) },
					{ "after", BuildExpectedState(operation) },
					{ "requiresBake", false },
					{ "savePerformed", false }
				}).ToList();
		}

		private static Dictionary<string, object> BuildExpectedState(
			UnityGraphicsMcpPreparedLightOperation operation)
		{
			UnityGraphicsMcpLightState state = operation.BaselineState == null
				? new UnityGraphicsMcpLightState
				{
					Name = operation.Name,
					ScenePath = operation.TargetScenePath,
					LightType = operation.LightType.Value.ToString(),
					Color = operation.Color.Value,
					Intensity = operation.Intensity.Value,
					Range = operation.Range ?? 10.0f,
					SpotAngle = operation.SpotAngle ?? 30.0f,
					Shadows = operation.Shadows.Value.ToString(),
					Position = operation.Position.Value,
					EulerAngles = operation.EulerAngles.Value,
					Enabled = operation.Enabled.Value
				}
				: CopyState(operation.BaselineState);

			ApplyOperationToState(state, operation);
			return ToDictionary(state);
		}

		private static void ApplyOperationToState(
			UnityGraphicsMcpLightState state,
			UnityGraphicsMcpPreparedLightOperation operation)
		{
			if (operation.Name != null) state.Name = operation.Name;
			if (operation.LightType.HasValue) state.LightType = operation.LightType.Value.ToString();
			if (operation.Color.HasValue) state.Color = operation.Color.Value;
			if (operation.Intensity.HasValue) state.Intensity = operation.Intensity.Value;
			if (operation.Range.HasValue) state.Range = operation.Range.Value;
			if (operation.SpotAngle.HasValue) state.SpotAngle = operation.SpotAngle.Value;
			if (operation.Shadows.HasValue) state.Shadows = operation.Shadows.Value.ToString();
			if (operation.Position.HasValue) state.Position = operation.Position.Value;
			if (operation.EulerAngles.HasValue) state.EulerAngles = operation.EulerAngles.Value;
			if (operation.Enabled.HasValue) state.Enabled = operation.Enabled.Value;
		}

		private static UnityGraphicsMcpLightState CaptureLightState(Light light)
		{
			string stability;
			return new UnityGraphicsMcpLightState
			{
				InstanceId = light.GetInstanceID(),
				ObjectId = ResolveObjectId(light, out stability),
				Name = light.gameObject.name,
				ScenePath = NormalizeSceneLabel(light.gameObject.scene),
				LightType = light.type.ToString(),
				Color = light.color,
				Intensity = light.intensity,
				Range = light.range,
				SpotAngle = light.spotAngle,
				Shadows = light.shadows.ToString(),
				Position = light.transform.position,
				EulerAngles = light.transform.eulerAngles,
				Enabled = light.enabled
			};
		}

		private static UnityGraphicsMcpLightState CopyState(UnityGraphicsMcpLightState source)
		{
			return new UnityGraphicsMcpLightState
			{
				InstanceId = source.InstanceId,
				ObjectId = source.ObjectId,
				Name = source.Name,
				ScenePath = source.ScenePath,
				LightType = source.LightType,
				Color = source.Color,
				Intensity = source.Intensity,
				Range = source.Range,
				SpotAngle = source.SpotAngle,
				Shadows = source.Shadows,
				Position = source.Position,
				EulerAngles = source.EulerAngles,
				Enabled = source.Enabled
			};
		}

		private static Dictionary<string, object> ToDictionary(
			UnityGraphicsMcpLightState state)
		{
			return new Dictionary<string, object>
			{
				{ "objectId", state.ObjectId },
				{ "name", state.Name },
				{ "scenePath", state.ScenePath },
				{ "lightType", state.LightType },
				{ "color", ColorToDictionary(state.Color) },
				{ "intensity", state.Intensity },
				{ "range", state.Range },
				{ "spotAngle", state.SpotAngle },
				{ "shadows", state.Shadows },
				{ "position", VectorToDictionary(state.Position) },
				{ "eulerAngles", VectorToDictionary(state.EulerAngles) },
				{ "enabled", state.Enabled }
			};
		}

		private static Dictionary<string, object> ColorToDictionary(Color color)
		{
			return new Dictionary<string, object>
			{
				{ "r", color.r },
				{ "g", color.g },
				{ "b", color.b },
				{ "a", color.a }
			};
		}

		private static string BuildPlanDigest(UnityGraphicsMcpExecutableLightPlan plan)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(plan.DirectionPlanId).Append('|');
			builder.Append(plan.Revision.ToString(CultureInfo.InvariantCulture)).Append('|');

			foreach (UnityGraphicsMcpPreparedLightOperation operation in plan.Operations)
			{
				builder.Append(operation.OperationId).Append('|');
				builder.Append(operation.Operation).Append('|');
				builder.Append(operation.TargetObjectId).Append('|');
				builder.Append(operation.TargetScenePath).Append('|');
				builder.Append(operation.Name).Append('|');
				builder.Append(operation.LightType).Append('|');
				builder.Append(operation.Color).Append('|');
				builder.Append(operation.Intensity).Append('|');
				builder.Append(operation.Range).Append('|');
				builder.Append(operation.SpotAngle).Append('|');
				builder.Append(operation.Shadows).Append('|');
				builder.Append(operation.Position).Append('|');
				builder.Append(operation.EulerAngles).Append('|');
				builder.Append(operation.Enabled).Append('|');
				builder.Append(Fingerprint(operation.BaselineState)).Append(';');
			}

			return UnityGraphicsMcpMutationSession.HashText(builder.ToString());
		}

		private static string Fingerprint(UnityGraphicsMcpLightState state)
		{
			if (state == null)
			{
				return "NONE";
			}

			return UnityGraphicsMcpMutationSession.HashText(string.Join("|", new[]
			{
				state.InstanceId.ToString(CultureInfo.InvariantCulture),
				state.ObjectId ?? string.Empty,
				state.Name ?? string.Empty,
				state.ScenePath ?? string.Empty,
				state.LightType ?? string.Empty,
				FormatColor(state.Color),
				FormatFloat(state.Intensity),
				FormatFloat(state.Range),
				FormatFloat(state.SpotAngle),
				state.Shadows ?? string.Empty,
				FormatVector(state.Position),
				FormatVector(state.EulerAngles),
				state.Enabled.ToString()
			}));
		}

		private static string FormatColor(Color value)
		{
			return FormatFloat(value.r) + "," +
				FormatFloat(value.g) + "," +
				FormatFloat(value.b) + "," +
				FormatFloat(value.a);
		}

		private static string FormatVector(Vector3 value)
		{
			return FormatFloat(value.x) + "," +
				FormatFloat(value.y) + "," +
				FormatFloat(value.z);
		}

		private static string FormatFloat(float value)
		{
			return value.ToString("R", CultureInfo.InvariantCulture);
		}

		private static bool TryParseLightType(string value, out LightType? lightType)
		{
			lightType = null;
			if (string.IsNullOrWhiteSpace(value))
			{
				return true;
			}

			LightType parsed;
			if (!Enum.TryParse(value, true, out parsed) ||
				(parsed != LightType.Directional &&
					parsed != LightType.Point &&
					parsed != LightType.Spot))
			{
				return false;
			}

			lightType = parsed;
			return true;
		}

		private static bool TryParseShadows(string value, out LightShadows? shadows)
		{
			shadows = null;
			if (string.IsNullOrWhiteSpace(value))
			{
				return true;
			}

			LightShadows parsed;
			if (!Enum.TryParse(value, true, out parsed) ||
				!Enum.IsDefined(typeof(LightShadows), parsed))
			{
				return false;
			}

			shadows = parsed;
			return true;
		}

		private static bool TryParseColor(
			UnityGraphicsMcpColorInput input,
			out Color? color)
		{
			color = null;
			if (input == null)
			{
				return true;
			}

			if (!IsFinite(input.r) || input.r < 0.0f ||
				!IsFinite(input.g) || input.g < 0.0f ||
				!IsFinite(input.b) || input.b < 0.0f ||
				!IsFinite(input.a) || input.a < 0.0f || input.a > 1.0f)
			{
				return false;
			}

			color = new Color(input.r, input.g, input.b, input.a);
			return true;
		}

		private static bool TryParseVector(
			UnityGraphicsMcpVector3Input input,
			out Vector3? vector)
		{
			vector = null;
			if (input == null)
			{
				return true;
			}

			if (!IsFinite(input.x) || !IsFinite(input.y) || !IsFinite(input.z))
			{
				return false;
			}

			vector = new Vector3(input.x, input.y, input.z);
			return true;
		}

		private static bool HasAnyUpdateValue(
			UnityGraphicsMcpPreparedLightOperation operation)
		{
			return operation.Name != null ||
				operation.LightType.HasValue ||
				operation.Color.HasValue ||
				operation.Intensity.HasValue ||
				operation.Range.HasValue ||
				operation.SpotAngle.HasValue ||
				operation.Shadows.HasValue ||
				operation.Position.HasValue ||
				operation.EulerAngles.HasValue ||
				operation.Enabled.HasValue;
		}

		private static bool TryResolveLoadedSceneByHandle(int handle, out Scene scene)
		{
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene candidate = SceneManager.GetSceneAt(index);
				if (candidate.IsValid() && candidate.isLoaded && candidate.handle == handle)
				{
					scene = candidate;
					return true;
				}
			}

			scene = default(Scene);
			return false;
		}

		private static bool TryResolveLoadedScene(string path, out Scene scene)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				scene = SceneManager.GetActiveScene();
				return scene.IsValid() && scene.isLoaded;
			}

			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene candidate = SceneManager.GetSceneAt(index);
				if (candidate.isLoaded &&
					(string.Equals(candidate.path, path, StringComparison.Ordinal) ||
						string.Equals(candidate.name, path, StringComparison.Ordinal)))
				{
					scene = candidate;
					return true;
				}
			}

			scene = default(Scene);
			return false;
		}

		private static string NormalizeSceneLabel(Scene scene)
		{
			return string.IsNullOrEmpty(scene.path)
				? "UNTITLED_SCENE:" + scene.handle.ToString(CultureInfo.InvariantCulture)
				: scene.path;
		}

		private static bool TryResolveLightByObjectId(string objectId, out Light light)
		{
			light = null;
			GlobalObjectId globalObjectId;
			if (!GlobalObjectId.TryParse(objectId, out globalObjectId))
			{
				return false;
			}

			Object target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
			light = target as Light;
			if (light == null)
			{
				GameObject gameObject = target as GameObject;
				light = gameObject == null ? null : gameObject.GetComponent<Light>();
			}

			return light != null &&
				light.gameObject.scene.IsValid() &&
				light.gameObject.scene.isLoaded;
		}

		private static Light ResolveLightByInstanceId(int instanceId)
		{
			Object target = EditorUtility.InstanceIDToObject(instanceId);
			return target as Light;
		}

		private static bool IsFinite(float value)
		{
			return !float.IsNaN(value) && !float.IsInfinity(value);
		}

		private static UnityGraphicsMcpIssue CreateMutationIssue(
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
