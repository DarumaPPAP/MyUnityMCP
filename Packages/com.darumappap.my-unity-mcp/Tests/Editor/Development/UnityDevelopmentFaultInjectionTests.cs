#if UNITY_EDITOR

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityBuildMcp;
using UnityDomainMcp;
using UnityGraphicsMcp;
using UnityLiveCreatorMcp;
using UnityProfilerMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityDevelopmentFaultInjectionTests
	{
		[SetUp]
		public void SetUp()
		{
			UnityDomainMcpPlanStore.ClearForTests();
		}

		[Test]
		public void RevisionRace_InvalidatesPreparedPlan()
		{
			long originalRevision = Session.Revision;
			UnityDomainMcpResult prepared = UnityDomainMcpPlanStore.Prepare(
				"fault.prepare",
				"fault_domain",
				"mutation",
				originalRevision,
				true,
				new JObject());
			JObject data = (JObject)prepared.data;
			Session.NotifyMutationApplied();

			bool consumed = UnityDomainMcpPlanStore.TryConsume(
				"fault.apply",
				"fault_domain",
				data.Value<string>("planId"),
				Session.Revision,
				data.Value<string>("approvalToken"),
				out _,
				out UnityDomainMcpResult failure);

			Assert.That(consumed, Is.False);
			Assert.That(failure.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.STALE_REVISION.ToString()));
		}

		[Test]
		public void ReloadEquivalentClear_InvalidatesPreparedPlan()
		{
			long revision = Session.Revision;
			UnityDomainMcpResult prepared = UnityDomainMcpPlanStore.Prepare(
				"fault.prepare",
				"fault_domain",
				"mutation",
				revision,
				true,
				new JObject());
			JObject data = (JObject)prepared.data;
			UnityDomainMcpPlanStore.ClearForTests();

			bool consumed = UnityDomainMcpPlanStore.TryConsume(
				"fault.apply",
				"fault_domain",
				data.Value<string>("planId"),
				revision,
				data.Value<string>("approvalToken"),
				out _,
				out UnityDomainMcpResult failure);

			Assert.That(consumed, Is.False);
			Assert.That(failure.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.NOT_FOUND.ToString()));
		}

		[Test]
		public void RunningBuildCancel_IsExplicitlyUnsupported()
		{
			UnityDomainMcpResult result = (UnityDomainMcpResult)BuildCancelPlayerTool.HandleCommand(new JObject());

			Assert.That(result.success, Is.False);
			Assert.That(result.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.BACKEND_NOT_IMPLEMENTED.ToString()));
		}

		[Test]
		public void ProfilerBaseline_DifferentEnvironmentIsRejected()
		{
			JObject baseline = new JObject
			{
				["environment"] = new JObject { ["fingerprint"] = "environment-a" },
				["metrics"] = new JObject()
			};
			JObject candidate = new JObject
			{
				["environment"] = new JObject { ["fingerprint"] = "environment-b" },
				["metrics"] = new JObject()
			};

			UnityDomainMcpResult result = UnityProfilerMcpRuntime.CompareBaseline(baseline, candidate);

			Assert.That(result.success, Is.False);
			Assert.That(result.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void LiveCreator_MissingRecoveryCueIsRejected()
		{
			JObject result = UnityLiveCreatorRuntime.CompileShow(
				"Fault injection show",
				new[]
				{
					new UnityLiveCreatorCueInput
					{
						cueId = "cue-a",
						atSeconds = 0.0,
						domainId = "unity_graphics_mcp",
						toolName = "graphics.inspect_project",
						toolGroup = "inspect",
						parameters = new JObject(),
						recoveryCueId = "missing-cue"
					}
				},
				10.0,
				false);

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("LIVE-RECOVERY-CUE-MISSING"));
		}
	}
}

#endif
