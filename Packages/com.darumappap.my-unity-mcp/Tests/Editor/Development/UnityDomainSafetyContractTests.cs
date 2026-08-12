#if UNITY_EDITOR

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityAnimationMcp;
using UnityAudioMcp;
using UnityBuildMcp;
using UnityCinematicMcp;
using UnityDomainMcp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using StandardEditorSceneManager = UnityEditor.SceneManagement.EditorSceneManager;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityGraphicsMcp;
using UnityProfilerMcp;
using UnityUiMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityDomainSafetyContractTests
	{
		private const string DOMAIN_SCENE_PATH = "Assets/__MyUnityMcpDomainSafetyContract.unity";

		[SetUp]
		public void SetUp()
		{
			UnityDomainMcpPlanStore.ClearForTests();
			CleanupDomainScene();
		}

		[TearDown]
		public void TearDown()
		{
			CleanupDomainScene();
		}

		[Test]
		public void AllMcpTools_AreExactly85AndRemainDefaultDisabled()
		{
			string[] sources = Directory.GetFiles(
				"Packages/com.darumappap.my-unity-mcp/Editor",
				"*.cs",
				SearchOption.AllDirectories);
			string combined = string.Join("\n", sources.Select(File.ReadAllText));
			int toolCount = Count(combined, "[McpForUnityTool(");
			int disabledCount = Count(combined, "AutoRegister = false");

			Assert.That(toolCount, Is.EqualTo(85));
			Assert.That(disabledCount, Is.EqualTo(toolCount));
		}

		[Test]
		public void SharedPlan_RequiresApprovalAndRejectsReuse()
		{
			long revision = Session.Revision;
			UnityDomainMcpResult prepared = UnityDomainMcpPlanStore.Prepare(
				"test.prepare",
				"test_domain",
				"test_operation",
				revision,
				true,
				new JObject());
			JObject data = (JObject)prepared.data;
			string planId = data.Value<string>("planId");
			string token = data.Value<string>("approvalToken");

			bool wrong = UnityDomainMcpPlanStore.TryConsume(
				"test.apply",
				"test_domain",
				planId,
				revision,
				"wrong",
				out _,
				out UnityDomainMcpResult wrongFailure);
			bool accepted = UnityDomainMcpPlanStore.TryConsume(
				"test.apply",
				"test_domain",
				planId,
				revision,
				token,
				out _,
				out _);
			bool reused = UnityDomainMcpPlanStore.TryConsume(
				"test.apply",
				"test_domain",
				planId,
				revision,
				token,
				out _,
				out UnityDomainMcpResult reuseFailure);

			Assert.That(wrong, Is.False);
			Assert.That(wrongFailure.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.APPROVAL_REQUIRED.ToString()));
			Assert.That(accepted, Is.True);
			Assert.That(reused, Is.False);
			Assert.That(reuseFailure.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void BuildOutput_RejectsAbsoluteAndEscapingPaths()
		{
			Assert.That(UnityBuildMcpRuntime.TryNormalizeOutput("C:/Build/Game.exe", out _, out _), Is.False);
			Assert.That(UnityBuildMcpRuntime.TryNormalizeOutput("Builds/MyUnityMCP/../escape/Game.exe", out _, out _), Is.False);
			Assert.That(UnityBuildMcpRuntime.TryNormalizeOutput("Builds/MyUnityMCP/Windows/Game.exe", out string normalized, out _), Is.True);
			Assert.That(normalized, Is.EqualTo("Builds/MyUnityMCP/Windows/Game.exe"));
		}

		[Test]
		public void ProfilerSummary_ComputesMedianP95AndMax()
		{
			JObject result = UnityProfilerMcpRuntime.SummarizeValues(new long[] {1, 2, 3, 4, 100});

			Assert.That(result.Value<int>("sampleCount"), Is.EqualTo(5));
			Assert.That(result.Value<long>("median"), Is.EqualTo(3));
			Assert.That(result.Value<long>("p95"), Is.EqualTo(100));
			Assert.That(result.Value<long>("max"), Is.EqualTo(100));
		}

		[Test]
		public void UiMutation_RequiresPreviewAndApproval()
		{
			GameObject gameObject = CreatePersistentSceneObject("UiTarget", typeof(RectTransform));
			RectTransform target = gameObject.GetComponent<RectTransform>();
			long revision = Session.Revision;
			UnityDomainMcpResult prepared = UnityUiMcpRuntime.PrepareRectTransform(
				UnityDomainMcpCommon.ObjectId(target),
				new UnityUiMcpVector2Input {x = 12f, y = 34f},
				null,
				null,
				null,
				null,
				revision);

			Assert.That(prepared.success, Is.True, prepared.summary);
			JObject data = prepared.data as JObject;
			Assert.That(data, Is.Not.Null);
			UnityDomainMcpResult applied = UnityUiMcpRuntime.ApplyRectTransform(
				data.Value<string>("planId"),
				revision,
				data.Value<string>("approvalToken"));

			Assert.That(applied.success, Is.True, applied.summary);
			Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(12f, 34f)));
		}

		[Test]
		public void AudioMutation_UpdatesOnlyApprovedProperties()
		{
			GameObject gameObject = CreatePersistentSceneObject("AudioTarget", typeof(AudioSource));
			AudioSource target = gameObject.GetComponent<AudioSource>();
			long revision = Session.Revision;
			UnityDomainMcpResult prepared = UnityAudioMcpRuntime.PrepareSource(
				UnityDomainMcpCommon.ObjectId(target),
				0.25f,
				1.1f,
				0.5f,
				true,
				false,
				false,
				revision);

			Assert.That(prepared.success, Is.True, prepared.summary);
			JObject data = prepared.data as JObject;
			Assert.That(data, Is.Not.Null);
			UnityDomainMcpResult applied = UnityAudioMcpRuntime.ApplySource(
				data.Value<string>("planId"),
				revision,
				data.Value<string>("approvalToken"));

			Assert.That(applied.success, Is.True, applied.summary);
			Assert.That(target.volume, Is.EqualTo(0.25f));
			Assert.That(target.spatialBlend, Is.EqualTo(0.5f));
			Assert.That(target.clip, Is.Null);
		}

		[Test]
		public void CinematicMutation_DoesNotChangePlayableAssetOrBindings()
		{
			GameObject gameObject = CreatePersistentSceneObject("DirectorTarget", typeof(PlayableDirector));
			PlayableDirector target = gameObject.GetComponent<PlayableDirector>();
			long revision = Session.Revision;
			UnityDomainMcpResult prepared = UnityCinematicMcpRuntime.PrepareDirector(
				UnityDomainMcpCommon.ObjectId(target),
				0.0,
				DirectorUpdateMode.Manual.ToString(),
				DirectorWrapMode.Hold.ToString(),
				false,
				revision);

			Assert.That(prepared.success, Is.True, prepared.summary);
			JObject data = prepared.data as JObject;
			Assert.That(data, Is.Not.Null);
			UnityDomainMcpResult applied = UnityCinematicMcpRuntime.ApplyDirector(
				data.Value<string>("planId"),
				revision,
				data.Value<string>("approvalToken"));

			Assert.That(applied.success, Is.True, applied.summary);
			Assert.That(target.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.Manual));
			Assert.That(target.playableAsset, Is.Null);
		}

		[Test]
		public void AnimationParameterMutation_AddsParameterWithoutStateRewrite()
		{
			const string path = "Assets/__MyUnityMcpAnimationContract.controller";
			AssetDatabase.DeleteAsset(path);
			AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
			try
			{
				long revision = Session.Revision;
				UnityDomainMcpResult prepared = UnityAnimationMcpRuntime.PrepareParameter(
					path,
					"ContractTrigger",
					AnimatorControllerParameterType.Trigger.ToString(),
					null,
					null,
					null,
					revision);
				JObject data = (JObject)prepared.data;
				UnityDomainMcpResult applied = UnityAnimationMcpRuntime.ApplyParameter(
					data.Value<string>("planId"),
					revision,
					data.Value<string>("approvalToken"));

				Assert.That(applied.success, Is.True, applied.summary);
				Assert.That(controller.parameters.Any(value => value.name == "ContractTrigger"), Is.True);
				Assert.That(controller.layers.Length, Is.EqualTo(1));
			}
			finally
			{
				AssetDatabase.DeleteAsset(path);
			}
		}

#if !MYUNITYMCP_ADDRESSABLES
		[Test]
		public void AddressablesWithoutPackage_ReturnsUnsupported()
		{
			UnityDomainMcpResult result = UnityAddressablesMcp.UnityAddressablesMcpRuntime.Inspect();

			Assert.That(result.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.UNSUPPORTED.ToString()));
			Assert.That(result.success, Is.False);
		}
#endif

		private static GameObject CreatePersistentSceneObject(string objectName, params Type[] components)
		{
			Scene scene = StandardEditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			GameObject gameObject = new GameObject(objectName, components);
			Assert.That(StandardEditorSceneManager.SaveScene(scene, DOMAIN_SCENE_PATH), Is.True);
			return gameObject;
		}

		private static void CleanupDomainScene()
		{
			Scene scene = SceneManager.GetSceneByPath(DOMAIN_SCENE_PATH);
			if (scene.IsValid() && scene.isLoaded)
			{
				StandardEditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			}
			AssetDatabase.DeleteAsset(DOMAIN_SCENE_PATH);
		}

		private static int Count(string value, string token)
		{
			return value.Split(new[] {token}, StringSplitOptions.None).Length - 1;
		}
	}
}

#endif
