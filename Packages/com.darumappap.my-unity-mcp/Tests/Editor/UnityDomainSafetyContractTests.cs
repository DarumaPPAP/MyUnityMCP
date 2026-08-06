#if UNITY_EDITOR

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
using UnityEngine;
using UnityEngine.Playables;
using UnityGraphicsMcp;
using UnityProfilerMcp;
using UnityUiMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityDomainSafetyContractTests
	{
		[SetUp]
		public void SetUp()
		{
			UnityDomainMcpPlanStore.ClearForTests();
		}

		[Test]
		public void AllMcpTools_RemainDefaultDisabled()
		{
			string[] sources = Directory.GetFiles(
				"Packages/com.darumappap.my-unity-mcp/Editor",
				"*.cs",
				SearchOption.TopDirectoryOnly);
			string combined = string.Join("\n", sources.Select(File.ReadAllText));
			int toolCount = Count(combined, "[McpForUnityTool(");
			int disabledCount = Count(combined, "AutoRegister = false");

			Assert.That(toolCount, Is.GreaterThanOrEqualTo(91));
			Assert.That(disabledCount, Is.EqualTo(toolCount));
		}

		[Test]
		public void SharedPlan_RequiresApprovalAndRejectsReuse()
		{
			long revision = UnityGraphicsMcpSession.Revision;
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
			GameObject gameObject = new GameObject("UiTarget", typeof(RectTransform));
			try
			{
				RectTransform target = gameObject.GetComponent<RectTransform>();
				long revision = UnityGraphicsMcpSession.Revision;
				UnityDomainMcpResult prepared = UnityUiMcpRuntime.PrepareRectTransform(
					UnityDomainMcpCommon.ObjectId(target),
					new UnityUiMcpVector2Input {x = 12f, y = 34f},
					null,
					null,
					null,
					null,
					revision);
				JObject data = (JObject)prepared.data;
				UnityDomainMcpResult applied = UnityUiMcpRuntime.ApplyRectTransform(
					data.Value<string>("planId"),
					revision,
					data.Value<string>("approvalToken"));

				Assert.That(applied.success, Is.True, applied.summary);
				Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(12f, 34f)));
			}
			finally
			{
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void AudioMutation_UpdatesOnlyApprovedProperties()
		{
			GameObject gameObject = new GameObject("AudioTarget", typeof(AudioSource));
			try
			{
				AudioSource target = gameObject.GetComponent<AudioSource>();
				long revision = UnityGraphicsMcpSession.Revision;
				UnityDomainMcpResult prepared = UnityAudioMcpRuntime.PrepareSource(
					UnityDomainMcpCommon.ObjectId(target),
					0.25f,
					1.1f,
					0.5f,
					true,
					false,
					false,
					revision);
				JObject data = (JObject)prepared.data;
				UnityDomainMcpResult applied = UnityAudioMcpRuntime.ApplySource(
					data.Value<string>("planId"),
					revision,
					data.Value<string>("approvalToken"));

				Assert.That(applied.success, Is.True, applied.summary);
				Assert.That(target.volume, Is.EqualTo(0.25f));
				Assert.That(target.spatialBlend, Is.EqualTo(0.5f));
				Assert.That(target.clip, Is.Null);
			}
			finally
			{
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void CinematicMutation_DoesNotChangePlayableAssetOrBindings()
		{
			GameObject gameObject = new GameObject("DirectorTarget", typeof(PlayableDirector));
			try
			{
				PlayableDirector target = gameObject.GetComponent<PlayableDirector>();
				long revision = UnityGraphicsMcpSession.Revision;
				UnityDomainMcpResult prepared = UnityCinematicMcpRuntime.PrepareDirector(
					UnityDomainMcpCommon.ObjectId(target),
					0.0,
					DirectorUpdateMode.Manual.ToString(),
					DirectorWrapMode.Hold.ToString(),
					false,
					revision);
				JObject data = (JObject)prepared.data;
				UnityDomainMcpResult applied = UnityCinematicMcpRuntime.ApplyDirector(
					data.Value<string>("planId"),
					revision,
					data.Value<string>("approvalToken"));

				Assert.That(applied.success, Is.True, applied.summary);
				Assert.That(target.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.Manual));
				Assert.That(target.playableAsset, Is.Null);
			}
			finally
			{
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void AnimationParameterMutation_AddsParameterWithoutStateRewrite()
		{
			const string path = "Assets/__MyUnityMcpAnimationContract.controller";
			AssetDatabase.DeleteAsset(path);
			AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
			try
			{
				long revision = UnityGraphicsMcpSession.Revision;
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

		private static int Count(string value, string token)
		{
			return value.Split(new[] {token}, System.StringSplitOptions.None).Length - 1;
		}
	}
}

#endif
