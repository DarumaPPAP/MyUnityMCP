#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpApvVisualAcceptanceTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpApvVisualAcceptanceTemporaryScene.unity";
		private const string FAKE_BAKING_SET_PATH =
			"Assets/MyUnityMcpApvVisualAcceptanceTests/FakeBakingSet.asset";
		private const string MAP_RELATIVE_PATH =
			"Library/MyUnityMCP/ApvVisualAcceptanceTests/object-id-map.json";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
			UnityGraphicsMcpSaveEvaluationSession.ClearForTests();
			UnityGraphicsMcpDependencyBakeSession.ClearForTests();
			UnityGraphicsMcpCaptureEvidenceSession.ClearForTests();
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.ClearForTests();
			UnityGraphicsMcpVisualAcceptanceSession.ClearForTests();
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			DeleteMapFile();
		}

		[TearDown]
		public void TearDown()
		{
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
			UnityGraphicsMcpSaveEvaluationSession.ClearForTests();
			UnityGraphicsMcpDependencyBakeSession.ClearForTests();
			UnityGraphicsMcpCaptureEvidenceSession.ClearForTests();
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.ClearForTests();
			UnityGraphicsMcpVisualAcceptanceSession.ClearForTests();
			Undo.ClearAll();
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			DeleteMapFile();
		}

		[Test]
		public void Bridge_DiscoversSevenApvVisualAcceptanceTools_AndKeepsThemDisabled()
		{
			CommandRegistry.Initialize();
			string[] names =
			{
				"graphics.prepare_apv_bake_plan",
				"graphics.start_apv_bake",
				"graphics.get_apv_bake_status",
				"graphics.cancel_apv_bake",
				"graphics.prepare_acceptance_profile",
				"graphics.evaluate_capture",
				"graphics.refine_from_evaluation"
			};
			foreach (string name in names)
			{
				Assert.That(CommandRegistry.GetHandler(name), Is.Not.Null, name);
			}
			Type[] types =
			{
				typeof(GraphicsPrepareApvBakePlanTool),
				typeof(GraphicsStartApvBakeTool),
				typeof(GraphicsGetApvBakeStatusTool),
				typeof(GraphicsCancelApvBakeTool),
				typeof(GraphicsPrepareAcceptanceProfileTool),
				typeof(GraphicsEvaluateCaptureTool),
				typeof(GraphicsRefineFromEvaluationTool)
			};
			foreach (Type type in types)
			{
				Assert.That(GetToolAttribute(type).AutoRegister, Is.False, type.Name);
			}
		}

		[Test]
		public void PrepareApvBakePlan_RequiresExpectedRevision()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.PrepareApvBakePlan(
					"apv-visual-acceptance-apv-no-revision",
					null,
					CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day"));
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void PrepareApvBakePlan_RejectsSceneSetMismatch()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(
				new[] { TEMP_SCENE_PATH, "Assets/OtherScene.unity" },
				new[] { "Day" });
			UnityGraphicsMcpToolResult result = PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day"));
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(result.summary, Does.Contain("完全一致"));
		}

		[Test]
		public void PrepareApvBakePlan_RejectsUnknownScenario()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			UnityGraphicsMcpToolResult result = PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Night"));
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(result.summary, Does.Contain("Lighting Scenario"));
		}

		[Test]
		public void PrepareApvBakePlan_ReturnsCapabilityAndCancellationContract()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			UnityGraphicsMcpToolResult result = PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day"));
			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["approvalToken"] as string, Is.Not.Empty);
			Assert.That(data["pipelineKind"], Is.EqualTo("URP"));
			Assert.That(data["bakeMethod"], Is.EqualTo("BakeAsync"));
			Dictionary<string, object> cancellation =
				data["cancellationContract"] as Dictionary<string, object>;
			Assert.That(cancellation, Is.Not.Null);
			Assert.That(cancellation["cancelTool"], Is.EqualTo("graphics.cancel_apv_bake"));
		}

		[Test]
		public void StartApvBake_RejectsMissingApproval()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			Dictionary<string, object> plan = ResultData(PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day")));
			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.StartApvBake(
					"apv-visual-acceptance-apv-no-approval",
					plan["planId"] as string,
					Convert.ToInt64(plan["expectedRevision"]),
					null,
					"EXPLICIT_APV_BAKING_SET");
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void ApvBakeJob_SucceedsAndRecordsOutputDiff()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			bool running = true;
			int snapshot = 0;
			ConfigureApvJob(
				() => running,
				() => snapshot++ == 0 ? Snapshot("old") : Snapshot("new"),
				() => true);
			Dictionary<string, object> plan = ResultData(PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day")));
			UnityGraphicsMcpToolResult started = StartApv(plan);
			Dictionary<string, object> startedData = ResultData(started);
			Assert.That(startedData["jobStatus"], Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.RUNNING.ToString()));

			running = false;
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.TickForTests();
			UnityGraphicsMcpToolResult completed =
				UnityGraphicsMcpInspection.GetApvBakeStatus(
					"apv-visual-acceptance-apv-status",
					startedData["jobId"] as string);
			Dictionary<string, object> completedData = ResultData(completed);
			Assert.That(completed.status, Is.EqualTo(E_MCP_TOOL_STATUS.SUCCESS.ToString()));
			Assert.That(completedData["jobStatus"], Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.SUCCEEDED.ToString()));
			Assert.That(((List<Dictionary<string, object>>)completedData["outputDiff"]).Count, Is.EqualTo(1));
		}

		[Test]
		public void ApvBakeJob_CancellationWithoutOutputReturnsCancelled()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			bool running = true;
			ConfigureApvJob(
				() => running,
				() => Snapshot("same"),
				() => { running = false; return true; });
			Dictionary<string, object> plan = ResultData(PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day")));
			Dictionary<string, object> started = ResultData(StartApv(plan));
			UnityGraphicsMcpToolResult cancelled =
				UnityGraphicsMcpInspection.CancelApvBake(
					"apv-visual-acceptance-apv-cancel",
					started["jobId"] as string);
			Dictionary<string, object> data = ResultData(cancelled);
			Assert.That(data["jobStatus"], Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.CANCELLED.ToString()));
			Assert.That(data["cancellationInvoked"], Is.EqualTo(true));
		}

		[Test]
		public void ApvBakeJob_CancellationAfterOutputReturnsPartial()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			bool running = true;
			int snapshot = 0;
			ConfigureApvJob(
				() => running,
				() => snapshot++ == 0 ? Snapshot("old") : Snapshot("partial"),
				() => { running = false; return true; });
			Dictionary<string, object> plan = ResultData(PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day")));
			Dictionary<string, object> started = ResultData(StartApv(plan));
			UnityGraphicsMcpToolResult cancelled =
				UnityGraphicsMcpInspection.CancelApvBake(
					"apv-visual-acceptance-apv-partial",
					started["jobId"] as string);
			Dictionary<string, object> data = ResultData(cancelled);
			Assert.That(data["jobStatus"], Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.PARTIAL.ToString()));
			Assert.That(data["partialResult"], Is.EqualTo(true));
			Assert.That(((List<Dictionary<string, object>>)data["outputDiff"]).Count, Is.EqualTo(1));
		}

		[Test]
		public void ApvBakeJob_NoOutputDiffIsFailure()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			ConfigureApvJob(() => false, () => Snapshot("same"), () => true);
			Dictionary<string, object> plan = ResultData(PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day")));
			UnityGraphicsMcpToolResult result = StartApv(plan);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.FAILED.ToString()));
			Assert.That(data["failureCode"], Is.EqualTo("APV_BAKE_NO_OUTPUT_DIFF"));
		}

		[Test]
		public void AcceptanceProfile_RejectsDuplicateCriterion()
		{
			UnityGraphicsMcpAcceptanceProfileInput input = CreateProfileInput();
			input.criteria = new[] { input.criteria[0], input.criteria[0] };
			UnityGraphicsMcpToolResult result = PrepareProfile(input);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void AcceptanceProfile_RejectsInvalidWeightAndThreshold()
		{
			UnityGraphicsMcpAcceptanceProfileInput input = CreateProfileInput();
			input.criteria[0].weight = 0.0;
			input.criteria[0].criticalFailureBelow = 101.0;
			UnityGraphicsMcpToolResult result = PrepareProfile(input);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void AcceptanceProfile_FixesReferenceCaptureProvenance()
		{
			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			UnityGraphicsMcpAcceptanceProfileInput input = CreateProfileInput();
			input.referenceCaptureId = capture.CaptureId;
			input.referenceEvidenceDigest = capture.EvidenceDigest;
			UnityGraphicsMcpToolResult result = PrepareProfile(input);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["referenceCaptureId"], Is.EqualTo(capture.CaptureId));
			Assert.That(data["referenceEvidenceDigest"], Is.EqualTo(capture.EvidenceDigest));
			Assert.That(data["referenceComparisonPerformedByUnity"], Is.EqualTo(false));
		}

		[Test]
		public void EvaluateCapture_PassesWeightedProfileButStillRequiresHumanAcceptance()
		{
			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			UnityGraphicsMcpToolResult result = Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { Measurement("composition", 90.0) },
				Performance(10.0, 11.0, 500.0, 100));
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["decision"], Is.EqualTo(E_GRAPHICS_VISUAL_EVALUATION_DECISION.PASSED.ToString()));
			Assert.That(data["automatedProfilePassed"], Is.EqualTo(true));
			Assert.That(data["visualAccepted"], Is.EqualTo(false));
			Assert.That(data["humanReviewRequired"], Is.EqualTo(true));
		}

		[Test]
		public void EvaluateCapture_CriticalFailureOverridesWeightedThreshold()
		{
			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			UnityGraphicsMcpAcceptanceProfileInput input = CreateProfileInput();
			input.minimumPassScore = 40.0;
			input.criteria[0].minimumScore = 40.0;
			input.criteria[0].criticalFailureBelow = 60.0;
			Dictionary<string, object> profile = ResultData(PrepareProfile(input));
			UnityGraphicsMcpToolResult result = Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { Measurement("composition", 50.0) },
				Performance(10.0, 11.0, 500.0, 100));
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["decision"], Is.EqualTo(E_GRAPHICS_VISUAL_EVALUATION_DECISION.FAILED.ToString()));
			Assert.That(data["hasCriticalFailure"], Is.EqualTo(true));
		}

		[Test]
		public void EvaluateCapture_FailsPerformanceBudget()
		{
			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			UnityGraphicsMcpToolResult result = Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { Measurement("composition", 90.0) },
				Performance(30.0, 35.0, 3000.0, 900));
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["decision"], Is.EqualTo(E_GRAPHICS_VISUAL_EVALUATION_DECISION.FAILED.ToString()));
			Assert.That(((List<Dictionary<string, object>>)data["performanceFailures"]).Count, Is.GreaterThan(0));
		}

		[Test]
		public void EvaluateCapture_MissingRequiredMeasurementIsIncomplete()
		{
			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			UnityGraphicsMcpToolResult result = Evaluate(
				capture,
				profile["profileId"] as string,
				new UnityGraphicsMcpEvaluationMeasurementInput[0],
				Performance(10.0, 11.0, 500.0, 100));
			Dictionary<string, object> data = ResultData(result);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.PARTIAL.ToString()));
			Assert.That(data["decision"], Is.EqualTo(E_GRAPHICS_VISUAL_EVALUATION_DECISION.INCOMPLETE.ToString()));
		}

		[Test]
		public void EvaluateCapture_MapsAffectedObjectIdToRenderer()
		{
			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			UnityGraphicsMcpEvaluationMeasurementInput measurement =
				Measurement("composition", 30.0);
			measurement.affectedObjectIds = new[] { 7 };
			UnityGraphicsMcpToolResult result = Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { measurement },
				Performance(10.0, 11.0, 500.0, 100));
			List<Dictionary<string, object>> affected =
				ResultData(result)["affectedObjects"] as List<Dictionary<string, object>>;
			Assert.That(affected, Is.Not.Null);
			Assert.That(affected.Count, Is.EqualTo(1));
			Assert.That(affected[0]["mappingStatus"], Is.EqualTo("RESOLVED"));
			Assert.That(affected[0]["rendererObjectId"], Is.EqualTo("GlobalObjectId_V1-TestRenderer"));
		}

		[Test]
		public void RefineFromEvaluation_CreatesStructuredDirectionForFailure()
		{
			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			Dictionary<string, object> evaluation = ResultData(Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { Measurement("composition", 20.0) },
				Performance(10.0, 11.0, 500.0, 100)));
			UnityGraphicsMcpDirectionPlan source = StoreDirectionPlan();
			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.RefineFromEvaluation(
					"apv-visual-acceptance-refine",
					source.PlanId,
					evaluation["evaluationId"] as string,
					UnityGraphicsMcpSession.Revision);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Assert.That(data["planId"] as string, Is.Not.Empty);
			Dictionary<string, object> direction =
				data["refineDirection"] as Dictionary<string, object>;
			Assert.That(direction, Is.Not.Null);
			Assert.That(direction["requiredRecaptureChannels"], Is.Not.Null);
			Assert.That(direction["humanReviewRequired"], Is.EqualTo(true));
		}

		[Test]
		public void RefineFromEvaluation_RejectsPassedEvaluation()
		{
			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			Dictionary<string, object> evaluation = ResultData(Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { Measurement("composition", 90.0) },
				Performance(10.0, 11.0, 500.0, 100)));
			UnityGraphicsMcpDirectionPlan source = StoreDirectionPlan();
			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.RefineFromEvaluation(
					"apv-visual-acceptance-refine-pass",
					source.PlanId,
					evaluation["evaluationId"] as string,
					UnityGraphicsMcpSession.Revision);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void ClosedLoop_ChangeSaveBakeCaptureEvaluateAndRefine_Completes()
		{
			GameObject target = new GameObject("ApvVisualAcceptance Closed Loop Target");
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false), Is.True);
			target.transform.position = Vector3.one;
			EditorSceneManager.MarkSceneDirty(scene);

			UnityGraphicsMcpToolResult savePlanResult =
				UnityGraphicsMcpInspection.PrepareSavePlan(
					"apv-visual-acceptance-loop-save-plan",
					UnityGraphicsMcpSession.Revision,
					new[] { new UnityGraphicsMcpSaveTargetInput { scenePath = TEMP_SCENE_PATH } });
			Dictionary<string, object> savePlan = ResultData(savePlanResult);
			UnityGraphicsMcpToolResult saveResult =
				UnityGraphicsMcpInspection.ApplySavePlan(
					"apv-visual-acceptance-loop-save",
					savePlan["planId"] as string,
					Convert.ToInt64(savePlan["expectedRevision"]),
					savePlan["approvalToken"] as string,
					"EXPLICIT_SCENE");
			Assert.That(saveResult.IsSuccessful, Is.True, saveResult.summary);

			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			int snapshot = 0;
			ConfigureApvJob(
				() => false,
				() => snapshot++ == 0 ? Snapshot("old") : Snapshot("new"),
				() => true);
			Dictionary<string, object> apvPlan = ResultData(PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day")));
			UnityGraphicsMcpToolResult bakeResult = StartApv(apvPlan);
			Assert.That(ResultData(bakeResult)["jobStatus"],
				Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.SUCCEEDED.ToString()));

			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture();
			Assert.That(capture.Artifacts.Select(item => item.Channel),
				Is.SupersetOf(new[] { "COLOR", "LINEAR_DEPTH", "OBJECT_ID" }));
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			Dictionary<string, object> evaluation = ResultData(Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { Measurement("composition", 25.0) },
				Performance(10.0, 11.0, 500.0, 100)));
			Assert.That(evaluation["decision"],
				Is.EqualTo(E_GRAPHICS_VISUAL_EVALUATION_DECISION.FAILED.ToString()));

			UnityGraphicsMcpDirectionPlan source = StoreDirectionPlan();
			UnityGraphicsMcpToolResult refine =
				UnityGraphicsMcpInspection.RefineFromEvaluation(
					"apv-visual-acceptance-loop-refine",
					source.PlanId,
					evaluation["evaluationId"] as string,
					UnityGraphicsMcpSession.Revision);
			Dictionary<string, object> refineData = ResultData(refine);
			Assert.That(refine.IsSuccessful, Is.True, refine.summary);
			Assert.That(refineData["planId"] as string, Is.Not.Empty);
			Assert.That(refineData["refineDirection"], Is.Not.Null);
		}

		private static Scene CreateSavedScene()
		{
			new GameObject("ApvVisualAcceptance APV Target");
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false), Is.True);
			return scene;
		}

		private static void ConfigureApvEnvironment(
			IEnumerable<string> scenes,
			IEnumerable<string> scenarios)
		{
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.EnvironmentOverrideForTests = input =>
				new UnityGraphicsMcpApvEnvironment
				{
					PipelineKind = "URP",
					PipelineAssetType = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset",
					BakingSetAssetPath = FAKE_BAKING_SET_PATH,
					BakingSetType = "UnityEngine.Rendering.ProbeVolumeBakingSet",
					BakingSetDigest = "fake-baking-set-digest",
					ScenePaths = scenes.ToList(),
					LightingScenarios = scenarios.ToList(),
					BackendType = "UnityEditor.Rendering.AdaptiveProbeVolumes",
					BakeMethod = "BakeAsync",
					RunningProperty = "isRunning",
					CancelMethod = "Cancel",
					NativeCancellationSupported = true
				};
		}

		private static void ConfigureApvJob(
			Func<bool> isRunning,
			Func<Dictionary<string, string>> snapshot,
			Func<bool> cancel)
		{
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.StartOverrideForTests = plan =>
				new UnityGraphicsMcpApvBackendState { Started = true };
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.IsRunningOverrideForTests = isRunning;
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.CancelOverrideForTests = cancel;
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.OutputSnapshotOverrideForTests = plan => snapshot();
		}

		private static UnityGraphicsMcpApvBakePlanInput CreateApvInput(
			string[] scenes,
			string scenario)
		{
			return new UnityGraphicsMcpApvBakePlanInput
			{
				bakingSetAssetPath = FAKE_BAKING_SET_PATH,
				lightingScenario = scenario,
				scenePaths = scenes,
				outputAssetRoots = new[] { "Assets" },
				timeoutSeconds = 300
			};
		}

		private static UnityGraphicsMcpToolResult PrepareApv(
			UnityGraphicsMcpApvBakePlanInput input)
		{
			return UnityGraphicsMcpInspection.PrepareApvBakePlan(
				"apv-visual-acceptance-apv-prepare",
				UnityGraphicsMcpSession.Revision,
				input);
		}

		private static UnityGraphicsMcpToolResult StartApv(
			Dictionary<string, object> plan)
		{
			return UnityGraphicsMcpInspection.StartApvBake(
				"apv-visual-acceptance-apv-start",
				plan["planId"] as string,
				Convert.ToInt64(plan["expectedRevision"]),
				plan["approvalToken"] as string,
				"EXPLICIT_APV_BAKING_SET");
		}

		private static Dictionary<string, string> Snapshot(string hash)
		{
			return new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "Assets/APV/ProbeData.asset", hash }
			};
		}

		private static UnityGraphicsMcpAcceptanceProfileInput CreateProfileInput()
		{
			return new UnityGraphicsMcpAcceptanceProfileInput
			{
				profileName = "ApvVisualAcceptance Visual Acceptance",
				minimumPassScore = 70.0,
				criteria = new[]
				{
					new UnityGraphicsMcpAcceptanceCriterionInput
					{
						criterionId = "composition",
						displayName = "Composition and silhouette",
						weight = 1.0,
						minimumScore = 60.0,
						criticalFailureBelow = 30.0,
						required = true,
						recommendedActions = new[]
						{
							"主役のSilhouetteと背景分離を再調整する。",
							"問題Object IDのLightとMaterialを再確認する。"
						}
					}
				},
				performanceBudget = new UnityGraphicsMcpPerformanceBudgetInput
				{
					maxCpuFrameMs = 16.7,
					maxGpuFrameMs = 16.7,
					maxMemoryMb = 1024.0,
					maxDrawCalls = 500,
					required = true
				}
			};
		}

		private static UnityGraphicsMcpToolResult PrepareProfile(
			UnityGraphicsMcpAcceptanceProfileInput input)
		{
			return UnityGraphicsMcpInspection.PrepareAcceptanceProfile(
				"apv-visual-acceptance-profile",
				UnityGraphicsMcpSession.Revision,
				input);
		}

		private static UnityGraphicsMcpEvaluationMeasurementInput Measurement(
			string criterionId,
			double score)
		{
			return new UnityGraphicsMcpEvaluationMeasurementInput
			{
				criterionId = criterionId,
				score = score,
				confidence = 0.95,
				summary = "External visual evaluator measurement.",
				evidence = new[] { "Color/Depth/Object ID bundle reviewed." }
			};
		}

		private static UnityGraphicsMcpPerformanceMeasurementInput Performance(
			double cpu,
			double gpu,
			double memory,
			int drawCalls)
		{
			return new UnityGraphicsMcpPerformanceMeasurementInput
			{
				cpuFrameMs = cpu,
				gpuFrameMs = gpu,
				memoryMb = memory,
				drawCalls = drawCalls,
				source = "ApvVisualAcceptance Test Measurement"
			};
		}

		private static UnityGraphicsMcpToolResult Evaluate(
			UnityGraphicsMcpCaptureEvidenceRecord capture,
			string profileId,
			UnityGraphicsMcpEvaluationMeasurementInput[] measurements,
			UnityGraphicsMcpPerformanceMeasurementInput performance)
		{
			return UnityGraphicsMcpInspection.EvaluateCapture(
				"apv-visual-acceptance-evaluate",
				capture.CaptureId,
				UnityGraphicsMcpSession.Revision,
				capture.EvidenceDigest,
				profileId,
				measurements,
				performance);
		}

		private static UnityGraphicsMcpCaptureEvidenceRecord StoreFakeCapture()
		{
			WriteObjectIdMap();
			UnityGraphicsMcpCaptureEvidenceRecord capture =
				new UnityGraphicsMcpCaptureEvidenceRecord
				{
					Revision = UnityGraphicsMcpSession.Revision,
					CameraObjectId = "GlobalObjectId_V1-TestCamera",
					CameraSceneHandle = UnityGraphicsMcpIdentityCompatibility.GetSceneToken(SceneManager.GetActiveScene()),
					CameraScenePath = SceneManager.GetActiveScene().path,
					CameraBaselineDigest = "camera-digest",
					EvidenceDigest = "apv-visual-acceptance-evidence-digest-" + Guid.NewGuid().ToString("N"),
					BundlePath = "Library/MyUnityMCP/ApvVisualAcceptanceTests",
					Width = 1280,
					Height = 720,
					EncodedRendererCount = 1
				};
			capture.Artifacts.Add(Artifact("COLOR", "color.png"));
			capture.Artifacts.Add(Artifact("LINEAR_DEPTH", "linear-depth.exr"));
			capture.Artifacts.Add(Artifact("OBJECT_ID", "object-id.png"));
			capture.Artifacts.Add(Artifact("OBJECT_ID_MAP", MAP_RELATIVE_PATH));
			UnityGraphicsMcpCaptureEvidenceSession.StoreCaptureForTests(capture);
			return capture;
		}

		private static UnityGraphicsMcpCaptureArtifactRecord Artifact(
			string channel,
			string outputPath)
		{
			return new UnityGraphicsMcpCaptureArtifactRecord
			{
				Channel = channel,
				OutputPath = outputPath,
				Sha256 = "sha-" + channel,
				ByteLength = 1,
				Format = channel == "LINEAR_DEPTH" ? "EXR_FLOAT" : "PNG",
				Semantics = channel
			};
		}

		private static void WriteObjectIdMap()
		{
			string absolute = AbsoluteProjectPath(MAP_RELATIVE_PATH);
			Directory.CreateDirectory(Path.GetDirectoryName(absolute));
			List<UnityGraphicsMcpObjectIdEntry> entries =
				new List<UnityGraphicsMcpObjectIdEntry>
				{
					new UnityGraphicsMcpObjectIdEntry
					{
						ObjectId = 7,
						EncodedColor = "#070000",
						RendererObjectId = "GlobalObjectId_V1-TestRenderer",
						RendererType = "UnityEngine.MeshRenderer",
						Name = "Problem Renderer",
						HierarchyPath = "Root/Problem Renderer",
						ScenePath = TEMP_SCENE_PATH,
						SubMeshCount = 1
					}
				};
			File.WriteAllText(
				absolute,
				JsonConvert.SerializeObject(entries),
				System.Text.Encoding.UTF8);
		}

		private static UnityGraphicsMcpDirectionPlan StoreDirectionPlan()
		{
			UnityGraphicsMcpDirectionPlan plan = new UnityGraphicsMcpDirectionPlan
			{
				Revision = UnityGraphicsMcpSession.Revision,
				CreatedUtc = DateTime.UtcNow,
				ProjectContext = new Dictionary<string, object>
				{
					{ "test", "apv-visual-acceptance" }
				},
				VisualIntent = new Dictionary<string, object>
				{
					{ "goal", "Close visual production loop" }
				}
			};
			UnityGraphicsMcpSession.StorePlan(plan);
			return plan;
		}

		private static Dictionary<string, object> ResultData(
			UnityGraphicsMcpToolResult result)
		{
			Dictionary<string, object> data =
				result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null, result.summary);
			return data;
		}

		private static McpForUnityToolAttribute GetToolAttribute(Type type)
		{
			McpForUnityToolAttribute attribute = Attribute.GetCustomAttribute(
				type,
				typeof(McpForUnityToolAttribute)) as McpForUnityToolAttribute;
			Assert.That(attribute, Is.Not.Null);
			return attribute;
		}

		private static string AbsoluteProjectPath(string relativePath)
		{
			return Path.GetFullPath(Path.Combine(
				Directory.GetParent(Application.dataPath).FullName,
				relativePath));
		}

		private static void DeleteMapFile()
		{
			string path = AbsoluteProjectPath(MAP_RELATIVE_PATH);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}
}

#endif