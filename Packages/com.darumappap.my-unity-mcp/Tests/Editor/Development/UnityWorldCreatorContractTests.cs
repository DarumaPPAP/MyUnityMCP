#if UNITY_EDITOR

using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityAgentMcp;
using UnityGraphicsMcp;
using UnityWorldCreatorMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityWorldCreatorContractTests
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
				new[] { "Editor" },
				new[] { "No mutation" },
				new[] { "Validation completed" },
				revision);

			Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());
			Assert.That(compiled.Value<string>("creator"), Is.EqualTo("world_creator"));
			Assert.That(compiled.Value<bool>("directUnityMutation"), Is.False);

			JObject started = UnityWorldCreatorRuntime.StartPreflight(
				compiled.Value<string>("graphId"),
				revision);
			Assert.That(started.Value<string>("status"), Is.EqualTo("RUNNING"), started.ToString());

			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();

			JObject completed = UnityAgentMcpRuntime.Instance.GetExecutionStatus(
				started.Value<string>("executionId"));
			Assert.That(completed.Value<string>("status"), Is.EqualTo("SUCCEEDED"), completed.ToString());
			Assert.That(completed.Value<bool>("executionSucceeded"), Is.True);

			JObject handoff = UnityWorldCreatorRuntime.CreateReviewHandoff(
				started.Value<string>("executionId"),
				"現在Sceneの構成とGraphics設定を確認する",
				new[] { "Validation completed" });
			Assert.That(handoff.Value<string>("handoffStatus"), Is.EqualTo("HUMAN_REVIEW_REQUIRED"));
			Assert.That(handoff.Value<bool>("automaticVisualAcceptance"), Is.False);
		}

		[Test]
		public void WorldCreator_RejectsStaleRevision()
		{
			JObject result = UnityWorldCreatorRuntime.CompileWorkflow(
				"現在Sceneを確認する",
				null,
				null,
				null,
				null,
				null,
				null,
				Session.Revision + 1);

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("WORLD-REVISION-STALE"));
		}

		[Test]
		public void WorldCreator_RequiresHumanReviewAndDoesNotCallUnityMutationApisDirectly()
		{
			string source = File.ReadAllText(
				"Packages/com.darumappap.my-unity-mcp/Editor/Development/Creators/UnityWorldCreatorMcp.cs");

			Assert.That(source, Does.Contain("HUMAN_REVIEW_REQUIRED"));
			Assert.That(source, Does.Contain("automaticVisualAcceptance"));
			Assert.That(source, Does.Not.Contain("Undo.RecordObject"));
			Assert.That(source, Does.Not.Contain("EditorUtility.SetDirty"));
			Assert.That(source, Does.Not.Contain("AssetDatabase.CreateAsset"));
			Assert.That(source, Does.Not.Contain("EditorSceneManager.SaveScene"));
			Assert.That(source, Does.Not.Contain("BuildPipeline.BuildPlayer"));
		}
	}
}

#endif
