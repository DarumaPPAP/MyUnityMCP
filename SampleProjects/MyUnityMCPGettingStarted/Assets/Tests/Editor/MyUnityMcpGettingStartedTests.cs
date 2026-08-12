#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityGraphicsMcp;

namespace MyUnityMcpGettingStarted
{
	public sealed class MyUnityMcpGettingStartedTests
	{
		private static readonly string[] TOOL_NAMES =
		{
			"graphics.inspect_project", "graphics.inspect_scene", "graphics.validate_scene",
			"graphics.compile_direction", "graphics.preview_plan", "graphics.prepare_light_plan",
			"graphics.apply_plan", "graphics.undo_last_transaction", "graphics.prepare_environment_plan",
			"graphics.apply_environment_plan", "graphics.undo_last_environment_transaction",
			"graphics.prepare_save_plan", "graphics.apply_save_plan", "graphics.prepare_bake_plan",
			"graphics.bake_dependencies", "graphics.capture_evaluation", "graphics.refine_direction",
			"graphics.capture_evidence", "graphics.submit_visual_review", "graphics.refine_from_visual_review",
			"graphics.prepare_apv_bake_plan", "graphics.start_apv_bake", "graphics.get_apv_bake_status",
			"graphics.cancel_apv_bake", "graphics.prepare_acceptance_profile", "graphics.evaluate_capture",
			"graphics.refine_from_evaluation", "graphics.get_execution_status", "graphics.cancel_execution",
			"graphics.get_execution_history", "graphics.get_error_catalog", "graphics.get_support_matrix",
			"agent.inspect_capabilities", "agent.validate_workflow", "agent.compile_graph",
			"agent.preview_execution", "agent.submit_approval", "agent.start_execution",
			"agent.get_execution_status", "agent.cancel_execution", "agent.get_execution_history",
			"agent.get_error_catalog",
			"world.compile_workflow", "world.start_preflight", "world.create_review_handoff",
			"profiler.inspect_environment", "profiler.inspect_counters", "profiler.prepare_capture",
			"profiler.start_capture", "profiler.get_capture_status", "profiler.cancel_capture",
			"profiler.summarize_capture", "profiler.compare_baseline",
			"addressables.inspect", "addressables.prepare_entry", "addressables.apply_entry", "addressables.get_support_matrix",
			"ui.inspect", "ui.validate", "ui.prepare_rect_transform", "ui.apply_rect_transform", "ui.get_support_matrix",
			"animation.inspect", "animation.validate", "animation.prepare_parameter", "animation.apply_parameter", "animation.get_support_matrix",
			"audio.inspect", "audio.validate", "audio.prepare_source", "audio.apply_source", "audio.get_support_matrix",
			"cinematic.inspect", "cinematic.validate", "cinematic.prepare_director", "cinematic.apply_director", "cinematic.get_support_matrix"
		};

		[Test]
		public void GettingStartedWorkflow_CompilesDiscoversToolsAndCreatesReadOnlyPlan()
		{
			EditorSceneManager.OpenScene(
				"Assets/Scenes/MyUnityMcpGettingStarted.unity",
				OpenSceneMode.Single);

			string[] discoveredToolNames = typeof(InspectProjectTool)
				.Assembly
				.GetTypes()
				.SelectMany(type => type.GetCustomAttributesData())
				.Where(attribute => string.Equals(
					attribute.AttributeType.FullName,
					"MCPForUnity.Editor.Tools.McpForUnityToolAttribute",
					StringComparison.Ordinal))
				.Select(attribute => attribute.ConstructorArguments.Count > 0
					? attribute.ConstructorArguments[0].Value as string
					: null)
				.Where(toolName => !string.IsNullOrWhiteSpace(toolName))
				.OrderBy(toolName => toolName, StringComparer.Ordinal)
				.ToArray();
			Assert.That(discoveredToolNames, Is.EquivalentTo(TOOL_NAMES));
			Assert.That(discoveredToolNames.Length, Is.EqualTo(77));

			ToolResult project =
				Inspection.InspectProject("sample-project");
			Assert.That(project.IsSuccessful, Is.True, project.summary);

			ToolResult scene = Inspection.InspectScene(
				"sample-scene", true, 50, null, null, null);
			Assert.That(scene.IsSuccessful, Is.True, scene.summary);
			Dictionary<string, object> sceneData = scene.data as Dictionary<string, object>;
			Assert.That(sceneData, Is.Not.Null);
			Assert.That(sceneData["snapshotId"] as string, Is.Not.Empty);

			ToolResult plan = Inspection.CompileDirection(
				"sample-plan",
				"被写体が読みやすい安全なGetting Started Scene",
				new[] { "Main Camera and Directional Light are present" },
				new[] { "clear and calm" },
				new[] { "camera then subject then background" },
				new[] { "stable camera" },
				new[] { "key light" },
				new[] { "neutral base with warm highlights" },
				new[] { "stable reflections" },
				new[] { "clear depth separation" },
				new[] { "low motion" },
				new[] { "stable editor workflow" },
				new[] { "EDITOR" },
				new[] { "no automatic save", "no bake" },
				project.revision);
			Assert.That(plan.IsSuccessful, Is.True, plan.summary);
			Assert.That(plan.revision, Is.EqualTo(project.revision));
		}
	}
}

#endif
