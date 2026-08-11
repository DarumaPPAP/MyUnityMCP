#if UNITY_EDITOR

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UnityGraphicsMcp
{
	[McpForUnityTool(
		"graphics.prepare_apv_bake_plan",
		Description = "APV Baking Set、Lighting Scenario、Scene集合、Pipeline Capability、Output RootをRead-onlyで固定します。",
		AutoRegister = false,
		Group = "core")]
	public static class PrepareApvBakePlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("APV Bakeの明示入力。", Required = true)]
			public ApvBakePlanInput input { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.PrepareApvBakePlan(
					parameters.requestId,
					parameters.expectedRevision,
					parameters.input));
		}
	}

	[McpForUnityTool(
		"graphics.start_apv_bake",
		Description = "承認済みAPV Bake Planを開始し、Job IDを返します。長時間処理はStatus Toolで追跡します。",
		AutoRegister = false,
		Group = "core")]
	public static class StartApvBakeTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("APV Bake Plan ID。", Required = true)]
			public string planId { get; set; }

			[ToolParameter("Planが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Prepare Toolが返した承認Token。", Required = true)]
			public string approvalToken { get; set; }

			[ToolParameter("EXPLICIT_APV_BAKING_SET。", Required = true)]
			public string bakeMode { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.StartApvBake(
					parameters.requestId,
					parameters.planId,
					parameters.expectedRevision,
					parameters.approvalToken,
					parameters.bakeMode));
		}
	}

	[McpForUnityTool(
		"graphics.get_apv_bake_status",
		Description = "APV Bake Jobの進行、出力差分、Partial Result、失敗理由、Cancellation状態を返します。",
		AutoRegister = false,
		Group = "core")]
	public static class GetApvBakeStatusTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("APV Bake Job ID。", Required = true)]
			public string jobId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.GetApvBakeStatus(
					parameters.requestId,
					parameters.jobId));
		}
	}

	[McpForUnityTool(
		"graphics.cancel_apv_bake",
		Description = "APV Bake JobへCancellationを要求し、停止後も生成済みOutputをPartial Resultとして記録します。",
		AutoRegister = false,
		Group = "core")]
	public static class CancelApvBakeTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("APV Bake Job ID。", Required = true)]
			public string jobId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.CancelApvBake(
					parameters.requestId,
					parameters.jobId));
		}
	}

	[McpForUnityTool(
		"graphics.prepare_acceptance_profile",
		Description = "Visual Acceptanceの評価項目、Weight、最低合格値、Critical Failure、Reference Capture、Performance Budgetを固定します。",
		AutoRegister = false,
		Group = "core")]
	public static class PrepareAcceptanceProfileTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Acceptance Profile入力。", Required = true)]
			public AcceptanceProfileInput input { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.PrepareAcceptanceProfile(
					parameters.requestId,
					parameters.expectedRevision,
					parameters.input));
		}
	}

	[McpForUnityTool(
		"graphics.evaluate_capture",
		Description = "Capture EvidenceをAcceptance Profileと外部Measurementで評価し、不合格理由をObject IDとPerformance Budgetへ関連付けます。",
		AutoRegister = false,
		Group = "core")]
	public static class EvaluateCaptureTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Capture ID。", Required = true)]
			public string captureId { get; set; }

			[ToolParameter("CaptureとProfileが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Capture Evidence Digest。", Required = true)]
			public string evidenceDigest { get; set; }

			[ToolParameter("Acceptance Profile ID。", Required = true)]
			public string profileId { get; set; }

			[ToolParameter("各評価項目の外部Measurement。", Required = false)]
			public EvaluationMeasurementInput[] measurements { get; set; }

			[ToolParameter("実機またはPlayer計測のPerformance Measurement。", Required = false)]
			public PerformanceMeasurementInput performance { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.EvaluateCapture(
					parameters.requestId,
					parameters.captureId,
					parameters.expectedRevision,
					parameters.evidenceDigest,
					parameters.profileId,
					parameters.measurements,
					parameters.performance));
		}
	}

	[McpForUnityTool(
		"graphics.refine_from_evaluation",
		Description = "FAILEDまたはINCOMPLETEのVisual Evaluationを、問題箇所、Object ID、Performance Budgetを含む次Direction Planへ変換します。",
		AutoRegister = false,
		Group = "core")]
	public static class RefineFromEvaluationTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Refine元Direction Plan ID。", Required = true)]
			public string directionPlanId { get; set; }

			[ToolParameter("Visual Evaluation ID。", Required = true)]
			public string evaluationId { get; set; }

			[ToolParameter("PlanとEvaluationが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.RefineFromEvaluation(
					parameters.requestId,
					parameters.directionPlanId,
					parameters.evaluationId,
					parameters.expectedRevision));
		}
	}
}

#endif
