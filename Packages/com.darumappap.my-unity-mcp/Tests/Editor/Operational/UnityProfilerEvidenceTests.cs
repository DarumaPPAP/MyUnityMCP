#if UNITY_EDITOR

using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityDomainMcp;
using UnityGraphicsMcp;
using UnityProfilerMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityProfilerEvidenceTests
	{
		private static JObject Summary(double p95 = 20.0)
		{
			return new JObject
			{
				["environment"] = new JObject { ["fingerprint"] = "test-editor" },
				["metrics"] = new JObject
				{
					["cpu"] = new JObject
					{
						["category"] = "Internal", ["name"] = "Main Thread", ["unit"] = "Nanoseconds",
						["sampleCount"] = 10, ["p95"] = p95
					}
				}
			};
		}

		private static void AssertRejected(JObject baseline, JObject candidate)
		{
			UnityDomainMcpResult result = UnityProfilerMcpRuntime.CompareBaseline(baseline, candidate);
			Assert.That(result.success, Is.False);
			Assert.That(result.status, Is.EqualTo("INVALID_REQUEST"));
		}

		[Test]
		public void Comparison_ComputesDeltaAndDoesNotMutateInputs()
		{
			JObject baseline = Summary(20);
			JObject candidate = Summary(25);
			JToken before = candidate.DeepClone();
			UnityDomainMcpResult result = UnityProfilerMcpRuntime.CompareBaseline(baseline, candidate);
			Assert.That(result.success, Is.True, result.summary);
			JToken comparison = ((JObject)result.data)["comparisons"][0];
			Assert.That(comparison.Value<double>("delta"), Is.EqualTo(5));
			Assert.That(comparison.Value<double>("deltaPercent"), Is.EqualTo(25));
			Assert.That(JToken.DeepEquals(candidate, before), Is.True);
		}

		[Test]
		public void Comparison_ZeroBaselineHasNoPercentage()
		{
			UnityDomainMcpResult result = UnityProfilerMcpRuntime.CompareBaseline(Summary(0), Summary(2));
			Assert.That(result.success, Is.True);
			Assert.That(((JObject)result.data)["comparisons"][0]["deltaPercent"].Type, Is.EqualTo(JTokenType.Null));
		}

		[TestCase("{}")]
		[TestCase("[]")]
		[TestCase("null")]
		[TestCase("{\"other\":{}}")]
		public void Comparison_RejectsEmptyMalformedOrDifferentCounterSets(string metrics)
		{
			JObject candidate = Summary();
			candidate["metrics"] = JToken.Parse(metrics);
			AssertRejected(Summary(), candidate);
			AssertRejected(candidate, Summary());
		}

		[TestCase("category", "\"Memory\"")]
		[TestCase("name", "\"Render Thread\"")]
		[TestCase("unit", "\"Milliseconds\"")]
		[TestCase("unit", "null")]
		[TestCase("unit", "[]")]
		[TestCase("sampleCount", "0")]
		[TestCase("sampleCount", "-1")]
		[TestCase("sampleCount", "1.5")]
		[TestCase("sampleCount", "\"10\"")]
		[TestCase("p95", "null")]
		[TestCase("p95", "\"20\"")]
		[TestCase("p95", "{}")]
		[TestCase("p95", "-1")]
		public void Comparison_RejectsInvalidMetricEvidence(string field, string value)
		{
			JObject candidate = Summary();
			candidate["metrics"]["cpu"][field] = JToken.Parse(value);
			AssertRejected(Summary(), candidate);
			AssertRejected(candidate, Summary());
		}

		[Test]
		public void Comparison_RejectsMissingP95AndPartialCounterOverlap()
		{
			JObject candidate = Summary();
			((JObject)candidate["metrics"]["cpu"]).Remove("p95");
			AssertRejected(Summary(), candidate);
			candidate = Summary();
			candidate["metrics"]["memory"] = candidate["metrics"]["cpu"].DeepClone();
			AssertRejected(Summary(), candidate);
			AssertRejected(candidate, Summary());
		}

		[Test]
		public void Comparison_RejectsNonFiniteValuesAndOverflowingPercentage()
		{
			foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
			{
				AssertRejected(Summary(), Summary(value));
				AssertRejected(Summary(value), Summary());
			}
			AssertRejected(Summary(double.Epsilon), Summary(double.MaxValue));
		}

		[TestCase("null")]
		[TestCase("[]")]
		[TestCase("{\"fingerprint\":{}}")]
		[TestCase("{\"fingerprint\":\"another-editor\"}")]
		public void Comparison_RejectsMalformedOrDifferentEnvironment(string environment)
		{
			JObject candidate = Summary();
			candidate["environment"] = JToken.Parse(environment);
			AssertRejected(Summary(), candidate);
		}

		[Test]
		public void Prepare_RejectsDuplicateCounterIdsBeforeCreatingPlan()
		{
			UnityDomainMcpResult result = UnityProfilerMcpRuntime.PrepareCapture(0, 10, new[]
			{
				new UnityProfilerMcpCounterInput { counterId = "cpu", category = "Internal", name = "Main Thread" },
				new UnityProfilerMcpCounterInput { counterId = "cpu", category = "Memory", name = "Total Used Memory" }
			}, Session.Revision);
			Assert.That(result.status, Is.EqualTo("INVALID_REQUEST"));
			Assert.That(result.success, Is.False);
		}

		[Test]
		public void Summary_EvenSampleCountUsesMeanOfCentralValues()
		{
			JObject result = UnityProfilerMcpRuntime.SummarizeValues(new long[] { 100, 1, 4, 3 });
			Assert.That(result.Value<double>("median"), Is.EqualTo(3.5));
			Assert.That(result.Value<long>("p95"), Is.EqualTo(100));
			result = UnityProfilerMcpRuntime.SummarizeValues(new[] { long.MaxValue, long.MaxValue });
			Assert.That(result.Value<double>("median"), Is.EqualTo((double)long.MaxValue));
		}

		[Test]
		public void Summary_EmptySamplesAreUnavailableRatherThanZero()
		{
			JObject result = UnityProfilerMcpRuntime.SummarizeValues(Array.Empty<long>());
			Assert.That(result.Value<int>("sampleCount"), Is.Zero);
			foreach (string field in new[] { "median", "p95", "max" })
			{
				Assert.That(result[field].Type, Is.EqualTo(JTokenType.Null));
			}
		}
	}
}

#endif
