#if UNITY_EDITOR

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UnityGraphicsMcp
{
	[McpForUnityTool(
		"graphics.get_execution_status",
		Description = "Execution IDから進捗、Cancellation状態、Timeout、Trace参照先を取得します。",
		AutoRegister = false,
		Group = "core")]
	public static class GetExecutionStatusTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("監視対象Execution ID。", Required = true)]
			public string executionId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => ExecutionHardening.GetExecutionStatus(
					parameters.requestId,
					parameters.executionId));
		}
	}

	[McpForUnityTool(
		"graphics.cancel_execution",
		Description = "Active Executionへ協調Cancellationを要求します。安全なCancellation Pointで停止します。",
		AutoRegister = false,
		Group = "core")]
	public static class CancelExecutionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Cancellation対象Execution ID。", Required = true)]
			public string executionId { get; set; }

			[ToolParameter("Cancellation理由。", Required = false)]
			public string reason { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => ExecutionHardening.CancelExecution(
					parameters.requestId,
					parameters.executionId,
					parameters.reason));
		}
	}

	[McpForUnityTool(
		"graphics.get_execution_history",
		Description = "保持中のTool実行履歴、構造化結果、DurationとMemory計測を取得します。",
		AutoRegister = false,
		Group = "core")]
	public static class GetExecutionHistoryTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("特定Tool名へ絞り込む場合に指定します。", Required = false)]
			public string tool { get; set; }

			[ToolParameter("取得件数。1～200、既定50。", Required = false)]
			public int? maxEntries { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => ExecutionHardening.GetExecutionHistory(
					parameters.requestId,
					parameters.tool,
					parameters.maxEntries ?? 50));
		}
	}

	[McpForUnityTool(
		"graphics.get_error_catalog",
		Description = "Hardening Error Code、再試行可能性、再実行開始点、復旧手順を取得します。",
		AutoRegister = false,
		Group = "core")]
	public static class GetErrorCatalogTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => ExecutionHardening.GetErrorCatalog(
					parameters.requestId));
		}
	}

	[McpForUnityTool(
		"graphics.get_support_matrix",
		Description = "Unity Version、Render Pipeline、Capability、Execution Recoveryの固定Support Matrixを取得します。",
		AutoRegister = false,
		Group = "core")]
	public static class GetSupportMatrixTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => ExecutionHardening.GetSupportMatrix(
					parameters.requestId));
		}
	}
}

#endif