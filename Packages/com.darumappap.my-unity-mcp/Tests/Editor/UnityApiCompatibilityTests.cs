#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace UnityGraphicsMcp
{
	public sealed class UnityApiCompatibilityTests
	{
		[Test]
		public void Resolve_6000_0_UsesBaseWithoutEntityIdRule()
		{
			List<UnityApiCompatibilityRule> rules =
				UnityApiCompatibility.GetApplicableRules("6000.0.75f1");

			Assert.That(rules.Any(item => item.patchBucket == E_UNITY_API_PATCH_BUCKET.BASE), Is.True);
			Assert.That(rules.Any(item => item.ruleId == "UNITY-6000-4-OBJECT-ENTITY-ID"), Is.False);
		}

		[Test]
		public void Resolve_6000_2_ActivatesEntityIdInside6000_4MaintenanceBucket()
		{
			List<UnityApiCompatibilityRule> rules =
				UnityApiCompatibility.GetApplicableRules("6000.2.0f1");

			UnityApiCompatibilityRule entityIdRule =
				rules.Single(item => item.ruleId == "UNITY-6000-4-OBJECT-ENTITY-ID");

			Assert.That(entityIdRule.patchBucket, Is.EqualTo(E_UNITY_API_PATCH_BUCKET.UNITY_6000_4));
			Assert.That(entityIdRule.preferredFrom, Is.EqualTo("6000.2"));
		}

		[Test]
		public void Resolve_6000_4_ContainsConfirmedEntityIdAndRenderGraphBoundary()
		{
			Dictionary<string, object> summary =
				UnityApiCompatibility.BuildProjectSummary("6000.4.11f1");

			List<string> buckets = summary["maintenanceBuckets"] as List<string>;
			Assert.That(buckets, Does.Contain(E_UNITY_API_PATCH_BUCKET.BASE.ToString()));
			Assert.That(buckets, Does.Contain(E_UNITY_API_PATCH_BUCKET.UNITY_6000_4.ToString()));

			List<Dictionary<string, object>> rules =
				summary["rules"] as List<Dictionary<string, object>>;
			Assert.That(
				rules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-4-OBJECT-ENTITY-ID" &&
					(string)item["state"] == "WARNING"),
				Is.True);
			Assert.That(
				rules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-4-URP-COMPATIBILITY-MODE" &&
					(string)item["state"] == "REMOVED"),
				Is.True);
		}

		[Test]
		public void Resolve_6000_5_TreatsLegacyComponentShortcutsAsRemoved()
		{
			Dictionary<string, object> summary =
				UnityApiCompatibility.BuildProjectSummary("6000.5.0f1");
			List<Dictionary<string, object>> rules =
				summary["rules"] as List<Dictionary<string, object>>;

			Assert.That(
				rules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-5-LEGACY-COMPONENT-REMOVAL" &&
					(string)item["state"] == "REMOVED"),
				Is.True);
			Assert.That(
				rules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-5-ENTITIES-FOREACH" &&
					(string)item["state"] == "REMOVED"),
				Is.True);
		}

		[Test]
		public void Resolve_6000_6_Uses6000_7RollupInsteadOfCreating6000_6Bucket()
		{
			Dictionary<string, object> summary =
				UnityApiCompatibility.BuildProjectSummary("6000.6.0a7");
			List<string> buckets = summary["maintenanceBuckets"] as List<string>;
			List<Dictionary<string, object>> rules =
				summary["rules"] as List<Dictionary<string, object>>;

			Assert.That(buckets, Does.Contain(E_UNITY_API_PATCH_BUCKET.UNITY_6000_7.ToString()));
			Assert.That(buckets.Any(item => item.Contains("6000_6")), Is.False);
			Assert.That(
				rules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-7-ROLLUP-UXML-FACTORY"),
				Is.True);
		}

		[Test]
		public void Resolve_6000_4_And_6000_5_TrackConfirmedSceneHandleBoundary()
		{
			Dictionary<string, object> warningSummary =
				UnityApiCompatibility.BuildProjectSummary("6000.4.12f1");
			List<Dictionary<string, object>> warningRules =
				warningSummary["rules"] as List<Dictionary<string, object>>;
			Dictionary<string, object> errorSummary =
				UnityApiCompatibility.BuildProjectSummary("6000.5.5f1");
			List<Dictionary<string, object>> errorRules =
				errorSummary["rules"] as List<Dictionary<string, object>>;

			Assert.That(
				warningRules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-4-SCENE-HANDLE-RAW-DATA" &&
					(string)item["state"] == "WARNING" &&
					(string)item["sourceStatus"] == E_UNITY_API_SOURCE_STATUS.CONFIRMED.ToString()),
				Is.True);
			Assert.That(
				errorRules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-4-SCENE-HANDLE-RAW-DATA" &&
					(string)item["state"] == "ERROR"),
				Is.True);
		}

		[Test]
		public void Resolve_6000_7_ExposesPlannedRenderGraphBehaviorChanges()
		{
			Dictionary<string, object> summary =
				UnityApiCompatibility.BuildProjectSummary("6000.7.0a2");
			List<Dictionary<string, object>> rules =
				summary["rules"] as List<Dictionary<string, object>>;

			Assert.That(
				rules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-7-RENDERGRAPH-Y-FLIP" &&
					(string)item["state"] == "PLANNED_BEHAVIOR_CHANGE"),
				Is.True);
			Assert.That(
				rules.Any(item =>
					(string)item["ruleId"] == "UNITY-6000-7-RENDERGRAPH-BLIT-SLICE" &&
					(string)item["sourceStatus"] == E_UNITY_API_SOURCE_STATUS.PLANNED.ToString()),
				Is.True);
		}

		[Test]
		public void ParseUnityVersion_StripsAlphaBetaAndFinalSuffix()
		{
			Assert.That(UnityApiCompatibility.ParseUnityVersion("6000.7.0a2").ToString(3), Is.EqualTo("6000.7.0"));
			Assert.That(UnityApiCompatibility.ParseUnityVersion("6000.4.11f1").ToString(3), Is.EqualTo("6000.4.11"));
		}

		[Test]
		public void InspectProject_ExposesApiCompatibilityInDetectedProject()
		{
			UnityGraphicsMcpToolResult result = UnityGraphicsMcpInspection.InspectProject(
				"test-api-compatibility-project-context",
				new string[0],
				new string[0]);

			Assert.That(result.IsSuccessful, Is.True);
			Dictionary<string, object> data = result.data as Dictionary<string, object>;
			Dictionary<string, object> detectedProject =
				data["detectedProject"] as Dictionary<string, object>;
			Dictionary<string, object> compatibility =
				detectedProject["apiCompatibility"] as Dictionary<string, object>;

			Assert.That(compatibility, Is.Not.Null);
			Assert.That(
				compatibility["policy"],
				Is.EqualTo("BASE_THEN_6000_4_THEN_6000_5_THEN_6000_7_ROLLUP"));
		}
	}
}

#endif