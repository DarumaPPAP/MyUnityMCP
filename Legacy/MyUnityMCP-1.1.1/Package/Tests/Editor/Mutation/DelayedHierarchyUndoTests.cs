#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class DelayedHierarchyUndoTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpDelayedHierarchyUndoTemporaryScene.unity";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			Session.ClearSnapshots();
			Session.ClearPlans();
			MutationSession.ClearForTests();
			Undo.ClearAll();
		}

		[TearDown]
		public void TearDown()
		{
			Session.ClearSnapshots();
			Session.ClearPlans();
			MutationSession.ClearForTests();
			Undo.ClearAll();
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[Test]
		public void UndoLastTransaction_AllowsOwnedDelayedHierarchyRevisionAfterUpdate()
		{
			Light light = CreateSavedLight("Existing Light", 2.0f);
			Dictionary<string, object> applyData = ApplyIntensityUpdate(light, 1.0f);
			long applyRevision = Convert.ToInt64(applyData["revision"]);

			Assert.That(light.intensity, Is.EqualTo(1.0f));

			// Unityの実EditorではLight UPDATE適用後、MCP応答後にhierarchyChangedが
			// 遅延到着する場合がある。Session側が先にRevisionを進め、その後に
			// MutationSessionが同イベントをMyUnityMCP所有として受ける順序を再現する。
			Session.NotifyMutationApplied();
			MutationSession.NotifyHierarchyChangedForTests();

			Assert.That(Session.Revision, Is.EqualTo(applyRevision + 1));

			ToolResult undoResult =
				Inspection.UndoLastTransaction(
					"test-delayed-owned-hierarchy-undo",
					applyData["transactionId"] as string,
					applyRevision);

			Assert.That(undoResult.IsSuccessful, Is.True, undoResult.summary);
			Assert.That(light.intensity, Is.EqualTo(2.0f));
		}

		[Test]
		public void UndoLastTransaction_RejectsUnownedRevisionAdvanceAfterUpdate()
		{
			Light light = CreateSavedLight("Existing Light", 2.0f);
			Dictionary<string, object> applyData = ApplyIntensityUpdate(light, 1.0f);
			long applyRevision = Convert.ToInt64(applyData["revision"]);

			Assert.That(light.intensity, Is.EqualTo(1.0f));

			// Owned hierarchy通知を伴わないRevision進行は従来どおりSTALE扱い。
			Session.NotifyMutationApplied();

			ToolResult undoResult =
				Inspection.UndoLastTransaction(
					"test-unowned-revision-undo",
					applyData["transactionId"] as string,
					applyRevision);

			Assert.That(
				undoResult.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
			Assert.That(light.intensity, Is.EqualTo(1.0f));
		}

		private static Dictionary<string, object> ApplyIntensityUpdate(
			Light light,
			float intensity)
		{
			Dictionary<string, object> directionData = CompileDirection();
			string objectId = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();

			ToolResult prepareResult =
				Inspection.PrepareLightPlan(
					"test-delayed-hierarchy-prepare",
					directionData["planId"] as string,
					Convert.ToInt64(directionData["expectedRevision"]),
					new[]
					{
						new LightOperationInput
						{
							operationId = "update-intensity",
							operation = "LIGHT_UPDATE",
							targetObjectId = objectId,
							intensity = intensity
						}
					});

			Assert.That(prepareResult.IsSuccessful, Is.True, prepareResult.summary);
			Dictionary<string, object> prepareData = ResultData(prepareResult);

			ToolResult applyResult =
				Inspection.ApplyPlan(
					"test-delayed-hierarchy-apply",
					prepareData["planId"] as string,
					Convert.ToInt64(prepareData["expectedRevision"]),
					prepareData["approvalToken"] as string,
					"NONE");

			Assert.That(applyResult.IsSuccessful, Is.True, applyResult.summary);
			return ResultData(applyResult);
		}

		private static Dictionary<string, object> CompileDirection()
		{
			ToolResult result =
				Inspection.CompileDirection(
					"test-delayed-hierarchy-direction",
					"Light Intensityを安全に変更し、Undo可能性を検証する",
					null,
					null,
					null,
					null,
					new[] { "Key" },
					new[] { "Neutral" },
					null,
					null,
					null,
					new[] { "Preserve frame time" },
					new[] { "PC" },
					new[] { "Automatic Save禁止" },
					null);

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			return ResultData(result);
		}

		private static Light CreateSavedLight(string name, float intensity)
		{
			GameObject gameObject = new GameObject(name);
			Light light = gameObject.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = intensity;
			light.shadows = LightShadows.Soft;

			Assert.That(
				EditorSceneManager.SaveScene(
					SceneManager.GetActiveScene(),
					TEMP_SCENE_PATH),
				Is.True);
			return light;
		}

		private static Dictionary<string, object> ResultData(ToolResult result)
		{
			Dictionary<string, object> data =
				result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null, result.summary);
			return data;
		}
	}
}

#endif