#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class MutationTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpLightMutationTemporaryScene.unity";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			Session.ClearSnapshots();
			Session.ClearPlans();
			Undo.ClearAll();
		}

		[TearDown]
		public void TearDown()
		{
			Session.ClearSnapshots();
			Session.ClearPlans();
			Undo.ClearAll();
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[Test]
		public void Bridge_DiscoversLightMutationTools_AndKeepsThemDisabledByDefault()
		{
			CommandRegistry.Initialize();

			Assert.That(
				CommandRegistry.GetHandler("graphics.prepare_light_plan"),
				Is.Not.Null);
			Assert.That(
				CommandRegistry.GetHandler("graphics.apply_plan"),
				Is.Not.Null);
			Assert.That(
				CommandRegistry.GetHandler("graphics.undo_last_transaction"),
				Is.Not.Null);

			McpForUnityToolAttribute prepareAttribute = GetToolAttribute(
				typeof(PrepareLightPlanTool));
			McpForUnityToolAttribute applyAttribute = GetToolAttribute(
				typeof(ApplyPlanTool));
			McpForUnityToolAttribute undoAttribute = GetToolAttribute(
				typeof(UndoLastTransactionTool));

			Assert.That(prepareAttribute.AutoRegister, Is.False);
			Assert.That(applyAttribute.AutoRegister, Is.False);
			Assert.That(undoAttribute.AutoRegister, Is.False);
		}

		[Test]
		public void Bridge_CanInvokePrepareLightPlanHandler()
		{
			Dictionary<string, object> directionData = CompileDirection(
				"test-light-mutation-bridge-direction");
			CommandRegistry.Initialize();
			Func<JObject, object> handler =
				CommandRegistry.GetHandler("graphics.prepare_light_plan");

			object response = handler(new JObject
			{
				["requestId"] = "test-light-mutation-bridge",
				["directionPlanId"] = directionData["planId"] as string,
				["expectedRevision"] = Convert.ToInt64(directionData["expectedRevision"]),
				["lightOperations"] = new JArray
				{
					new JObject
					{
						["operationId"] = "create-key",
						["operation"] = "LIGHT_CREATE",
						["name"] = "Key Light",
						["lightType"] = "Directional",
						["color"] = new JObject
						{
							["r"] = 1.0f,
							["g"] = 0.9f,
							["b"] = 0.8f,
							["a"] = 1.0f
						},
						["intensity"] = 2.0f,
						["shadows"] = "Soft",
						["position"] = VectorJson(0.0f, 3.0f, 0.0f),
						["eulerAngles"] = VectorJson(45.0f, -30.0f, 0.0f),
						["enabled"] = true
					}
				}
			});

			Assert.That(response, Is.TypeOf<SuccessResponse>());
		}

		[Test]
		public void PrepareLightPlan_IsReadOnlyAndReturnsExactDiffAndApprovalToken()
		{
			Dictionary<string, object> directionData = CompileDirection(
				"test-prepare-read-only-direction");
			Scene scene = SceneManager.GetActiveScene();
			bool dirtyBefore = scene.isDirty;

			ToolResult result = Prepare(
				"test-prepare-read-only",
				directionData,
				new[] { CreateDirectionalOperation("create-key", "Key Light", 2.0f) });

			Assert.That(result.IsSuccessful, Is.True);
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));

			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["planId"] as string, Is.Not.Empty);
			Assert.That(data["approvalToken"] as string, Is.Not.Empty);
			Assert.That(data["diffDigest"] as string, Is.Not.Empty);
			Assert.That(data["mutationApplied"], Is.EqualTo(false));
			Assert.That(data["savePerformed"], Is.EqualTo(false));
			Assert.That(data["bakePerformed"], Is.EqualTo(false));
		}

		[Test]
		public void ApplyPlan_RejectsMissingApprovalTokenWithoutMutation()
		{
			Dictionary<string, object> executableData = PrepareCreatePlan(
				"test-missing-approval");

			ToolResult result =
				Inspection.ApplyPlan(
					"test-missing-approval-result",
					executableData["planId"] as string,
					Convert.ToInt64(executableData["expectedRevision"]),
					null,
					"NONE");

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(
				UnityEngine.Object.FindObjectsByType<Light>(
					FindObjectsInactive.Include,
					FindObjectsSortMode.None).Length,
				Is.EqualTo(0));
		}

		[Test]
		public void ApplyPlan_RejectsStaleRevisionWithoutMutation()
		{
			Dictionary<string, object> executableData = PrepareCreatePlan(
				"test-stale-revision");

			ToolResult result =
				Inspection.ApplyPlan(
					"test-stale-revision-result",
					executableData["planId"] as string,
					Convert.ToInt64(executableData["expectedRevision"]) + 1,
					executableData["approvalToken"] as string,
					"NONE");

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
			Assert.That(
				UnityEngine.Object.FindObjectsByType<Light>(
					FindObjectsInactive.Include,
					FindObjectsSortMode.None).Length,
				Is.EqualTo(0));
		}

		[Test]
		public void ApplyPlan_RejectsAutomaticSaveMode()
		{
			Dictionary<string, object> executableData = PrepareCreatePlan(
				"test-save-mode");

			ToolResult result =
				Inspection.ApplyPlan(
					"test-save-mode-result",
					executableData["planId"] as string,
					Convert.ToInt64(executableData["expectedRevision"]),
					executableData["approvalToken"] as string,
					"SAVE_SCENE");

			Assert.That(
				result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.UNSUPPORTED.ToString()));
			Assert.That(SceneManager.GetActiveScene().path, Is.Empty);
		}

		[Test]
		public void ApplyPlan_CreatesLightWithoutSavingOrBaking()
		{
			Dictionary<string, object> executableData = PrepareCreatePlan(
				"test-create-light");
			Scene scene = SceneManager.GetActiveScene();

			ToolResult result = Apply(
				"test-create-light-result",
				executableData);

			Assert.That(result.IsSuccessful, Is.True);
			Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
				FindObjectsInactive.Include,
				FindObjectsSortMode.None);
			Assert.That(lights.Length, Is.EqualTo(1));
			Assert.That(lights[0].gameObject.name, Is.EqualTo("Key Light"));
			Assert.That(lights[0].type, Is.EqualTo(LightType.Directional));
			Assert.That(lights[0].intensity, Is.EqualTo(2.0f));
			Assert.That(scene.path, Is.Empty);
			Assert.That(scene.isDirty, Is.True);

			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["savePerformed"], Is.EqualTo(false));
			Assert.That(data["bakePerformed"], Is.EqualTo(false));
			Assert.That(data["undoAvailable"], Is.EqualTo(true));
		}

		[Test]
		public void UndoLastTransaction_RemovesCreatedLight()
		{
			Dictionary<string, object> executableData = PrepareCreatePlan(
				"test-create-undo");
			ToolResult applyResult = Apply(
				"test-create-undo-apply",
				executableData);
			Dictionary<string, object> applyData = ResultData(applyResult);

			ToolResult undoResult =
				Inspection.UndoLastTransaction(
					"test-create-undo-result",
					applyData["transactionId"] as string,
					Convert.ToInt64(applyData["revision"]));

			Assert.That(undoResult.IsSuccessful, Is.True);
			Assert.That(
				UnityEngine.Object.FindObjectsByType<Light>(
					FindObjectsInactive.Include,
					FindObjectsSortMode.None).Length,
				Is.EqualTo(0));
		}

		[Test]
		public void ApplyAndUndo_UpdateRestoresExistingLight()
		{
			Light light = CreateSavedLight("Existing Light", 1.0f);
			string objectId = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();
			Dictionary<string, object> directionData = CompileDirection(
				"test-update-direction");

			LightOperationInput update =
				new LightOperationInput
				{
					operationId = "update-key",
					operation = "LIGHT_UPDATE",
					targetObjectId = objectId,
					name = "Updated Key Light",
					intensity = 4.0f,
					shadows = "Soft",
					eulerAngles = Vector(30.0f, 15.0f, 0.0f)
				};

			ToolResult prepareResult = Prepare(
				"test-update-prepare",
				directionData,
				new[] { update });
			ToolResult applyResult = Apply(
				"test-update-apply",
				ResultData(prepareResult));

			Assert.That(applyResult.IsSuccessful, Is.True);
			Assert.That(light.gameObject.name, Is.EqualTo("Updated Key Light"));
			Assert.That(light.intensity, Is.EqualTo(4.0f));
			Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));

			Dictionary<string, object> applyData = ResultData(applyResult);
			ToolResult undoResult =
				Inspection.UndoLastTransaction(
					"test-update-undo",
					applyData["transactionId"] as string,
					Convert.ToInt64(applyData["revision"]));

			Assert.That(undoResult.IsSuccessful, Is.True);
			Assert.That(light.gameObject.name, Is.EqualTo("Existing Light"));
			Assert.That(light.intensity, Is.EqualTo(1.0f));
			Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
		}

		[Test]
		public void ApplyPlan_RejectsTargetChangedAfterPreview()
		{
			Light light = CreateSavedLight("Existing Light", 1.0f);
			string objectId = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();
			Dictionary<string, object> directionData = CompileDirection(
				"test-baseline-change-direction");
			ToolResult prepareResult = Prepare(
				"test-baseline-change-prepare",
				directionData,
				new[]
				{
					new LightOperationInput
					{
						operationId = "update-key",
						operation = "LIGHT_UPDATE",
						targetObjectId = objectId,
						intensity = 3.0f
					}
				});
			Dictionary<string, object> executableData = ResultData(prepareResult);

			light.intensity = 2.0f;

			ToolResult applyResult = Apply(
				"test-baseline-change-apply",
				executableData);

			Assert.That(
				applyResult.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
			Assert.That(light.intensity, Is.EqualTo(2.0f));
		}

		[Test]
		public void UndoLastTransaction_RejectsExternalTargetChange()
		{
			Light light = CreateSavedLight("Existing Light", 1.0f);
			string objectId = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();
			Dictionary<string, object> directionData = CompileDirection(
				"test-external-change-direction");
			ToolResult prepareResult = Prepare(
				"test-external-change-prepare",
				directionData,
				new[]
				{
					new LightOperationInput
					{
						operationId = "update-key",
						operation = "LIGHT_UPDATE",
						targetObjectId = objectId,
						intensity = 3.0f
					}
				});
			ToolResult applyResult = Apply(
				"test-external-change-apply",
				ResultData(prepareResult));
			Dictionary<string, object> applyData = ResultData(applyResult);

			light.intensity = 5.0f;

			ToolResult undoResult =
				Inspection.UndoLastTransaction(
					"test-external-change-undo",
					applyData["transactionId"] as string,
					Convert.ToInt64(applyData["revision"]));

			Assert.That(
				undoResult.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
			Assert.That(light.intensity, Is.EqualTo(5.0f));
		}

		[Test]
		public void UndoLastTransaction_RestoresMultipleOperationsAsOneTransaction()
		{
			Dictionary<string, object> directionData = CompileDirection(
				"test-atomic-undo-direction");
			ToolResult prepareResult = Prepare(
				"test-atomic-undo-prepare",
				directionData,
				new[]
				{
					CreateDirectionalOperation("create-key", "Key Light", 2.0f),
					CreateDirectionalOperation("create-rim", "Rim Light", 1.5f)
				});
			ToolResult applyResult = Apply(
				"test-atomic-undo-apply",
				ResultData(prepareResult));

			Assert.That(
				UnityEngine.Object.FindObjectsByType<Light>(
					FindObjectsInactive.Include,
					FindObjectsSortMode.None).Length,
				Is.EqualTo(2));

			Dictionary<string, object> applyData = ResultData(applyResult);
			ToolResult undoResult =
				Inspection.UndoLastTransaction(
					"test-atomic-undo-result",
					applyData["transactionId"] as string,
					Convert.ToInt64(applyData["revision"]));

			Assert.That(undoResult.IsSuccessful, Is.True);
			Assert.That(
				UnityEngine.Object.FindObjectsByType<Light>(
					FindObjectsInactive.Include,
					FindObjectsSortMode.None).Length,
				Is.EqualTo(0));
		}

		private static Dictionary<string, object> PrepareCreatePlan(string requestId)
		{
			Dictionary<string, object> directionData = CompileDirection(
				requestId + "-direction");
			ToolResult prepareResult = Prepare(
				requestId + "-prepare",
				directionData,
				new[] { CreateDirectionalOperation("create-key", "Key Light", 2.0f) });
			Assert.That(prepareResult.IsSuccessful, Is.True);
			return ResultData(prepareResult);
		}

		private static ToolResult Prepare(
			string requestId,
			Dictionary<string, object> directionData,
			LightOperationInput[] operations)
		{
			return Inspection.PrepareLightPlan(
				requestId,
				directionData["planId"] as string,
				Convert.ToInt64(directionData["expectedRevision"]),
				operations);
		}

		private static ToolResult Apply(
			string requestId,
			Dictionary<string, object> executableData)
		{
			return Inspection.ApplyPlan(
				requestId,
				executableData["planId"] as string,
				Convert.ToInt64(executableData["expectedRevision"]),
				executableData["approvalToken"] as string,
				"NONE");
		}

		private static Dictionary<string, object> CompileDirection(string requestId)
		{
			ToolResult result =
				Inspection.CompileDirection(
					requestId,
					"明示されたLight差分を安全に適用する",
					null,
					null,
					null,
					null,
					new[] { "Key", "Fill", "Rim" },
					new[] { "Neutral" },
					null,
					null,
					null,
					new[] { "Preserve frame time" },
					new[] { "PC" },
					new[] { "Automatic Save禁止" },
					null);

			Assert.That(result.IsSuccessful, Is.True);
			return ResultData(result);
		}

		private static LightOperationInput CreateDirectionalOperation(
			string operationId,
			string name,
			float intensity)
		{
			return new LightOperationInput
			{
				operationId = operationId,
				operation = "LIGHT_CREATE",
				name = name,
				lightType = "Directional",
				color = new ColorInput
				{
					r = 1.0f,
					g = 0.9f,
					b = 0.8f,
					a = 1.0f
				},
				intensity = intensity,
				shadows = "Soft",
				position = Vector(0.0f, 3.0f, 0.0f),
				eulerAngles = Vector(45.0f, -30.0f, 0.0f),
				enabled = true
			};
		}

		private static Light CreateSavedLight(string name, float intensity)
		{
			GameObject gameObject = new GameObject(name);
			Light light = gameObject.AddComponent<Light>();
			light.type = LightType.Point;
			light.intensity = intensity;
			light.shadows = LightShadows.None;
			Assert.That(
				EditorSceneManager.SaveScene(
					SceneManager.GetActiveScene(),
					TEMP_SCENE_PATH),
				Is.True);
			return light;
		}

		private static Vector3Input Vector(float x, float y, float z)
		{
			return new Vector3Input { x = x, y = y, z = z };
		}

		private static JObject VectorJson(float x, float y, float z)
		{
			return new JObject
			{
				["x"] = x,
				["y"] = y,
				["z"] = z
			};
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
