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
	public sealed class UnityGraphicsMcpInspectionTests
	{
		private const string TEMP_ASSET_PATH = "Assets/MyUnityMcpPhase1Temp.asset";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			UnityGraphicsMcpSession.ClearSnapshots();
			AssetDatabase.DeleteAsset(TEMP_ASSET_PATH);
		}

		[TearDown]
		public void TearDown()
		{
			UnityGraphicsMcpSession.ClearSnapshots();
			AssetDatabase.DeleteAsset(TEMP_ASSET_PATH);
		}

		[Test]
		public void Bridge_DiscoversAllPhase1Tools_AndKeepsThemDisabledByDefault()
		{
			CommandRegistry.Initialize();

			Assert.That(CommandRegistry.GetHandler("graphics.inspect_project"), Is.Not.Null);
			Assert.That(CommandRegistry.GetHandler("graphics.inspect_scene"), Is.Not.Null);
			Assert.That(CommandRegistry.GetHandler("graphics.validate_scene"), Is.Not.Null);

			McpForUnityToolAttribute attribute =
				Attribute.GetCustomAttribute(
					typeof(GraphicsInspectProjectTool),
					typeof(McpForUnityToolAttribute)) as McpForUnityToolAttribute;

			Assert.That(attribute, Is.Not.Null);
			Assert.That(attribute.AutoRegister, Is.False);
		}

		[Test]
		public void Bridge_CanInvokeInspectProjectHandler()
		{
			CommandRegistry.Initialize();
			Func<JObject, object> handler = CommandRegistry.GetHandler("graphics.inspect_project");

			object response = handler(new JObject
			{
				["requestId"] = "test-bridge-invoke",
				["requestedPlatforms"] = new JArray("Windows")
			});

			Assert.That(response, Is.TypeOf<SuccessResponse>());
		}

		[Test]
		public void InspectProject_DoesNotChangeSceneDirtyState()
		{
			Scene scene = SceneManager.GetActiveScene();
			bool dirtyBefore = scene.isDirty;

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.InspectProject("test-inspect-project");

			Assert.That(result.IsSuccessful, Is.True);
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));
		}

		[Test]
		public void InspectProject_DoesNotChangePersistentAssetDirtyState()
		{
			UnityGraphicsMcpTestAsset asset =
				ScriptableObject.CreateInstance<UnityGraphicsMcpTestAsset>();
			AssetDatabase.CreateAsset(asset, TEMP_ASSET_PATH);
			AssetDatabase.SaveAssets();

			Assert.That(EditorUtility.IsDirty(asset), Is.False);

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.InspectProject("test-asset-dirty-guard");

			Assert.That(result.IsSuccessful, Is.True);
			Assert.That(EditorUtility.IsDirty(asset), Is.False);
		}

		[Test]
		public void InspectProject_SeparatesDetectedProjectAndRequestedTarget()
		{
			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.InspectProject(
				"test-project-context",
				new[] { "Nintendo Switch", "PC" },
				new[] { "No Camera Stack" });

			Assert.That(result.IsSuccessful, Is.True);

			Dictionary<string, object> data = result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null);
			Assert.That(data.ContainsKey("detectedProject"), Is.True);
			Assert.That(data.ContainsKey("requestedTarget"), Is.True);
			Assert.That(data.ContainsKey("backendSelection"), Is.True);
			Assert.That(data.ContainsKey("capabilities"), Is.True);
			Assert.That(
				data["verificationState"],
				Is.EqualTo(E_MCP_CAPABILITY_STATUS.UNVERIFIED.ToString()));

			Dictionary<string, object> requestedTarget =
				data["requestedTarget"] as Dictionary<string, object>;
			Assert.That(requestedTarget, Is.Not.Null);

			List<string> platforms = requestedTarget["platforms"] as List<string>;
			Assert.That(platforms, Is.EquivalentTo(new[] { "Nintendo Switch", "PC" }));
		}

		[Test]
		public void InspectScene_ReturnsCameraAndLightWithoutDirtyChange()
		{
			GameObject cameraObject = new GameObject("TestCamera");
			cameraObject.AddComponent<Camera>();

			GameObject lightObject = new GameObject("TestLight");
			lightObject.AddComponent<Light>();

			Scene scene = SceneManager.GetActiveScene();
			bool dirtyBefore = scene.isDirty;

			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.InspectScene(
				"test-inspect-scene",
				true,
				200,
				new[] { "CAMERA", "LIGHT" },
				null,
				null);

			Assert.That(result.IsSuccessful, Is.True);
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));

			Dictionary<string, object> data = result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null);

			Dictionary<string, object> summary =
				data["summary"] as Dictionary<string, object>;
			Assert.That(summary, Is.Not.Null);

			Dictionary<string, int> counts =
				summary["counts"] as Dictionary<string, int>;
			Assert.That(counts, Is.Not.Null);
			Assert.That(counts["CAMERA"], Is.EqualTo(1));
			Assert.That(counts["LIGHT"], Is.EqualTo(1));
		}

		[Test]
		public void InspectScene_DoesNotInstantiateRendererMaterial()
		{
			GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			Renderer renderer = cube.GetComponent<Renderer>();
			Material materialBefore = renderer.sharedMaterial;
			int materialIdBefore = materialBefore == null ? 0 : materialBefore.GetInstanceID();

			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.InspectScene(
				"test-material-readonly",
				true,
				200,
				new[] { "RENDERER_MATERIAL" },
				null,
				null);

			Material materialAfter = renderer.sharedMaterial;
			int materialIdAfter = materialAfter == null ? 0 : materialAfter.GetInstanceID();

			Assert.That(result.IsSuccessful, Is.True);
			Assert.That(materialIdAfter, Is.EqualTo(materialIdBefore));
		}

		[Test]
		public void ValidateScene_ReportsOutOfRangeLightmapIndex()
		{
			GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			Renderer renderer = cube.GetComponent<Renderer>();
			renderer.lightmapIndex = 10;

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.ValidateScene("test-lightmap-validation", true);

			Dictionary<string, object> data = result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null);

			List<UnityGraphicsMcpFinding> findings =
				data["findings"] as List<UnityGraphicsMcpFinding>;
			Assert.That(findings, Is.Not.Null);
			Assert.That(
				findings.Any(item => item.ruleId == "GFX-LIGHTMAP-001"),
				Is.True);
		}

		[Test]
		public void InspectScene_RejectsCursorBeyondSnapshot()
		{
			UnityGraphicsMcpToolResult first = UnityGraphicsMcpInspection.InspectScene(
				"test-snapshot-create",
				true,
				10,
				new[] { "CAMERA" },
				null,
				null);

			Dictionary<string, object> firstData =
				first.data as Dictionary<string, object>;
			Assert.That(firstData, Is.Not.Null);

			string snapshotId = firstData["snapshotId"] as string;
			UnityGraphicsMcpToolResult second = UnityGraphicsMcpInspection.InspectScene(
				"test-snapshot-cursor",
				true,
				10,
				null,
				snapshotId,
				"9999");

			Assert.That(
				second.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}
	}
}

#endif
