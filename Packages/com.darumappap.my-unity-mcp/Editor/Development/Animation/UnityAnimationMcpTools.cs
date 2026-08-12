#if UNITY_EDITOR

using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;

namespace UnityAnimationMcp
{
	[McpForUnityTool("animation.inspect", Description = "Loaded SceneのAnimatorと参照AnimatorController／ClipをRead-onlyで取得します。", AutoRegister = false, Group = "animation")]
	public static class AnimationInspectTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Inactive Objectを含めるか。", Required = false)] public bool? includeInactive { get; set; }
		}

		public static object HandleCommand(JObject @params) =>
			UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityAnimationMcpRuntime.Inspect(value.includeInactive ?? true));
	}

	[McpForUnityTool("animation.validate", Description = "Animator Controller欠落、無効Parameter、Clip EventをRead-onlyで検証します。", AutoRegister = false, Group = "animation")]
	public static class AnimationValidateTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) =>
			UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityAnimationMcpRuntime.Validate());
	}

	[McpForUnityTool("animation.prepare_parameter", Description = "既存AnimatorControllerへのParameter追加をExact Previewし、承認Tokenを発行します。", AutoRegister = false, Group = "animation")]
	public static class AnimationPrepareParameterTool
	{
		public sealed class Parameters
		{
			[ToolParameter("AnimatorController Asset Path。", Required = true)] public string controllerAssetPath { get; set; }
			[ToolParameter("Parameter名。", Required = true)] public string parameterName { get; set; }
			[ToolParameter("Float／Int／Bool／Trigger。", Required = true)] public string parameterType { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? expectedRevision { get; set; }
			[ToolParameter("Float既定値。", Required = false)] public float? defaultFloat { get; set; }
			[ToolParameter("Int既定値。", Required = false)] public int? defaultInt { get; set; }
			[ToolParameter("Bool既定値。", Required = false)] public bool? defaultBool { get; set; }
		}

		public static object HandleCommand(JObject @params) =>
			UnityDomainMcpCommon.Execute<Parameters>(@params, value =>
				UnityAnimationMcpRuntime.PrepareParameter(
					value.controllerAssetPath,
					value.parameterName,
					value.parameterType,
					value.defaultFloat,
					value.defaultInt,
					value.defaultBool,
					value.expectedRevision));
	}

	[McpForUnityTool("animation.apply_parameter", Description = "承認済みPlanでAnimatorController Parameterを追加します。State／Transition／Clipは変更しません。", AutoRegister = false, Group = "animation")]
	public static class AnimationApplyParameterTool
	{
		public sealed class Parameters
		{
			[ToolParameter("animation.prepare_parameterが返したPlan ID。", Required = true)] public string planId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? currentRevision { get; set; }
			[ToolParameter("Approval Token。", Required = true)] public string approvalToken { get; set; }
		}

		public static object HandleCommand(JObject @params) =>
			UnityDomainMcpCommon.Execute<Parameters>(@params, value =>
				UnityAnimationMcpRuntime.ApplyParameter(value.planId, value.currentRevision, value.approvalToken));
	}

	[McpForUnityTool("animation.get_support_matrix", Description = "Animation MCPの実装・非対象・未検証範囲を取得します。", AutoRegister = false, Group = "animation")]
	public static class AnimationGetSupportMatrixTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) =>
			UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityAnimationMcpRuntime.GetSupportMatrix());
	}
}

#endif
