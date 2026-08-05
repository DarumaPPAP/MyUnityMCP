#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpPhase4BakeTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpPhase4BakeTemporaryScene.unity";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
			UnityGraphicsMcpPhase4Session.ClearForTests();
			UnityGraphicsMcpPhase4BakeSession.ClearForTests();
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[TearDown]
		public void TearDown()
		{
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
			UnityGraphicsMcpPhase4Session.ClearForTests();
			UnityGraphicsMcpPhase4BakeSession.ClearForTests();
			Undo.ClearAll();
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[Test]
		public void Bridge_DiscoversPhase4BTools_AndKeepsThemDisabledByDefault()
		{
			CommandRegistry.Initialize();

			Assert.That(
				CommandRegistry.GetHandler("graphics.prepare_bake_plan"),
				Is.Not.Null);
			Assert.That(
				CommandRegistry.GetHandler("graphics.bake_dependencies"),
				Is.Not.Null);
			Assert.That(
				GetToolAttribute(typeof(GraphicsPrepareBakePlanTool)).AutoRegister,
				Is.False);
			Assert.That(
				GetToolAttribute(typeof(GraphicsBakeDependenciesTool)).AutoRegister,
				Is.False);
		}

		[Test]
		public void PrepareBakePlan_IsReadOnly_AndDirtyDependencySurvivesSave()
		{
			GameObject target = CreateTrackedSceneAndSave();
			Scene scene = target.scene;
			Assert.That(scene.isDirty, Is.False);
			Assert.That(
				UnityGraphicsMcpPhase4BakeSession.HasDirtySceneForTests(
					TEMP_SCENE_PATH),
				Is.True);

			int undoGroupBefore = Undo.GetCurrentGroup();
			UnityGraphicsMcpToolResult result = PrepareLightmapBake();

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Assert.That(scene.isDirty, Is.False);
			Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroupBefore));

			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["approvalToken"] as string, Is.Not.Empty);
			Assert.That(data["diffDigest"] as string, Is.Not.Empty);
			Assert.That(data["bakePerformed"], Is.EqualTo(false));
			Assert.That(data["savePerformed"], Is.EqualTo(false));
			Assert.That(data["automaticRollback"], Is.EqualTo(false));

			List<Dictionary<string, object>> dependencies =
				data["dependencies"] as List<Dictionary<string, object>>;
			Assert.That(dependencies, Is.Not.Null);
			Assert.That(dependencies.Count, Is.EqualTo(1));
			Assert.That(
				dependencies[0]["kind"],
				Is.EqualTo(
					E_GRAPHICS_BAKE_DEPENDENCY_KIND.LIGHTMAP_SCENE.ToString()));
		}

		[Test]
		public void BakeDependencies_RejectsMissingApproval()
		{
			CreateTrackedSceneAndSave();
			Dictionary<string, object> executable =
				ResultData(PrepareLightmapBake());

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.BakeDependencies(
					"phase4b-missing-approval",
					executable["planId"] as string,
					Convert.ToInt64(executable["expectedRevision"]),
					null,
					"EXPLICIT_DEPENDENCIES");

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(
				UnityGraphicsMcpPhase4BakeSession.HasDirtySceneForTests(
					TEMP_SCENE_PATH),
				Is.True);
		}

		[Test]
		public void BakeDependencies_RejectsChangedBaseline()
		{
			GameObject target = CreateTrackedSceneAndSave();
			Dictionary<string, object> executable =
				ResultData(PrepareLightmapBake());

			target.transform.position = new Vector3(2.0f, 3.0f, 4.0f);
			EditorSceneManager.MarkSceneDirty(target.scene);
			UnityGraphicsMcpPhase4BakeSession.TrackDirtySceneForTests(
				target.scene);

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.BakeDependencies(
					"phase4b-changed-baseline",
					executable["planId"] as string,
					Convert.ToInt64(executable["expectedRevision"]),
					executable["approvalToken"] as string,
					"EXPLICIT_DEPENDENCIES");

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
		}

		[Test]
		public void BakeDependencies_ExecutesExplicitSceneBackend_AndClearsDependency()
		{
			CreateTrackedSceneAndSave();
			Dictionary<string, object> executable =
				ResultData(PrepareLightmapBake());

			int invocationCount = 0;
			string bakedScenePath = null;
			UnityGraphicsMcpPhase4BakeSession.SceneBakeOverrideForTests =
				scene =>
				{
					invocationCount++;
					bakedScenePath = scene.path;
					return true;
				};

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.BakeDependencies(
					"phase4b-apply",
					executable["planId"] as string,
					Convert.ToInt64(executable["expectedRevision"]),
					executable["approvalToken"] as string,
					"EXPLICIT_DEPENDENCIES");

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Assert.That(invocationCount, Is.EqualTo(1));
			Assert.That(bakedScenePath, Is.EqualTo(TEMP_SCENE_PATH));
			Assert.That(
				UnityGraphicsMcpPhase4BakeSession.HasDirtySceneForTests(
					TEMP_SCENE_PATH),
				Is.False);

			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["bakePerformed"], Is.EqualTo(true));
			Assert.That(data["savePerformed"], Is.EqualTo(false));
			Assert.That(data["undoAvailable"], Is.EqualTo(false));
			Assert.That(data["automaticRollback"], Is.EqualTo(false));
		}

		[Test]
		public void PrepareBakePlan_RejectsApvUntilPipelineSpecificBackendExists()
		{
			CreateTrackedSceneAndSave();

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.PrepareBakePlan(
					"phase4b-apv",
					UnityGraphicsMcpSession.Revision,
					new[]
					{
						new UnityGraphicsMcpBakeTargetInput
						{
							scenePath = TEMP_SCENE_PATH,
							dependencyKinds = new[]
							{
								E_GRAPHICS_BAKE_DEPENDENCY_KIND
									.ADAPTIVE_PROBE_VOLUME
									.ToString()
							}
						}
					});

			Assert.That(
				result.status,
				Is.EqualTo(
					E_MCP_TOOL_STATUS.BACKEND_NOT_IMPLEMENTED.ToString()));
			Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
		}

		[Test]
		public void PrepareBakePlan_RejectsCleanSceneOutsideDirtyDependencySet()
		{
			new GameObject("Clean Scene Target");
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(
				EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false),
				Is.True);
			UnityGraphicsMcpPhase4BakeSession.ClearForTests();

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.PrepareBakePlan(
					"phase4b-clean",
					UnityGraphicsMcpSession.Revision,
					new[]
					{
						new UnityGraphicsMcpBakeTargetInput
						{
							scenePath = TEMP_SCENE_PATH,
							dependencyKinds = new[]
							{
								E_GRAPHICS_BAKE_DEPENDENCY_KIND
									.LIGHTMAP_SCENE
									.ToString()
							}
						}
					});

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		private static GameObject CreateTrackedSceneAndSave()
		{
			GameObject target = new GameObject("Phase4B Bake Target");
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(
				EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false),
				Is.True);

			target.transform.position = Vector3.one;
			EditorSceneManager.MarkSceneDirty(scene);
			UnityGraphicsMcpPhase4BakeSession.TrackDirtySceneForTests(scene);

			Assert.That(
				EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false),
				Is.True);
			Assert.That(scene.isDirty, Is.False);
			return target;
		}

		private static UnityGraphicsMcpToolResult PrepareLightmapBake()
		{
			return UnityGraphicsMcpInspection.PrepareBakePlan(
				"phase4b-prepare",
				UnityGraphicsMcpSession.Revision,
				new[]
				{
					new UnityGraphicsMcpBakeTargetInput
					{
						scenePath = TEMP_SCENE_PATH,
						dependencyKinds = new[]
						{
							E_GRAPHICS_BAKE_DEPENDENCY_KIND
								.LIGHTMAP_SCENE
								.ToString()
						}
					}
				});
		}

		private static Dictionary<string, object> ResultData(
			UnityGraphicsMcpToolResult result)
		{
			Dictionary<string, object> data =
				result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null, result.summary);
			return data;
		}

		private static McpForUnityToolAttribute GetToolAttribute(Type type)
		{
			McpForUnityToolAttribute attribute =
				Attribute.GetCustomAttribute(
					type,
					typeof(McpForUnityToolAttribute)) as McpForUnityToolAttribute;
			Assert.That(attribute, Is.Not.Null);
			return attribute;
		}
	}
}

#endif
