#if UNITY_EDITOR

using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UnityAgentMcp
{
	[McpForUnityTool("agent.inspect_capabilities", Description = "利用可能なDomain、Tool Group、実行可否をCatalogからRead-onlyで取得します。", AutoRegister = false, Group = "agent")]
	public static class AgentInspectCapabilitiesTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, _ => UnityAgentMcpRuntime.Instance.InspectCapabilities());
	}

	[McpForUnityTool("agent.validate_workflow", Description = "Workflow StepのDomain、Tool Group、Tool、依存関係をRead-onlyで検証します。", AutoRegister = false, Group = "agent")]
	public static class AgentValidateWorkflowTool
	{
		public sealed class Parameters
		{
			[ToolParameter("検証するWorkflow Step。", Required = true)]
			public UnityAgentMcpStepInput[] steps { get; set; }
		}
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, value => UnityAgentMcpRuntime.Instance.ValidateWorkflow(value.steps));
	}

	[McpForUnityTool("agent.compile_graph", Description = "検証済みWorkflowをProduct Runtime GraphへCompileします。Unity状態は変更しません。", AutoRegister = false, Group = "agent")]
	public static class AgentCompileGraphTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Graphが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }
			[ToolParameter("CompileするWorkflow Step。", Required = true)]
			public UnityAgentMcpStepInput[] steps { get; set; }
		}
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, value =>
				value.expectedRevision.HasValue
					? UnityAgentMcpRuntime.Instance.CompileGraph(value.expectedRevision.Value, value.steps)
					: UnityAgentMcpToolBridge.Error("AGENT-REVISION-MISSING", "expectedRevisionが必要です。"));
	}

	[McpForUnityTool("agent.preview_execution", Description = "Compiled GraphのStep、Side Effect、必要ApprovalをRead-onlyでPreviewします。", AutoRegister = false, Group = "agent")]
	public static class AgentPreviewExecutionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("agent.compile_graphが返したGraph ID。", Required = true)]
			public string graphId { get; set; }
		}
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, value => UnityAgentMcpRuntime.Instance.PreviewExecution(value.graphId));
	}

	[McpForUnityTool("agent.submit_approval", Description = "Preview済みGraphのSide Effect Groupを明示承認し、一時Approval Tokenを発行します。", AutoRegister = false, Group = "agent")]
	public static class AgentSubmitApprovalTool
	{
		public sealed class Parameters
		{
			[ToolParameter("承認対象Graph ID。", Required = true)]
			public string graphId { get; set; }
			[ToolParameter("承認するTool Group。", Required = true)]
			public string[] approvedGroups { get; set; }
			[ToolParameter("APPROVE_AGENT_EXECUTIONを指定します。", Required = true)]
			public string confirmation { get; set; }
		}
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, value =>
				UnityAgentMcpRuntime.Instance.SubmitApproval(value.graphId, value.approvedGroups, value.confirmation));
	}

	[McpForUnityTool("agent.start_execution", Description = "RevisionとApprovalを再検証して協調Executionを開始します。StepはEditor Update境界でDomain Toolへ委譲し、Control PlaneはUnity APIを直接Mutationしません。", AutoRegister = false, Group = "agent")]
	public static class AgentStartExecutionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("実行するGraph ID。", Required = true)]
			public string graphId { get; set; }
			[ToolParameter("実行直前のEditor Revision。", Required = true)]
			public long? currentRevision { get; set; }
			[ToolParameter("Side Effectがある場合のApproval Token。", Required = false)]
			public string approvalToken { get; set; }
			[ToolParameter("Execution Timeout秒。1～3600。未指定時60秒。", Required = false)]
			public int? timeoutSeconds { get; set; }
		}
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, value =>
				value.currentRevision.HasValue
					? UnityAgentMcpRuntime.Instance.StartExecution(
						value.graphId,
						value.currentRevision.Value,
						value.approvalToken,
						value.timeoutSeconds ?? 60)
					: UnityAgentMcpToolBridge.Error("AGENT-REVISION-MISSING", "currentRevisionが必要です。"));
	}

	[McpForUnityTool("agent.get_execution_status", Description = "Agent Executionの現在状態とStep Resultを取得します。", AutoRegister = false, Group = "agent")]
	public static class AgentGetExecutionStatusTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Execution ID。", Required = true)]
			public string executionId { get; set; }
		}
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, value => UnityAgentMcpRuntime.Instance.GetExecutionStatus(value.executionId));
	}

	[McpForUnityTool("agent.cancel_execution", Description = "Running中のAgent Executionを次の安全なStep境界より前に協調Cancelします。", AutoRegister = false, Group = "agent")]
	public static class AgentCancelExecutionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Execution ID。", Required = true)]
			public string executionId { get; set; }
		}
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, value => UnityAgentMcpRuntime.Instance.CancelExecution(value.executionId));
	}

	[McpForUnityTool("agent.get_execution_history", Description = "永続化済みAgent Execution Historyを取得します。", AutoRegister = false, Group = "agent")]
	public static class AgentGetExecutionHistoryTool
	{
		public sealed class Parameters
		{
			[ToolParameter("取得件数。1～100。", Required = false)]
			public int? maxItems { get; set; }
		}
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, value => UnityAgentMcpRuntime.Instance.GetExecutionHistory(value.maxItems ?? 20));
	}

	[McpForUnityTool("agent.get_error_catalog", Description = "Agent Control Planeの構造化Error Catalogを取得します。", AutoRegister = false, Group = "agent")]
	public static class AgentGetErrorCatalogTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) =>
			UnityAgentMcpToolBridge.Execute<Parameters>(@params, _ => UnityAgentMcpRuntime.Instance.GetErrorCatalog());
	}

	internal static class UnityAgentMcpToolBridge
	{
		public static object Execute<T>(JObject @params, Func<T, JObject> operation) where T : new()
		{
			try
			{
				T parameters = @params == null || !@params.HasValues ? new T() : @params.ToObject<T>();
				return operation(parameters ?? new T());
			}
			catch (Exception exception)
			{
				return Error("AGENT-REQUEST-INVALID", exception.Message);
			}
		}

		public static JObject Error(string code, string message)
		{
			return new JObject
			{
				["success"] = false,
				["errorCode"] = code,
				["message"] = message
			};
		}
	}
}

#endif