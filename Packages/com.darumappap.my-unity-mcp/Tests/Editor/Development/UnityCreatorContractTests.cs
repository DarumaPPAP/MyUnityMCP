#if UNITY_EDITOR

using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityAgentMcp;
using UnityGraphicsMcp;
using UnityLiveCreatorMcp;
using UnityMovieCreatorMcp;
using UnityWorldCreatorMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityCreatorContractTests
	{
		[SetUp]
		public void SetUp()
		{
			UnityAgentMcpRuntime.Instance.ResetExecutionsForTests();
		}

		[TearDown]
		public void TearDown()
		{
			UnityAgentMcpRuntime.Instance.ResetExecutionsForTests();
		}

		[Test]
		public void WorldCreator_CompilesAndExecutesReadOnlyPreflight()
		{
			long revision = Session.Revision;
			JObject compiled = UnityWorldCreatorRuntime.CompileWorkflow(
				"現在Sceneの構成とGraphics設定を確認する",
				null,
				"environment",
				"neutral",
				new[] {"Editor"},
				new[] {"No mutation"},
				new[] {"Validation completed"},
				revision);

			JObject started = UnityWorldCreatorRuntime.StartPreflight(
				compiled.Value<string>("graphId"),
				revision);
			Assert.That(started.Value<string>("status"), Is.EqualTo("RUNNING"), started.ToString());

			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject completed = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));
			JObject handoff = UnityWorldCreatorRuntime.CreateReviewHandoff(
				started.Value<string>("executionId"),
				"現在Sceneの構成とGraphics設定を確認する",
				new[] {"Validation completed"});

			Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());
			Assert.That(compiled.Value<bool>("directUnityMutation"), Is.False);
			Assert.That(completed.Value<string>("status"), Is.EqualTo("SUCCEEDED"), completed.ToString());
			Assert.That(completed.Value<bool>("executionSucceeded"), Is.True);
			Assert.That(handoff.Value<string>("handoffStatus"), Is.EqualTo("HUMAN_REVIEW_REQUIRED"));
			Assert.That(handoff.Value<bool>("automaticVisualAcceptance"), Is.False);
		}

		[Test]
		public void MovieCreator_BlocksUntilCinematicDomainIsOperational()
		{
			JObject result = UnityMovieCreatorRuntime.CompileProduction(
				"短い検証Movie",
				new[]
				{
					new UnityMovieCreatorShotInput
					{
						shotId = "shot-01",
						durationSeconds = 2.0,
						visualGoal = "Scene overview",
						acceptanceCriteria = new[] {"Camera framing reviewed"}
					}
				},
				new[] {"Editor"},
				new[] {"No automatic save"});

			Assert.That(result.Value<bool>("success"), Is.True);
			Assert.That(result.Value<bool>("executionReady"), Is.False);
			Assert.That((result["blockingConditions"] as JArray)?.Count, Is.GreaterThan(0));
			Assert.That(result.Value<bool>("directUnityMutation"), Is.False);
		}

		[Test]
		public void LiveCreator_RejectsUnattendedExecution()
		{
			JObject result = UnityLiveCreatorRuntime.CompileShow(
				"Operator controlled test show",
				new[]
				{
					new UnityLiveCreatorCueInput
					{
						cueId = "cue-01",
						atSeconds = 0.0,
						domainId = "unity_graphics_mcp",
						toolName = "graphics.inspect_project",
						toolGroup = "inspect",
						parameters = new JObject()
					}
				},
				10.0,
				true);

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("LIVE-UNATTENDED-FORBIDDEN"));
		}

		[Test]
		public void LiveCreator_CompilesOperationalReadOnlyCue()
		{
			JObject result = UnityLiveCreatorRuntime.CompileShow(
				"Operator controlled test show",
				new[]
				{
					new UnityLiveCreatorCueInput
					{
						cueId = "cue-01",
						atSeconds = 0.0,
						domainId = "unity_graphics_mcp",
						toolName = "graphics.inspect_project",
						toolGroup = "inspect",
						parameters = new JObject(),
						requiresOperatorApproval = false
					}
				},
				10.0,
				false);

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("executionReady"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("unattended"), Is.False);
			Assert.That(result.Value<bool>("operatorRequired"), Is.True);
		}

		[Test]
		public void Creators_DoNotCallUnityMutationApisDirectly()
		{
			string source = string.Join("\n",
				File.ReadAllText("Packages/com.darumappap.my-unity-mcp/Editor/Development/Creators/UnityWorldCreatorMcp.cs"),
				File.ReadAllText("Packages/com.darumappap.my-unity-mcp/Editor/Development/Creators/UnityMovieCreatorMcp.cs"),
				File.ReadAllText("Packages/com.darumappap.my-unity-mcp/Editor/Development/Creators/UnityLiveCreatorMcp.cs"));

			Assert.That(source, Does.Not.Contain("Undo.RecordObject"));
			Assert.That(source, Does.Not.Contain("EditorUtility.SetDirty"));
			Assert.That(source, Does.Not.Contain("AssetDatabase.CreateAsset"));
			Assert.That(source, Does.Not.Contain("EditorSceneManager.SaveScene"));
			Assert.That(source, Does.Not.Contain("BuildPipeline.BuildPlayer"));
		}
	}
}

#endif