#if UNITY_EDITOR

using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityAgentMcp;
using UnityGraphicsMcp;

namespace UnityWorldCreatorMcp
{
	[McpForUnityTool(
		"world.compile_workflow",
		Description = "Visual Goalを既存GraphicsMCPのRead-only Preflight Graphと、後続の承認制制作Handoffへ変換します。",
		AutoRegister = false,
		Group = "creator")]
	public static class WorldCompileWorkflowTool
	{
		public sealed class Parameters
		{
			[ToolParameter("制作Goal。", Required = true)]
			public string visualGoal { get; set; }

			[ToolParameter("対象Scene Scope。", Required = false)]
			public string sceneScope { get; set; }

			[ToolParameter("環境種別。", Required = false)]
			public string environmentType { get; set; }

			[ToolParameter("求めるMood。", Required = false)]
			public string desiredMood { get; set; }

			[ToolParameter("対象Platform。", Required = false)]
			public string[] targetPlatforms { get; set; }

			[ToolParameter("禁止変更。", Required = false)]
			public string[] prohibitedChanges { get; set; }

			[ToolParameter("Acceptance条件。", Required = false)]
			public string[] acceptanceCriteria { get; set; }

			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityWorldCreatorRuntime.Execute<Parameters>(
				@params,
				value => UnityWorldCreatorRuntime.CompileWorkflow(
					value.visualGoal,
					value.sceneScope,
					value.environmentType,
					value.desiredMood,
					value.targetPlatforms,
					value.prohibitedChanges,
					value.acceptanceCriteria,
					value.expectedRevision));
		}
	}

	[McpForUnityTool(
		"world.start_preflight",
		Description = "World WorkflowのRead-only Graphics PreflightをAgent経由で実行します。",
		AutoRegister = false,
		Group = "creator")]
	public static class WorldStartPreflightTool
	{
		public sealed class Parameters
		{
			[ToolParameter("world.compile_workflowが返したAgent Graph ID。", Required = true)]
			public string graphId { get; set; }

			[ToolParameter("現在のEditor Revision。", Required = true)]
			public long? currentRevision { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityWorldCreatorRuntime.Execute<Parameters>(
				@params,
				value => value.currentRevision.HasValue
					? UnityWorldCreatorRuntime.StartPreflight(
						value.graphId,
						value.currentRevision.Value)
					: UnityWorldCreatorRuntime.Error(
						"WORLD-REVISION-MISSING",
						"currentRevisionが必要です。"));
		}
	}

	[McpForUnityTool(
		"world.create_review_handoff",
		Description = "World Preflight結果と制作意図を、人間が合否・修正指示を返せるReview Handoffへ変換します。",
		AutoRegister = false,
		Group = "creator")]
	public static class WorldCreateReviewHandoffTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Agent Execution ID。", Required = true)]
			public string executionId { get; set; }

			[ToolParameter("制作Goal。", Required = true)]
			public string visualGoal { get; set; }

			[ToolParameter("Acceptance条件。", Required = false)]
			public string[] acceptanceCriteria { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityWorldCreatorRuntime.Execute<Parameters>(
				@params,
				value => UnityWorldCreatorRuntime.CreateReviewHandoff(
					value.executionId,
					value.visualGoal,
					value.acceptanceCriteria));
		}
	}

	internal static class UnityWorldCreatorRuntime
	{
		public static object Execute<T>(
			JObject @params,
			Func<T, JObject> operation)
			where T : new()
		{
			try
			{
				T value = @params == null || !@params.HasValues
					? new T()
					: @params.ToObject<T>();
				return operation(value ?? new T());
			}
			catch (Exception exception)
			{
				return Error("WORLD-REQUEST-INVALID", exception.Message);
			}
		}

		public static JObject CompileWorkflow(
			string visualGoal,
			string sceneScope,
			string environmentType,
			string desiredMood,
			string[] targetPlatforms,
			string[] prohibitedChanges,
			string[] acceptanceCriteria,
			long? expectedRevision)
		{
			if (string.IsNullOrWhiteSpace(visualGoal))
			{
				return Error("WORLD-GOAL-MISSING", "visualGoalが必要です。");
			}

			if (!expectedRevision.HasValue || expectedRevision.Value != Session.Revision)
			{
				return Error(
					"WORLD-REVISION-STALE",
					"expectedRevisionが現在のEditor Revisionと一致しません。");
			}

			UnityAgentMcpStepInput[] steps =
			{
				new UnityAgentMcpStepInput
				{
					stepId = "inspect_project",
					domainId = "unity_graphics_mcp",
					toolName = "graphics.inspect_project",
					toolGroup = "inspect",
					dependsOn = Array.Empty<string>(),
					parameters = new JObject
					{
						["requestedPlatforms"] = targetPlatforms == null
							? null
							: new JArray(targetPlatforms),
						["requestedConstraints"] = prohibitedChanges == null
							? null
							: new JArray(prohibitedChanges)
					}
				},
				new UnityAgentMcpStepInput
				{
					stepId = "inspect_scene",
					domainId = "unity_graphics_mcp",
					toolName = "graphics.inspect_scene",
					toolGroup = "inspect",
					dependsOn = new[] { "inspect_project" },
					parameters = new JObject
					{
						["includeInactive"] = true,
						["maxItems"] = 200
					}
				},
				new UnityAgentMcpStepInput
				{
					stepId = "validate_scene",
					domainId = "unity_graphics_mcp",
					toolName = "graphics.validate_scene",
					toolGroup = "inspect",
					dependsOn = new[] { "inspect_scene" },
					parameters = new JObject
					{
						["includeInactive"] = true
					}
				}
			};

			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(
				expectedRevision.Value,
				steps);
			if (!(compiled.Value<bool?>("success") ?? false))
			{
				return compiled;
			}

			compiled["creator"] = "world_creator";
			compiled["visualIntent"] = new JObject
			{
				["visualGoal"] = visualGoal,
				["sceneScope"] = sceneScope,
				["environmentType"] = environmentType,
				["desiredMood"] = desiredMood,
				["targetPlatforms"] = targetPlatforms == null
					? null
					: new JArray(targetPlatforms),
				["prohibitedChanges"] = prohibitedChanges == null
					? null
					: new JArray(prohibitedChanges),
				["acceptanceCriteria"] = acceptanceCriteria == null
					? null
					: new JArray(acceptanceCriteria)
			};
			compiled["nextStages"] = new JArray
			{
				"graphics.compile_direction",
				"graphics.preview_plan",
				"human_approval",
				"graphics_domain_mutation",
				"optional_save_or_bake_approval",
				"graphics.capture_evidence",
				"human_visual_review"
			};
			compiled["directUnityMutation"] = false;
			return compiled;
		}

		public static JObject StartPreflight(
			string graphId,
			long currentRevision)
		{
			return UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				currentRevision,
				null);
		}

		public static JObject CreateReviewHandoff(
			string executionId,
			string visualGoal,
			string[] acceptanceCriteria)
		{
			if (string.IsNullOrWhiteSpace(executionId))
			{
				return Error("WORLD-EXECUTION-MISSING", "executionIdが必要です。");
			}

			if (string.IsNullOrWhiteSpace(visualGoal))
			{
				return Error("WORLD-GOAL-MISSING", "visualGoalが必要です。");
			}

			JObject status = UnityAgentMcpRuntime.Instance.GetExecutionStatus(executionId);
			if (!(status.Value<bool?>("success") ?? false))
			{
				return status;
			}

			return new JObject
			{
				["success"] = true,
				["handoffStatus"] = "HUMAN_REVIEW_REQUIRED",
				["visualGoal"] = visualGoal,
				["acceptanceCriteria"] = acceptanceCriteria == null
					? new JArray()
					: new JArray(acceptanceCriteria),
				["preflight"] = status,
				["reviewQuestions"] = new JArray
				{
					"Visual GoalとScene構成は一致しているか",
					"禁止変更を守れるPlanか",
					"Mutation／Save／Bakeを承認するか",
					"追加の修正指示は何か"
				},
				["automaticVisualAcceptance"] = false
			};
		}

		public static JObject Error(
			string code,
			string message)
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
