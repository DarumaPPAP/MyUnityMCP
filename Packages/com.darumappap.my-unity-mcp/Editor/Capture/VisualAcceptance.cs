#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityGraphicsMcp
{
	public enum E_GRAPHICS_VISUAL_EVALUATION_DECISION
	{
		PASSED,
		FAILED,
		INCOMPLETE
	}

	public sealed class AcceptanceCriterionInput
	{
		public string criterionId { get; set; }
		public string displayName { get; set; }
		public double weight { get; set; }
		public double minimumScore { get; set; }
		public double? criticalFailureBelow { get; set; }
		public bool? required { get; set; }
		public string[] recommendedActions { get; set; }
	}

	public sealed class PerformanceBudgetInput
	{
		public double? maxCpuFrameMs { get; set; }
		public double? maxGpuFrameMs { get; set; }
		public double? maxMemoryMb { get; set; }
		public int? maxDrawCalls { get; set; }
		public bool? required { get; set; }
	}

	public sealed class AcceptanceProfileInput
	{
		public string profileName { get; set; }
		public double minimumPassScore { get; set; }
		public AcceptanceCriterionInput[] criteria { get; set; }
		public string referenceCaptureId { get; set; }
		public string referenceEvidenceDigest { get; set; }
		public PerformanceBudgetInput performanceBudget { get; set; }
	}

	public sealed class EvaluationMeasurementInput
	{
		public string criterionId { get; set; }
		public double score { get; set; }
		public double? confidence { get; set; }
		public string summary { get; set; }
		public int[] affectedObjectIds { get; set; }
		public string[] evidence { get; set; }
	}

	public sealed class PerformanceMeasurementInput
	{
		public double? cpuFrameMs { get; set; }
		public double? gpuFrameMs { get; set; }
		public double? memoryMb { get; set; }
		public int? drawCalls { get; set; }
		public string source { get; set; }
	}

	internal sealed class AcceptanceCriterion
	{
		public string CriterionId { get; set; }
		public string DisplayName { get; set; }
		public double Weight { get; set; }
		public double MinimumScore { get; set; }
		public double? CriticalFailureBelow { get; set; }
		public bool Required { get; set; }
		public List<string> RecommendedActions { get; set; } = new List<string>();
	}

	internal sealed class PerformanceBudget
	{
		public double? MaxCpuFrameMs { get; set; }
		public double? MaxGpuFrameMs { get; set; }
		public double? MaxMemoryMb { get; set; }
		public int? MaxDrawCalls { get; set; }
		public bool Required { get; set; }
	}

	internal sealed class AcceptanceProfile
	{
		public string ProfileId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public string ProfileName { get; set; }
		public double MinimumPassScore { get; set; }
		public List<AcceptanceCriterion> Criteria { get; set; } =
			new List<AcceptanceCriterion>();
		public string ReferenceCaptureId { get; set; }
		public string ReferenceEvidenceDigest { get; set; }
		public PerformanceBudget PerformanceBudget { get; set; }
		public string ProfileDigest { get; set; }
	}

	internal sealed class EvaluationCriterionResult
	{
		public string CriterionId { get; set; }
		public string DisplayName { get; set; }
		public double Score { get; set; }
		public double Weight { get; set; }
		public double WeightedContribution { get; set; }
		public double MinimumScore { get; set; }
		public double? CriticalFailureBelow { get; set; }
		public double Confidence { get; set; }
		public bool PassedMinimum { get; set; }
		public bool CriticalFailure { get; set; }
		public bool MeasurementPresent { get; set; }
		public string Summary { get; set; }
		public List<int> AffectedObjectIds { get; set; } = new List<int>();
		public List<string> Evidence { get; set; } = new List<string>();
		public List<string> RecommendedActions { get; set; } = new List<string>();
	}

	internal sealed class VisualEvaluationRecord
	{
		public string EvaluationId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public string CaptureId { get; set; }
		public string EvidenceDigest { get; set; }
		public string ProfileId { get; set; }
		public string ProfileDigest { get; set; }
		public string Decision { get; set; }
		public double WeightedScore { get; set; }
		public bool MeetsWeightedThreshold { get; set; }
		public bool HasCriticalFailure { get; set; }
		public bool HasIncompleteRequiredEvidence { get; set; }
		public List<EvaluationCriterionResult> Criteria { get; set; } =
			new List<EvaluationCriterionResult>();
		public List<Dictionary<string, object>> PerformanceFailures { get; set; } =
			new List<Dictionary<string, object>>();
		public List<Dictionary<string, object>> AffectedObjects { get; set; } =
			new List<Dictionary<string, object>>();
		public Dictionary<string, object> RefineDirection { get; set; } =
			new Dictionary<string, object>();
		public string EvaluationDigest { get; set; }
	}

	[InitializeOnLoad]
	internal static class VisualAcceptanceSession
	{
		private const int MAX_PROFILE_COUNT = 16;
		private const int MAX_EVALUATION_COUNT = 32;
		private static readonly TimeSpan RECORD_LIFETIME = TimeSpan.FromHours(2.0);
		private static readonly Dictionary<string, AcceptanceProfile> _profiles =
			new Dictionary<string, AcceptanceProfile>(StringComparer.Ordinal);
		private static readonly Dictionary<string, VisualEvaluationRecord> _evaluations =
			new Dictionary<string, VisualEvaluationRecord>(StringComparer.Ordinal);

		static VisualAcceptanceSession()
		{
			EditorApplication.playModeStateChanged += state => Clear();
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			CompilationPipeline.compilationStarted += context => Clear();
			EditorApplication.quitting += Clear;
		}

		public static string StoreProfile(AcceptanceProfile profile)
		{
			RemoveExpiredRecords();
			while (_profiles.Count >= MAX_PROFILE_COUNT)
			{
				string oldest = _profiles.OrderBy(item => item.Value.CreatedUtc).First().Key;
				_profiles.Remove(oldest);
			}
			profile.ProfileId = Session.SessionId +
				":acceptance-profile:" + Guid.NewGuid().ToString("N");
			profile.CreatedUtc = DateTime.UtcNow;
			_profiles[profile.ProfileId] = profile;
			return profile.ProfileId;
		}

		public static bool TryGetProfile(
			string profileId,
			long expectedRevision,
			out AcceptanceProfile profile,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			profile = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;
			RemoveExpiredRecords();
			if (string.IsNullOrWhiteSpace(profileId) ||
				!profileId.StartsWith(
					Session.SessionId + ":acceptance-profile:",
					StringComparison.Ordinal) ||
				!_profiles.TryGetValue(profileId, out profile))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Acceptance Profileが現在のEditor Sessionに存在しません。";
				return false;
			}
			if (profile.Revision != expectedRevision ||
				expectedRevision != Session.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Acceptance Profile作成後にEditor Revisionが変更されました。";
				return false;
			}
			return true;
		}

		public static string StoreEvaluation(VisualEvaluationRecord evaluation)
		{
			RemoveExpiredRecords();
			while (_evaluations.Count >= MAX_EVALUATION_COUNT)
			{
				string oldest = _evaluations.OrderBy(item => item.Value.CreatedUtc).First().Key;
				_evaluations.Remove(oldest);
			}
			evaluation.EvaluationId = Session.SessionId +
				":visual-evaluation:" + Guid.NewGuid().ToString("N");
			evaluation.CreatedUtc = DateTime.UtcNow;
			_evaluations[evaluation.EvaluationId] = evaluation;
			return evaluation.EvaluationId;
		}

		public static bool TryGetEvaluation(
			string evaluationId,
			long expectedRevision,
			out VisualEvaluationRecord evaluation,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			evaluation = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;
			RemoveExpiredRecords();
			if (string.IsNullOrWhiteSpace(evaluationId) ||
				!evaluationId.StartsWith(
					Session.SessionId + ":visual-evaluation:",
					StringComparison.Ordinal) ||
				!_evaluations.TryGetValue(evaluationId, out evaluation))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Visual Evaluationが現在のEditor Sessionに存在しません。";
				return false;
			}
			if (evaluation.Revision != expectedRevision ||
				expectedRevision != Session.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Visual Evaluation後にEditor Revisionが変更されました。";
				return false;
			}
			return true;
		}

		public static void ClearForTests()
		{
			Clear();
		}

		private static void RemoveExpiredRecords()
		{
			DateTime threshold = DateTime.UtcNow - RECORD_LIFETIME;
			foreach (string id in _profiles
				.Where(item => item.Value.CreatedUtc < threshold)
				.Select(item => item.Key)
				.ToArray())
			{
				_profiles.Remove(id);
			}
			foreach (string id in _evaluations
				.Where(item => item.Value.CreatedUtc < threshold)
				.Select(item => item.Key)
				.ToArray())
			{
				_evaluations.Remove(id);
			}
		}

		private static void Clear()
		{
			_profiles.Clear();
			_evaluations.Clear();
		}
	}

	public static partial class Inspection
	{
		private const int MAX_ACCEPTANCE_CRITERIA = 32;

		public static ToolResult PrepareAcceptanceProfile(
			string requestId,
			long? expectedRevision,
			AcceptanceProfileInput input)
		{
			return ExecuteReadOnly(
				"graphics.prepare_acceptance_profile",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.prepare_acceptance_profile",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}
					if (expectedRevision.Value != Session.Revision)
					{
						return CreateResult(
							"graphics.prepare_acceptance_profile",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"expectedRevisionが現在のEditor Revisionと一致しません。",
							null);
					}
					if (input == null ||
						string.IsNullOrWhiteSpace(input.profileName) ||
						input.criteria == null ||
						input.criteria.Length == 0 ||
						input.criteria.Length > MAX_ACCEPTANCE_CRITERIA ||
						!IsApvVisualAcceptanceScore(input.minimumPassScore))
					{
						return CreateResult(
							"graphics.prepare_acceptance_profile",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Profile名、1～32件の評価項目、0～100の最低合格値を指定してください。",
							null);
					}

					HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
					List<AcceptanceCriterion> criteria =
						new List<AcceptanceCriterion>();
					foreach (AcceptanceCriterionInput criterion in input.criteria)
					{
						string id = criterion == null || string.IsNullOrWhiteSpace(criterion.criterionId)
							? string.Empty
							: criterion.criterionId.Trim();
						if (id.Length == 0 || id.Length > 64 || !ids.Add(id) ||
							criterion.weight <= 0.0 || !IsApvVisualAcceptanceScore(criterion.minimumScore) ||
							(criterion.criticalFailureBelow.HasValue &&
							 !IsApvVisualAcceptanceScore(criterion.criticalFailureBelow.Value)))
						{
							return CreateResult(
								"graphics.prepare_acceptance_profile",
								requestId,
								E_MCP_TOOL_STATUS.INVALID_REQUEST,
								"criterionId重複、Weight、最低値、Critical Failure条件を確認してください。",
								new Dictionary<string, object> { { "criterionId", id } });
						}

						criteria.Add(new AcceptanceCriterion
						{
							CriterionId = id,
							DisplayName = string.IsNullOrWhiteSpace(criterion.displayName)
								? id
								: criterion.displayName.Trim(),
							Weight = criterion.weight,
							MinimumScore = criterion.minimumScore,
							CriticalFailureBelow = criterion.criticalFailureBelow,
							Required = criterion.required ?? true,
							RecommendedActions = NormalizeApvVisualAcceptanceTextValues(criterion.recommendedActions)
						});
					}

					string referenceCaptureId = null;
					string referenceDigest = null;
					if (!string.IsNullOrWhiteSpace(input.referenceCaptureId) ||
						!string.IsNullOrWhiteSpace(input.referenceEvidenceDigest))
					{
						CaptureEvidenceRecord referenceCapture;
						E_MCP_TOOL_STATUS referenceFailureStatus;
						string referenceFailureMessage;
						if (!CaptureEvidenceSession.TryGetCapture(
							input.referenceCaptureId,
							expectedRevision.Value,
							input.referenceEvidenceDigest,
							out referenceCapture,
							out referenceFailureStatus,
							out referenceFailureMessage))
						{
							return CreateResult(
								"graphics.prepare_acceptance_profile",
								requestId,
								referenceFailureStatus,
								referenceFailureMessage,
								null);
						}
						referenceCaptureId = referenceCapture.CaptureId;
						referenceDigest = referenceCapture.EvidenceDigest;
					}

					PerformanceBudget budget;
					string budgetFailure;
					if (!TryBuildApvVisualAcceptancePerformanceBudget(
						input.performanceBudget,
						out budget,
						out budgetFailure))
					{
						return CreateResult(
							"graphics.prepare_acceptance_profile",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							budgetFailure,
							null);
					}

					AcceptanceProfile profile =
						new AcceptanceProfile
						{
							Revision = expectedRevision.Value,
							ProfileName = input.profileName.Trim(),
							MinimumPassScore = input.minimumPassScore,
							Criteria = criteria,
							ReferenceCaptureId = referenceCaptureId,
							ReferenceEvidenceDigest = referenceDigest,
							PerformanceBudget = budget
						};
					VisualAcceptanceSession.StoreProfile(profile);
					profile.ProfileDigest = BuildApvVisualAcceptanceProfileDigest(profile);

					return CreateResult(
						"graphics.prepare_acceptance_profile",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Acceptance Profileの評価項目、Weight、合格値、Critical Failure、Reference、Performance Budgetを固定しました。",
						BuildApvVisualAcceptanceProfileData(profile));
				});
		}

		public static ToolResult EvaluateCapture(
			string requestId,
			string captureId,
			long? expectedRevision,
			string evidenceDigest,
			string profileId,
			EvaluationMeasurementInput[] measurements,
			PerformanceMeasurementInput performance)
		{
			return ExecuteReadOnly(
				"graphics.evaluate_capture",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.evaluate_capture",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					CaptureEvidenceRecord capture;
					E_MCP_TOOL_STATUS failureStatus;
					string failureMessage;
					if (!CaptureEvidenceSession.TryGetCapture(
						captureId,
						expectedRevision.Value,
						evidenceDigest,
						out capture,
						out failureStatus,
						out failureMessage))
					{
						return CreateResult(
							"graphics.evaluate_capture",
							requestId,
							failureStatus,
							failureMessage,
							null);
					}

					AcceptanceProfile profile;
					if (!VisualAcceptanceSession.TryGetProfile(
						profileId,
						expectedRevision.Value,
						out profile,
						out failureStatus,
						out failureMessage))
					{
						return CreateResult(
							"graphics.evaluate_capture",
							requestId,
							failureStatus,
							failureMessage,
							null);
					}

					Dictionary<string, EvaluationMeasurementInput> measurementMap;
					if (!TryBuildApvVisualAcceptanceMeasurementMap(
						measurements,
						profile,
						out measurementMap,
						out failureMessage))
					{
						return CreateResult(
							"graphics.evaluate_capture",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							failureMessage,
							null);
					}

					VisualEvaluationRecord evaluation =
						BuildApvVisualAcceptanceEvaluation(
							capture,
							profile,
							measurementMap,
							performance);
					VisualAcceptanceSession.StoreEvaluation(evaluation);
					evaluation.EvaluationDigest = BuildApvVisualAcceptanceEvaluationDigest(evaluation);
					evaluation.RefineDirection = BuildApvVisualAcceptanceRefineDirection(evaluation);

					E_MCP_TOOL_STATUS resultStatus = evaluation.Decision ==
						E_GRAPHICS_VISUAL_EVALUATION_DECISION.INCOMPLETE.ToString()
						? E_MCP_TOOL_STATUS.PARTIAL
						: E_MCP_TOOL_STATUS.SUCCESS;
					ToolResult result = CreateResult(
						"graphics.evaluate_capture",
						requestId,
						resultStatus,
						evaluation.Decision == E_GRAPHICS_VISUAL_EVALUATION_DECISION.PASSED.ToString()
							? "CaptureはAcceptance Profileの自動評価条件を満たしました。Human Acceptanceは別途必要です。"
							: "CaptureはAcceptance Profileの不合格またはEvidence不足理由を構造化しました。",
						BuildApvVisualAcceptanceEvaluationData(evaluation));
					foreach (EvaluationCriterionResult criterion in evaluation.Criteria
						.Where(item => !item.MeasurementPresent || !item.PassedMinimum || item.CriticalFailure))
					{
						result.issues.Add(new Issue
						{
							code = criterion.CriticalFailure
								? "VISUAL_CRITICAL_FAILURE"
								: !criterion.MeasurementPresent
									? "VISUAL_MEASUREMENT_MISSING"
									: "VISUAL_CRITERION_BELOW_MINIMUM",
							message = criterion.DisplayName + ": " + criterion.Summary,
							evidence = BuildApvVisualAcceptanceCriterionData(criterion)
						});
					}
					return result;
				});
		}

		public static ToolResult RefineFromEvaluation(
			string requestId,
			string directionPlanId,
			string evaluationId,
			long? expectedRevision)
		{
			return ExecuteReadOnly(
				"graphics.refine_from_evaluation",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.refine_from_evaluation",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					DirectionPlan sourcePlan;
					E_MCP_TOOL_STATUS failureStatus;
					if (!Session.TryGetPlan(
						directionPlanId,
						expectedRevision.Value,
						out sourcePlan,
						out failureStatus))
					{
						return CreateResult(
							"graphics.refine_from_evaluation",
							requestId,
							failureStatus,
							"Direction Planを現在Revisionで解決できません。",
							null);
					}

					VisualEvaluationRecord evaluation;
					string failureMessage;
					if (!VisualAcceptanceSession.TryGetEvaluation(
						evaluationId,
						expectedRevision.Value,
						out evaluation,
						out failureStatus,
						out failureMessage))
					{
						return CreateResult(
							"graphics.refine_from_evaluation",
							requestId,
							failureStatus,
							failureMessage,
							null);
					}
					if (evaluation.Decision ==
						E_GRAPHICS_VISUAL_EVALUATION_DECISION.PASSED.ToString())
					{
						return CreateResult(
							"graphics.refine_from_evaluation",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"PASSED EvaluationからRefine Directionは作成しません。",
							BuildApvVisualAcceptanceEvaluationData(evaluation));
					}

					Dictionary<string, object> intent = new Dictionary<string, object>(
						sourcePlan.VisualIntent ?? new Dictionary<string, object>());
					intent["refinementSourcePlanId"] = directionPlanId;
					intent["visualEvaluationId"] = evaluation.EvaluationId;
					intent["visualEvaluationDigest"] = evaluation.EvaluationDigest;
					intent["captureId"] = evaluation.CaptureId;
					intent["captureEvidenceDigest"] = evaluation.EvidenceDigest;
					intent["acceptanceProfileId"] = evaluation.ProfileId;
					intent["evaluationDecision"] = evaluation.Decision;
					intent["structuredRefineDirection"] = evaluation.RefineDirection;
					intent["humanReviewRequired"] = true;

					List<PlanRecommendation> recommendations =
						(sourcePlan.Recommendations ?? new List<PlanRecommendation>())
						.Select(CloneSaveEvaluationRecommendation)
						.ToList();
					recommendations.Add(new PlanRecommendation
					{
						recommendationId = "EVAL-REFINE-" + Guid.NewGuid().ToString("N"),
						section = "LOOK",
						recommendedValue = evaluation.RefineDirection,
						reason = "Acceptance Profileの不合格理由、Performance Budget、Object ID関連を次Iterationへ反映します。",
						dependencies = new List<string>
						{
							directionPlanId,
							evaluation.EvaluationId,
							evaluation.CaptureId,
							evaluation.ProfileId
						},
						confidence = E_GRAPHICS_PLAN_CONFIDENCE.HIGH.ToString(),
						pipelineImpact = "PLAN_ONLY",
						platformImpact = new List<string>(),
						verificationLevel = E_GRAPHICS_PLAN_VERIFICATION.HUMAN_REVIEW_REQUIRED.ToString(),
						nativeMutationBackendStatus = "NOT_REQUESTED"
					});

					DirectionPlan refined = new DirectionPlan
					{
						Revision = expectedRevision.Value,
						CreatedUtc = DateTime.UtcNow,
						ProjectContext = new Dictionary<string, object>(
							sourcePlan.ProjectContext ?? new Dictionary<string, object>()),
						VisualIntent = intent,
						Recommendations = recommendations,
						Issues = new List<Issue>(
							sourcePlan.Issues ?? new List<Issue>())
					};
					refined.Issues.Add(new Issue
					{
						code = "VISUAL_EVALUATION_REQUIRES_REFINEMENT",
						message = "Visual Evaluationが不合格またはEvidence不足のため次Iterationを要求します。",
						evidence = evaluation.RefineDirection
					});
					Session.StorePlan(refined);

					ToolResult result = CreateResult(
						"graphics.refine_from_evaluation",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Visual Evaluationの不合格理由をObject IDとPerformance Budgetを含む次のRefine Directionへ構造化しました。",
						new Dictionary<string, object>
						{
							{ "sourcePlanId", directionPlanId },
							{ "evaluationId", evaluation.EvaluationId },
							{ "captureId", evaluation.CaptureId },
							{ "profileId", evaluation.ProfileId },
							{ "planId", refined.PlanId },
							{ "decision", evaluation.Decision },
							{ "refineDirection", evaluation.RefineDirection },
							{ "visualAccepted", false },
							{ "humanReviewRequired", true },
							{ "mutationApplied", false },
							{ "savePerformed", false },
							{ "bakePerformed", false }
						});
					result.issues.AddRange(refined.Issues);
					return result;
				});
		}

		private static VisualEvaluationRecord BuildApvVisualAcceptanceEvaluation(
			CaptureEvidenceRecord capture,
			AcceptanceProfile profile,
			Dictionary<string, EvaluationMeasurementInput> measurementMap,
			PerformanceMeasurementInput performance)
		{
			VisualEvaluationRecord evaluation =
				new VisualEvaluationRecord
				{
					Revision = Session.Revision,
					CaptureId = capture.CaptureId,
					EvidenceDigest = capture.EvidenceDigest,
					ProfileId = profile.ProfileId,
					ProfileDigest = profile.ProfileDigest
				};

			double totalWeight = profile.Criteria.Sum(item => item.Weight);
			double weightedTotal = 0.0;
			foreach (AcceptanceCriterion criterion in profile.Criteria)
			{
				EvaluationMeasurementInput measurement;
				bool present = measurementMap.TryGetValue(criterion.CriterionId, out measurement);
				EvaluationCriterionResult item =
					new EvaluationCriterionResult
					{
						CriterionId = criterion.CriterionId,
						DisplayName = criterion.DisplayName,
						Weight = criterion.Weight,
						MinimumScore = criterion.MinimumScore,
						CriticalFailureBelow = criterion.CriticalFailureBelow,
						MeasurementPresent = present,
						Score = present ? measurement.score : 0.0,
						Confidence = present ? (measurement.confidence ?? 1.0) : 0.0,
						Summary = present && !string.IsNullOrWhiteSpace(measurement.summary)
							? measurement.summary.Trim()
							: present ? "明示Measurementを受領しました。" : "必須Measurementがありません。",
						AffectedObjectIds = present && measurement.affectedObjectIds != null
							? measurement.affectedObjectIds.Where(value => value > 0).Distinct().ToList()
							: new List<int>(),
						Evidence = present ? NormalizeApvVisualAcceptanceTextValues(measurement.evidence) : new List<string>(),
						RecommendedActions = new List<string>(criterion.RecommendedActions)
					};
				item.PassedMinimum = present && item.Score >= item.MinimumScore;
				item.CriticalFailure = present &&
					item.CriticalFailureBelow.HasValue &&
					item.Score < item.CriticalFailureBelow.Value;
				item.WeightedContribution = present && totalWeight > 0.0
					? item.Score * item.Weight / totalWeight
					: 0.0;
				weightedTotal += item.WeightedContribution;
				evaluation.Criteria.Add(item);
				if (criterion.Required && !present)
				{
					evaluation.HasIncompleteRequiredEvidence = true;
				}
				if (item.CriticalFailure)
				{
					evaluation.HasCriticalFailure = true;
				}
			}

			evaluation.WeightedScore = Math.Round(weightedTotal, 4);
			evaluation.MeetsWeightedThreshold =
				evaluation.WeightedScore >= profile.MinimumPassScore;
			evaluation.PerformanceFailures = EvaluateApvVisualAcceptancePerformance(
				profile.PerformanceBudget,
				performance,
				out bool performanceIncomplete);
			evaluation.HasIncompleteRequiredEvidence |= performanceIncomplete;
			evaluation.AffectedObjects = ResolveApvVisualAcceptanceAffectedObjects(capture, evaluation.Criteria);

			bool criterionFailure = evaluation.Criteria.Any(item =>
				item.MeasurementPresent && !item.PassedMinimum);
			if (evaluation.HasIncompleteRequiredEvidence)
			{
				evaluation.Decision = E_GRAPHICS_VISUAL_EVALUATION_DECISION.INCOMPLETE.ToString();
			}
			else if (evaluation.HasCriticalFailure ||
				criterionFailure ||
				!evaluation.MeetsWeightedThreshold ||
				evaluation.PerformanceFailures.Count > 0)
			{
				evaluation.Decision = E_GRAPHICS_VISUAL_EVALUATION_DECISION.FAILED.ToString();
			}
			else
			{
				evaluation.Decision = E_GRAPHICS_VISUAL_EVALUATION_DECISION.PASSED.ToString();
			}
			return evaluation;
		}

		private static List<Dictionary<string, object>> EvaluateApvVisualAcceptancePerformance(
			PerformanceBudget budget,
			PerformanceMeasurementInput measurement,
			out bool incomplete)
		{
			incomplete = false;
			List<Dictionary<string, object>> failures =
				new List<Dictionary<string, object>>();
			if (budget == null)
			{
				return failures;
			}
			if (measurement == null)
			{
				incomplete = budget.Required;
				return failures;
			}

			AddApvVisualAcceptancePerformanceFailure(
				failures,
				"CPU_FRAME_MS",
				measurement.cpuFrameMs,
				budget.MaxCpuFrameMs,
				measurement.source);
			AddApvVisualAcceptancePerformanceFailure(
				failures,
				"GPU_FRAME_MS",
				measurement.gpuFrameMs,
				budget.MaxGpuFrameMs,
				measurement.source);
			AddApvVisualAcceptancePerformanceFailure(
				failures,
				"MEMORY_MB",
				measurement.memoryMb,
				budget.MaxMemoryMb,
				measurement.source);
			if (budget.MaxDrawCalls.HasValue)
			{
				if (!measurement.drawCalls.HasValue)
				{
					incomplete |= budget.Required;
				}
				else if (measurement.drawCalls.Value > budget.MaxDrawCalls.Value)
				{
					failures.Add(new Dictionary<string, object>
					{
						{ "metric", "DRAW_CALLS" },
						{ "measured", measurement.drawCalls.Value },
						{ "maximum", budget.MaxDrawCalls.Value },
						{ "source", measurement.source }
					});
				}
			}
			return failures;
		}

		private static void AddApvVisualAcceptancePerformanceFailure(
			List<Dictionary<string, object>> failures,
			string metric,
			double? measured,
			double? maximum,
			string source)
		{
			if (maximum.HasValue && measured.HasValue && measured.Value > maximum.Value)
			{
				failures.Add(new Dictionary<string, object>
				{
					{ "metric", metric },
					{ "measured", measured.Value },
					{ "maximum", maximum.Value },
					{ "source", source }
				});
			}
		}

		private static List<Dictionary<string, object>> ResolveApvVisualAcceptanceAffectedObjects(
			CaptureEvidenceRecord capture,
			IEnumerable<EvaluationCriterionResult> criteria)
		{
			HashSet<int> requestedIds = new HashSet<int>(criteria
				.SelectMany(item => item.AffectedObjectIds)
				.Where(value => value > 0));
			if (requestedIds.Count == 0)
			{
				return new List<Dictionary<string, object>>();
			}

			CaptureArtifactRecord mapArtifact = capture.Artifacts
				.FirstOrDefault(item => item.Channel == "OBJECT_ID_MAP");
			if (mapArtifact == null || string.IsNullOrWhiteSpace(mapArtifact.OutputPath))
			{
				return requestedIds.Select(value => new Dictionary<string, object>
				{
					{ "objectId", value },
					{ "mappingStatus", "OBJECT_ID_MAP_NOT_AVAILABLE" }
				}).ToList();
			}

			try
			{
				string absolutePath = Path.GetFullPath(Path.Combine(
					Directory.GetParent(Application.dataPath).FullName,
					mapArtifact.OutputPath));
				if (!File.Exists(absolutePath))
				{
					throw new FileNotFoundException("Object ID Mapがありません。", absolutePath);
				}
				List<ObjectIdEntry> entries =
					JsonConvert.DeserializeObject<List<ObjectIdEntry>>(
						File.ReadAllText(absolutePath, Encoding.UTF8)) ??
					new List<ObjectIdEntry>();
				Dictionary<int, ObjectIdEntry> map = entries
					.GroupBy(item => item.ObjectId)
					.ToDictionary(group => group.Key, group => group.First());
				return requestedIds.OrderBy(value => value).Select(value =>
				{
					ObjectIdEntry entry;
					if (!map.TryGetValue(value, out entry))
					{
						return new Dictionary<string, object>
						{
							{ "objectId", value },
							{ "mappingStatus", "NOT_FOUND" }
						};
					}
					return new Dictionary<string, object>
					{
						{ "objectId", entry.ObjectId },
						{ "rendererObjectId", entry.RendererObjectId },
						{ "rendererType", entry.RendererType },
						{ "name", entry.Name },
						{ "hierarchyPath", entry.HierarchyPath },
						{ "scenePath", entry.ScenePath },
						{ "mappingStatus", "RESOLVED" }
					};
				}).ToList();
			}
			catch (Exception exception)
			{
				return requestedIds.OrderBy(value => value).Select(value =>
					new Dictionary<string, object>
					{
						{ "objectId", value },
						{ "mappingStatus", "MAP_READ_FAILED" },
						{ "message", exception.Message }
					}).ToList();
			}
		}

		private static Dictionary<string, object> BuildApvVisualAcceptanceRefineDirection(
			VisualEvaluationRecord evaluation)
		{
			List<Dictionary<string, object>> failedCriteria = evaluation.Criteria
				.Where(item => !item.MeasurementPresent || !item.PassedMinimum || item.CriticalFailure)
				.Select(BuildApvVisualAcceptanceCriterionData)
				.ToList();
			List<string> actions = evaluation.Criteria
				.Where(item => !item.MeasurementPresent || !item.PassedMinimum || item.CriticalFailure)
				.SelectMany(item => item.RecommendedActions)
				.Distinct(StringComparer.Ordinal)
				.ToList();
			return new Dictionary<string, object>
			{
				{ "schemaVersion", "1.0" },
				{ "priority", evaluation.HasCriticalFailure ? "CRITICAL" : evaluation.Decision == E_GRAPHICS_VISUAL_EVALUATION_DECISION.INCOMPLETE.ToString() ? "BLOCKED" : "NORMAL" },
				{ "evaluationId", evaluation.EvaluationId },
				{ "evaluationDigest", evaluation.EvaluationDigest },
				{ "captureId", evaluation.CaptureId },
				{ "evidenceDigest", evaluation.EvidenceDigest },
				{ "profileId", evaluation.ProfileId },
				{ "decision", evaluation.Decision },
				{ "weightedScore", evaluation.WeightedScore },
				{ "failedCriteria", failedCriteria },
				{ "performanceFailures", evaluation.PerformanceFailures },
				{ "affectedObjects", evaluation.AffectedObjects },
				{ "recommendedActions", actions },
				{ "requiredRecaptureChannels", new[] { "COLOR", "LINEAR_DEPTH", "OBJECT_ID" } },
				{ "referenceComparisonPerformedByUnity", false },
				{ "humanReviewRequired", true },
				{ "acceptanceConfirmationRequired", "VISUAL_ACCEPTED" }
			};
		}

		private static Dictionary<string, object> BuildApvVisualAcceptanceProfileData(
			AcceptanceProfile profile)
		{
			return new Dictionary<string, object>
			{
				{ "profileId", profile.ProfileId },
				{ "profileDigest", profile.ProfileDigest },
				{ "expectedRevision", profile.Revision },
				{ "profileName", profile.ProfileName },
				{ "minimumPassScore", profile.MinimumPassScore },
				{ "criteria", profile.Criteria.Select(item => new Dictionary<string, object>
					{
						{ "criterionId", item.CriterionId },
						{ "displayName", item.DisplayName },
						{ "weight", item.Weight },
						{ "minimumScore", item.MinimumScore },
						{ "criticalFailureBelow", item.CriticalFailureBelow },
						{ "required", item.Required },
						{ "recommendedActions", item.RecommendedActions }
					}).ToList() },
				{ "referenceCaptureId", profile.ReferenceCaptureId },
				{ "referenceEvidenceDigest", profile.ReferenceEvidenceDigest },
				{ "performanceBudget", BuildApvVisualAcceptancePerformanceBudgetData(profile.PerformanceBudget) },
				{ "referenceComparisonPerformedByUnity", false },
				{ "imageMeaningAnalysisPerformedByUnity", false },
				{ "humanReviewRequired", true },
				{ "mutationApplied", false }
			};
		}

		private static Dictionary<string, object> BuildApvVisualAcceptanceEvaluationData(
			VisualEvaluationRecord evaluation)
		{
			return new Dictionary<string, object>
			{
				{ "evaluationId", evaluation.EvaluationId },
				{ "evaluationDigest", evaluation.EvaluationDigest },
				{ "captureId", evaluation.CaptureId },
				{ "evidenceDigest", evaluation.EvidenceDigest },
				{ "profileId", evaluation.ProfileId },
				{ "profileDigest", evaluation.ProfileDigest },
				{ "decision", evaluation.Decision },
				{ "weightedScore", evaluation.WeightedScore },
				{ "meetsWeightedThreshold", evaluation.MeetsWeightedThreshold },
				{ "hasCriticalFailure", evaluation.HasCriticalFailure },
				{ "hasIncompleteRequiredEvidence", evaluation.HasIncompleteRequiredEvidence },
				{ "criteria", evaluation.Criteria.Select(BuildApvVisualAcceptanceCriterionData).ToList() },
				{ "performanceFailures", evaluation.PerformanceFailures },
				{ "affectedObjects", evaluation.AffectedObjects },
				{ "refineDirection", evaluation.RefineDirection },
				{ "automatedProfilePassed", evaluation.Decision == E_GRAPHICS_VISUAL_EVALUATION_DECISION.PASSED.ToString() },
				{ "visualAccepted", false },
				{ "humanReviewRequired", true },
				{ "imageMeaningAnalysisPerformedByUnity", false },
				{ "mutationApplied", false },
				{ "savePerformed", false },
				{ "bakePerformed", false }
			};
		}

		private static Dictionary<string, object> BuildApvVisualAcceptanceCriterionData(
			EvaluationCriterionResult item)
		{
			return new Dictionary<string, object>
			{
				{ "criterionId", item.CriterionId },
				{ "displayName", item.DisplayName },
				{ "measurementPresent", item.MeasurementPresent },
				{ "score", item.Score },
				{ "weight", item.Weight },
				{ "weightedContribution", item.WeightedContribution },
				{ "minimumScore", item.MinimumScore },
				{ "criticalFailureBelow", item.CriticalFailureBelow },
				{ "confidence", item.Confidence },
				{ "passedMinimum", item.PassedMinimum },
				{ "criticalFailure", item.CriticalFailure },
				{ "summary", item.Summary },
				{ "affectedObjectIds", item.AffectedObjectIds },
				{ "evidence", item.Evidence },
				{ "recommendedActions", item.RecommendedActions }
			};
		}

		private static bool TryBuildApvVisualAcceptanceMeasurementMap(
			EvaluationMeasurementInput[] measurements,
			AcceptanceProfile profile,
			out Dictionary<string, EvaluationMeasurementInput> map,
			out string failureMessage)
		{
			map = new Dictionary<string, EvaluationMeasurementInput>(StringComparer.Ordinal);
			failureMessage = null;
			HashSet<string> allowed = new HashSet<string>(
				profile.Criteria.Select(item => item.CriterionId),
				StringComparer.Ordinal);
			foreach (EvaluationMeasurementInput measurement in
				measurements ?? new EvaluationMeasurementInput[0])
			{
				string id = measurement == null || string.IsNullOrWhiteSpace(measurement.criterionId)
					? string.Empty
					: measurement.criterionId.Trim();
				if (!allowed.Contains(id) || map.ContainsKey(id) ||
					!IsApvVisualAcceptanceScore(measurement.score) ||
					(measurement.confidence.HasValue &&
					 (measurement.confidence.Value < 0.0 || measurement.confidence.Value > 1.0)))
				{
					failureMessage = "MeasurementのcriterionId、重複、Score、Confidenceを確認してください。";
					return false;
				}
				map[id] = measurement;
			}
			return true;
		}

		private static bool TryBuildApvVisualAcceptancePerformanceBudget(
			PerformanceBudgetInput input,
			out PerformanceBudget budget,
			out string failureMessage)
		{
			budget = null;
			failureMessage = null;
			if (input == null)
			{
				return true;
			}
			if ((input.maxCpuFrameMs.HasValue && input.maxCpuFrameMs.Value <= 0.0) ||
				(input.maxGpuFrameMs.HasValue && input.maxGpuFrameMs.Value <= 0.0) ||
				(input.maxMemoryMb.HasValue && input.maxMemoryMb.Value <= 0.0) ||
				(input.maxDrawCalls.HasValue && input.maxDrawCalls.Value <= 0))
			{
				failureMessage = "Performance Budgetの最大値は正数で指定してください。";
				return false;
			}
			budget = new PerformanceBudget
			{
				MaxCpuFrameMs = input.maxCpuFrameMs,
				MaxGpuFrameMs = input.maxGpuFrameMs,
				MaxMemoryMb = input.maxMemoryMb,
				MaxDrawCalls = input.maxDrawCalls,
				Required = input.required ?? false
			};
			return true;
		}

		private static object BuildApvVisualAcceptancePerformanceBudgetData(
			PerformanceBudget budget)
		{
			if (budget == null)
			{
				return null;
			}
			return new Dictionary<string, object>
			{
				{ "maxCpuFrameMs", budget.MaxCpuFrameMs },
				{ "maxGpuFrameMs", budget.MaxGpuFrameMs },
				{ "maxMemoryMb", budget.MaxMemoryMb },
				{ "maxDrawCalls", budget.MaxDrawCalls },
				{ "required", budget.Required }
			};
		}

		private static string BuildApvVisualAcceptanceProfileDigest(
			AcceptanceProfile profile)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(profile.Revision).Append('|');
			builder.Append(profile.ProfileName).Append('|');
			builder.Append(profile.MinimumPassScore.ToString("R", CultureInfo.InvariantCulture)).Append('|');
			builder.Append(profile.ReferenceCaptureId).Append('|');
			builder.Append(profile.ReferenceEvidenceDigest).Append('|');
			foreach (AcceptanceCriterion criterion in profile.Criteria)
			{
				builder.Append(criterion.CriterionId).Append('|');
				builder.Append(criterion.Weight.ToString("R", CultureInfo.InvariantCulture)).Append('|');
				builder.Append(criterion.MinimumScore.ToString("R", CultureInfo.InvariantCulture)).Append('|');
				builder.Append(criterion.CriticalFailureBelow).Append('|');
				builder.Append(criterion.Required).Append('|');
			}
			builder.Append(JsonConvert.SerializeObject(BuildApvVisualAcceptancePerformanceBudgetData(profile.PerformanceBudget)));
			return SaveEvaluationSession.HashText(builder.ToString());
		}

		private static string BuildApvVisualAcceptanceEvaluationDigest(
			VisualEvaluationRecord evaluation)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(evaluation.Revision).Append('|');
			builder.Append(evaluation.CaptureId).Append('|');
			builder.Append(evaluation.EvidenceDigest).Append('|');
			builder.Append(evaluation.ProfileId).Append('|');
			builder.Append(evaluation.ProfileDigest).Append('|');
			builder.Append(evaluation.Decision).Append('|');
			builder.Append(evaluation.WeightedScore.ToString("R", CultureInfo.InvariantCulture)).Append('|');
			foreach (EvaluationCriterionResult criterion in evaluation.Criteria)
			{
				builder.Append(criterion.CriterionId).Append('|');
				builder.Append(criterion.Score.ToString("R", CultureInfo.InvariantCulture)).Append('|');
				builder.Append(criterion.MeasurementPresent).Append('|');
				builder.Append(criterion.CriticalFailure).Append('|');
			}
			builder.Append(JsonConvert.SerializeObject(evaluation.PerformanceFailures));
			builder.Append(JsonConvert.SerializeObject(evaluation.AffectedObjects));
			return SaveEvaluationSession.HashText(builder.ToString());
		}

		private static List<string> NormalizeApvVisualAcceptanceTextValues(string[] values)
		{
			return values == null
				? new List<string>()
				: values
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Select(value => value.Trim())
					.Where(value => value.Length <= 512)
					.Distinct(StringComparer.Ordinal)
					.Take(64)
					.ToList();
		}

		private static bool IsApvVisualAcceptanceScore(double value)
		{
			return !double.IsNaN(value) &&
				!double.IsInfinity(value) &&
				value >= 0.0 && value <= 100.0;
		}
	}
}

#endif
