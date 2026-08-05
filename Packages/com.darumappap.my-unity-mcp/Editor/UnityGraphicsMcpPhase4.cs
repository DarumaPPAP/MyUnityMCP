#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpSaveTargetInput
	{
		public string scenePath { get; set; }
	}

	internal sealed class UnityGraphicsMcpSaveSceneBaseline
	{
		public int SceneHandle { get; set; }
		public string ScenePath { get; set; }
		public bool WasDirty { get; set; }
		public string ContentDigest { get; set; }
	}

	internal sealed class UnityGraphicsMcpExecutableSavePlan
	{
		public string PlanId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public DateTime ExpiresUtc { get; set; }
		public string ApprovalTokenHash { get; set; }
		public string DiffDigest { get; set; }
		public bool Consumed { get; set; }
		public UnityGraphicsMcpSaveSceneBaseline Target { get; set; }
	}

	internal sealed class UnityGraphicsMcpCaptureRecord
	{
		public string CaptureId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public string CameraObjectId { get; set; }
		public string OutputPath { get; set; }
		public string Sha256 { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
	}

	internal static class UnityGraphicsMcpPhase4Session
	{
		private const int MAX_SAVE_PLAN_COUNT = 8;
		private const int MAX_CAPTURE_COUNT = 8;
		private static readonly TimeSpan SAVE_PLAN_LIFETIME = TimeSpan.FromMinutes(10.0);
		private static readonly TimeSpan CAPTURE_LIFETIME = TimeSpan.FromMinutes(30.0);
		private static readonly Dictionary<string, UnityGraphicsMcpExecutableSavePlan> _savePlans =
			new Dictionary<string, UnityGraphicsMcpExecutableSavePlan>();
		private static readonly Dictionary<string, UnityGraphicsMcpCaptureRecord> _captures =
			new Dictionary<string, UnityGraphicsMcpCaptureRecord>();

		static UnityGraphicsMcpPhase4Session()
		{
			EditorApplication.playModeStateChanged += state => Clear();
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			CompilationPipeline.compilationStarted += context => Clear();
			EditorApplication.quitting += Clear;
		}

		public static string StoreSavePlan(UnityGraphicsMcpExecutableSavePlan plan)
		{
			RemoveExpiredSavePlans();
			RemoveOldestSavePlansWhenFull();

			plan.PlanId = UnityGraphicsMcpSession.SessionId +
				":save-plan:" + Guid.NewGuid().ToString("N");
			plan.CreatedUtc = DateTime.UtcNow;
			plan.ExpiresUtc = plan.CreatedUtc + SAVE_PLAN_LIFETIME;
			_savePlans[plan.PlanId] = plan;
			return plan.PlanId;
		}

		public static bool TryGetSavePlan(
			string planId,
			long expectedRevision,
			string approvalToken,
			out UnityGraphicsMcpExecutableSavePlan plan,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			plan = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;
			RemoveExpiredSavePlans();

			if (string.IsNullOrWhiteSpace(planId) ||
				!planId.StartsWith(
					UnityGraphicsMcpSession.SessionId + ":save-plan:",
					StringComparison.Ordinal) ||
				!_savePlans.TryGetValue(planId, out plan))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Save Planが現在のEditor Sessionに存在しないか有効期限切れです。";
				return false;
			}

			if (plan.Consumed)
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "Save Planは既に使用済みです。";
				return false;
			}

			if (expectedRevision != UnityGraphicsMcpSession.Revision ||
				plan.Revision != UnityGraphicsMcpSession.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Save Plan作成後にEditor Revisionが変更されました。";
				return false;
			}

			if (string.IsNullOrWhiteSpace(approvalToken) ||
				!string.Equals(
					plan.ApprovalTokenHash,
					HashText(approvalToken),
					StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "Save承認Tokenが不足しているか一致しません。";
				return false;
			}

			return true;
		}

		public static void ConsumeSavePlan(UnityGraphicsMcpExecutableSavePlan plan)
		{
			if (plan != null)
			{
				plan.Consumed = true;
			}
		}

		public static string StoreCapture(UnityGraphicsMcpCaptureRecord capture)
		{
			RemoveExpiredCaptures();
			RemoveOldestCapturesWhenFull();

			capture.CaptureId = UnityGraphicsMcpSession.SessionId +
				":capture:" + Guid.NewGuid().ToString("N");
			capture.CreatedUtc = DateTime.UtcNow;
			_captures[capture.CaptureId] = capture;
			return capture.CaptureId;
		}

		public static bool TryGetCapture(
			string captureId,
			long expectedRevision,
			out UnityGraphicsMcpCaptureRecord capture,
			out E_MCP_TOOL_STATUS failureStatus)
		{
			capture = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			RemoveExpiredCaptures();

			if (string.IsNullOrWhiteSpace(captureId) ||
				!captureId.StartsWith(
					UnityGraphicsMcpSession.SessionId + ":capture:",
					StringComparison.Ordinal) ||
				!_captures.TryGetValue(captureId, out capture))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				return false;
			}

			if (expectedRevision != UnityGraphicsMcpSession.Revision ||
				capture.Revision != UnityGraphicsMcpSession.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				return false;
			}

			return true;
		}

		public static string HashText(string value)
		{
			using (SHA256 sha256 = SHA256.Create())
			{
				byte[] bytes = sha256.ComputeHash(
					Encoding.UTF8.GetBytes(value ?? string.Empty));
				return ToHex(bytes);
			}
		}

		public static void ClearForTests()
		{
			Clear();
		}

		private static void RemoveExpiredSavePlans()
		{
			DateTime now = DateTime.UtcNow;
			foreach (string id in _savePlans
				.Where(pair => pair.Value.ExpiresUtc <= now)
				.Select(pair => pair.Key)
				.ToArray())
			{
				_savePlans.Remove(id);
			}
		}

		private static void RemoveExpiredCaptures()
		{
			DateTime threshold = DateTime.UtcNow - CAPTURE_LIFETIME;
			foreach (string id in _captures
				.Where(pair => pair.Value.CreatedUtc < threshold)
				.Select(pair => pair.Key)
				.ToArray())
			{
				_captures.Remove(id);
			}
		}

		private static void RemoveOldestSavePlansWhenFull()
		{
			while (_savePlans.Count >= MAX_SAVE_PLAN_COUNT)
			{
				string oldestId = _savePlans
					.OrderBy(pair => pair.Value.CreatedUtc)
					.First()
					.Key;
				_savePlans.Remove(oldestId);
			}
		}

		private static void RemoveOldestCapturesWhenFull()
		{
			while (_captures.Count >= MAX_CAPTURE_COUNT)
			{
				string oldestId = _captures
					.OrderBy(pair => pair.Value.CreatedUtc)
					.First()
					.Key;
				_captures.Remove(oldestId);
			}
		}

		private static string ToHex(byte[] bytes)
		{
			StringBuilder builder = new StringBuilder(bytes.Length * 2);
			foreach (byte item in bytes)
			{
				builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
			}
			return builder.ToString();
		}

		private static void Clear()
		{
			_savePlans.Clear();
			_captures.Clear();
		}
	}

	/// <summary>
	/// 明示Save、Color Capture、Human Review起点のRefine Planを所有します。
	/// </summary>
	public static partial class UnityGraphicsMcpInspection
	{
		private const string SAVE_MODE_EXPLICIT_SCENE = "EXPLICIT_SCENE";
		private const int MIN_CAPTURE_SIZE = 64;
		private const int MAX_CAPTURE_SIZE = 4096;
		private const long MAX_CAPTURE_PIXEL_COUNT = 8388608L;

		public static UnityGraphicsMcpToolResult PrepareSavePlan(
			string requestId,
			long? expectedRevision,
			UnityGraphicsMcpSaveTargetInput[] targets)
		{
			return ExecuteReadOnly(
				"graphics.prepare_save_plan",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.prepare_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					if (expectedRevision.Value != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult(
							"graphics.prepare_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"expectedRevisionが現在のEditor Revisionと一致しません。",
							new Dictionary<string, object>
							{
								{ "expectedRevision", expectedRevision.Value },
								{ "currentRevision", UnityGraphicsMcpSession.Revision }
							});
					}

					if (targets == null ||
						targets.Length != 1 ||
						targets[0] == null ||
						string.IsNullOrWhiteSpace(targets[0].scenePath))
					{
						return CreateResult(
							"graphics.prepare_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Phase 4Aでは、一つの保存済みLoaded Sceneを明示指定してください。",
							null);
					}

					string scenePath = NormalizePhase4SceneAssetPath(targets[0].scenePath);
					if (!IsSupportedPhase4SceneAssetPath(scenePath))
					{
						return CreateResult(
							"graphics.prepare_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"scenePathはAssets配下の既存.unity Assetを指定してください。Save Asは行いません。",
							new Dictionary<string, object>
							{
								{ "scenePath", scenePath }
							});
					}

					Scene scene;
					if (!TryResolvePhase4LoadedScene(scenePath, out scene))
					{
						return CreateResult(
							"graphics.prepare_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"指定SceneはLoaded Sceneとして解決できません。",
							null);
					}

					if (!scene.isDirty)
					{
						return CreateResult(
							"graphics.prepare_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"指定SceneはDirtyではないため、保存対象がありません。",
							null);
					}

					UnityGraphicsMcpSaveSceneBaseline baseline =
						CapturePhase4SaveSceneBaseline(scene);
					string approvalToken = Guid.NewGuid().ToString("N") +
						Guid.NewGuid().ToString("N");
					UnityGraphicsMcpExecutableSavePlan plan =
						new UnityGraphicsMcpExecutableSavePlan
						{
							Revision = expectedRevision.Value,
							ApprovalTokenHash =
								UnityGraphicsMcpPhase4Session.HashText(approvalToken),
							Target = baseline
						};
					plan.DiffDigest = BuildPhase4SavePlanDigest(plan);
					UnityGraphicsMcpPhase4Session.StoreSavePlan(plan);

					return CreateResult(
						"graphics.prepare_save_plan",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Dirty Sceneの永続化差分をRead-onlyで固定し、明示承認待ちSave Planを作成しました。",
						new Dictionary<string, object>
						{
							{ "planId", plan.PlanId },
							{ "expectedRevision", plan.Revision },
							{ "approvalToken", approvalToken },
							{ "approvalTokenExpiresUtc", plan.ExpiresUtc.ToString("O") },
							{ "diffDigest", plan.DiffDigest },
							{ "scenePath", baseline.ScenePath },
							{ "before", new Dictionary<string, object>
								{
									{ "isDirty", true },
									{ "contentDigest", baseline.ContentDigest }
								}
							},
							{ "after", new Dictionary<string, object>
								{
									{ "isDirty", false },
									{ "persistentSceneFileUpdated", true }
								}
							},
							{ "saveMode", SAVE_MODE_EXPLICIT_SCENE },
							{ "savePerformed", false },
							{ "undoAvailable", false }
						});
				});
		}

		public static UnityGraphicsMcpToolResult ApplySavePlan(
			string requestId,
			string planId,
			long? expectedRevision,
			string approvalToken,
			string saveMode)
		{
			return ExecutePhase4PersistentOperation(
				"graphics.apply_save_plan",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.apply_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					string normalizedSaveMode = string.IsNullOrWhiteSpace(saveMode)
						? string.Empty
						: saveMode.Trim();
					if (!string.Equals(
						normalizedSaveMode,
						SAVE_MODE_EXPLICIT_SCENE,
						StringComparison.OrdinalIgnoreCase))
					{
						return CreateResult(
							"graphics.apply_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.UNSUPPORTED,
							"Phase 4AのsaveModeはEXPLICIT_SCENEだけです。",
							null);
					}

					UnityGraphicsMcpExecutableSavePlan plan;
					E_MCP_TOOL_STATUS failureStatus;
					string failureMessage;
					if (!UnityGraphicsMcpPhase4Session.TryGetSavePlan(
						planId,
						expectedRevision.Value,
						approvalToken,
						out plan,
						out failureStatus,
						out failureMessage))
					{
						return CreateResult(
							"graphics.apply_save_plan",
							requestId,
							failureStatus,
							failureMessage,
							new Dictionary<string, object>
							{
								{ "planId", planId },
								{ "currentRevision", UnityGraphicsMcpSession.Revision }
							});
					}

					Scene scene;
					if (!TryResolvePhase4LoadedSceneByHandleAndPath(
						plan.Target.SceneHandle,
						plan.Target.ScenePath,
						out scene))
					{
						return CreateResult(
							"graphics.apply_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"Save対象SceneがPreview時と同じLoaded Sceneとして解決できません。",
							null);
					}

					UnityGraphicsMcpSaveSceneBaseline current =
						CapturePhase4SaveSceneBaseline(scene);
					if (!current.WasDirty ||
						!string.Equals(
							current.ContentDigest,
							plan.Target.ContentDigest,
							StringComparison.Ordinal) ||
						!string.Equals(
							plan.DiffDigest,
							BuildPhase4SavePlanDigest(plan),
							StringComparison.Ordinal))
					{
						return CreateResult(
							"graphics.apply_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"Save Preview後にScene内容またはDirty状態が変化したため保存を拒否しました。",
							new Dictionary<string, object>
							{
								{ "scenePath", plan.Target.ScenePath },
								{ "previewDigest", plan.Target.ContentDigest },
								{ "currentDigest", current.ContentDigest },
								{ "currentDirty", current.WasDirty }
							});
					}

					long revisionBeforeSave = UnityGraphicsMcpSession.Revision;
					if (!EditorSceneManager.SaveScene(
						scene,
						plan.Target.ScenePath,
						false))
					{
						return CreateResult(
							"graphics.apply_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.FAILED,
							"Unity EditorがScene保存を完了できませんでした。",
							null);
					}

					if (scene.isDirty)
					{
						return CreateResult(
							"graphics.apply_save_plan",
							requestId,
							E_MCP_TOOL_STATUS.FAILED,
							"Scene保存後もDirty状態が残っているため、保存成功として扱いません。",
							null);
					}

					UnityGraphicsMcpPhase4Session.ConsumeSavePlan(plan);
					if (revisionBeforeSave == UnityGraphicsMcpSession.Revision)
					{
						UnityGraphicsMcpSession.NotifyMutationApplied();
					}

					return CreateResult(
						"graphics.apply_save_plan",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"明示承認された一つのDirty Sceneを保存しました。",
						new Dictionary<string, object>
						{
							{ "planId", plan.PlanId },
							{ "scenePath", plan.Target.ScenePath },
							{ "saveMode", SAVE_MODE_EXPLICIT_SCENE },
							{ "savePerformed", true },
							{ "undoAvailable", false },
							{ "revision", UnityGraphicsMcpSession.Revision }
						});
				});
		}

		public static UnityGraphicsMcpToolResult CaptureEvaluation(
			string requestId,
			string cameraObjectId,
			long? expectedRevision,
			int? width,
			int? height,
			string captureLabel)
		{
			return ExecutePhase4CaptureOperation(
				"graphics.capture_evaluation",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.capture_evaluation",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					if (expectedRevision.Value != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult(
							"graphics.capture_evaluation",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"expectedRevisionが現在のEditor Revisionと一致しません。",
							null);
					}

					Camera camera;
					if (!TryResolvePhase4Camera(cameraObjectId, out camera))
					{
						return CreateResult(
							"graphics.capture_evaluation",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"cameraObjectIdからLoaded SceneのCameraを解決できません。",
							null);
					}

					int captureWidth = width ?? 1280;
					int captureHeight = height ?? 720;
					if (!IsValidPhase4CaptureSize(captureWidth, captureHeight))
					{
						return CreateResult(
							"graphics.capture_evaluation",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Capture解像度は各辺64～4096、総Pixel数8388608以下で指定してください。",
							new Dictionary<string, object>
							{
								{ "width", captureWidth },
								{ "height", captureHeight }
							});
					}

					if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
					{
						return CreateResult(
							"graphics.capture_evaluation",
							requestId,
							E_MCP_TOOL_STATUS.UNVERIFIED,
							"Graphics DeviceがNullのためColor Captureを実行できません。",
							new Dictionary<string, object>
							{
								{ "graphicsDeviceType", SystemInfo.graphicsDeviceType.ToString() },
								{ "temporaryStateChanged", false },
								{ "humanReviewStatus", "PENDING" },
								{ "visualAccepted", false }
							});
					}

					return CapturePhase4Camera(
						requestId,
						cameraObjectId,
						camera,
						captureWidth,
						captureHeight,
						captureLabel);
				});
		}

		public static UnityGraphicsMcpToolResult RefineDirection(
			string requestId,
			string directionPlanId,
			string captureId,
			long? expectedRevision,
			string[] humanObservations,
			string[] requestedAdjustments)
		{
			return ExecuteReadOnly(
				"graphics.refine_direction",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.refine_direction",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					List<string> observations =
						NormalizePhase4ExplicitReviewValues(humanObservations);
					List<string> adjustments =
						NormalizePhase4ExplicitReviewValues(requestedAdjustments);
					if (observations.Count == 0 && adjustments.Count == 0)
					{
						return CreateResult(
							"graphics.refine_direction",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Human Reviewによる観察または調整要求を一つ以上指定してください。",
							null);
					}

					UnityGraphicsMcpDirectionPlan sourcePlan;
					E_MCP_TOOL_STATUS planFailureStatus;
					if (!UnityGraphicsMcpSession.TryGetPlan(
						directionPlanId,
						expectedRevision.Value,
						out sourcePlan,
						out planFailureStatus))
					{
						return CreateResult(
							"graphics.refine_direction",
							requestId,
							planFailureStatus,
							"Direction Planは現在のEditor SessionまたはRevisionでは利用できません。",
							null);
					}

					UnityGraphicsMcpCaptureRecord capture;
					E_MCP_TOOL_STATUS captureFailureStatus;
					if (!UnityGraphicsMcpPhase4Session.TryGetCapture(
						captureId,
						expectedRevision.Value,
						out capture,
						out captureFailureStatus))
					{
						return CreateResult(
							"graphics.refine_direction",
							requestId,
							captureFailureStatus,
							"Capture Evidenceは現在のEditor SessionまたはRevisionでは利用できません。",
							null);
					}

					Dictionary<string, object> refinedIntent =
						new Dictionary<string, object>(
							sourcePlan.VisualIntent ?? new Dictionary<string, object>());
					refinedIntent["refinementSourcePlanId"] = directionPlanId;
					refinedIntent["captureId"] = captureId;
					refinedIntent["humanObservations"] = observations;
					refinedIntent["requestedAdjustments"] = adjustments;
					refinedIntent["imageAnalysisPerformedByUnity"] = false;
					refinedIntent["humanReviewRequired"] = true;

					List<UnityGraphicsMcpPlanRecommendation> recommendations =
						(sourcePlan.Recommendations ??
							new List<UnityGraphicsMcpPlanRecommendation>())
						.Select(ClonePhase4Recommendation)
						.ToList();
					recommendations.Add(
						BuildPhase4RefineRecommendation(
							directionPlanId,
							captureId,
							observations,
							adjustments));

					UnityGraphicsMcpDirectionPlan refinedPlan =
						new UnityGraphicsMcpDirectionPlan
						{
							Revision = expectedRevision.Value,
							CreatedUtc = DateTime.UtcNow,
							ProjectContext = new Dictionary<string, object>(
								sourcePlan.ProjectContext ?? new Dictionary<string, object>()),
							VisualIntent = refinedIntent,
							Recommendations = recommendations,
							Issues = new List<UnityGraphicsMcpIssue>(
								sourcePlan.Issues ?? new List<UnityGraphicsMcpIssue>())
						};
					refinedPlan.Issues.Add(
						new UnityGraphicsMcpIssue
						{
							code = "VISUAL_ACCEPTANCE_REQUIRES_HUMAN_REVIEW",
							message = "Refine Planは自動的にVisual Acceptedとは判定されません。",
							evidence = new Dictionary<string, object>
							{
								{ "captureId", captureId },
								{ "humanReviewStatus", "PENDING" },
								{ "visualAccepted", false }
							}
						});
					UnityGraphicsMcpSession.StorePlan(refinedPlan);

					UnityGraphicsMcpToolResult result = CreateResult(
						"graphics.refine_direction",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Human Reviewを保持した次IterationのDirection PlanをRead-onlyで作成しました。",
						new Dictionary<string, object>
						{
							{ "sourcePlanId", directionPlanId },
							{ "captureId", captureId },
							{ "planId", refinedPlan.PlanId },
							{ "expectedRevision", refinedPlan.Revision },
							{ "humanObservations", observations },
							{ "requestedAdjustments", adjustments },
							{ "imageAnalysisPerformedByUnity", false },
							{ "humanReviewStatus", "PENDING" },
							{ "visualAccepted", false },
							{ "mutationApplied", false },
							{ "savePerformed", false },
							{ "bakePerformed", false }
						});
					result.issues.AddRange(refinedPlan.Issues);
					return result;
				});
		}

		private static UnityGraphicsMcpToolResult CapturePhase4Camera(
			string requestId,
			string cameraObjectId,
			Camera camera,
			int width,
			int height,
			string captureLabel)
		{
			long startRevision = UnityGraphicsMcpSession.Revision;
			Scene scene = camera.gameObject.scene;
			bool sceneDirtyBefore = scene.isDirty;
			RenderTexture originalTargetTexture = camera.targetTexture;
			RenderTexture originalActiveTexture = RenderTexture.active;
			RenderTexture temporaryTarget = null;
			Texture2D capturedTexture = null;
			string relativeOutputPath = null;
			string absoluteOutputPath = null;
			byte[] pngBytes = null;
			Exception captureException = null;

			try
			{
				temporaryTarget = new RenderTexture(
					width,
					height,
					24,
					RenderTextureFormat.ARGB32,
					RenderTextureReadWrite.Default)
				{
					name = "MyUnityMCP Evaluation Capture"
				};
				temporaryTarget.Create();

				camera.targetTexture = temporaryTarget;
				camera.Render();
				RenderTexture.active = temporaryTarget;

				capturedTexture = new Texture2D(
					width,
					height,
					TextureFormat.RGBA32,
					false);
				capturedTexture.ReadPixels(
					new Rect(0.0f, 0.0f, width, height),
					0,
					0,
					false);
				capturedTexture.Apply(false, false);
				pngBytes = capturedTexture.EncodeToPNG();

				relativeOutputPath = BuildPhase4CaptureOutputPath(captureLabel);
				absoluteOutputPath = ToPhase4ProjectAbsolutePath(relativeOutputPath);
				Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath));
				File.WriteAllBytes(absoluteOutputPath, pngBytes);
			}
			catch (Exception exception)
			{
				captureException = exception;
			}
			finally
			{
				camera.targetTexture = originalTargetTexture;
				RenderTexture.active = originalActiveTexture;

				if (capturedTexture != null)
				{
					Object.DestroyImmediate(capturedTexture);
				}

				if (temporaryTarget != null)
				{
					temporaryTarget.Release();
					Object.DestroyImmediate(temporaryTarget);
				}

				if (!sceneDirtyBefore && scene.IsValid() && scene.isDirty)
				{
					EditorSceneManager.ClearSceneDirtiness(scene);
				}
			}

			if (captureException != null)
			{
				DeletePhase4CaptureFile(absoluteOutputPath);
				return CreateResult(
					"graphics.capture_evaluation",
					requestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Camera Color Capture中に例外が発生しました。Editor一時状態は復元済みです。",
					new Dictionary<string, object>
					{
						{ "exceptionType", captureException.GetType().FullName },
						{ "message", captureException.Message },
						{ "temporaryStateRestored",
							camera.targetTexture == originalTargetTexture &&
							RenderTexture.active == originalActiveTexture },
						{ "sceneDirtyStatePreserved", scene.isDirty == sceneDirtyBefore }
					});
			}

			if (camera.targetTexture != originalTargetTexture ||
				RenderTexture.active != originalActiveTexture ||
				scene.isDirty != sceneDirtyBefore)
			{
				DeletePhase4CaptureFile(absoluteOutputPath);
				return CreateResult(
					"graphics.capture_evaluation",
					requestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Capture後のCamera TargetTexture、Active RenderTexture、Scene Dirty状態を復元できませんでした。",
					null);
			}

			if (startRevision != UnityGraphicsMcpSession.Revision)
			{
				DeletePhase4CaptureFile(absoluteOutputPath);
				return CreateResult(
					"graphics.capture_evaluation",
					requestId,
					E_MCP_TOOL_STATUS.STALE_DURING_SCAN,
					"Capture中にEditor Revisionが変更されたためEvidenceを破棄しました。",
					null);
			}

			if (pngBytes == null ||
				pngBytes.Length == 0 ||
				string.IsNullOrWhiteSpace(absoluteOutputPath) ||
				!File.Exists(absoluteOutputPath))
			{
				return CreateResult(
					"graphics.capture_evaluation",
					requestId,
					E_MCP_TOOL_STATUS.FAILED,
					"PNG Evidenceを生成または保存できませんでした。",
					null);
			}

			UnityGraphicsMcpCaptureRecord capture =
				new UnityGraphicsMcpCaptureRecord
				{
					Revision = startRevision,
					CameraObjectId = cameraObjectId,
					OutputPath = relativeOutputPath,
					Sha256 = HashPhase4Bytes(pngBytes),
					Width = width,
					Height = height
				};
			UnityGraphicsMcpPhase4Session.StoreCapture(capture);

			return CreateResult(
				"graphics.capture_evaluation",
				requestId,
				E_MCP_TOOL_STATUS.SUCCESS,
				"Camera Color CaptureをLibrary配下へ保存し、Editor一時状態を復元しました。",
				new Dictionary<string, object>
				{
					{ "captureId", capture.CaptureId },
					{ "cameraObjectId", capture.CameraObjectId },
					{ "outputPath", capture.OutputPath },
					{ "sha256", capture.Sha256 },
					{ "width", capture.Width },
					{ "height", capture.Height },
					{ "temporaryStateRestored", true },
					{ "sceneDirtyStatePreserved", true },
					{ "imageAnalysisPerformedByUnity", false },
					{ "humanReviewStatus", "PENDING" },
					{ "visualAccepted", false }
				});
		}

		private static UnityGraphicsMcpToolResult ExecutePhase4CaptureOperation(
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

			Dictionary<int, bool> sceneDirtyState = CapturePhase4SceneDirtyState();
			Dictionary<int, bool> assetDirtyState = CapturePhase4AssetDirtyState();
			int undoGroup = Undo.GetCurrentGroup();

			try
			{
				UnityGraphicsMcpToolResult result = operation();
				Dictionary<string, object> evidence;
				if (HasPhase4CaptureReadOnlyViolation(
					sceneDirtyState,
					assetDirtyState,
					undoGroup,
					out evidence))
				{
					return CreateResult(
						toolName,
						normalizedRequestId,
						E_MCP_TOOL_STATUS.READ_ONLY_CONTRACT_VIOLATION,
						"Capture Toolの実行前後でScene、AssetまたはUndo状態が変化しました。",
						evidence);
				}
				return result;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return CreateResult(
					toolName,
					normalizedRequestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Capture処理中に例外が発生しました。",
					new Dictionary<string, object>
					{
						{ "exceptionType", exception.GetType().FullName },
						{ "message", exception.Message }
					});
			}
		}

		private static UnityGraphicsMcpToolResult ExecutePhase4PersistentOperation(
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
					"永続化処理中に例外が発生しました。保存済みFileは自動Rollbackできません。",
					new Dictionary<string, object>
					{
						{ "exceptionType", exception.GetType().FullName },
						{ "message", exception.Message },
						{ "automaticRollback", false }
					});
			}
		}

		private static Dictionary<int, bool> CapturePhase4SceneDirtyState()
		{
			Dictionary<int, bool> states = new Dictionary<int, bool>();
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (scene.IsValid())
				{
					states[scene.handle] = scene.isDirty;
				}
			}
			return states;
		}

		private static Dictionary<int, bool> CapturePhase4AssetDirtyState()
		{
			Dictionary<int, bool> states = new Dictionary<int, bool>();
			foreach (Object target in Resources.FindObjectsOfTypeAll<Object>())
			{
				if (!IsPhase4ProjectAsset(target))
				{
					continue;
				}
				states[target.GetInstanceID()] = EditorUtility.IsDirty(target);
			}
			return states;
		}

		private static bool HasPhase4CaptureReadOnlyViolation(
			Dictionary<int, bool> sceneDirtyState,
			Dictionary<int, bool> assetDirtyState,
			int undoGroup,
			out Dictionary<string, object> evidence)
		{
			evidence = new Dictionary<string, object>();
			List<Dictionary<string, object>> changedScenes =
				new List<Dictionary<string, object>>();
			List<Dictionary<string, object>> changedAssets =
				new List<Dictionary<string, object>>();

			if (sceneDirtyState.Count != SceneManager.sceneCount)
			{
				evidence["sceneCountBefore"] = sceneDirtyState.Count;
				evidence["sceneCountAfter"] = SceneManager.sceneCount;
			}

			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				bool beforeDirty;
				if (scene.IsValid() &&
					(!sceneDirtyState.TryGetValue(scene.handle, out beforeDirty) ||
					 beforeDirty != scene.isDirty))
				{
					changedScenes.Add(new Dictionary<string, object>
					{
						{ "scene", scene.path },
						{ "beforeDirty", beforeDirty },
						{ "afterDirty", scene.isDirty }
					});
				}
			}

			foreach (Object target in Resources.FindObjectsOfTypeAll<Object>())
			{
				if (!IsPhase4ProjectAsset(target))
				{
					continue;
				}

				bool beforeDirty;
				bool afterDirty = EditorUtility.IsDirty(target);
				bool existedBefore = assetDirtyState.TryGetValue(
					target.GetInstanceID(),
					out beforeDirty);
				if ((existedBefore && beforeDirty != afterDirty) ||
					(!existedBefore && afterDirty))
				{
					changedAssets.Add(new Dictionary<string, object>
					{
						{ "asset", target.name },
						{ "assetPath", AssetDatabase.GetAssetPath(target) },
						{ "beforeDirty", existedBefore ? (object)beforeDirty : null },
						{ "afterDirty", afterDirty }
					});
				}
			}

			int currentUndoGroup = Undo.GetCurrentGroup();
			if (changedScenes.Count > 0)
			{
				evidence["changedScenes"] = changedScenes;
			}
			if (changedAssets.Count > 0)
			{
				evidence["changedAssets"] = changedAssets;
			}
			if (currentUndoGroup != undoGroup)
			{
				evidence["undoGroupBefore"] = undoGroup;
				evidence["undoGroupAfter"] = currentUndoGroup;
			}

			return changedScenes.Count > 0 ||
				changedAssets.Count > 0 ||
				currentUndoGroup != undoGroup ||
				sceneDirtyState.Count != SceneManager.sceneCount;
		}

		private static bool IsPhase4ProjectAsset(Object target)
		{
			return target != null &&
				EditorUtility.IsPersistent(target) &&
				!string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(target));
		}

		private static UnityGraphicsMcpPlanRecommendation BuildPhase4RefineRecommendation(
			string directionPlanId,
			string captureId,
			List<string> observations,
			List<string> adjustments)
		{
			return new UnityGraphicsMcpPlanRecommendation
			{
				recommendationId = "REFINE-" + Guid.NewGuid().ToString("N"),
				section = "LOOK",
				recommendedValue = new Dictionary<string, object>
				{
					{ "humanObservations", observations },
					{ "requestedAdjustments", adjustments }
				},
				reason = "Capture Evidenceに対する明示的なHuman Reviewを次のDirection Iterationへ反映します。",
				dependencies = new List<string>
				{
					directionPlanId,
					captureId
				},
				confidence = E_GRAPHICS_PLAN_CONFIDENCE.MEDIUM.ToString(),
				pipelineImpact = "PLAN_ONLY",
				platformImpact = new List<string>(),
				verificationLevel =
					E_GRAPHICS_PLAN_VERIFICATION.HUMAN_REVIEW_REQUIRED.ToString(),
				nativeMutationBackendStatus = "PLAN_ONLY"
			};
		}

		private static UnityGraphicsMcpSaveSceneBaseline CapturePhase4SaveSceneBaseline(
			Scene scene)
		{
			return new UnityGraphicsMcpSaveSceneBaseline
			{
				SceneHandle = scene.handle,
				ScenePath = scene.path,
				WasDirty = scene.isDirty,
				ContentDigest = BuildPhase4SceneContentDigest(scene)
			};
		}

		private static string BuildPhase4SavePlanDigest(
			UnityGraphicsMcpExecutableSavePlan plan)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(plan.Revision).Append('|');
			builder.Append(plan.Target.SceneHandle).Append('|');
			builder.Append(plan.Target.ScenePath).Append('|');
			builder.Append(plan.Target.WasDirty).Append('|');
			builder.Append(plan.Target.ContentDigest);
			return UnityGraphicsMcpPhase4Session.HashText(builder.ToString());
		}

		private static string BuildPhase4SceneContentDigest(Scene scene)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(scene.handle).Append('|');
			builder.Append(scene.path).Append('|');
			builder.Append(scene.isDirty).Append('|');

			List<GameObject> objects = scene.GetRootGameObjects()
				.SelectMany(root =>
					root.GetComponentsInChildren<Transform>(true)
						.Select(transform => transform.gameObject))
				.OrderBy(BuildPhase4StableHierarchyPath, StringComparer.Ordinal)
				.ToList();

			foreach (GameObject gameObject in objects)
			{
				builder.Append(BuildPhase4StableHierarchyPath(gameObject)).Append('|');
				builder.Append(gameObject.activeSelf).Append('|');
				builder.Append(gameObject.layer).Append('|');
				builder.Append(gameObject.tag).Append('|');
				builder.Append(gameObject.isStatic).Append('|');

				Transform transform = gameObject.transform;
				AppendPhase4Vector(builder, transform.localPosition);
				AppendPhase4Vector(builder, transform.localEulerAngles);
				AppendPhase4Vector(builder, transform.localScale);

				Component[] components = gameObject
					.GetComponents<Component>()
					.Where(component => component != null)
					.OrderBy(component => component.GetType().AssemblyQualifiedName)
					.ThenBy(component => component.GetInstanceID())
					.ToArray();
				foreach (Component component in components)
				{
					builder.Append(component.GetType().AssemblyQualifiedName).Append('|');
					if (!(component is Transform))
					{
						builder.Append(EditorJsonUtility.ToJson(component, false));
					}
					builder.Append('|');
				}
			}

			return UnityGraphicsMcpPhase4Session.HashText(builder.ToString());
		}

		private static void AppendPhase4Vector(
			StringBuilder builder,
			Vector3 value)
		{
			builder.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
			builder.Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
			builder.Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
		}

		private static string BuildPhase4StableHierarchyPath(GameObject gameObject)
		{
			List<string> parts = new List<string>();
			Transform current = gameObject.transform;
			while (current != null)
			{
				parts.Add(
					current.name + "[" +
					current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) +
					"]");
				current = current.parent;
			}
			parts.Reverse();
			return string.Join("/", parts);
		}

		private static string NormalizePhase4SceneAssetPath(string scenePath)
		{
			return string.IsNullOrWhiteSpace(scenePath)
				? string.Empty
				: scenePath.Trim().Replace('\\', '/');
		}

		private static bool IsSupportedPhase4SceneAssetPath(string scenePath)
		{
			return scenePath.StartsWith("Assets/", StringComparison.Ordinal) &&
				scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
				AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;
		}

		private static bool TryResolvePhase4LoadedScene(
			string scenePath,
			out Scene scene)
		{
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene candidate = SceneManager.GetSceneAt(index);
				if (candidate.IsValid() &&
					candidate.isLoaded &&
					string.Equals(candidate.path, scenePath, StringComparison.Ordinal))
				{
					scene = candidate;
					return true;
				}
			}
			scene = default;
			return false;
		}

		private static bool TryResolvePhase4LoadedSceneByHandleAndPath(
			int sceneHandle,
			string scenePath,
			out Scene scene)
		{
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene candidate = SceneManager.GetSceneAt(index);
				if (candidate.IsValid() &&
					candidate.isLoaded &&
					candidate.handle == sceneHandle &&
					string.Equals(candidate.path, scenePath, StringComparison.Ordinal))
				{
					scene = candidate;
					return true;
				}
			}
			scene = default;
			return false;
		}

		private static bool TryResolvePhase4Camera(
			string objectId,
			out Camera camera)
		{
			camera = null;
			GlobalObjectId globalObjectId;
			if (string.IsNullOrWhiteSpace(objectId) ||
				!GlobalObjectId.TryParse(objectId, out globalObjectId))
			{
				return false;
			}

			Object target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
			camera = target as Camera;
			if (camera == null)
			{
				GameObject gameObject = target as GameObject;
				if (gameObject != null)
				{
					camera = gameObject.GetComponent<Camera>();
				}
			}

			return camera != null &&
				camera.gameObject.scene.IsValid() &&
				camera.gameObject.scene.isLoaded;
		}

		private static bool IsValidPhase4CaptureSize(int width, int height)
		{
			return width >= MIN_CAPTURE_SIZE &&
				width <= MAX_CAPTURE_SIZE &&
				height >= MIN_CAPTURE_SIZE &&
				height <= MAX_CAPTURE_SIZE &&
				(long)width * height <= MAX_CAPTURE_PIXEL_COUNT;
		}

		private static string BuildPhase4CaptureOutputPath(string captureLabel)
		{
			string label = SanitizePhase4FileName(captureLabel);
			if (string.IsNullOrWhiteSpace(label))
			{
				label = "evaluation";
			}
			return "Library/MyUnityMCP/Captures/" +
				DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) +
				"-" + label + "-" + Guid.NewGuid().ToString("N") + ".png";
		}

		private static string SanitizePhase4FileName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			HashSet<char> invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
			StringBuilder builder = new StringBuilder();
			foreach (char character in value.Trim())
			{
				if (!invalid.Contains(character) && !char.IsControl(character))
				{
					builder.Append(character);
				}
			}

			string result = builder.ToString();
			return result.Length <= 48 ? result : result.Substring(0, 48);
		}

		private static string ToPhase4ProjectAbsolutePath(string relativePath)
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			return Path.GetFullPath(
				Path.Combine(
					projectRoot,
					relativePath.Replace('/', Path.DirectorySeparatorChar)));
		}

		private static string HashPhase4Bytes(byte[] value)
		{
			using (SHA256 sha256 = SHA256.Create())
			{
				byte[] bytes = sha256.ComputeHash(value);
				StringBuilder builder = new StringBuilder(bytes.Length * 2);
				foreach (byte item in bytes)
				{
					builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
				}
				return builder.ToString();
			}
		}

		private static void DeletePhase4CaptureFile(string absolutePath)
		{
			if (!string.IsNullOrWhiteSpace(absolutePath) && File.Exists(absolutePath))
			{
				File.Delete(absolutePath);
			}
		}

		private static List<string> NormalizePhase4ExplicitReviewValues(
			IEnumerable<string> values)
		{
			return values == null
				? new List<string>()
				: values
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Select(value => value.Trim())
					.Distinct(StringComparer.Ordinal)
					.ToList();
		}

		private static UnityGraphicsMcpPlanRecommendation ClonePhase4Recommendation(
			UnityGraphicsMcpPlanRecommendation source)
		{
			return new UnityGraphicsMcpPlanRecommendation
			{
				recommendationId = source.recommendationId,
				section = source.section,
				recommendedValue = source.recommendedValue,
				allowedRange = source.allowedRange == null
					? new List<string>()
					: new List<string>(source.allowedRange),
				reason = source.reason,
				dependencies = source.dependencies == null
					? new List<string>()
					: new List<string>(source.dependencies),
				confidence = source.confidence,
				pipelineImpact = source.pipelineImpact,
				platformImpact = source.platformImpact == null
					? new List<string>()
					: new List<string>(source.platformImpact),
				verificationLevel = source.verificationLevel,
				nativeMutationBackendStatus = source.nativeMutationBackendStatus
			};
		}
	}
}

#endif
