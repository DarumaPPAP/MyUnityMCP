#if UNITY_EDITOR

using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityGraphicsMcp;

namespace MyUnityMcpGettingStarted
{
	public sealed class GraphEngineeringFreshProjectTests
	{
		private static readonly string[] REQUIRED_DEVELOPMENT_TOOLS =
		{
			"agent.inspect_capabilities",
			"profiler.inspect_environment",
			"build.inspect_environment",
			"addressables.inspect",
			"ui.inspect",
			"animation.inspect",
			"audio.inspect",
			"cinematic.inspect",
			"world.compile_workflow",
			"movie.compile_production",
			"live.compile_show"
		};

		[Test]
		public void FreshProject_CompilesDiscoversAllGraphToolsAndRunsReadOnlyInspection()
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
				.Distinct(StringComparer.Ordinal)
				.OrderBy(toolName => toolName, StringComparer.Ordinal)
				.ToArray();

			Assert.That(discoveredToolNames.Length, Is.EqualTo(91));
			Assert.That(discoveredToolNames, Does.Contain("graphics.inspect_project"));
			foreach (string toolName in REQUIRED_DEVELOPMENT_TOOLS)
			{
				Assert.That(discoveredToolNames, Does.Contain(toolName), $"Missing Development Tool: {toolName}");
			}

			ToolResult project = Inspection.InspectProject("graph-fresh-project");
			Assert.That(project.IsSuccessful, Is.True, project.summary);

			ToolResult scene = Inspection.InspectScene(
				"graph-fresh-scene",
				true,
				50,
				null,
				null,
				null);
			Assert.That(scene.IsSuccessful, Is.True, scene.summary);
		}
	}
}

#endif
