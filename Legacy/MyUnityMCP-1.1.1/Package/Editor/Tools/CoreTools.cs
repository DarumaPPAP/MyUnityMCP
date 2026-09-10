#if UNITY_EDITOR

using System;
using System.Collections.Generic;
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
	public static class InspectProjectTool
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
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.InspectProject(
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
	public static class InspectSceneTool
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
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.InspectScene(
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
	public static class ValidateSceneTool
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
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.ValidateScene(
					parameters.requestId,
					parameters.includeInactive ?? true));
		}
	}

	[McpForUnityTool(
		"graphics.compile_direction",
		Description = "構造化Visual Intentと対象Project事実から、Pipeline非依存のDirection PlanをRead-onlyで生成します。",
		AutoRegister = false,
		Group = "core")]
	public static class CompileDirectionTool
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
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.CompileDirection(
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
	public static class PreviewPlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("graphics.compile_directionが返したPlan ID。", Required = true)]
			public string planId { get; set; }

			[ToolParameter("graphics.compile_directionが返したexpectedRevision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.PreviewPlan(
					parameters.requestId,
					parameters.planId,
					parameters.expectedRevision));
		}
	}

	[McpForUnityTool(
		"graphics.prepare_light_plan",
		Description = "Direction Planへ明示的なLIGHT_CREATE / LIGHT_UPDATEを関連付け、正確な差分と承認TokenをRead-onlyで発行します。",
		AutoRegister = false,
		Group = "core")]
	public static class PrepareLightPlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("graphics.compile_directionが返したDirection Plan ID。", Required = true)]
			public string directionPlanId { get; set; }

			[ToolParameter("Direction Planが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("明示的なLIGHT_CREATE / LIGHT_UPDATE操作。曖昧な自然言語からUnity側で数値を推測しません。", Required = true)]
			public LightOperationInput[] lightOperations { get; set; }

			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.PrepareLightPlan(
					parameters.requestId,
					parameters.directionPlanId,
					parameters.expectedRevision,
					parameters.lightOperations));
		}
	}

	[McpForUnityTool(
		"graphics.apply_plan",
		Description = "prepare_light_planでPreview・承認されたExecutable Planを、一つのUnity Undo Transactionとして適用します。自動保存とBakeは行いません。",
		AutoRegister = false,
		Group = "core")]
	public static class ApplyPlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("graphics.prepare_light_planが返したExecutable Plan ID。", Required = true)]
			public string planId { get; set; }

			[ToolParameter("graphics.prepare_light_planが返したexpectedRevision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("Preview確認後に使用する一時承認Token。", Required = true)]
			public string approvalToken { get; set; }

			[ToolParameter("Light MutationではNONEのみ。Scene / Assetを自動保存しません。", Required = true)]
			public string saveMode { get; set; }

			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.ApplyPlan(
					parameters.requestId,
					parameters.planId,
					parameters.expectedRevision,
					parameters.approvalToken,
					parameters.saveMode));
		}
	}

	[McpForUnityTool(
		"graphics.undo_last_transaction",
		Description = "直近のMyUnityMCP Light TransactionがUndo Stackの最新で、適用後状態から変化していない場合だけUnity Undoで復元します。",
		AutoRegister = false,
		Group = "core")]
	public static class UndoLastTransactionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("graphics.apply_planが返したTransaction ID。", Required = true)]
			public string transactionId { get; set; }

			[ToolParameter("graphics.apply_planが返したrevision。", Required = true)]
			public long? expectedRevision { get; set; }

			[ToolParameter("呼び出し元が付与するRequest ID。省略時はUnity側で生成します。", Required = false)]
			public string requestId { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return ToolBridge.Execute<Parameters>(
				@params,
				parameters => Inspection.UndoLastTransaction(
					parameters.requestId,
					parameters.transactionId,
					parameters.expectedRevision));
		}
	}

	internal static class ToolBridge
	{
		public static object Execute<T>(
			JObject @params,
			Func<T, ToolResult> operation)
			where T : new()
		{
			string requestId = @params == null || @params["requestId"] == null
				? null
				: @params["requestId"].ToString();
			Type declaringType = typeof(T).DeclaringType;
			string provisionalToolName = declaringType == null
				? "unknown"
				: declaringType.Name;
			ExecutionScope scope =
				ExecutionHardening.Begin(
					provisionalToolName,
					requestId);

			T parameters;
			try
			{
				parameters = ParseParameters<T>(@params);
			}
			catch (Exception exception)
			{
				ToolResult invalid =
					Inspection.CreateHardeningResult(
						provisionalToolName,
						requestId,
						E_MCP_TOOL_STATUS.INVALID_REQUEST,
						"Tool Parameterを解釈できませんでした。",
						new Dictionary<string, object>
						{
							{ "failureCode", "MCP_INVALID_REQUEST" },
							{ "exceptionType", exception.GetType().FullName },
							{ "detail", exception.Message }
						});
				return Wrap(ExecutionHardening.Complete(scope, invalid));
			}

			try
			{
				ToolResult result = operation(parameters);
				if (result == null)
				{
					result = Inspection.CreateHardeningResult(
						provisionalToolName,
						requestId,
						E_MCP_TOOL_STATUS.FAILED,
						"MyUnityMCP ToolがResultを返しませんでした。",
						new Dictionary<string, object>
						{
							{ "failureCode", "MYUNITYMCP_NULL_RESULT" }
						});
				}
				return Wrap(ExecutionHardening.Complete(scope, result));
			}
			catch (OperationCanceledException exception)
			{
				ToolResult cancelled =
					Inspection.CreateHardeningResult(
						provisionalToolName,
						requestId,
						E_MCP_TOOL_STATUS.FAILED,
						"Tool実行はCancellation Pointで停止しました。",
						new Dictionary<string, object>
						{
							{ "failureCode", "EXECUTION_CANCEL_REQUESTED" },
							{ "detail", exception.Message }
						});
				return Wrap(ExecutionHardening.Complete(scope, cancelled));
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
				ToolResult failed =
					Inspection.CreateHardeningResult(
						provisionalToolName,
						requestId,
						E_MCP_TOOL_STATUS.FAILED,
						"Tool実行中に未処理例外が発生しました。",
						new Dictionary<string, object>
						{
							{ "failureCode", "MCP_FAILED" },
							{ "exceptionType", exception.GetType().FullName },
							{ "detail", exception.Message }
						});
				return Wrap(ExecutionHardening.Complete(scope, failed));
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

		public static object Wrap(ToolResult result)
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
