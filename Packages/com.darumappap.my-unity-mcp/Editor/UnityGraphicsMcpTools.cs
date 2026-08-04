#if UNITY_EDITOR

using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace UnityGraphicsMcp
{
	[McpForUnityTool(
		"graphics.inspect_project",
		Description = "対象Unity Projectの検出済み事実と、今回要求されたTargetを分離してRead-onlyで取得します。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsInspectProjectTool
	{
		public sealed class Parameters
		{
			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("今回の依頼で明示されたTarget Platform。ProjectのActive Build Targetとは別に保持します。", Required = false)]
			public string[] requestedPlatforms { get; set; }

			[ToolParameter("今回の依頼で明示された品質方針または禁止事項。検出済みProject事実を上書きしません。", Required = false)]
			public string[] requestedConstraints { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.InspectProject(
					parameters.requestId,
					parameters.requestedPlatforms,
					parameters.requestedConstraints));
		}
	}

	[McpForUnityTool(
		"graphics.inspect_scene",
		Description = "Loaded SceneのCamera、Light、Probe、Renderer、Material、Volume等をRead-onlyでSnapshot化し、Pagingして返します。",
		AutoRegister = false,
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
		AutoRegister = false,
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

	[McpForUnityTool(
		"graphics.compile_direction",
		Description = "構造化Visual Intentと対象Project事実から、Pipeline非依存のDirection PlanをRead-onlyで生成します。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsCompileDirectionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("制作Goalまたは自然言語によるVisual Direction。", Required = false)]
			public string goal { get; set; }

			[ToolParameter("参考画像を外部Visionまたは人間が観察して構造化した記述。Unity側では画像解析を行いません。", Required = false)]
			public string[] referenceObservations { get; set; }

			[ToolParameter("感情・印象の意図。例: 幻想的、切ない、華やか。", Required = false)]
			public string[] emotionalIntent { get; set; }

			[ToolParameter("Hero / Support / Landmark / Foreground / Midground / Background等の構図階層。", Required = false)]
			public string[] compositionHierarchy { get; set; }

			[ToolParameter("画角、Lens、Camera movement、Shot language等。", Required = false)]
			public string[] cameraLanguage { get; set; }

			[ToolParameter("Key / Fill / Rim / Practical / Motivated等のLighting階層。", Required = false)]
			public string[] lightingHierarchy { get; set; }

			[ToolParameter("色相、明度、彩度、温度、時間変化等のColor Script。", Required = false)]
			public string[] colorScript { get; set; }

			[ToolParameter("Material、Specular、Reflection、Roughness等の意図。", Required = false)]
			public string[] materialReflectionIntent { get; set; }

			[ToolParameter("Fog、Aerial Perspective、Depth Cue等のAtmospheric Depth。", Required = false)]
			public string[] atmosphericDepth { get; set; }

			[ToolParameter("静的、穏やか、激しい等のMotion Energy。", Required = false)]
			public string[] motionEnergy { get; set; }

			[ToolParameter("Frame Time、Memory、Resolution、Quality等のPerformance Priority。", Required = false)]
			public string[] performancePriorities { get; set; }

			[ToolParameter("今回のPlan対象Platform。ProjectのActive Build Targetとは別に保持します。", Required = false)]
			public string[] requestedPlatforms { get; set; }

			[ToolParameter("禁止事項、維持条件、品質制約。", Required = false)]
			public string[] requestedConstraints { get; set; }

			[ToolParameter("呼び出し元が前提とするEditor Revision。省略時は現在Revisionを使用します。", Required = false)]
			public long? expectedRevision { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.CompileDirection(
					parameters.requestId,
					parameters.goal,
					parameters.referenceObservations,
					parameters.emotionalIntent,
					parameters.compositionHierarchy,
					parameters.cameraLanguage,
					parameters.lightingHierarchy,
					parameters.colorScript,
					parameters.materialReflectionIntent,
					parameters.atmosphericDepth,
					parameters.motionEnergy,
					parameters.performancePriorities,
					parameters.requestedPlatforms,
					parameters.requestedConstraints,
					parameters.expectedRevision));
		}
	}

	[McpForUnityTool(
		"graphics.preview_plan",
		Description = "保存済みDirection Planが生むCreated / Modified / Dirty / Bake / Unsupported / Unverified候補を、Unity状態を変更せずに返します。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsPreviewPlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }

			[ToolParameter("graphics.compile_directionが返したPlan ID。", Required = true)]
			public string planId { get; set; }

			[ToolParameter("graphics.compile_directionが返したexpectedRevision。", Required = true)]
			public long? expectedRevision { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.PreviewPlan(
					parameters.requestId,
					parameters.planId,
					parameters.expectedRevision));
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
