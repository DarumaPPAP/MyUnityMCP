#if UNITY_EDITOR

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityBuildMcp;
using UnityDomainMcp;
using UnityEditor;
using UnityGraphicsMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityBuildMcpTransportContractTests
	{
		[SetUp]
		public void SetUp()
		{
			UnityDomainMcpPlanStore.ClearForTests();
			UnityBuildMcpRuntime.ClearQueuedBuildForTests();
		}

		[TearDown]
		public void TearDown()
		{
			UnityBuildMcpRuntime.ClearQueuedBuildForTests();
			UnityDomainMcpPlanStore.ClearForTests();
		}

		[Test]
		public void StartPlayer_ConsumesApprovedPlanAndReturnsBeforeBuildPipelineStarts()
		{
			long revision = Session.Revision;
			UnityDomainMcpResult prepared = UnityDomainMcpPlanStore.Prepare(
				"build.prepare_player",
				"unity_build_mcp",
				"build_player",
				revision,
				true,
				new JObject
				{
					["target"] = BuildTarget.StandaloneWindows64.ToString(),
					["scenes"] = new JArray("Assets/__BuildQueueContract.unity"),
					["outputPath"] = "Builds/MyUnityMCP/__BuildQueueContract.exe",
					["options"] = BuildOptions.None.ToString()
				});
			JObject plan = (JObject)prepared.data;

			UnityDomainMcpResult started = UnityBuildMcpRuntime.StartPlayer(
				plan.Value<string>("planId"),
				revision,
				plan.Value<string>("approvalToken"));

			Assert.That(started.success, Is.True, started.summary);
			Assert.That(started.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.PARTIAL.ToString()));
			Assert.That(BuildPipeline.isBuildingPlayer, Is.False, "BuildPipeline must not start inside the MCP command handler.");
			Assert.That(UnityBuildMcpRuntime.HasQueuedBuildForTests, Is.True);
			Assert.That(UnityBuildMcpRuntime.ActiveBuildForTests?.Value<string>("state"), Is.EqualTo("QUEUED"));

			JObject data = started.data as JObject;
			Assert.That(data, Is.Not.Null);
			Assert.That(data.Value<bool>("planConsumed"), Is.True);
			Assert.That(data.Value<bool>("buildStarted"), Is.False);
			Assert.That(data.Value<bool>("commandResultReturnedBeforeBuild"), Is.True);
			Assert.That(data.Value<string>("pollTool"), Is.EqualTo("build.get_history"));
		}
	}
}

#endif
