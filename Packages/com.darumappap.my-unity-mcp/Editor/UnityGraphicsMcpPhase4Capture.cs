#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public enum E_GRAPHICS_CAPTURE_CHANNEL
	{
		COLOR,
		LINEAR_DEPTH,
		OBJECT_ID
	}

	public enum E_GRAPHICS_VISUAL_REVIEW_DECISION
	{
		ACCEPTED,
		REJECTED,
		NEEDS_ADJUSTMENT
	}

	internal sealed class UnityGraphicsMcpCaptureArtifactRecord
	{
		public string Channel { get; set; }
		public string OutputPath { get; set; }
		public string Sha256 { get; set; }
		public long ByteLength { get; set; }
		public string Format { get; set; }
		public string Semantics { get; set; }
	}

	internal sealed class UnityGraphicsMcpObjectIdEntry
	{
		public int ObjectId { get; set; }
		public string EncodedColor { get; set; }
		public string RendererObjectId { get; set; }
		public string RendererType { get; set; }
		public string Name { get; set; }
		public string HierarchyPath { get; set; }
		public string ScenePath { get; set; }
		public int SubMeshCount { get; set; }
	}

	internal sealed class UnityGraphicsMcpCaptureEvidenceRecord
	{
		public string CaptureId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public string CameraObjectId { get; set; }
		public int CameraSceneHandle { get; set; }
		public string CameraScenePath { get; set; }
		public string CameraBaselineDigest { get; set; }
		public string EvidenceDigest { get; set; }
		public string BundlePath { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public List<UnityGraphicsMcpCaptureArtifactRecord> Artifacts { get; set; } =
			new List<UnityGraphicsMcpCaptureArtifactRecord>();
		public int EncodedRendererCount { get; set; }
		public int SkippedRendererCount { get; set; }
		public int UnsupportedTerrainCount { get; set; }
		public string ReviewStatus { get; set; } = "PENDING";
		public bool VisualAccepted { get; set; }
		public string LatestReviewId { get; set; }
	}

	internal sealed class UnityGraphicsMcpVisualReviewRecord
	{
		public string ReviewId { get; set; }
		public string CaptureId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public string EvidenceDigest { get; set; }
		public string Decision { get; set; }
		public string Reviewer { get; set; }
		public List<string> Observations { get; set; } = new List<string>();
		public List<string> RequestedAdjustments { get; set; } = new List<string>();
		public string ReviewDigest { get; set; }
	}

	internal sealed class UnityGraphicsMcpCaptureManifest
	{
		public string SchemaVersion { get; set; }
		public string CaptureId { get; set; }
		public long Revision { get; set; }
		public string CreatedUtc { get; set; }
		public string CameraObjectId { get; set; }
		public int CameraSceneHandle { get; set; }
		public string CameraScenePath { get; set; }
		public string CameraBaselineDigest { get; set; }
		public string EvidenceDigest { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public List<UnityGraphicsMcpCaptureArtifactRecord> Artifacts { get; set; }
		public int EncodedRendererCount { get; set; }
		public int SkippedRendererCount { get; set; }
		public int UnsupportedTerrainCount { get; set; }
		public string ObjectIdCoverage { get; set; }
		public string DepthSemantics { get; set; }
		public bool ImageAnalysisPerformedByUnity { get; set; }
		public bool HumanReviewRequired { get; set; }
		public string HumanReviewStatus { get; set; }
		public bool VisualAccepted { get; set; }
	}

	[InitializeOnLoad]
	internal static class UnityGraphicsMcpPhase4CaptureSession
	{
		private const int MAX_CAPTURE_COUNT = 8;
		private const int MAX_REVIEW_COUNT = 16;
		private static readonly TimeSpan CAPTURE_LIFETIME = TimeSpan.FromMinutes(30.0);
		private static readonly Dictionary<string, UnityGraphicsMcpCaptureEvidenceRecord> _captures =
			new Dictionary<string, UnityGraphicsMcpCaptureEvidenceRecord>(StringComparer.Ordinal);
		private static readonly Dictionary<string, UnityGraphicsMcpVisualReviewRecord> _reviews =
			new Dictionary<string, UnityGraphicsMcpVisualReviewRecord>(StringComparer.Ordinal);

		static UnityGraphicsMcpPhase4CaptureSession()
		{
			EditorApplication.playModeStateChanged += state => Clear();
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			CompilationPipeline.compilationStarted += context => Clear();
			EditorApplication.quitting += Clear;
		}

		public static string StoreCapture(UnityGraphicsMcpCaptureEvidenceRecord capture)
		{
			RemoveExpiredRecords();
			RemoveOldestCaptureWhenFull();

			capture.CaptureId = UnityGraphicsMcpSession.SessionId +
				":capture-evidence:" + Guid.NewGuid().ToString("N");
			capture.CreatedUtc = DateTime.UtcNow;
			_captures[capture.CaptureId] = capture;
			return capture.CaptureId;
		}

		public static void RemoveCapture(string captureId)
		{
			if (string.IsNullOrWhiteSpace(captureId))
			{
				return;
			}

			_captures.Remove(captureId);
			foreach (string reviewId in _reviews
				.Where(pair => pair.Value.CaptureId == captureId)
				.Select(pair => pair.Key)
				.ToArray())
			{
				_reviews.Remove(reviewId);
			}
		}

		public static bool TryGetCapture(
			string captureId,
			long expectedRevision,
			string evidenceDigest,
			out UnityGraphicsMcpCaptureEvidenceRecord capture,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			capture = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;
			RemoveExpiredRecords();

			if (string.IsNullOrWhiteSpace(captureId) ||
				!captureId.StartsWith(
					UnityGraphicsMcpSession.SessionId + ":capture-evidence:",
					StringComparison.Ordinal) ||
				!_captures.TryGetValue(captureId, out capture))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Capture Evidenceが現在のEditor Sessionに存在しないか有効期限切れです。";
				return false;
			}

			if (expectedRevision != UnityGraphicsMcpSession.Revision ||
				capture.Revision != UnityGraphicsMcpSession.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Capture後にEditor Revisionが変更されました。";
				return false;
			}

			if (!string.IsNullOrWhiteSpace(evidenceDigest) &&
				!string.Equals(
					capture.EvidenceDigest,
					evidenceDigest.Trim(),
					StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "指定されたEvidence DigestがCapture Bundleと一致しません。";
				return false;
			}

			return true;
		}

		public static bool TryStoreReview(
			UnityGraphicsMcpCaptureEvidenceRecord capture,
			UnityGraphicsMcpVisualReviewRecord review,
			out string failureMessage)
		{
			failureMessage = null;
			if (capture == null || review == null)
			{
				failureMessage = "CaptureまたはVisual Reviewがありません。";
				return false;
			}

			if (!string.IsNullOrWhiteSpace(capture.LatestReviewId))
			{
				failureMessage =
					"同じCapture Evidenceには既にVisual Reviewが確定しています。再評価には新しいCaptureを作成してください。";
				return false;
			}

			RemoveOldestReviewWhenFull();
			review.ReviewId = UnityGraphicsMcpSession.SessionId +
				":visual-review:" + Guid.NewGuid().ToString("N");
			review.CreatedUtc = DateTime.UtcNow;
			_reviews[review.ReviewId] = review;

			capture.LatestReviewId = review.ReviewId;
			capture.ReviewStatus = review.Decision;
			capture.VisualAccepted = string.Equals(
				review.Decision,
				E_GRAPHICS_VISUAL_REVIEW_DECISION.ACCEPTED.ToString(),
				StringComparison.Ordinal);
			return true;
		}

		public static bool TryGetReview(
			string reviewId,
			long expectedRevision,
			out UnityGraphicsMcpVisualReviewRecord review,
			out UnityGraphicsMcpCaptureEvidenceRecord capture,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			review = null;
			capture = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;
			RemoveExpiredRecords();

			if (string.IsNullOrWhiteSpace(reviewId) ||
				!reviewId.StartsWith(
					UnityGraphicsMcpSession.SessionId + ":visual-review:",
					StringComparison.Ordinal) ||
				!_reviews.TryGetValue(reviewId, out review))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Visual Reviewが現在のEditor Sessionに存在しないか有効期限切れです。";
				return false;
			}

			if (expectedRevision != UnityGraphicsMcpSession.Revision ||
				review.Revision != UnityGraphicsMcpSession.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Visual Review確定後にEditor Revisionが変更されました。";
				return false;
			}

			if (!_captures.TryGetValue(review.CaptureId, out capture) ||
				!string.Equals(
					review.EvidenceDigest,
					capture.EvidenceDigest,
					StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Visual Reviewが参照するCapture Evidenceを再検証できません。";
				return false;
			}

			return true;
		}

		public static void ClearForTests()
		{
			Clear();
		}

		public static string StoreCaptureForTests(UnityGraphicsMcpCaptureEvidenceRecord capture)
		{
			return StoreCapture(capture);
		}

		private static void RemoveExpiredRecords()
		{
			DateTime threshold = DateTime.UtcNow - CAPTURE_LIFETIME;
			HashSet<string> expiredCaptureIds = new HashSet<string>(
				_captures
					.Where(pair => pair.Value.CreatedUtc < threshold)
					.Select(pair => pair.Key),
				StringComparer.Ordinal);

			foreach (string captureId in expiredCaptureIds)
			{
				_captures.Remove(captureId);
			}

			foreach (string reviewId in _reviews
				.Where(pair =>
					pair.Value.CreatedUtc < threshold ||
					expiredCaptureIds.Contains(pair.Value.CaptureId))
				.Select(pair => pair.Key)
				.ToArray())
			{
				_reviews.Remove(reviewId);
			}
		}

		private static void RemoveOldestCaptureWhenFull()
		{
			while (_captures.Count >= MAX_CAPTURE_COUNT)
			{
				string oldestCaptureId = _captures
					.OrderBy(pair => pair.Value.CreatedUtc)
					.First()
					.Key;
				_captures.Remove(oldestCaptureId);

				foreach (string reviewId in _reviews
					.Where(pair => pair.Value.CaptureId == oldestCaptureId)
					.Select(pair => pair.Key)
					.ToArray())
				{
					_reviews.Remove(reviewId);
				}
			}
		}

		private static void RemoveOldestReviewWhenFull()
		{
			while (_reviews.Count >= MAX_REVIEW_COUNT)
			{
				string oldestReviewId = _reviews
					.OrderBy(pair => pair.Value.CreatedUtc)
					.First()
					.Key;
				_reviews.Remove(oldestReviewId);
			}
		}

		private static void Clear()
		{
			_captures.Clear();
			_reviews.Clear();
		}
	}

	/// <summary>
	/// Phase 4CのCapture Evidence Bundleと明示的Visual Acceptanceを所有します。
	/// </summary>
	public static partial class UnityGraphicsMcpInspection
	{
		private const string PHASE4C_CAPTURE_SHADER =
			"Hidden/MyUnityMCP/CaptureEvidence";
		private const string PHASE4C_ACCEPTANCE_CONFIRMATION =
			"VISUAL_ACCEPTED";
		private const int DEFAULT_PHASE4C_RENDERER_LIMIT = 4096;
		private const int MAX_PHASE4C_RENDERER_LIMIT = 16384;

		public static UnityGraphicsMcpToolResult CaptureEvidence(
			string requestId,
			string cameraObjectId,
			long? expectedRevision,
			int? width,
			int? height,
			string[] channels,
			string captureLabel,
			int? maxRendererCount)
		{
			string createdBundleAbsolutePath = null;
			string createdCaptureId = null;
			return ExecutePhase4CCaptureOperation(
				"graphics.capture_evidence",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.capture_evidence",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					if (expectedRevision.Value != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult(
							"graphics.capture_evidence",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"expectedRevisionが現在のEditor Revisionと一致しません。",
							null);
					}

					Camera camera;
					if (!TryResolvePhase4Camera(cameraObjectId, out camera))
					{
						return CreateResult(
							"graphics.capture_evidence",
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
							"graphics.capture_evidence",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Capture解像度は各辺64～4096、総Pixel数8388608以下で指定してください。",
							new Dictionary<string, object>
							{
								{ "width", captureWidth },
								{ "height", captureHeight }
							});
					}

					List<E_GRAPHICS_CAPTURE_CHANNEL> normalizedChannels;
					string channelFailureMessage;
					if (!TryNormalizePhase4CCaptureChannels(
						channels,
						out normalizedChannels,
						out channelFailureMessage))
					{
						return CreateResult(
							"graphics.capture_evidence",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							channelFailureMessage,
							null);
					}

					int rendererLimit = maxRendererCount ??
						DEFAULT_PHASE4C_RENDERER_LIMIT;
					if (rendererLimit < 1 || rendererLimit > MAX_PHASE4C_RENDERER_LIMIT)
					{
						return CreateResult(
							"graphics.capture_evidence",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"maxRendererCountは1～16384で指定してください。",
							new Dictionary<string, object>
							{
								{ "maxRendererCount", rendererLimit }
							});
					}

					if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
					{
						return CreateResult(
							"graphics.capture_evidence",
							requestId,
							E_MCP_TOOL_STATUS.UNVERIFIED,
							"Graphics DeviceがNullのためCapture Evidenceを生成できません。",
							new Dictionary<string, object>
							{
								{
									"requestedChannels",
									normalizedChannels.Select(item => item.ToString()).ToList()
								},
								{ "graphicsDeviceType", SystemInfo.graphicsDeviceType.ToString() },
								{ "temporaryStateChanged", false },
								{ "humanReviewStatus", "PENDING" },
								{ "visualAccepted", false }
							});
					}

					bool requiresEvidenceShader =
						normalizedChannels.Contains(E_GRAPHICS_CAPTURE_CHANNEL.LINEAR_DEPTH) ||
						normalizedChannels.Contains(E_GRAPHICS_CAPTURE_CHANNEL.OBJECT_ID);
					Shader evidenceShader = requiresEvidenceShader
						? Shader.Find(PHASE4C_CAPTURE_SHADER)
						: null;
					if (requiresEvidenceShader && evidenceShader == null)
					{
						return CreateResult(
							"graphics.capture_evidence",
							requestId,
							E_MCP_TOOL_STATUS.BACKEND_NOT_IMPLEMENTED,
							"Capture Evidence Shaderを解決できません。",
							new Dictionary<string, object>
							{
								{ "shader", PHASE4C_CAPTURE_SHADER }
							});
					}

					return CapturePhase4CEvidenceBundle(
						requestId,
						cameraObjectId,
						camera,
						captureWidth,
						captureHeight,
						normalizedChannels,
						captureLabel,
						rendererLimit,
						evidenceShader,
						(path, captureId) =>
						{
							createdBundleAbsolutePath = path;
							createdCaptureId = captureId;
						});
				},
				delegate
				{
					DeletePhase4CDirectory(createdBundleAbsolutePath);
					UnityGraphicsMcpPhase4CaptureSession.RemoveCapture(
						createdCaptureId);
				});
		}

		public static UnityGraphicsMcpToolResult SubmitVisualReview(
			string requestId,
			string captureId,
			long? expectedRevision,
			string evidenceDigest,
			string decision,
			string reviewer,
			string[] observations,
			string[] requestedAdjustments,
			string acceptanceConfirmation)
		{
			return ExecuteReadOnly(
				"graphics.submit_visual_review",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.submit_visual_review",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					if (string.IsNullOrWhiteSpace(evidenceDigest))
					{
						return CreateResult(
							"graphics.submit_visual_review",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Capture結果が返したevidenceDigestを明示してください。",
							null);
					}

					UnityGraphicsMcpCaptureEvidenceRecord capture;
					E_MCP_TOOL_STATUS failureStatus;
					string failureMessage;
					if (!UnityGraphicsMcpPhase4CaptureSession.TryGetCapture(
						captureId,
						expectedRevision.Value,
						evidenceDigest,
						out capture,
						out failureStatus,
						out failureMessage))
					{
						return CreateResult(
							"graphics.submit_visual_review",
							requestId,
							failureStatus,
							failureMessage,
							null);
					}

					E_GRAPHICS_VISUAL_REVIEW_DECISION normalizedDecision;
					if (!Enum.TryParse(
						string.IsNullOrWhiteSpace(decision)
							? string.Empty
							: decision.Trim(),
						true,
						out normalizedDecision))
					{
						return CreateResult(
							"graphics.submit_visual_review",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"decisionはACCEPTED、REJECTED、NEEDS_ADJUSTMENTのいずれかです。",
							null);
					}

					string normalizedReviewer = string.IsNullOrWhiteSpace(reviewer)
						? string.Empty
						: reviewer.Trim();
					if (normalizedReviewer.Length == 0 ||
						normalizedReviewer.Length > 128)
					{
						return CreateResult(
							"graphics.submit_visual_review",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"reviewerを1～128文字で明示してください。",
							null);
					}

					List<string> normalizedObservations =
						NormalizePhase4ExplicitReviewValues(observations);
					List<string> normalizedAdjustments =
						NormalizePhase4ExplicitReviewValues(requestedAdjustments);

					if (normalizedDecision ==
						E_GRAPHICS_VISUAL_REVIEW_DECISION.ACCEPTED)
					{
						if (!string.Equals(
							acceptanceConfirmation,
							PHASE4C_ACCEPTANCE_CONFIRMATION,
							StringComparison.Ordinal))
						{
							return CreateResult(
								"graphics.submit_visual_review",
								requestId,
								E_MCP_TOOL_STATUS.INVALID_REQUEST,
								"ACCEPTEDにはacceptanceConfirmation=VISUAL_ACCEPTEDが必要です。",
								null);
						}

						if (normalizedObservations.Count == 0)
						{
							return CreateResult(
								"graphics.submit_visual_review",
								requestId,
								E_MCP_TOOL_STATUS.INVALID_REQUEST,
								"ACCEPTEDには確認根拠となるHuman Observationを一つ以上指定してください。",
								null);
						}

						if (normalizedAdjustments.Count > 0)
						{
							return CreateResult(
								"graphics.submit_visual_review",
								requestId,
								E_MCP_TOOL_STATUS.INVALID_REQUEST,
								"ACCEPTEDとrequestedAdjustmentsは同時に指定できません。",
								null);
						}
					}
					else if (normalizedObservations.Count == 0 &&
						normalizedAdjustments.Count == 0)
					{
						return CreateResult(
							"graphics.submit_visual_review",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"REJECTEDまたはNEEDS_ADJUSTMENTには観察結果か調整要求が必要です。",
							null);
					}

					UnityGraphicsMcpVisualReviewRecord review =
						new UnityGraphicsMcpVisualReviewRecord
						{
							CaptureId = capture.CaptureId,
							Revision = expectedRevision.Value,
							EvidenceDigest = capture.EvidenceDigest,
							Decision = normalizedDecision.ToString(),
							Reviewer = normalizedReviewer,
							Observations = normalizedObservations,
							RequestedAdjustments = normalizedAdjustments
						};
					review.ReviewDigest = BuildPhase4CReviewDigest(review);

					if (!UnityGraphicsMcpPhase4CaptureSession.TryStoreReview(
						capture,
						review,
						out failureMessage))
					{
						return CreateResult(
							"graphics.submit_visual_review",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							failureMessage,
							null);
					}

					return CreateResult(
						"graphics.submit_visual_review",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						normalizedDecision ==
							E_GRAPHICS_VISUAL_REVIEW_DECISION.ACCEPTED
							? "Capture Evidenceに対するHuman Visual Acceptanceを確定しました。"
							: "Capture Evidenceに対するHuman Reviewを確定し、次Iterationの判断材料を固定しました。",
						new Dictionary<string, object>
						{
							{ "reviewId", review.ReviewId },
							{ "reviewDigest", review.ReviewDigest },
							{ "captureId", capture.CaptureId },
							{ "evidenceDigest", capture.EvidenceDigest },
							{ "decision", review.Decision },
							{ "reviewer", review.Reviewer },
							{ "humanObservations", review.Observations },
							{ "requestedAdjustments", review.RequestedAdjustments },
							{ "humanReviewStatus", "COMPLETED" },
							{ "visualAccepted", capture.VisualAccepted },
							{
								"requiresRefinement",
								normalizedDecision !=
									E_GRAPHICS_VISUAL_REVIEW_DECISION.ACCEPTED
							},
							{ "imageAnalysisPerformedByUnity", false },
							{ "mutationApplied", false },
							{ "savePerformed", false },
							{ "bakePerformed", false }
						});
				});
		}

		public static UnityGraphicsMcpToolResult RefineFromVisualReview(
			string requestId,
			string directionPlanId,
			string reviewId,
			long? expectedRevision)
		{
			return ExecuteReadOnly(
				"graphics.refine_from_visual_review",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.refine_from_visual_review",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
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
							"graphics.refine_from_visual_review",
							requestId,
							planFailureStatus,
							"Direction Planは現在のEditor SessionまたはRevisionでは利用できません。",
							null);
					}

					UnityGraphicsMcpVisualReviewRecord review;
					UnityGraphicsMcpCaptureEvidenceRecord capture;
					E_MCP_TOOL_STATUS reviewFailureStatus;
					string reviewFailureMessage;
					if (!UnityGraphicsMcpPhase4CaptureSession.TryGetReview(
						reviewId,
						expectedRevision.Value,
						out review,
						out capture,
						out reviewFailureStatus,
						out reviewFailureMessage))
					{
						return CreateResult(
							"graphics.refine_from_visual_review",
							requestId,
							reviewFailureStatus,
							reviewFailureMessage,
							null);
					}

					if (string.Equals(
						review.Decision,
						E_GRAPHICS_VISUAL_REVIEW_DECISION.ACCEPTED.ToString(),
						StringComparison.Ordinal))
					{
						return CreateResult(
							"graphics.refine_from_visual_review",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"ACCEPTED済みVisual ReviewからRefine Planは作成しません。",
							new Dictionary<string, object>
							{
								{ "reviewId", review.ReviewId },
								{ "visualAccepted", true }
							});
					}

					Dictionary<string, object> refinedIntent =
						new Dictionary<string, object>(
							sourcePlan.VisualIntent ??
							new Dictionary<string, object>());
					refinedIntent["refinementSourcePlanId"] = directionPlanId;
					refinedIntent["captureId"] = capture.CaptureId;
					refinedIntent["captureEvidenceDigest"] = capture.EvidenceDigest;
					refinedIntent["visualReviewId"] = review.ReviewId;
					refinedIntent["visualReviewDigest"] = review.ReviewDigest;
					refinedIntent["visualReviewDecision"] = review.Decision;
					refinedIntent["visualReviewer"] = review.Reviewer;
					refinedIntent["humanObservations"] = review.Observations;
					refinedIntent["requestedAdjustments"] =
						review.RequestedAdjustments;
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
							capture.CaptureId,
							review.Observations,
							review.RequestedAdjustments));

					UnityGraphicsMcpDirectionPlan refinedPlan =
						new UnityGraphicsMcpDirectionPlan
						{
							Revision = expectedRevision.Value,
							CreatedUtc = DateTime.UtcNow,
							ProjectContext = new Dictionary<string, object>(
								sourcePlan.ProjectContext ??
								new Dictionary<string, object>()),
							VisualIntent = refinedIntent,
							Recommendations = recommendations,
							Issues = new List<UnityGraphicsMcpIssue>(
								sourcePlan.Issues ??
								new List<UnityGraphicsMcpIssue>())
						};
					refinedPlan.Issues.Add(
						new UnityGraphicsMcpIssue
						{
							code = "VISUAL_REVIEW_REQUIRES_REFINEMENT",
							message =
								"確定済みHuman ReviewはVisual Acceptedではなく、次Iterationの調整を要求しています。",
							evidence = new Dictionary<string, object>
							{
								{ "reviewId", review.ReviewId },
								{ "captureId", capture.CaptureId },
								{ "decision", review.Decision },
								{ "humanReviewStatus", "COMPLETED" },
								{ "visualAccepted", false }
							}
						});
					UnityGraphicsMcpSession.StorePlan(refinedPlan);

					UnityGraphicsMcpToolResult result = CreateResult(
						"graphics.refine_from_visual_review",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"確定済みVisual Reviewを保持した次IterationのDirection PlanをRead-onlyで作成しました。",
						new Dictionary<string, object>
						{
							{ "sourcePlanId", directionPlanId },
							{ "reviewId", review.ReviewId },
							{ "captureId", capture.CaptureId },
							{ "planId", refinedPlan.PlanId },
							{ "expectedRevision", refinedPlan.Revision },
							{ "decision", review.Decision },
							{ "humanObservations", review.Observations },
							{ "requestedAdjustments", review.RequestedAdjustments },
							{ "imageAnalysisPerformedByUnity", false },
							{ "humanReviewStatus", "COMPLETED" },
							{ "visualAccepted", false },
							{ "mutationApplied", false },
							{ "savePerformed", false },
							{ "bakePerformed", false }
						});
					result.issues.AddRange(refinedPlan.Issues);
					return result;
				});
		}

		internal static Color32 EncodePhase4CObjectIdForTests(int objectId)
		{
			return EncodePhase4CObjectId(objectId);
		}

		internal static string BuildPhase4CEvidenceDigestForTests(
			UnityGraphicsMcpCaptureEvidenceRecord capture)
		{
			return BuildPhase4CEvidenceDigest(capture);
		}

		private static UnityGraphicsMcpToolResult CapturePhase4CEvidenceBundle(
			string requestId,
			string cameraObjectId,
			Camera camera,
			int width,
			int height,
			List<E_GRAPHICS_CAPTURE_CHANNEL> channels,
			string captureLabel,
			int maxRendererCount,
			Shader evidenceShader,
			Action<string, string> bundleCreated)
		{
			long startRevision = UnityGraphicsMcpSession.Revision;
			Scene cameraScene = camera.gameObject.scene;
			bool cameraSceneDirtyBefore = cameraScene.isDirty;
			RenderTexture originalTargetTexture = camera.targetTexture;
			RenderTexture originalActiveTexture = RenderTexture.active;
			string normalizedCaptureLabel =
				SanitizePhase4CCaptureLabel(captureLabel);
			string captureKey = DateTime.UtcNow.ToString(
				"yyyyMMdd-HHmmssfff",
				CultureInfo.InvariantCulture) + "-" +
				(string.IsNullOrWhiteSpace(normalizedCaptureLabel)
					? "evidence"
					: normalizedCaptureLabel) + "-" +
				Guid.NewGuid().ToString("N");
			string relativeBundlePath =
				"Library/MyUnityMCP/Captures/" + captureKey;
			string absoluteBundlePath =
				ToPhase4ProjectAbsolutePath(relativeBundlePath);
			string stagingPath = absoluteBundlePath +
				".tmp-" + Guid.NewGuid().ToString("N");

			List<Renderer> renderers = CollectPhase4CCaptureRenderers(camera);
			bool requiresRendererEvidence =
				channels.Contains(E_GRAPHICS_CAPTURE_CHANNEL.LINEAR_DEPTH) ||
				channels.Contains(E_GRAPHICS_CAPTURE_CHANNEL.OBJECT_ID);
			if (requiresRendererEvidence &&
				renderers.Count > maxRendererCount)
			{
				return CreateResult(
					"graphics.capture_evidence",
					requestId,
					E_MCP_TOOL_STATUS.INVALID_REQUEST,
					"Object ID／Depth Capture対象Renderer数がmaxRendererCountを超えています。",
					new Dictionary<string, object>
					{
						{ "rendererCount", renderers.Count },
						{ "maxRendererCount", maxRendererCount }
					});
			}

			UnityGraphicsMcpCaptureEvidenceRecord capture =
				new UnityGraphicsMcpCaptureEvidenceRecord
				{
					Revision = startRevision,
					CameraObjectId = cameraObjectId,
					CameraSceneHandle = cameraScene.handle,
					CameraScenePath = cameraScene.path,
					CameraBaselineDigest =
						BuildPhase4CCameraBaselineDigest(camera),
					BundlePath = relativeBundlePath,
					Width = width,
					Height = height,
					EncodedRendererCount = renderers.Count,
					SkippedRendererCount =
						CountPhase4CSkippedRenderers(camera),
					UnsupportedTerrainCount =
						CountPhase4CUnsupportedTerrains(camera)
				};
			UnityGraphicsMcpPhase4CaptureSession.StoreCapture(capture);

			List<Material> temporaryMaterials = new List<Material>();
			Exception captureException = null;

			try
			{
				Directory.CreateDirectory(stagingPath);

				if (channels.Contains(E_GRAPHICS_CAPTURE_CHANNEL.COLOR))
				{
					byte[] colorBytes = CapturePhase4CColor(
						camera,
						width,
						height);
					AddPhase4CArtifact(
						capture,
						stagingPath,
						relativeBundlePath,
						"COLOR",
						"color.png",
						colorBytes,
						"PNG_RGBA8",
						"Camera color output after the active render pipeline.");
				}

				if (channels.Contains(
					E_GRAPHICS_CAPTURE_CHANNEL.LINEAR_DEPTH))
				{
					byte[] depthBytes = CapturePhase4CLinearDepth(
						camera,
						width,
						height,
						renderers,
						evidenceShader,
						temporaryMaterials);
					AddPhase4CArtifact(
						capture,
						stagingPath,
						relativeBundlePath,
						"LINEAR_DEPTH",
						"linear-depth.exr",
						depthBytes,
						"EXR_RGBA32F",
						"Linear eye depth normalized by camera near/far. 0=near, 1=far/background.");
				}

				if (channels.Contains(E_GRAPHICS_CAPTURE_CHANNEL.OBJECT_ID))
				{
					List<UnityGraphicsMcpObjectIdEntry> objectIdEntries;
					byte[] objectIdBytes = CapturePhase4CObjectId(
						camera,
						width,
						height,
						renderers,
						evidenceShader,
						temporaryMaterials,
						out objectIdEntries);
					AddPhase4CArtifact(
						capture,
						stagingPath,
						relativeBundlePath,
						"OBJECT_ID",
						"object-id.png",
						objectIdBytes,
						"PNG_RGB24_ID",
						"24-bit deterministic Renderer ID. Black is background.");

					byte[] mapBytes = Encoding.UTF8.GetBytes(
						JsonConvert.SerializeObject(
							objectIdEntries,
							Formatting.Indented));
					AddPhase4CArtifact(
						capture,
						stagingPath,
						relativeBundlePath,
						"OBJECT_ID_MAP",
						"object-id-map.json",
						mapBytes,
						"JSON_UTF8",
						"Object ID to GlobalObjectId, hierarchy and scene mapping.");
				}

				capture.EvidenceDigest =
					BuildPhase4CEvidenceDigest(capture);

				UnityGraphicsMcpCaptureManifest manifest =
					new UnityGraphicsMcpCaptureManifest
					{
						SchemaVersion = "1.0",
						CaptureId = capture.CaptureId,
						Revision = capture.Revision,
						CreatedUtc = capture.CreatedUtc.ToString(
							"O",
							CultureInfo.InvariantCulture),
						CameraObjectId = capture.CameraObjectId,
						CameraSceneHandle = capture.CameraSceneHandle,
						CameraScenePath = capture.CameraScenePath,
						CameraBaselineDigest =
							capture.CameraBaselineDigest,
						EvidenceDigest = capture.EvidenceDigest,
						Width = capture.Width,
						Height = capture.Height,
						Artifacts =
							new List<UnityGraphicsMcpCaptureArtifactRecord>(
								capture.Artifacts),
						EncodedRendererCount =
							capture.EncodedRendererCount,
						SkippedRendererCount =
							capture.SkippedRendererCount,
						UnsupportedTerrainCount =
							capture.UnsupportedTerrainCount,
						ObjectIdCoverage =
							"Loaded active Renderer components visible to the Camera culling mask and frustum. Terrain, DecalProjector and procedural draws are reported but not encoded.",
						DepthSemantics =
							"Linear eye depth normalized with Camera.nearClipPlane and Camera.farClipPlane.",
						ImageAnalysisPerformedByUnity = false,
						HumanReviewRequired = true,
						HumanReviewStatus = "PENDING",
						VisualAccepted = false
					};
				byte[] manifestBytes = Encoding.UTF8.GetBytes(
					JsonConvert.SerializeObject(
						manifest,
						Formatting.Indented));
				AddPhase4CArtifact(
					capture,
					stagingPath,
					relativeBundlePath,
					"MANIFEST",
					"capture-manifest.json",
					manifestBytes,
					"JSON_UTF8",
					"Capture bundle contract and artifact digests.");

				if (Directory.Exists(absoluteBundlePath))
				{
					throw new IOException(
						"Capture Bundleの最終出力先が既に存在します。");
				}

				Directory.Move(stagingPath, absoluteBundlePath);
				bundleCreated(
					absoluteBundlePath,
					capture.CaptureId);
			}
			catch (Exception exception)
			{
				captureException = exception;
			}
			finally
			{
				camera.targetTexture = originalTargetTexture;
				RenderTexture.active = originalActiveTexture;

				foreach (Material material in temporaryMaterials)
				{
					if (material != null)
					{
						Object.DestroyImmediate(material);
					}
				}

				if (!cameraSceneDirtyBefore &&
					cameraScene.IsValid() &&
					cameraScene.isDirty)
				{
					EditorSceneManager.ClearSceneDirtiness(cameraScene);
				}
			}

			if (captureException != null)
			{
				UnityGraphicsMcpPhase4CaptureSession.RemoveCapture(
					capture.CaptureId);
				DeletePhase4CDirectory(stagingPath);
				DeletePhase4CDirectory(absoluteBundlePath);
				return CreateResult(
					"graphics.capture_evidence",
					requestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Capture Evidence Bundle生成中に例外が発生しました。Editor一時状態は復元済みです。",
					new Dictionary<string, object>
					{
						{ "exceptionType", captureException.GetType().FullName },
						{ "message", captureException.Message },
						{
							"temporaryStateRestored",
							camera.targetTexture == originalTargetTexture &&
							RenderTexture.active == originalActiveTexture
						},
						{
							"sceneDirtyStatePreserved",
							cameraScene.isDirty == cameraSceneDirtyBefore
						}
					});
			}

			if (camera.targetTexture != originalTargetTexture ||
				RenderTexture.active != originalActiveTexture ||
				cameraScene.isDirty != cameraSceneDirtyBefore)
			{
				UnityGraphicsMcpPhase4CaptureSession.RemoveCapture(
					capture.CaptureId);
				DeletePhase4CDirectory(absoluteBundlePath);
				return CreateResult(
					"graphics.capture_evidence",
					requestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Capture後のCamera TargetTexture、Active RenderTexture、Scene Dirty状態を復元できませんでした。",
					null);
			}

			if (startRevision != UnityGraphicsMcpSession.Revision)
			{
				UnityGraphicsMcpPhase4CaptureSession.RemoveCapture(
					capture.CaptureId);
				DeletePhase4CDirectory(absoluteBundlePath);
				return CreateResult(
					"graphics.capture_evidence",
					requestId,
					E_MCP_TOOL_STATUS.STALE_DURING_SCAN,
					"Capture中にEditor Revisionが変更されたためEvidence Bundleを破棄しました。",
					null);
			}

			if (!Directory.Exists(absoluteBundlePath) ||
				capture.Artifacts.Count == 0)
			{
				UnityGraphicsMcpPhase4CaptureSession.RemoveCapture(
					capture.CaptureId);
				DeletePhase4CDirectory(absoluteBundlePath);
				return CreateResult(
					"graphics.capture_evidence",
					requestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Capture Evidence Bundleを最終出力先へ確定できませんでした。",
					null);
			}

			return CreateResult(
				"graphics.capture_evidence",
				requestId,
				E_MCP_TOOL_STATUS.SUCCESS,
				"Color／Linear Depth／Object IDを選択可能なCapture Evidence BundleをLibrary配下へ原子的に保存しました。",
				new Dictionary<string, object>
				{
					{ "captureId", capture.CaptureId },
					{ "cameraObjectId", capture.CameraObjectId },
					{ "cameraBaselineDigest", capture.CameraBaselineDigest },
					{ "evidenceDigest", capture.EvidenceDigest },
					{ "bundlePath", capture.BundlePath },
					{
						"artifacts",
						capture.Artifacts.Select(ToPhase4CArtifactData).ToList()
					},
					{ "width", capture.Width },
					{ "height", capture.Height },
					{ "encodedRendererCount", capture.EncodedRendererCount },
					{ "skippedRendererCount", capture.SkippedRendererCount },
					{
						"unsupportedTerrainCount",
						capture.UnsupportedTerrainCount
					},
					{ "temporaryStateRestored", true },
					{ "sceneDirtyStatePreserved", true },
					{ "imageAnalysisPerformedByUnity", false },
					{ "humanReviewRequired", true },
					{ "humanReviewStatus", "PENDING" },
					{ "visualAccepted", false }
				});
		}

		private static UnityGraphicsMcpToolResult ExecutePhase4CCaptureOperation(
			string toolName,
			string requestId,
			Func<UnityGraphicsMcpToolResult> operation,
			Action cleanup)
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

			Dictionary<int, bool> sceneDirtyState =
				CapturePhase4SceneDirtyState();
			Dictionary<int, bool> assetDirtyState =
				CapturePhase4AssetDirtyState();
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
					cleanup();
					return CreateResult(
						toolName,
						normalizedRequestId,
						E_MCP_TOOL_STATUS.READ_ONLY_CONTRACT_VIOLATION,
						"Capture Toolの実行前後でScene、AssetまたはUndo状態が変化したためEvidence Bundleを破棄しました。",
						evidence);
				}

				return result;
			}
			catch (Exception exception)
			{
				cleanup();
				Debug.LogException(exception);
				return CreateResult(
					toolName,
					normalizedRequestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Phase 4C Capture処理中に例外が発生しました。",
					new Dictionary<string, object>
					{
						{ "exceptionType", exception.GetType().FullName },
						{ "message", exception.Message }
					});
			}
		}

		private static bool TryNormalizePhase4CCaptureChannels(
			IEnumerable<string> channels,
			out List<E_GRAPHICS_CAPTURE_CHANNEL> normalized,
			out string failureMessage)
		{
			normalized = new List<E_GRAPHICS_CAPTURE_CHANNEL>();
			failureMessage = null;
			IEnumerable<string> requested = channels == null ||
				!channels.Any()
				? new[]
				{
					E_GRAPHICS_CAPTURE_CHANNEL.COLOR.ToString(),
					E_GRAPHICS_CAPTURE_CHANNEL.LINEAR_DEPTH.ToString(),
					E_GRAPHICS_CAPTURE_CHANNEL.OBJECT_ID.ToString()
				}
				: channels;

			foreach (string channel in requested)
			{
				E_GRAPHICS_CAPTURE_CHANNEL parsed;
				if (!Enum.TryParse(
					string.IsNullOrWhiteSpace(channel)
						? string.Empty
						: channel.Trim(),
					true,
					out parsed))
				{
					failureMessage =
						"channelsはCOLOR、LINEAR_DEPTH、OBJECT_IDだけを指定できます。";
					return false;
				}

				if (!normalized.Contains(parsed))
				{
					normalized.Add(parsed);
				}
			}

			if (normalized.Count == 0)
			{
				failureMessage = "Capture Channelを一つ以上指定してください。";
				return false;
			}

			normalized.Sort();
			return true;
		}

		private static List<Renderer> CollectPhase4CCaptureRenderers(
			Camera camera)
		{
			Plane[] frustumPlanes =
				GeometryUtility.CalculateFrustumPlanes(camera);
			return Resources.FindObjectsOfTypeAll<Renderer>()
				.Where(renderer =>
					IsPhase4CRendererEligible(
						renderer,
						camera,
						frustumPlanes))
				.OrderBy(renderer => renderer.gameObject.scene.path, StringComparer.Ordinal)
				.ThenBy(
					renderer => BuildPhase4StableHierarchyPath(renderer.gameObject),
					StringComparer.Ordinal)
				.ThenBy(renderer => renderer.GetType().FullName, StringComparer.Ordinal)
				.ThenBy(renderer => renderer.GetInstanceID())
				.ToList();
		}

		private static int CountPhase4CSkippedRenderers(Camera camera)
		{
			Plane[] frustumPlanes =
				GeometryUtility.CalculateFrustumPlanes(camera);
			return Resources.FindObjectsOfTypeAll<Renderer>()
				.Count(renderer =>
					renderer != null &&
					renderer.gameObject.scene.IsValid() &&
					renderer.gameObject.scene.isLoaded &&
					!IsPhase4CRendererEligible(
						renderer,
						camera,
						frustumPlanes));
		}

		private static int CountPhase4CUnsupportedTerrains(Camera camera)
		{
			return Terrain.activeTerrains.Count(terrain =>
				terrain != null &&
				terrain.gameObject.scene.IsValid() &&
				terrain.gameObject.scene.isLoaded &&
				(terrain.gameObject.layer >= 0 &&
				 ((1 << terrain.gameObject.layer) &
				  camera.cullingMask) != 0));
		}

		private static bool IsPhase4CRendererEligible(
			Renderer renderer,
			Camera camera,
			Plane[] frustumPlanes)
		{
			if (renderer == null ||
				!renderer.gameObject.scene.IsValid() ||
				!renderer.gameObject.scene.isLoaded ||
				!renderer.enabled ||
				!renderer.gameObject.activeInHierarchy ||
				(renderer.hideFlags & HideFlags.HideAndDontSave) != 0 ||
				(renderer.gameObject.hideFlags & HideFlags.HideAndDontSave) != 0 ||
				renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
			{
				return false;
			}

			int layerMask = 1 << renderer.gameObject.layer;
			return (camera.cullingMask & layerMask) != 0 &&
				GeometryUtility.TestPlanesAABB(
					frustumPlanes,
					renderer.bounds);
		}

		private static int GetPhase4CSubMeshCount(Renderer renderer)
		{
			MeshRenderer meshRenderer = renderer as MeshRenderer;
			if (meshRenderer != null)
			{
				MeshFilter meshFilter =
					meshRenderer.GetComponent<MeshFilter>();
				if (meshFilter != null &&
					meshFilter.sharedMesh != null)
				{
					return Math.Max(
						1,
						meshFilter.sharedMesh.subMeshCount);
				}
			}

			SkinnedMeshRenderer skinned =
				renderer as SkinnedMeshRenderer;
			if (skinned != null && skinned.sharedMesh != null)
			{
				return Math.Max(1, skinned.sharedMesh.subMeshCount);
			}

			return Math.Max(1, renderer.sharedMaterials.Length);
		}

		private static byte[] CapturePhase4CColor(
			Camera camera,
			int width,
			int height)
		{
			RenderTexture originalTarget = camera.targetTexture;
			RenderTexture originalActive = RenderTexture.active;
			RenderTexture target = null;
			Texture2D texture = null;
			try
			{
				target = new RenderTexture(
					width,
					height,
					24,
					RenderTextureFormat.ARGB32,
					RenderTextureReadWrite.Default)
				{
					name = "MyUnityMCP Phase4C Color"
				};
				target.Create();
				camera.targetTexture = target;
				camera.Render();
				RenderTexture.active = target;

				texture = new Texture2D(
					width,
					height,
					TextureFormat.RGBA32,
					false);
				texture.ReadPixels(
					new Rect(0.0f, 0.0f, width, height),
					0,
					0,
					false);
				texture.Apply(false, false);
				return texture.EncodeToPNG();
			}
			finally
			{
				camera.targetTexture = originalTarget;
				RenderTexture.active = originalActive;
				if (texture != null)
				{
					Object.DestroyImmediate(texture);
				}

				if (target != null)
				{
					target.Release();
					Object.DestroyImmediate(target);
				}
			}
		}

		private static byte[] CapturePhase4CLinearDepth(
			Camera camera,
			int width,
			int height,
			List<Renderer> renderers,
			Shader shader,
			List<Material> temporaryMaterials)
		{
			Material depthMaterial = new Material(shader)
			{
				name = "MyUnityMCP Phase4C Linear Depth",
				hideFlags = HideFlags.HideAndDontSave
			};
			depthMaterial.SetFloat("_McpNear", camera.nearClipPlane);
			depthMaterial.SetFloat("_McpFar", camera.farClipPlane);
			temporaryMaterials.Add(depthMaterial);

			return RenderPhase4COverride(
				camera,
				width,
				height,
				renderers,
				depthMaterial,
				1,
				RenderTextureFormat.ARGBFloat,
				TextureFormat.RGBAFloat,
				true);
		}

		private static byte[] CapturePhase4CObjectId(
			Camera camera,
			int width,
			int height,
			List<Renderer> renderers,
			Shader shader,
			List<Material> temporaryMaterials,
			out List<UnityGraphicsMcpObjectIdEntry> objectIdEntries)
		{
			objectIdEntries = new List<UnityGraphicsMcpObjectIdEntry>();
			Dictionary<Renderer, Material> materials =
				new Dictionary<Renderer, Material>();

			for (int index = 0; index < renderers.Count; index++)
			{
				Renderer renderer = renderers[index];
				int objectId = index + 1;
				Color32 encoded = EncodePhase4CObjectId(objectId);
				Material material = new Material(shader)
				{
					name = "MyUnityMCP Object ID " +
						objectId.ToString(CultureInfo.InvariantCulture),
					hideFlags = HideFlags.HideAndDontSave
				};
				material.SetColor(
					"_McpObjectIdColor",
					new Color32(
						encoded.r,
						encoded.g,
						encoded.b,
						255));
				temporaryMaterials.Add(material);
				materials[renderer] = material;

				objectIdEntries.Add(
					new UnityGraphicsMcpObjectIdEntry
					{
						ObjectId = objectId,
						EncodedColor = "#" +
							encoded.r.ToString("X2", CultureInfo.InvariantCulture) +
							encoded.g.ToString("X2", CultureInfo.InvariantCulture) +
							encoded.b.ToString("X2", CultureInfo.InvariantCulture),
						RendererObjectId =
							GlobalObjectId.GetGlobalObjectIdSlow(renderer).ToString(),
						RendererType = renderer.GetType().FullName,
						Name = renderer.name,
						HierarchyPath =
							BuildPhase4StableHierarchyPath(renderer.gameObject),
						ScenePath = renderer.gameObject.scene.path,
						SubMeshCount = GetPhase4CSubMeshCount(renderer)
					});
			}

			return RenderPhase4COverride(
				camera,
				width,
				height,
				renderers,
				null,
				0,
				RenderTextureFormat.ARGB32,
				TextureFormat.RGBA32,
				false,
				materials);
		}

		private static byte[] RenderPhase4COverride(
			Camera camera,
			int width,
			int height,
			List<Renderer> renderers,
			Material sharedMaterial,
			int shaderPass,
			RenderTextureFormat renderTextureFormat,
			TextureFormat textureFormat,
			bool encodeExr,
			Dictionary<Renderer, Material> materials = null)
		{
			RenderTexture target = null;
			Texture2D texture = null;
			CommandBuffer commandBuffer = null;
			RenderTexture previousActive = RenderTexture.active;

			try
			{
				target = new RenderTexture(
					width,
					height,
					24,
					renderTextureFormat,
					RenderTextureReadWrite.Linear)
				{
					name = "MyUnityMCP Phase4C Evidence"
				};
				target.Create();

				commandBuffer = new CommandBuffer
				{
					name = "MyUnityMCP Phase4C Capture"
				};
				commandBuffer.SetRenderTarget(target);
				commandBuffer.ClearRenderTarget(
					true,
					true,
					encodeExr ? Color.white : Color.clear);
				commandBuffer.SetViewProjectionMatrices(
					camera.worldToCameraMatrix,
					GL.GetGPUProjectionMatrix(
						camera.projectionMatrix,
						true));

				foreach (Renderer renderer in renderers)
				{
					Material material = sharedMaterial;
					if (materials != null)
					{
						materials.TryGetValue(renderer, out material);
					}

					if (material == null)
					{
						continue;
					}

					int subMeshCount =
						GetPhase4CSubMeshCount(renderer);
					for (int subMesh = 0;
						subMesh < subMeshCount;
						subMesh++)
					{
						commandBuffer.DrawRenderer(
							renderer,
							material,
							subMesh,
							shaderPass);
					}
				}

				Graphics.ExecuteCommandBuffer(commandBuffer);
				RenderTexture.active = target;

				texture = new Texture2D(
					width,
					height,
					textureFormat,
					false,
					true);
				texture.ReadPixels(
					new Rect(0.0f, 0.0f, width, height),
					0,
					0,
					false);
				texture.Apply(false, false);

				return encodeExr
					? texture.EncodeToEXR(
						Texture2D.EXRFlags.OutputAsFloat)
					: texture.EncodeToPNG();
			}
			finally
			{
				RenderTexture.active = previousActive;
				if (commandBuffer != null)
				{
					commandBuffer.Release();
				}

				if (texture != null)
				{
					Object.DestroyImmediate(texture);
				}

				if (target != null)
				{
					target.Release();
					Object.DestroyImmediate(target);
				}
			}
		}

		private static Color32 EncodePhase4CObjectId(int objectId)
		{
			if (objectId < 1 || objectId > 0xFFFFFF)
			{
				throw new ArgumentOutOfRangeException(
					nameof(objectId),
					"Object IDは24-bit範囲で指定してください。");
			}

			return new Color32(
				(byte)(objectId & 0xFF),
				(byte)((objectId >> 8) & 0xFF),
				(byte)((objectId >> 16) & 0xFF),
				255);
		}

		private static void AddPhase4CArtifact(
			UnityGraphicsMcpCaptureEvidenceRecord capture,
			string stagingPath,
			string relativeBundlePath,
			string channel,
			string fileName,
			byte[] bytes,
			string format,
			string semantics)
		{
			if (bytes == null || bytes.Length == 0)
			{
				throw new InvalidOperationException(
					channel + " Artifactが空です。");
			}

			string stagingFilePath = Path.Combine(
				stagingPath,
				fileName);
			File.WriteAllBytes(stagingFilePath, bytes);
			capture.Artifacts.Add(
				new UnityGraphicsMcpCaptureArtifactRecord
				{
					Channel = channel,
					OutputPath = relativeBundlePath + "/" + fileName,
					Sha256 = HashPhase4Bytes(bytes),
					ByteLength = bytes.LongLength,
					Format = format,
					Semantics = semantics
				});
		}

		private static string BuildPhase4CCameraBaselineDigest(
			Camera camera)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(
				GlobalObjectId.GetGlobalObjectIdSlow(camera).ToString())
				.Append('|');
			builder.Append(camera.gameObject.scene.handle).Append('|');
			builder.Append(camera.gameObject.scene.path).Append('|');
			builder.Append(EditorJsonUtility.ToJson(camera, false)).Append('|');
			AppendPhase4Vector(builder, camera.transform.position);
			AppendPhase4Vector(builder, camera.transform.eulerAngles);
			AppendPhase4Vector(builder, camera.transform.lossyScale);
			return UnityGraphicsMcpPhase4Session.HashText(
				builder.ToString());
		}

		private static string BuildPhase4CEvidenceDigest(
			UnityGraphicsMcpCaptureEvidenceRecord capture)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(capture.CaptureId).Append('|');
			builder.Append(capture.Revision).Append('|');
			builder.Append(capture.CameraObjectId).Append('|');
			builder.Append(capture.CameraSceneHandle).Append('|');
			builder.Append(capture.CameraScenePath).Append('|');
			builder.Append(capture.CameraBaselineDigest).Append('|');
			builder.Append(capture.Width).Append('x')
				.Append(capture.Height).Append('|');
			builder.Append(capture.EncodedRendererCount).Append('|');
			builder.Append(capture.SkippedRendererCount).Append('|');
			builder.Append(capture.UnsupportedTerrainCount).Append('|');

			foreach (UnityGraphicsMcpCaptureArtifactRecord artifact in
				capture.Artifacts
					.Where(item => item.Channel != "MANIFEST")
					.OrderBy(item => item.Channel, StringComparer.Ordinal)
					.ThenBy(item => item.OutputPath, StringComparer.Ordinal))
			{
				builder.Append(artifact.Channel).Append('|');
				builder.Append(artifact.OutputPath).Append('|');
				builder.Append(artifact.Sha256).Append('|');
				builder.Append(artifact.ByteLength).Append('|');
				builder.Append(artifact.Format).Append('|');
			}

			return UnityGraphicsMcpPhase4Session.HashText(
				builder.ToString());
		}

		private static string BuildPhase4CReviewDigest(
			UnityGraphicsMcpVisualReviewRecord review)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(review.CaptureId).Append('|');
			builder.Append(review.Revision).Append('|');
			builder.Append(review.EvidenceDigest).Append('|');
			builder.Append(review.Decision).Append('|');
			builder.Append(review.Reviewer).Append('|');
			foreach (string observation in review.Observations)
			{
				builder.Append("O:").Append(observation).Append('|');
			}
			foreach (string adjustment in review.RequestedAdjustments)
			{
				builder.Append("A:").Append(adjustment).Append('|');
			}
			return UnityGraphicsMcpPhase4Session.HashText(
				builder.ToString());
		}

		private static Dictionary<string, object> ToPhase4CArtifactData(
			UnityGraphicsMcpCaptureArtifactRecord artifact)
		{
			return new Dictionary<string, object>
			{
				{ "channel", artifact.Channel },
				{ "outputPath", artifact.OutputPath },
				{ "sha256", artifact.Sha256 },
				{ "byteLength", artifact.ByteLength },
				{ "format", artifact.Format },
				{ "semantics", artifact.Semantics }
			};
		}

		private static string SanitizePhase4CCaptureLabel(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			StringBuilder builder = new StringBuilder();
			foreach (char character in value.Trim())
			{
				if (char.IsLetterOrDigit(character) ||
					character == '-' ||
					character == '_')
				{
					builder.Append(character);
				}
				else if (char.IsWhiteSpace(character))
				{
					builder.Append('-');
				}
			}

			string result = builder.ToString().Trim('-');
			return result.Length <= 48
				? result
				: result.Substring(0, 48);
		}

		private static void DeletePhase4CDirectory(string absolutePath)
		{
			if (!string.IsNullOrWhiteSpace(absolutePath) &&
				Directory.Exists(absolutePath))
			{
				Directory.Delete(absolutePath, true);
			}
		}
	}
}

#endif
