#if UNITY_EDITOR

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UnityGraphicsMcp
{
	[McpForUnityTool(
		"graphics.capture_evidence",
		Description = "指定CameraからColor、Linear Depth、Object IDを選択してCapture Evidence BundleをLibrary配下へ原子的に生成します。",
		AutoRegister = false,
		Group = "core")]
	public static class CaptureEvidenceTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Capture対象Camera ComponentのGlobalObjectId。", Required = true)]
			public string cameraObjectId { get; set; }

			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Capture幅。既定1280、64～4096。", Required = false)]
			public int? width { get; set; }

			[ToolParameter("Capture高さ。既定720、64～4096。", Required = false)]
			public int? height { get; set; }

			[ToolParameter("COLOR、LINEAR_DEPTH、OBJECT_ID。省略時は全Channel。", Required = false)]
			public string[] channels { get; set; }

			[ToolParameter("出力Bundle名へ使用する短いLabel。", Required = false)]
			public string captureLabel { get; set; }

			[ToolParameter("Depth／Object ID対象Renderer上限。既定4096、最大16384。", Required = false)]
			public int? maxRendererCount { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.CaptureEvidence(
					parameters.requestId,
					parameters.cameraObjectId,
					parameters.expectedRevision,
					parameters.width,
					parameters.height,
					parameters.channels,
					parameters.captureLabel,
					parameters.maxRendererCount));
		}
	}

	[McpForUnityTool(
		"graphics.submit_visual_review",
		Description = "Capture Evidence DigestへHuman Reviewを一度だけ確定し、ACCEPTED時は明示Confirmationを要求します。",
		AutoRegister = false,
		Group = "core")]
	public static class SubmitVisualReviewTool
	{
		public sealed class Parameters
		{
			[ToolParameter("graphics.capture_evidenceが返したCapture ID。", Required = true)]
			public string captureId { get; set; }

			[ToolParameter("Captureが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Capture結果が返したEvidence Digest。", Required = true)]
			public string evidenceDigest { get; set; }

			[ToolParameter("ACCEPTED、REJECTED、NEEDS_ADJUSTMENT。", Required = true)]
			public string decision { get; set; }

			[ToolParameter("Human Reviewer識別名。", Required = true)]
			public string reviewer { get; set; }

			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("人間または外部Visionが明示した観察結果。", Required = false)]
			public string[] observations { get; set; }

			[ToolParameter("次Iterationへ反映する明示的な調整要求。", Required = false)]
			public string[] requestedAdjustments { get; set; }

			[ToolParameter("ACCEPTED時のみVISUAL_ACCEPTEDを指定。", Required = false)]
			public string acceptanceConfirmation { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.SubmitVisualReview(
					parameters.requestId,
					parameters.captureId,
					parameters.expectedRevision,
					parameters.evidenceDigest,
					parameters.decision,
					parameters.reviewer,
					parameters.observations,
					parameters.requestedAdjustments,
					parameters.acceptanceConfirmation));
		}
	}

	[McpForUnityTool(
		"graphics.refine_from_visual_review",
		Description = "REJECTEDまたはNEEDS_ADJUSTMENTで確定したVisual Reviewだけを用いて、次IterationのDirection PlanをRead-onlyで作成します。",
		AutoRegister = false,
		Group = "core")]
	public static class RefineFromVisualReviewTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Refine元のDirection Plan ID。", Required = true)]
			public string directionPlanId { get; set; }

			[ToolParameter("graphics.submit_visual_reviewが返したReview ID。", Required = true)]
			public string reviewId { get; set; }

			[ToolParameter("Direction PlanとReviewが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.RefineFromVisualReview(
					parameters.requestId,
					parameters.directionPlanId,
					parameters.reviewId,
					parameters.expectedRevision));
		}
	}
}

#endif
