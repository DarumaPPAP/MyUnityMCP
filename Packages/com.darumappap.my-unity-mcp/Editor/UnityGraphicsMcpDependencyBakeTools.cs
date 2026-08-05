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
	public static class GraphicsPrepareBakePlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Scene Path、Dependency Kind、必要ならReflection Probe Object IDを指定します。", Required = true)]
			public UnityGraphicsMcpBakeTargetInput[] targets { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.PrepareBakePlan(
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
	public static class GraphicsBakeDependenciesTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Bake Plan ID。", Required = true)]
			public string planId { get; set; }

			[ToolParameter("Bake Planが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Exact Dependency確認後の一時Bake承認Token。", Required = true)]
			public string approvalToken { get; set; }

			[ToolParameter("EXPLICIT_DEPENDENCIESのみ。", Required = true)]
			public string bakeMode { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.BakeDependencies(
					parameters.requestId,
					parameters.planId,
					parameters.expectedRevision,
					parameters.approvalToken,
					parameters.bakeMode));
		}
	}
}

#endif
