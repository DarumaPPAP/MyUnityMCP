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
	public sealed class DependencyBakeTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpDependencyBakeTemporaryScene.unity";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			DependencyBakeSession.ClearForTests();
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[TearDown]
		public void TearDown()
		{
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			DependencyBakeSession.ClearForTests();
			Undo.ClearAll();
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[Test]
		public void Bridge_DiscoversDependencyBakeTools_AndKeepsThemDisabledByDefault()
		{
			CommandRegistry.Initialize();

			Assert.That(
				CommandRegistry.GetHandler("graphics.prepare_bake_plan"),
				Is.Not.Null);
			Assert.That(
				CommandRegistry.GetHandler("graphics.bake_dependencies"),
				Is.Not.Null);
			Assert.That(
				GetToolAttribute(typeof(PrepareBakePlanTool)).AutoRegister,
				Is.False);
			Assert.That(
				GetToolAttribute(typeof(BakeDependenciesTool)).AutoRegister,
				Is.False);
		}

		[Test]
		public void PrepareBakePlan_IsReadOnly_AndDirtyDependencySurvivesSave()
		{
			GameObject target = CreateTrackedSceneAndSave();
			Scene scene = target.scene;
			Assert.That(scene.isDirty, Is.False);
			Assert.That(
				DependencyBakeSession.HasDirtySceneForTests(
					TEMP_SCENE_PATH),
				Is.True);

			int undoGroupBefore = Undo.GetCurrentGroup();
			ToolResult result = PrepareLightmapBake();

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

			ToolResult result =
				Inspection.BakeDependencies(
					"dependency-bake-missing-approval",
					executable["planId"] as string,
					Convert.ToInt64(executable["expectedRevision"]),
					null,
					"EXPLICIT_DEPENDENCIES");

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(
				DependencyBakeSession.HasDirtySceneForTests(
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
			DependencyBakeSession.TrackDirtySceneForTests(
				target.scene);

			ToolResult result =
				Inspection.BakeDependencies(
					"dependency-bake-changed-baseline",
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
			DependencyBakeSession.SceneBakeOverrideForTests =
				scene =>
				{
					invocationCount++;
					bakedScenePath = scene.path;
					return true;
				};

			ToolResult result =
				Inspection.BakeDependencies(
					"dependency-bake-apply",
					executable["planId"] as string,
					Convert.ToInt64(executable["expectedRevision"]),
					executable["approvalToken"] as string,
					"EXPLICIT_DEPENDENCIES");

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Assert.That(invocationCount, Is.EqualTo(1));
			Assert.That(bakedScenePath, Is.EqualTo(TEMP_SCENE_PATH));
			Assert.That(
				DependencyBakeSession.HasDirtySceneForTests(
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

			ToolResult result =
				Inspection.PrepareBakePlan(
					"dependency-bake-apv",
					Session.Revision,
					new[]
					{
						new BakeTargetInput
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
			DependencyBakeSession.ClearForTests();

			ToolResult result =
				Inspection.PrepareBakePlan(
					"dependency-bake-clean",
					Session.Revision,
					new[]
					{
						new BakeTargetInput
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
			GameObject target = new GameObject("DependencyBake Bake Target");
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(
				EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false),
				Is.True);

			target.transform.position = Vector3.one;
			EditorSceneManager.MarkSceneDirty(scene);
			DependencyBakeSession.TrackDirtySceneForTests(scene);

			Assert.That(
				EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false),
				Is.True);
			Assert.That(scene.isDirty, Is.False);
			return target;
		}

		private static ToolResult PrepareLightmapBake()
		{
			return Inspection.PrepareBakePlan(
				"dependency-bake-prepare",
				Session.Revision,
				new[]
				{
					new BakeTargetInput
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
			ToolResult result)
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
