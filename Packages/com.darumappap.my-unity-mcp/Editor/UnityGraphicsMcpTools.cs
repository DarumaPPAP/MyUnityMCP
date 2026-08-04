#if UNITY_EDITOR

using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UnityGraphicsMcp
{
	[McpForUnityTool(
		"graphics.inspect_project",
		Description = "対象Unity ProjectのVersion、Pipeline、Renderer、Build Target、Graphics API、関連PackageをRead-onlyで取得します。",
		Group = "core")]
	public static class GraphicsInspectProjectTool
	{
		public sealed class Parameters
		{
			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.InspectProject(parameters.requestId));
		}
	}

	[McpForUnityTool(
		"graphics.inspect_scene",
		Description = "Loaded SceneのCamera、Light、Probe、Renderer、Material、Volume等をRead-onlyでSnapshot化し、Pagingして返します。",
		Group = "core")]
	public static class GraphicsInspectSceneTool
	{
		public sealed class Parameters
		{
			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Inactive GameObjectを解析対象へ含めるか。", Required = false)]
			public bool? includeInactive { get; set; }

			[ToolParameter("1回で返す最大項目数。1～200。", Required = false)]
			public int? maxItems { get; set; }

			[ToolParameter("取得Section。例: CAMERA, LIGHT, LIGHTMAP, LIGHT_PROBE, APV, REFLECTION_PROBE, RENDERER_MATERIAL, VOLUME, DECAL, PARTICLE, VFX, CINEMATIC, RENDERER_FEATURE。省略時は全Section。", Required = false)]
			public string[] sections { get; set; }

			[ToolParameter("既存Snapshotの続きを読む場合のSnapshot ID。", Required = false)]
			public string snapshotId { get; set; }

			[ToolParameter("既存Snapshotの続きを読む場合の0以上のCursor。", Required = false)]
			public string cursor { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.InspectScene(
					parameters.requestId,
					parameters.includeInactive ?? true,
					parameters.maxItems ?? 50,
					parameters.sections,
					parameters.snapshotId,
					parameters.cursor));
		}
	}

	[McpForUnityTool(
		"graphics.validate_scene",
		Description = "Loaded SceneのGraphics不整合をRead-onlyで検証し、Invariant、Policy、Heuristicを区別して返します。",
		Group = "core")]
	public static class GraphicsValidateSceneTool
	{
		public sealed class Parameters
		{
			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("Inactive GameObjectを検証対象へ含めるか。", Required = false)]
			public bool? includeInactive { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.ValidateScene(
					parameters.requestId,
					parameters.includeInactive ?? true));
		}
	}

	internal static class UnityGraphicsMcpToolBridge
	{
		public static object Execute<T>(
			JObject @params,
			Func<T, UnityGraphicsMcpToolResult> operation)
			where T : new()
		{
			try
			{
				T parameters = ParseParameters<T>(@params);
				return Wrap(operation(parameters));
			}
			catch (Exception exception)
			{
				return new ErrorResponse(
					E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString(),
					new
					{
						message = "Tool Parameterを解釈できませんでした。",
						exceptionType = exception.GetType().FullName,
						detail = exception.Message
					});
			}
		}

		public static T ParseParameters<T>(JObject @params)
			where T : new()
		{
			if (@params == null)
			{
				return new T();
			}

			T parameters = @params.ToObject<T>();
			return parameters == null ? new T() : parameters;
		}

		public static object Wrap(UnityGraphicsMcpToolResult result)
		{
			if (result == null)
			{
				return new ErrorResponse(
					"MYUNITYMCP_NULL_RESULT",
					new { message = "MyUnityMCP ToolがResultを返しませんでした。" });
			}

			if (result.IsSuccessful)
			{
				return new SuccessResponse(result.summary, result);
			}

			return new ErrorResponse(result.status, result);
		}
	}
}

#endif
