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
		private const string TEMP_SCENE_PATH = "Assets/MyUnityMcpEnvironmentTemporaryScene.unity";
		private const string TEMP_PROFILE_A_PATH = "Assets/MyUnityMcpEnvironmentProfileA.asset";
		private const string TEMP_PROFILE_B_PATH = "Assets/MyUnityMcpEnvironmentProfileB.asset";

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
			AssetDatabase.DeleteAsset(TEMP_PROFILE_A_PATH);
			AssetDatabase.DeleteAsset(TEMP_PROFILE_B_PATH);
		}

		[Test]
		public void Bridge_DiscoversEnvironmentTools_AndKeepsThemDisabledByDefault()
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
			Dictionary<string, object> direction = CompileDirection("environment-readonly-direction");
			Scene scene = SceneManager.GetActiveScene();
			bool dirtyBefore = scene.isDirty;

			UnityGraphicsMcpToolResult result = Prepare(
				"environment-readonly",
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
			Dictionary<string, object> executable = PrepareSingle("environment-missing-approval", CreateCameraOperation("camera-create"));
			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.ApplyEnvironmentPlan(
				"environment-missing-approval-result",
				executable["planId"] as string,
				Convert.ToInt64(executable["expectedRevision"]),
				null,
				"NONE");

			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(FindAll<Camera>().Length, Is.EqualTo(0));
		}

		[Test]
		public void ApplyEnvironmentPlan_RejectsAutomaticSave()
		{
			Dictionary<string, object> executable = PrepareSingle("environment-save-mode", CreateCameraOperation("camera-create"));
			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.ApplyEnvironmentPlan(
				"environment-save-mode-result",
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
			Dictionary<string, object> executable = PrepareSingle("environment-camera-create", CreateCameraOperation("camera-create"));
			UnityGraphicsMcpToolResult applyResult = Apply("environment-camera-apply", executable);

			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Camera[] cameras = FindAll<Camera>();
			Assert.That(cameras.Length, Is.EqualTo(1));
			Assert.That(cameras[0].fieldOfView, Is.EqualTo(52.0f));
			Assert.That(cameras[0].allowHDR, Is.True);
			Assert.That(SceneManager.GetActiveScene().path, Is.Empty);

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-camera-undo", applyResult);
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(FindAll<Camera>().Length, Is.EqualTo(0));
		}

		[Test]
		public void ApplyAndUndo_UpdatesCamera()
		{
			GameObject gameObject = new GameObject("Existing Camera");
			Camera camera = gameObject.AddComponent<Camera>();
			camera.fieldOfView = 60.0f;
			camera.enabled = true;
			SaveScene();

			Dictionary<string, object> direction = CompileDirection("environment-camera-update-direction");
			Dictionary<string, object> executable = ResultData(Prepare(
				"environment-camera-update-prepare",
				direction,
				new[]
				{
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "camera-update",
						operation = "CAMERA_UPDATE",
						targetObjectId = ObjectId(camera),
						fieldOfView = 38.0f,
						position = Vector(1.0f, 2.0f, 3.0f),
						enabled = false
					}
				}));

			UnityGraphicsMcpToolResult applyResult = Apply("environment-camera-update-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Assert.That(camera.fieldOfView, Is.EqualTo(38.0f));
			Assert.That(camera.transform.position, Is.EqualTo(new Vector3(1.0f, 2.0f, 3.0f)));
			Assert.That(camera.enabled, Is.False);

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-camera-update-undo", applyResult);
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(camera.fieldOfView, Is.EqualTo(60.0f));
			Assert.That(camera.transform.position, Is.EqualTo(Vector3.zero));
			Assert.That(camera.enabled, Is.True);
		}

		[Test]
		public void ApplyAndUndo_CreatesReflectionProbe()
		{
			Dictionary<string, object> executable = PrepareSingle(
				"environment-probe-create",
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

			UnityGraphicsMcpToolResult applyResult = Apply("environment-probe-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			ReflectionProbe[] probes = FindAll<ReflectionProbe>();
			Assert.That(probes.Length, Is.EqualTo(1));
			Assert.That(probes[0].importance, Is.EqualTo(5));
			Assert.That(probes[0].boxProjection, Is.True);

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-probe-undo", applyResult);
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(FindAll<ReflectionProbe>().Length, Is.EqualTo(0));
		}

		[Test]
		public void ApplyAndUndo_UpdatesReflectionProbe()
		{
			GameObject gameObject = new GameObject("Existing Probe");
			ReflectionProbe probe = gameObject.AddComponent<ReflectionProbe>();
			probe.importance = 1;
			probe.intensity = 0.5f;
			probe.boxProjection = false;
			probe.size = new Vector3(4.0f, 4.0f, 4.0f);
			SaveScene();

			Dictionary<string, object> direction = CompileDirection("environment-probe-update-direction");
			Dictionary<string, object> executable = ResultData(Prepare(
				"environment-probe-update-prepare",
				direction,
				new[]
				{
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "probe-update",
						operation = "REFLECTION_PROBE_UPDATE",
						targetObjectId = ObjectId(probe),
						importance = 8,
						intensity = 1.75f,
						boxProjection = true,
						size = Vector(10.0f, 6.0f, 8.0f),
						center = Vector(0.0f, 2.0f, 0.0f)
					}
				}));

			UnityGraphicsMcpToolResult applyResult = Apply("environment-probe-update-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Assert.That(probe.importance, Is.EqualTo(8));
			Assert.That(probe.intensity, Is.EqualTo(1.75f));
			Assert.That(probe.boxProjection, Is.True);
			Assert.That(probe.size, Is.EqualTo(new Vector3(10.0f, 6.0f, 8.0f)));

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-probe-update-undo", applyResult);
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(probe.importance, Is.EqualTo(1));
			Assert.That(probe.intensity, Is.EqualTo(0.5f));
			Assert.That(probe.boxProjection, Is.False);
			Assert.That(probe.size, Is.EqualTo(new Vector3(4.0f, 4.0f, 4.0f)));
		}

		[Test]
		public void ApplyAndUndo_CreatesVolume_WhenCorePackageIsAvailable()
		{
			Type volumeType = RequireVolumeType();
			Dictionary<string, object> executable = PrepareSingle(
				"environment-volume-create",
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

			UnityGraphicsMcpToolResult applyResult = Apply("environment-volume-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Component volume = FindComponent(volumeType);
			Assert.That(volume, Is.Not.Null);
			Assert.That((bool)GetMemberValue(volume, "isGlobal"), Is.True);
			Assert.That(Convert.ToSingle(GetMemberValue(volume, "priority")), Is.EqualTo(10.0f));
			Assert.That(Convert.ToSingle(GetMemberValue(volume, "weight")), Is.EqualTo(0.75f));

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-volume-undo", applyResult);
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(FindComponent(volumeType), Is.Null);
		}

		[Test]
		public void ApplyAndUndo_UpdatesVolumeAndSharedProfile()
		{
			Type volumeType = RequireVolumeType();
			Type profileType = RequireVolumeProfileType();
			ScriptableObject profileA = CreateProfile(profileType, TEMP_PROFILE_A_PATH);
			ScriptableObject profileB = CreateProfile(profileType, TEMP_PROFILE_B_PATH);
			GameObject gameObject = new GameObject("Existing Volume");
			Component volume = gameObject.AddComponent(volumeType);
			SetMemberValue(volume, "isGlobal", false);
			SetMemberValue(volume, "priority", 1.0f);
			SetMemberValue(volume, "blendDistance", 2.0f);
			SetMemberValue(volume, "weight", 0.25f);
			SetMemberValue(volume, "sharedProfile", profileA);
			((Behaviour)volume).enabled = true;
			SaveScene();

			Dictionary<string, object> direction = CompileDirection("environment-volume-update-direction");
			Dictionary<string, object> executable = ResultData(Prepare(
				"environment-volume-update-prepare",
				direction,
				new[]
				{
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "volume-update",
						operation = "VOLUME_UPDATE",
						targetObjectId = ObjectId(volume),
						isGlobal = true,
						priority = 12.0f,
						blendDistance = 0.5f,
						weight = 0.9f,
						sharedProfileAssetPath = TEMP_PROFILE_B_PATH,
						enabled = false
					}
				}));

			UnityGraphicsMcpToolResult applyResult = Apply("environment-volume-update-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Assert.That((bool)GetMemberValue(volume, "isGlobal"), Is.True);
			Assert.That(Convert.ToSingle(GetMemberValue(volume, "priority")), Is.EqualTo(12.0f));
			Assert.That(Convert.ToSingle(GetMemberValue(volume, "blendDistance")), Is.EqualTo(0.5f));
			Assert.That(Convert.ToSingle(GetMemberValue(volume, "weight")), Is.EqualTo(0.9f));
			Assert.That(GetMemberValue(volume, "sharedProfile"), Is.SameAs(profileB));
			Assert.That(((Behaviour)volume).enabled, Is.False);

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-volume-update-undo", applyResult);
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That((bool)GetMemberValue(volume, "isGlobal"), Is.False);
			Assert.That(Convert.ToSingle(GetMemberValue(volume, "priority")), Is.EqualTo(1.0f));
			Assert.That(Convert.ToSingle(GetMemberValue(volume, "blendDistance")), Is.EqualTo(2.0f));
			Assert.That(Convert.ToSingle(GetMemberValue(volume, "weight")), Is.EqualTo(0.25f));
			Assert.That(GetMemberValue(volume, "sharedProfile"), Is.SameAs(profileA));
			Assert.That(((Behaviour)volume).enabled, Is.True);
		}

		[Test]
		public void ApplyEnvironmentPlan_RejectsChangedBaseline()
		{
			GameObject gameObject = new GameObject("Baseline Camera");
			Camera camera = gameObject.AddComponent<Camera>();
			SaveScene();
			Dictionary<string, object> direction = CompileDirection("environment-baseline-direction");
			Dictionary<string, object> executable = ResultData(Prepare(
				"environment-baseline-prepare",
				direction,
				new[]
				{
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "camera-update",
						operation = "CAMERA_UPDATE",
						targetObjectId = ObjectId(camera),
						fieldOfView = 40.0f
					}
				}));
			camera.fieldOfView = 25.0f;

			UnityGraphicsMcpToolResult result = Apply("environment-baseline-apply", executable);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
			Assert.That(camera.fieldOfView, Is.EqualTo(25.0f));
		}

		[Test]
		public void ApplyEnvironmentPlan_IsAtomicAcrossCameraProbeAndVolume()
		{
			Type volumeType = RequireVolumeType();
			Dictionary<string, object> direction = CompileDirection("environment-atomic-direction");
			Dictionary<string, object> executable = ResultData(Prepare(
				"environment-atomic-prepare",
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

			UnityGraphicsMcpToolResult applyResult = Apply("environment-atomic-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Assert.That(FindAll<Camera>().Length, Is.EqualTo(1));
			Assert.That(FindAll<ReflectionProbe>().Length, Is.EqualTo(1));
			Assert.That(FindComponent(volumeType), Is.Not.Null);

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-atomic-undo", applyResult);
			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(FindAll<Camera>().Length, Is.EqualTo(0));
			Assert.That(FindAll<ReflectionProbe>().Length, Is.EqualTo(0));
			Assert.That(FindComponent(volumeType), Is.Null);
		}

		[Test]
		public void PrepareEnvironmentPlan_RejectsDuplicateOperationId()
		{
			Dictionary<string, object> direction = CompileDirection("environment-duplicate-id-direction");
			UnityGraphicsMcpToolResult result = Prepare(
				"environment-duplicate-id-prepare",
				direction,
				new[]
				{
					CreateCameraOperation("duplicate"),
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "duplicate",
						operation = "REFLECTION_PROBE_CREATE"
					}
				});

			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(FindAll<Camera>().Length, Is.EqualTo(0));
		}

		[Test]
		public void PrepareEnvironmentPlan_RejectsMultipleUpdatesToSameComponent()
		{
			GameObject gameObject = new GameObject("Duplicate Target Camera");
			Camera camera = gameObject.AddComponent<Camera>();
			SaveScene();
			Dictionary<string, object> direction = CompileDirection("environment-duplicate-target-direction");
			string objectId = ObjectId(camera);

			UnityGraphicsMcpToolResult result = Prepare(
				"environment-duplicate-target-prepare",
				direction,
				new[]
				{
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "camera-fov",
						operation = "CAMERA_UPDATE",
						targetObjectId = objectId,
						fieldOfView = 40.0f
					},
					new UnityGraphicsMcpEnvironmentOperationInput
					{
						operationId = "camera-position",
						operation = "CAMERA_UPDATE",
						targetObjectId = objectId,
						position = Vector(1.0f, 2.0f, 3.0f)
					}
				});

			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(camera.fieldOfView, Is.EqualTo(60.0f));
		}

		[Test]
		public void UndoLastEnvironmentTransaction_RejectsExternalTargetChange()
		{
			Dictionary<string, object> executable = PrepareSingle("environment-external-change", CreateCameraOperation("camera-create"));
			UnityGraphicsMcpToolResult applyResult = Apply("environment-external-change-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			Camera camera = FindAll<Camera>()[0];
			camera.fieldOfView = 23.0f;

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-external-change-undo", applyResult);
			Assert.That(undoResult.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(camera, Is.Not.Null);
			Assert.That(camera.fieldOfView, Is.EqualTo(23.0f));
		}

		[Test]
		public void UndoLastEnvironmentTransaction_RejectsNewerUndoGroup()
		{
			Dictionary<string, object> executable = PrepareSingle("environment-newer-undo", CreateCameraOperation("camera-create"));
			UnityGraphicsMcpToolResult applyResult = Apply("environment-newer-undo-apply", executable);
			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);

			UnityGraphicsMcpTestAsset marker = ScriptableObject.CreateInstance<UnityGraphicsMcpTestAsset>();
			Undo.IncrementCurrentGroup();
			Undo.RecordObject(marker, "Unrelated Newer Undo Group");
			marker.value = 10;

			UnityGraphicsMcpToolResult undoResult = UndoEnvironment("environment-newer-undo-result", applyResult);
			Assert.That(undoResult.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(FindAll<Camera>().Length, Is.EqualTo(1));
			UnityEngine.Object.DestroyImmediate(marker);
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

		private static UnityGraphicsMcpToolResult UndoEnvironment(string requestId, UnityGraphicsMcpToolResult applyResult)
		{
			Dictionary<string, object> applyData = ResultData(applyResult);
			return UnityGraphicsMcpInspection.UndoLastEnvironmentTransaction(
				requestId,
				applyData["transactionId"] as string,
				Convert.ToInt64(applyData["revision"]));
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

		private static string ObjectId(UnityEngine.Object target)
		{
			return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
		}

		private static T[] FindAll<T>() where T : UnityEngine.Object
		{
			return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

		private static Type RequireVolumeType()
		{
			Type type = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime", false);
			Assert.That(type, Is.Not.Null, "Environment Mutation Verification ProjectにはRender Pipelines Core Packageが必要です。");
			return type;
		}

		private static Type RequireVolumeProfileType()
		{
			Type type = Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime", false);
			Assert.That(type, Is.Not.Null, "VolumeProfile APIを解決できません。");
			return type;
		}

		private static ScriptableObject CreateProfile(Type profileType, string assetPath)
		{
			ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
			AssetDatabase.CreateAsset(profile, assetPath);
			AssetDatabase.SaveAssets();
			return profile;
		}

		private static object GetMemberValue(object target, string memberName)
		{
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
			PropertyInfo property = target.GetType().GetProperty(memberName, flags);
			if (property != null)
			{
				return property.GetValue(target, null);
			}

			FieldInfo field = target.GetType().GetField(memberName, flags);
			Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + memberName + "を公開PropertyまたはFieldとして解決できません。");
			return field.GetValue(target);
		}

		private static void SetMemberValue(object target, string memberName, object value)
		{
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
			PropertyInfo property = target.GetType().GetProperty(memberName, flags);
			if (property != null && property.CanWrite)
			{
				property.SetValue(target, value, null);
				return;
			}

			FieldInfo field = target.GetType().GetField(memberName, flags);
			Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + memberName + "を書き込めません。");
			field.SetValue(target, value);
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
