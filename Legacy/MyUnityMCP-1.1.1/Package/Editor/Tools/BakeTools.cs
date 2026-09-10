#if UNITY_EDITOR

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UnityGraphicsMcp
{
	[McpForUnityTool(
		"graphics.prepare_bake_plan",
		Description = "現在SessionのDirty Dependency SetからScene限定Lightmapまたは明示Reflection ProbeのExact Bake Planと別Approval Tokenを発行します。",
		AutoRegister = false,
		Group = "core")]
	public static class PrepareBakePlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Scene Path、Dependency Kind、必要ならReflection Probe Object IDを指定します。", Required = true)]
			public BakeTargetInput[] targets { get; set; }

			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.PrepareBakePlan(
					parameters.requestId,
					parameters.expectedRevision,
					parameters.targets));
		}
	}

	[McpForUnityTool(
		"graphics.bake_dependencies",
		Description = "prepare_bake_planで固定・承認されたDependencyだけを同期Bakeします。自動Save、全Scene BakeへのFallback、Undo保証は行いません。",
		AutoRegister = false,
		Group = "core")]
	public static class BakeDependenciesTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Bake Plan ID。", Required = true)]
			public string planId { get; set; }

			[ToolParameter("Bake Planが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Exact Dependency確認後の一時Bake承認Token。", Required = true)]
			public string approvalToken { get; set; }

			[ToolParameter("EXPLICIT_DEPENDENCIESのみ。", Required = true)]
			public string bakeMode { get; set; }

			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.BakeDependencies(
					parameters.requestId,
					parameters.planId,
					parameters.expectedRevision,
					parameters.approvalToken,
					parameters.bakeMode));
		}
	}
}

#endif
