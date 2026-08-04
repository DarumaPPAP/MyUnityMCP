#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpPlanningTests
	{
		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
		}

		[TearDown]
		public void TearDown()
		{
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
		}

		[Test]
		public void Bridge_DiscoversPhase2Tools_AndKeepsThemDisabledByDefault()
		{
			CommandRegistry.Initialize();

			Assert.That(
				CommandRegistry.GetHandler("graphics.compile_direction"),
				Is.Not.Null);
			Assert.That(
				CommandRegistry.GetHandler("graphics.preview_plan"),
				Is.Not.Null);

			McpForUnityToolAttribute compileAttribute =
				Attribute.GetCustomAttribute(
					typeof(GraphicsCompileDirectionTool),
					typeof(McpForUnityToolAttribute)) as McpForUnityToolAttribute;

			McpForUnityToolAttribute previewAttribute =
				Attribute.GetCustomAttribute(
					typeof(GraphicsPreviewPlanTool),
					typeof(McpForUnityToolAttribute)) as McpForUnityToolAttribute;

			Assert.That(compileAttribute, Is.Not.Null);
			Assert.That(previewAttribute, Is.Not.Null);
			Assert.That(compileAttribute.AutoRegister, Is.False);
			Assert.That(previewAttribute.AutoRegister, Is.False);
		}

		[Test]
		public void Bridge_CanInvokeCompileDirectionHandler()
		{
			CommandRegistry.Initialize();
			Func<JObject, object> handler =
				CommandRegistry.GetHandler("graphics.compile_direction");

			object response = handler(new JObject
			{
				["requestId"] = "test-plan-bridge",
				["goal"] = "夜のステージを華やかにする",
				["lightingHierarchy"] = new JArray("Key", "Rim"),
				["colorScript"] = new JArray("Cool shadows", "Warm highlights")
			});

			Assert.That(response, Is.TypeOf<SuccessResponse>());
		}

		[Test]
		public void CompileDirection_RejectsEmptyIntent()
		{
			UnityGraphicsMcpToolResult result = CompileDirection(
				"test-empty-intent",
				null,
				null,
				null,
				null);

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void CompileDirection_NaturalLanguageOnlyReturnsPartialAndDoesNotPretendImageAnalysis()
		{
			UnityGraphicsMcpToolResult result = CompileDirection(
				"test-natural-language-only",
				"幻想的な夜の空気感",
				null,
				null,
				null);

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.PARTIAL.ToString()));
			Assert.That(
				result.issues.Any(issue =>
					issue.code == "STRUCTURED_VISUAL_INTENT_REQUIRED"),
				Is.True);

			Dictionary<string, object> data =
				result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null);

			Dictionary<string, object> visualIntent =
				data["visualIntent"] as Dictionary<string, object>;
			Assert.That(visualIntent, Is.Not.Null);
			Assert.That(
				visualIntent["imageAnalysisPerformedByUnity"],
				Is.EqualTo(false));
			Assert.That(
				visualIntent["semanticInterpretationSource"],
				Is.EqualTo("NATURAL_LANGUAGE_UNPARSED"));
		}

		[Test]
		public void CompileDirection_StructuredIntentCreatesSixSectionPlan()
		{
			UnityGraphicsMcpToolResult result = CompileDirection(
				"test-structured-plan",
				"夜のライブステージ",
				new[] { "Key", "Rim" },
				new[] { "Cool shadows", "Warm highlights" },
				new[] { "Stable frame time" });

			Assert.That(result.IsSuccessful, Is.True);

			Dictionary<string, object> data =
				result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null);
			Assert.That(data["planId"] as string, Is.Not.Empty);
			Assert.That(data.ContainsKey("projectContext"), Is.True);

			List<UnityGraphicsMcpPlanRecommendation> recommendations =
				data["recommendations"] as List<UnityGraphicsMcpPlanRecommendation>;
			Assert.That(recommendations, Is.Not.Null);
			Assert.That(recommendations.Count, Is.EqualTo(6));
			Assert.That(
				recommendations.Select(item => item.section),
				Is.EquivalentTo(new[]
				{
					"LIGHTING",
					"GI",
					"REFLECTION",
					"ATMOSPHERE",
					"LOOK",
					"PLATFORM"
				}));
		}

		[Test]
		public void CompileDirection_DoesNotChangeSceneDirtyState()
		{
			Scene scene = SceneManager.GetActiveScene();
			bool dirtyBefore = scene.isDirty;

			UnityGraphicsMcpToolResult result = CompileDirection(
				"test-plan-dirty",
				"落ち着いた室内",
				new[] { "Soft key" },
				new[] { "Low contrast" },
				new[] { "Preserve quality" });

			Assert.That(result.IsSuccessful, Is.True);
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));
		}

		[Test]
		public void PreviewPlan_UsesExistingLightAsModifiedForecastWithoutApplyingChanges()
		{
			GameObject lightObject = new GameObject("ExistingLight");
			lightObject.AddComponent<Light>();

			UnityGraphicsMcpToolResult compileResult = CompileDirection(
				"test-preview-existing-light",
				"既存ライトを活かす",
				new[] { "Key", "Fill" },
				new[] { "Neutral" },
				new[] { "Stable frame time" });

			Dictionary<string, object> compileData =
				compileResult.data as Dictionary<string, object>;
			Assert.That(compileData, Is.Not.Null);

			string planId = compileData["planId"] as string;
			long expectedRevision = Convert.ToInt64(compileData["expectedRevision"]);
			Scene scene = SceneManager.GetActiveScene();
			bool dirtyBefore = scene.isDirty;

			UnityGraphicsMcpToolResult previewResult =
				UnityGraphicsMcpInspection.PreviewPlan(
					"test-preview-existing-light-result",
					planId,
					expectedRevision);

			Assert.That(previewResult.IsSuccessful, Is.True);
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));

			Dictionary<string, object> preview =
				previewResult.data as Dictionary<string, object>;
			Assert.That(preview, Is.Not.Null);
			Assert.That(preview["actualChangesApplied"], Is.EqualTo(false));

			List<Dictionary<string, object>> modified =
				preview["modified"] as List<Dictionary<string, object>>;
			List<Dictionary<string, object>> created =
				preview["created"] as List<Dictionary<string, object>>;

			Assert.That(modified, Is.Not.Null);
			Assert.That(created, Is.Not.Null);
			Assert.That(
				modified.Any(item =>
					item["targetType"] as string == "UnityEngine.Light"),
				Is.True);
			Assert.That(
				created.Any(item =>
					item["targetType"] as string == "UnityEngine.Light"),
				Is.False);
		}

		[Test]
		public void PreviewPlan_ReturnsConditionalBakeAndVerificationRequirements()
		{
			UnityGraphicsMcpToolResult compileResult = CompileDirection(
				"test-preview-bake",
				"反射と間接光を強化",
				new[] { "Soft key" },
				new[] { "Metallic highlights" },
				new[] { "Target device budget" });

			Dictionary<string, object> compileData =
				compileResult.data as Dictionary<string, object>;
			string planId = compileData["planId"] as string;
			long expectedRevision = Convert.ToInt64(compileData["expectedRevision"]);

			UnityGraphicsMcpToolResult previewResult =
				UnityGraphicsMcpInspection.PreviewPlan(
					"test-preview-bake-result",
					planId,
					expectedRevision);

			Dictionary<string, object> preview =
				previewResult.data as Dictionary<string, object>;
			Assert.That(preview, Is.Not.Null);

			List<Dictionary<string, object>> bakeRequired =
				preview["bakeRequired"] as List<Dictionary<string, object>>;
			List<Dictionary<string, object>> unsupported =
				preview["unsupported"] as List<Dictionary<string, object>>;
			List<Dictionary<string, object>> unverified =
				preview["unverified"] as List<Dictionary<string, object>>;

			Assert.That(
				bakeRequired.Any(item =>
					item["dependency"] as string == "LIGHTMAP"),
				Is.True);
			Assert.That(
				bakeRequired.Any(item =>
					item["dependency"] as string == "REFLECTION_PROBE"),
				Is.True);
			Assert.That(
				unsupported.Any(item =>
					item["code"] as string ==
					"NATIVE_MUTATION_BACKEND_NOT_IMPLEMENTED"),
				Is.True);
			Assert.That(
				unverified.Any(item =>
					item["code"] as string ==
					"HUMAN_VISUAL_REVIEW_REQUIRED"),
				Is.True);
		}

		[Test]
		public void PreviewPlan_RejectsMismatchedRevision()
		{
			UnityGraphicsMcpToolResult compileResult = CompileDirection(
				"test-preview-stale",
				"静かな夕景",
				new[] { "Soft key" },
				new[] { "Warm gradient" },
				new[] { "Preserve quality" });

			Dictionary<string, object> compileData =
				compileResult.data as Dictionary<string, object>;
			string planId = compileData["planId"] as string;
			long expectedRevision = Convert.ToInt64(compileData["expectedRevision"]);

			UnityGraphicsMcpToolResult previewResult =
				UnityGraphicsMcpInspection.PreviewPlan(
					"test-preview-stale-result",
					planId,
					expectedRevision + 1);

			Assert.That(
				previewResult.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
		}

		private static UnityGraphicsMcpToolResult CompileDirection(
			string requestId,
			string goal,
			string[] lightingHierarchy,
			string[] colorScript,
			string[] performancePriorities)
		{
			return UnityGraphicsMcpInspection.CompileDirection(
				requestId,
				goal,
				null,
				null,
				null,
				null,
				lightingHierarchy,
				colorScript,
				null,
				null,
				null,
				performancePriorities,
				new[] { "PC" },
				new[] { "Automatic Save禁止" },
				null);
		}
	}
}

#endif
