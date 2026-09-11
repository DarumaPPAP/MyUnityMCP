#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class SaveEvaluationTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpSaveEvaluationTemporaryScene.unity";

		private readonly List<string> _capturePaths = new List<string>();

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			_capturePaths.Clear();
		}

		[TearDown]
		public void TearDown()
		{
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			Undo.ClearAll();
			RenderTexture.active = null;
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);

			foreach (string path in _capturePaths)
			{
				if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
				{
					File.Delete(path);
				}
			}
		}

		[Test]
		public void Bridge_DiscoversSaveEvaluationTools_AndKeepsThemDisabledByDefault()
		{
			CommandRegistry.Initialize();

			Assert.That(
				CommandRegistry.GetHandler("graphics.prepare_save_plan"),
				Is.Not.Null);
			Assert.That(
				CommandRegistry.GetHandler("graphics.apply_save_plan"),
				Is.Not.Null);
			Assert.That(
				CommandRegistry.GetHandler("graphics.capture_evaluation"),
				Is.Not.Null);
			Assert.That(
				CommandRegistry.GetHandler("graphics.refine_direction"),
				Is.Not.Null);

			Assert.That(
				GetToolAttribute(typeof(PrepareSavePlanTool)).AutoRegister,
				Is.False);
			Assert.That(
				GetToolAttribute(typeof(ApplySavePlanTool)).AutoRegister,
				Is.False);
			Assert.That(
				GetToolAttribute(typeof(CaptureEvaluationTool)).AutoRegister,
				Is.False);
			Assert.That(
				GetToolAttribute(typeof(RefineDirectionTool)).AutoRegister,
				Is.False);
		}

		[Test]
		public void PrepareSavePlan_IsReadOnly()
		{
			Scene scene = CreateSavedDirtyScene();
			bool dirtyBefore = scene.isDirty;
			int undoGroupBefore = Undo.GetCurrentGroup();

			ToolResult result =
				Inspection.PrepareSavePlan(
					"save-evaluation-save-readonly",
					Session.Revision,
					new[]
					{
						new SaveTargetInput
						{
							scenePath = TEMP_SCENE_PATH
						}
					});

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));
			Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroupBefore));

			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["approvalToken"] as string, Is.Not.Empty);
			Assert.That(data["diffDigest"] as string, Is.Not.Empty);
			Assert.That(data["savePerformed"], Is.EqualTo(false));
			Assert.That(data["undoAvailable"], Is.EqualTo(false));
		}

		[Test]
		public void ApplySavePlan_RejectsMissingApproval()
		{
			Dictionary<string, object> executable = PrepareSave();

			ToolResult result =
				Inspection.ApplySavePlan(
					"save-evaluation-save-missing-approval",
					executable["planId"] as string,
					Convert.ToInt64(executable["expectedRevision"]),
					null,
					"EXPLICIT_SCENE");

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(SceneManager.GetActiveScene().isDirty, Is.True);
		}

		[Test]
		public void ApplySavePlan_RejectsChangedBaseline()
		{
			GameObject target = new GameObject("Baseline Target");
			SaveScene();
			target.transform.position = Vector3.one;
			EditorSceneManager.MarkSceneDirty(target.scene);

			Dictionary<string, object> executable = PrepareSaveForCurrentScene();

			target.transform.position = new Vector3(2.0f, 3.0f, 4.0f);
			EditorSceneManager.MarkSceneDirty(target.scene);

			ToolResult result =
				Inspection.ApplySavePlan(
					"save-evaluation-save-changed-baseline",
					executable["planId"] as string,
					Convert.ToInt64(executable["expectedRevision"]),
					executable["approvalToken"] as string,
					"EXPLICIT_SCENE");

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
			Assert.That(SceneManager.GetActiveScene().isDirty, Is.True);
		}

		[Test]
		public void ApplySavePlan_SavesExplicitDirtyScene()
		{
			Dictionary<string, object> executable = PrepareSave();

			ToolResult result =
				Inspection.ApplySavePlan(
					"save-evaluation-save-apply",
					executable["planId"] as string,
					Convert.ToInt64(executable["expectedRevision"]),
					executable["approvalToken"] as string,
					"EXPLICIT_SCENE");

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
			Assert.That(File.Exists(ProjectAbsolutePath(TEMP_SCENE_PATH)), Is.True);

			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["savePerformed"], Is.EqualTo(true));
			Assert.That(data["undoAvailable"], Is.EqualTo(false));
		}

		[Test]
		public void PrepareSavePlan_RejectsUnsavedSceneAndSaveAs()
		{
			new GameObject("Unsaved Target");
			Scene scene = SceneManager.GetActiveScene();
			EditorSceneManager.MarkSceneDirty(scene);

			ToolResult result =
				Inspection.PrepareSavePlan(
					"save-evaluation-save-as-reject",
					Session.Revision,
					new[]
					{
						new SaveTargetInput
						{
							scenePath = TEMP_SCENE_PATH
						}
					});

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(scene.path, Is.Empty);
			Assert.That(File.Exists(ProjectAbsolutePath(TEMP_SCENE_PATH)), Is.False);
		}

		[Test]
		public void CaptureEvaluation_RestoresTemporaryState_AndWritesPngWhenAvailable()
		{
			GameObject cameraObject = new GameObject("SaveEvaluation Capture Camera");
			Camera camera = cameraObject.AddComponent<Camera>();
			SaveScene();

			RenderTexture originalActive = new RenderTexture(16, 16, 0);
			originalActive.Create();
			RenderTexture.active = originalActive;
			RenderTexture originalTarget = camera.targetTexture;
			bool dirtyBefore = camera.gameObject.scene.isDirty;

			try
			{
				ToolResult result =
					Inspection.CaptureEvaluation(
						"save-evaluation-capture",
						ObjectId(camera),
						Session.Revision,
						64,
						64,
						"save-evaluation-test");

				Assert.That(camera.targetTexture, Is.SameAs(originalTarget));
				Assert.That(RenderTexture.active, Is.SameAs(originalActive));
				Assert.That(
					camera.gameObject.scene.isDirty,
					Is.EqualTo(dirtyBefore));

				if (result.status == E_MCP_TOOL_STATUS.UNVERIFIED.ToString())
				{
					Assert.That(
						SystemInfo.graphicsDeviceType,
						Is.EqualTo(GraphicsDeviceType.Null));
					return;
				}

				Assert.That(result.IsSuccessful, Is.True, result.summary);
				Dictionary<string, object> data = ResultData(result);
				string outputPath = data["outputPath"] as string;
				string absolutePath = ProjectAbsolutePath(outputPath);
				_capturePaths.Add(absolutePath);

				Assert.That(File.Exists(absolutePath), Is.True);
				Assert.That(new FileInfo(absolutePath).Length, Is.GreaterThan(0));
				Assert.That(data["temporaryStateRestored"], Is.EqualTo(true));
				Assert.That(data["visualAccepted"], Is.EqualTo(false));
			}
			finally
			{
				RenderTexture.active = null;
				originalActive.Release();
				UnityEngine.Object.DestroyImmediate(originalActive);
			}
		}

		[Test]
		public void RefineDirection_CreatesNewPlanFromExplicitHumanReview()
		{
			Dictionary<string, object> direction = CompileDirection();
			string captureId = StoreFakeCapture();

			ToolResult result =
				Inspection.RefineDirection(
					"save-evaluation-refine",
					direction["planId"] as string,
					captureId,
					Convert.ToInt64(direction["expectedRevision"]),
					new[] { "背景がHeroより明るく、視線誘導が弱い。" },
					new[] { "背景露出を下げ、Rim Lightを明確にする。" });

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["planId"] as string, Is.Not.Empty);
			Assert.That(
				data["planId"] as string,
				Is.Not.EqualTo(direction["planId"] as string));
			Assert.That(data["imageAnalysisPerformedByUnity"], Is.EqualTo(false));
			Assert.That(data["visualAccepted"], Is.EqualTo(false));
			Assert.That(data["mutationApplied"], Is.EqualTo(false));
		}

		[Test]
		public void RefineDirection_RejectsMissingHumanReview()
		{
			Dictionary<string, object> direction = CompileDirection();
			string captureId = StoreFakeCapture();

			ToolResult result =
				Inspection.RefineDirection(
					"save-evaluation-refine-empty",
					direction["planId"] as string,
					captureId,
					Convert.ToInt64(direction["expectedRevision"]),
					null,
					null);

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		private static Scene CreateSavedDirtyScene()
		{
			new GameObject("SaveEvaluation Save Target");
			SaveScene();
			new GameObject("SaveEvaluation Unsaved Change");
			Scene scene = SceneManager.GetActiveScene();
			EditorSceneManager.MarkSceneDirty(scene);
			return scene;
		}

		private static Dictionary<string, object> PrepareSave()
		{
			CreateSavedDirtyScene();
			return PrepareSaveForCurrentScene();
		}

		private static Dictionary<string, object> PrepareSaveForCurrentScene()
		{
			ToolResult result =
				Inspection.PrepareSavePlan(
					"save-evaluation-save-prepare",
					Session.Revision,
					new[]
					{
						new SaveTargetInput
						{
							scenePath = TEMP_SCENE_PATH
						}
					});

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			return ResultData(result);
		}

		private static void SaveScene()
		{
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(
				EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false),
				Is.True);
			Assert.That(scene.isDirty, Is.False);
		}

		private static Dictionary<string, object> CompileDirection()
		{
			ToolResult result =
				Inspection.CompileDirection(
					"save-evaluation-direction",
					"CaptureをHuman Reviewし、画作りを再調整する。",
					new[] { "Heroが画面中央に配置されている。" },
					new[] { "ドラマチック" },
					new[] { "Hero / Background" },
					new[] { "Eye level" },
					new[] { "Key / Rim" },
					new[] { "暖色Keyと寒色背景" },
					new[] { "HeroのSpecularを維持" },
					new[] { "背景をFogで分離" },
					new[] { "静的" },
					new[] { "Editor Evaluation" },
					null,
					new[] { "Human Review required" },
					Session.Revision);

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			return ResultData(result);
		}

		private static string StoreFakeCapture()
		{
			CaptureRecord capture =
				new CaptureRecord
				{
					Revision = Session.Revision,
					CameraObjectId = "test-camera",
					OutputPath = "Library/MyUnityMCP/Captures/test.png",
					Sha256 = new string('a', 64),
					Width = 64,
					Height = 64
				};

			return SaveEvaluationSession.StoreCapture(capture);
		}

		private static string ObjectId(UnityEngine.Object target)
		{
			return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
		}

		private static Dictionary<string, object> ResultData(
			ToolResult result)
		{
			Dictionary<string, object> data =
				result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null);
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

		private static string ProjectAbsolutePath(string relativePath)
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			return Path.GetFullPath(
				Path.Combine(
					projectRoot,
					(relativePath ?? string.Empty).Replace(
						'/',
						Path.DirectorySeparatorChar)));
		}
	}
}

#endif
