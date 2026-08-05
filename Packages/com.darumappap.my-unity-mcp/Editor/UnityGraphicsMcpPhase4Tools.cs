#if UNITY_EDITOR

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UnityGraphicsMcp
{
	[McpForUnityTool(
		"graphics.prepare_save_plan",
		Description = "一つのDirty Loaded SceneをRead-onlyで固定し、永続化用のExact Diffと一時承認Tokenを発行します。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsPrepareSavePlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Phase 4Aでは一つの保存済みLoaded Sceneを指定します。", Required = true)]
			public UnityGraphicsMcpSaveTargetInput[] targets { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.PrepareSavePlan(
					parameters.requestId,
					parameters.expectedRevision,
					parameters.targets));
		}
	}

	[McpForUnityTool(
		"graphics.apply_save_plan",
		Description = "prepare_save_planで固定・承認された一つのDirty Sceneだけを明示的に保存します。Save Asと自動保存は行いません。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsApplySavePlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Save Plan ID。", Required = true)]
			public string planId { get; set; }

			[ToolParameter("Save Planが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Exact Diff確認後の一時Save承認Token。", Required = true)]
			public string approvalToken { get; set; }

			[ToolParameter("Phase 4AではEXPLICIT_SCENEのみ。", Required = true)]
			public string saveMode { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.ApplySavePlan(
					parameters.requestId,
					parameters.planId,
					parameters.expectedRevision,
					parameters.approvalToken,
					parameters.saveMode));
		}
	}

	[McpForUnityTool(
		"graphics.capture_evaluation",
		Description = "指定CameraからColor PNGをLibrary配下へ取得し、TargetTexture、Active RenderTexture、Scene Dirty状態を必ず復元します。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsCaptureEvaluationTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Capture対象Camera ComponentのGlobalObjectId。", Required = true)]
			public string cameraObjectId { get; set; }

			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Capture幅。既定1280、64～4096。", Required = false)]
			public int? width { get; set; }

			[ToolParameter("Capture高さ。既定720、64～4096。", Required = false)]
			public int? height { get; set; }

			[ToolParameter("出力File名へ使用する短いLabel。", Required = false)]
			public string captureLabel { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.CaptureEvaluation(
					parameters.requestId,
					parameters.cameraObjectId,
					parameters.expectedRevision,
					parameters.width,
					parameters.height,
					parameters.captureLabel));
		}
	}

	[McpForUnityTool(
		"graphics.refine_direction",
		Description = "Capture Evidenceに対する明示的なHuman Reviewだけを用いて、次IterationのDirection PlanをRead-onlyで作成します。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsRefineDirectionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Refine元のDirection Plan ID。", Required = true)]
			public string directionPlanId { get; set; }

			[ToolParameter("graphics.capture_evaluationが返したCapture ID。", Required = true)]
			public string captureId { get; set; }

			[ToolParameter("Direction PlanとCaptureが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("人間または外部Visionが明示したCapture観察結果。", Required = false)]
			public string[] humanObservations { get; set; }

			[ToolParameter("次Iterationへ反映する明示的な調整要求。", Required = false)]
			public string[] requestedAdjustments { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.RefineDirection(
					parameters.requestId,
					parameters.directionPlanId,
					parameters.captureId,
					parameters.expectedRevision,
					parameters.humanObservations,
					parameters.requestedAdjustments));
		}
	}
}

#endif
