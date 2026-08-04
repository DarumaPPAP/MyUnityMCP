#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using MCPForUnity.Editor.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpEnvironmentMutationTests
	{
		private const string TEMP_SCENE_PATH = "Assets/MyUnityMcpPhase3BTemporaryScene.unity";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
			UnityGraphicsMcpEnvironmentMutationSession.ClearForTests();
			Undo.ClearAll();
		}

		[TearDown]
		public void TearDown()
		{
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
			UnityGraphicsMcpEnvironmentMutationSession.ClearForTests();
			Undo.ClearAll();
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[Test]
		public void Bridge_DiscoversPhase3BTools_AndKeepsThemDisabledByDefault()
		{
			CommandRegistry.Initialize();
			Assert.That(CommandRegistry.GetHandler("graphics.prepare_environment_plan"), Is.Not.Null);
			Assert.That(CommandRegistry.GetHandler("graphics.apply_environment_plan"), Is.Not.Null);
			Assert.That(CommandRegistry.GetHandler("graphics.undo_last_environment_transaction"), Is.Not.Null);
			Assert.That(GetToolAttribute(typeof(GraphicsPrepareEnvironmentPlanTool)).AutoRegister, Is.False);
			Assert.That(GetToolAttribute(typeof(GraphicsApplyEnvironmentPlanTool)).AutoRegister, Is.False);
			Assert.That(GetToolAttribute(typeof(GraphicsUndoLastEnvironmentTransactionTool)).AutoRegister, Is.False);
		}

		[Test]
		public void PrepareEnvironmentPlan_IsReadOnly()
		{
			Dictionary<string, object> direction = CompileDirection("phase3b-readonly-direction");
			Scene scene = SceneManager.GetActiveScene();
			bool dirtyBefore = scene.isDirty;

			UnityGraphicsMcpToolResult result = Prepare(
				"phase3b-readonly",
				direction,
				new[] { CreateCameraOperation("camera-create") });

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["approvalToken"] as string, Is.Not.Empty);
			Assert.That(data["diffDigest"] as string, Is.Not.Empty);
			Assert.That(data["mutationApplied"], Is.EqualTo(false));
		}

		[Test]
		public void ApplyEnvironmentPlan_RejectsMissingApproval()
		{
			Dictionary<string, object> executable = PrepareSingle(
				"phase3b-missing-approval",
				CreateCameraOperation("camera-create"));

			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.ApplyEnvironmentPlan(
				"phase3b-missing-approval-result",
				executable["planId"] as string,
				Convert.ToInt64(executable["expectedRevision"]),
				null,
				"NONE");

			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(0));
		}

		[Test]
		public void ApplyEnvironmentPlan_RejectsAutomaticSave()
		{
			Dictionary<string, object> executable = PrepareSingle(
				"phase3b-save-mode",
				CreateCameraOperation("camera-create"));

			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.ApplyEnvironmentPlan(
				"phase3b-save-mode-result",
				executable["planId"] as string,
				Convert.ToInt64(executable["expectedRevision"]),
				executable["approvalToken"] as string,
				"SAVE_SCENE");

			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.UNSUPPORTED.ToString()));
			Assert.That(SceneManager.GetActiveScene().path, Is.Empty);
		}

		[Test]
		public void ApplyAndUndo_CreatesCamera()
		{
			Dictionary<string, object> executable = PrepareSingle(
				"phase3b-camera-create",
				CreateCameraOperation("camera-create"));
			UnityGraphicsMcpToolResult applyResult = Apply("phase3b-camera-apply", executable);

			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			Assert.That(cameras.Length, Is.EqualTo(1));
			Assert.That(cameras[0].fieldOfView, Is.EqualTo(52.0f));
			Assert.That(cameras[0].allowHDR, Is.True);
			Assert.That(SceneManager.GetActiveScene().path, Is.Empty);

			Dictionary<string, object> applyData = ResultData(applyResult);
			UnityGraphicsMcpToolResult undoResult = UnityGraphicsMcpInspection.UndoLastEnvironmentTransaction(
				"phase3b-camera-undo",
				applyData["transactionId"] as string,
				Convert.ToInt64(applyData["revision"]));

			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(0));
		}

		[Test]
		public void ApplyAndUndo_UpdatesCamera()
		{
			GameObject gameObject = new GameObject("Existing Camera");
			Camera camera = gameObject.AddComponent<Camera>();
			camera.fieldOfView = 60.0f;
			SaveScene();
			string objectId = GlobalObjectId.GetGlobalObjectIdSlow(camera).ToString();
			Dictionary<string, object> direction = CompileDirection("phase3b-camera-update-direction");
			UnityGraphicsMcpEnvironmentOperationInput operation = new UnityGraphicsMcpEnvironmentOperationInput
			{
				operationId = "camera-update",
				operation = "CAMERA_UPDATE",
				targetObjectId = objectId,
				fieldOfView = 38.0f,
				position = Vector(1.0f, 2.0f, 3.0f),
				enabled = false
			};
			Dictionary<string, object> executable = ResultData(Prepare("phase3b-camera-update-prepare", direction, new[] { operation }));
			UnityGraphicsMcpToolResult applyResult = Apply("phase3b-camera-update-apply", executable);

			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Assert.That(camera.fieldOfView, Is.EqualTo(38.0f));
			Assert.That(camera.enabled, Is.False);
			Dictionary<string, object> applyData = ResultData(applyResult);
			UnityGraphicsMcpToolResult undoResult = UnityGraphicsMcpInspection.UndoLastEnvironmentTransaction(
				"phase3b-camera-update-undo",
				applyData["transactionId"] as string,
				Convert.ToInt64(applyData["revision"]));

			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(camera.fieldOfView, Is.EqualTo(60.0f));
			Assert.That(camera.enabled, Is.True);
		}

		[Test]
		public void ApplyAndUndo_CreatesReflectionProbe()
		{
			Dictionary<string, object> executable = PrepareSingle(
				"phase3b-probe-create",
				new UnityGraphicsMcpEnvironmentOperationInput
				{
					operationId = "probe-create",
					operation = "REFLECTION_PROBE_CREATE",
					name = "Hero Reflection Probe",
					probeMode = "Realtime",
					refreshMode = "ViaScripting",
					importance = 5,
					intensity = 1.25f,
					boxProjection = true,
					size = Vector(12.0f, 6.0f, 12.0f),
					blendDistance = 1.5f,
					resolution = 128,
					enabled = true
				});
			UnityGraphicsMcpToolResult applyResult = Apply("phase3b-probe-apply", executable);

			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			ReflectionProbe[] probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			Assert.That(probes.Length, Is.EqualTo(1));
			Assert.That(probes[0].importance, Is.EqualTo(5));
			Assert.That(probes[0].boxProjection, Is.True);

			Dictionary<string, object> applyData = ResultData(applyResult);
			UnityGraphicsMcpToolResult undoResult = UnityGraphicsMcpInspection.UndoLastEnvironmentTransaction(
				"phase3b-probe-undo",
				applyData["transactionId"] as string,
				Convert.ToInt64(applyData["revision"]));
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(0));
		}

		[Test]
		public void ApplyAndUndo_CreatesVolume_WhenCorePackageIsAvailable()
		{
			Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime", false);
			Assert.That(volumeType, Is.Not.Null, "Phase 3B Verification ProjectにはRender Pipelines Core Packageが必要です。");
			Dictionary<string, object> executable = PrepareSingle(
				"phase3b-volume-create",
				new UnityGraphicsMcpEnvironmentOperationInput
				{
					operationId = "volume-create",
					operation = "VOLUME_CREATE",
					name = "Global Look Volume",
					isGlobal = true,
					priority = 10.0f,
					weight = 0.75f,
					blendDistance = 0.0f,
					enabled = true
				});
			UnityGraphicsMcpToolResult applyResult = Apply("phase3b-volume-apply", executable);

			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Component volume = FindComponent(volumeType);
			Assert.That(volume, Is.Not.Null);
			Assert.That((bool)GetProperty(volume, "isGlobal"), Is.True);
			Assert.That(Convert.ToSingle(GetProperty(volume, "priority")), Is.EqualTo(10.0f));
			Assert.That(Convert.ToSingle(GetProperty(volume, "weight")), Is.EqualTo(0.75f));

			Dictionary<string, object> applyData = ResultData(applyResult);
			UnityGraphicsMcpToolResult undoResult = UnityGraphicsMcpInspection.UndoLastEnvironmentTransaction(
				"phase3b-volume-undo",
				applyData["transactionId"] as string,
				Convert.ToInt64(applyData["revision"]));
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(FindComponent(volumeType), Is.Null);
		}

		[Test]
		public void ApplyEnvironmentPlan_RejectsChangedBaseline()
		{
			GameObject gameObject = new GameObject("Baseline Camera");
			Camera camera = gameObject.AddComponent<Camera>();
			SaveScene();
			Dictionary<string, object> direction = CompileDirection("phase3b-baseline-direction");
			Dictionary<string, object> executable = ResultData(Prepare(
				"phase3b-baseline-prepare",
				direction,
				new[]
				{
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "camera-update",
						operation = "CAMERA_UPDATE",
						targetObjectId = GlobalObjectId.GetGlobalObjectIdSlow(camera).ToString(),
						fieldOfView = 40.0f
					}
				}));
			camera.fieldOfView = 25.0f;

			UnityGraphicsMcpToolResult result = Apply("phase3b-baseline-apply", executable);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
			Assert.That(camera.fieldOfView, Is.EqualTo(25.0f));
		}

		[Test]
		public void ApplyEnvironmentPlan_IsAtomicAcrossCameraProbeAndVolume()
		{
			Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime", false);
			Assert.That(volumeType, Is.Not.Null);
			Dictionary<string, object> direction = CompileDirection("phase3b-atomic-direction");
			Dictionary<string, object> executable = ResultData(Prepare(
				"phase3b-atomic-prepare",
				direction,
				new[]
				{
					CreateCameraOperation("camera-create"),
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "probe-create",
						operation = "REFLECTION_PROBE_CREATE",
						name = "Atomic Probe",
						probeMode = "Baked"
					},
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "volume-create",
						operation = "VOLUME_CREATE",
						name = "Atomic Volume",
						isGlobal = true,
						weight = 1.0f
					}
				}));
			UnityGraphicsMcpToolResult applyResult = Apply("phase3b-atomic-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
			Assert.That(UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
			Assert.That(FindComponent(volumeType), Is.Not.Null);

			Dictionary<string, object> applyData = ResultData(applyResult);
			UnityGraphicsMcpToolResult undoResult = UnityGraphicsMcpInspection.UndoLastEnvironmentTransaction(
				"phase3b-atomic-undo",
				applyData["transactionId"] as string,
				Convert.ToInt64(applyData["revision"]));
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(0));
			Assert.That(UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(0));
			Assert.That(FindComponent(volumeType), Is.Null);
		}

		private static Dictionary<string, object> PrepareSingle(string requestId, UnityGraphicsMcpEnvironmentOperationInput operation)
		{
			Dictionary<string, object> direction = CompileDirection(requestId + "-direction");
			UnityGraphicsMcpToolResult result = Prepare(requestId + "-prepare", direction, new[] { operation });
			Assert.That(result.IsSuccessful, Is.True, result.summary);
			return ResultData(result);
		}

		private static UnityGraphicsMcpToolResult Prepare(
			string requestId,
			Dictionary<string, object> direction,
			UnityGraphicsMcpEnvironmentOperationInput[] operations)
		{
			return UnityGraphicsMcpInspection.PrepareEnvironmentPlan(
				requestId,
				direction["planId"] as string,
				Convert.ToInt64(direction["expectedRevision"]),
				operations);
		}

		private static UnityGraphicsMcpToolResult Apply(string requestId, Dictionary<string, object> executable)
		{
			return UnityGraphicsMcpInspection.ApplyEnvironmentPlan(
				requestId,
				executable["planId"] as string,
				Convert.ToInt64(executable["expectedRevision"]),
				executable["approvalToken"] as string,
				"NONE");
		}

		private static Dictionary<string, object> CompileDirection(string requestId)
		{
			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.CompileDirection(
				requestId,
				"Camera、Reflection Probe、Volumeを明示差分で安全に構成する",
				null,
				null,
				new[] { "Hero", "Support", "Background" },
				new[] { "Perspective", "Stable framing" },
				new[] { "Motivated lighting" },
				new[] { "Neutral grade" },
				new[] { "Controlled reflections" },
				new[] { "Atmospheric depth" },
				null,
				new[] { "Preserve frame time" },
				new[] { "PC" },
				new[] { "Automatic Save禁止", "Automatic Bake禁止" },
				null);
			Assert.That(result.IsSuccessful, Is.True, result.summary);
			return ResultData(result);
		}

		private static UnityGraphicsMcpEnvironmentOperationInput CreateCameraOperation(string operationId)
		{
			return new UnityGraphicsMcpEnvironmentOperationInput
			{
				operationId = operationId,
				operation = "CAMERA_CREATE",
				name = "Hero Camera",
				projection = "PERSPECTIVE",
				fieldOfView = 52.0f,
				nearClipPlane = 0.1f,
				farClipPlane = 800.0f,
				clearFlags = "SolidColor",
				backgroundColor = new UnityGraphicsMcpColorInput { r = 0.05f, g = 0.06f, b = 0.08f, a = 1.0f },
				position = Vector(0.0f, 1.6f, -6.0f),
				eulerAngles = Vector(8.0f, 0.0f, 0.0f),
				allowHdr = true,
				allowMsaa = true,
				enabled = true
			};
		}

		private static UnityGraphicsMcpVector3Input Vector(float x, float y, float z)
		{
			return new UnityGraphicsMcpVector3Input { x = x, y = y, z = z };
		}

		private static void SaveScene()
		{
			Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TEMP_SCENE_PATH), Is.True);
		}

		private static Component FindComponent(Type type)
		{
			UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
			foreach (UnityEngine.Object item in objects)
			{
				Component component = item as Component;
				if (component != null && component.gameObject.scene.IsValid()) return component;
			}
			return null;
		}

		private static object GetProperty(object target, string propertyName)
		{
			PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
			Assert.That(property, Is.Not.Null);
			return property.GetValue(target, null);
		}

		private static Dictionary<string, object> ResultData(UnityGraphicsMcpToolResult result)
		{
			Dictionary<string, object> data = result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null, result.summary);
			return data;
		}

		private static McpForUnityToolAttribute GetToolAttribute(Type type)
		{
			McpForUnityToolAttribute attribute = Attribute.GetCustomAttribute(type, typeof(McpForUnityToolAttribute)) as McpForUnityToolAttribute;
			Assert.That(attribute, Is.Not.Null);
			return attribute;
		}
	}
}

#endif
