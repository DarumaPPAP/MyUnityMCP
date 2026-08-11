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
	public sealed class ApvVisualAcceptanceTests
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
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			DependencyBakeSession.ClearForTests();
			CaptureEvidenceSession.ClearForTests();
			AdaptiveProbeVolumeBakeSession.ClearForTests();
			VisualAcceptanceSession.ClearForTests();
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			DeleteMapFile();
		}

		[TearDown]
		public void TearDown()
		{
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			DependencyBakeSession.ClearForTests();
			CaptureEvidenceSession.ClearForTests();
			AdaptiveProbeVolumeBakeSession.ClearForTests();
			VisualAcceptanceSession.ClearForTests();
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
				typeof(PrepareApvBakePlanTool),
				typeof(StartApvBakeTool),
				typeof(GetApvBakeStatusTool),
				typeof(CancelApvBakeTool),
				typeof(PrepareAcceptanceProfileTool),
				typeof(EvaluateCaptureTool),
				typeof(RefineFromEvaluationTool)
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
			ToolResult result =
				Inspection.PrepareApvBakePlan(
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
			ToolResult result = PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Day"));
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(result.summary, Does.Contain("完全一致"));
		}

		[Test]
		public void PrepareApvBakePlan_RejectsUnknownScenario()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			ToolResult result = PrepareApv(
				CreateApvInput(new[] { TEMP_SCENE_PATH }, "Night"));
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(result.summary, Does.Contain("Lighting Scenario"));
		}

		[Test]
		public void PrepareApvBakePlan_ReturnsCapabilityAndCancellationContract()
		{
			CreateSavedScene();
			ConfigureApvEnvironment(new[] { TEMP_SCENE_PATH }, new[] { "Day" });
			ToolResult result = PrepareApv(
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
			ToolResult result =
				Inspection.StartApvBake(
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
			ToolResult started = StartApv(plan);
			Dictionary<string, object> startedData = ResultData(started);
			Assert.That(startedData["jobStatus"], Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.RUNNING.ToString()));

			running = false;
			AdaptiveProbeVolumeBakeSession.TickForTests();
			ToolResult completed =
				Inspection.GetApvBakeStatus(
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
			ToolResult cancelled =
				Inspection.CancelApvBake(
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
			ToolResult cancelled =
				Inspection.CancelApvBake(
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
			ToolResult result = StartApv(plan);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.FAILED.ToString()));
			Assert.That(data["failureCode"], Is.EqualTo("APV_BAKE_NO_OUTPUT_DIFF"));
		}

		[Test]
		public void AcceptanceProfile_RejectsDuplicateCriterion()
		{
			AcceptanceProfileInput input = CreateProfileInput();
			input.criteria = new[] { input.criteria[0], input.criteria[0] };
			ToolResult result = PrepareProfile(input);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void AcceptanceProfile_RejectsInvalidWeightAndThreshold()
		{
			AcceptanceProfileInput input = CreateProfileInput();
			input.criteria[0].weight = 0.0;
			input.criteria[0].criticalFailureBelow = 101.0;
			ToolResult result = PrepareProfile(input);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void AcceptanceProfile_FixesReferenceCaptureProvenance()
		{
			CaptureEvidenceRecord capture = StoreFakeCapture();
			AcceptanceProfileInput input = CreateProfileInput();
			input.referenceCaptureId = capture.CaptureId;
			input.referenceEvidenceDigest = capture.EvidenceDigest;
			ToolResult result = PrepareProfile(input);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["referenceCaptureId"], Is.EqualTo(capture.CaptureId));
			Assert.That(data["referenceEvidenceDigest"], Is.EqualTo(capture.EvidenceDigest));
			Assert.That(data["referenceComparisonPerformedByUnity"], Is.EqualTo(false));
		}

		[Test]
		public void EvaluateCapture_PassesWeightedProfileButStillRequiresHumanAcceptance()
		{
			CaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			ToolResult result = Evaluate(
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
			CaptureEvidenceRecord capture = StoreFakeCapture();
			AcceptanceProfileInput input = CreateProfileInput();
			input.minimumPassScore = 40.0;
			input.criteria[0].minimumScore = 40.0;
			input.criteria[0].criticalFailureBelow = 60.0;
			Dictionary<string, object> profile = ResultData(PrepareProfile(input));
			ToolResult result = Evaluate(
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
			CaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			ToolResult result = Evaluate(
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
			CaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			ToolResult result = Evaluate(
				capture,
				profile["profileId"] as string,
				new EvaluationMeasurementInput[0],
				Performance(10.0, 11.0, 500.0, 100));
			Dictionary<string, object> data = ResultData(result);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.PARTIAL.ToString()));
			Assert.That(data["decision"], Is.EqualTo(E_GRAPHICS_VISUAL_EVALUATION_DECISION.INCOMPLETE.ToString()));
		}

		[Test]
		public void EvaluateCapture_MapsAffectedObjectIdToRenderer()
		{
			CaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			EvaluationMeasurementInput measurement =
				Measurement("composition", 30.0);
			measurement.affectedObjectIds = new[] { 7 };
			ToolResult result = Evaluate(
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
			CaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			Dictionary<string, object> evaluation = ResultData(Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { Measurement("composition", 20.0) },
				Performance(10.0, 11.0, 500.0, 100)));
			DirectionPlan source = StoreDirectionPlan();
			ToolResult result =
				Inspection.RefineFromEvaluation(
					"apv-visual-acceptance-refine",
					source.PlanId,
					evaluation["evaluationId"] as string,
					Session.Revision);
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
			CaptureEvidenceRecord capture = StoreFakeCapture();
			Dictionary<string, object> profile = ResultData(PrepareProfile(CreateProfileInput()));
			Dictionary<string, object> evaluation = ResultData(Evaluate(
				capture,
				profile["profileId"] as string,
				new[] { Measurement("composition", 90.0) },
				Performance(10.0, 11.0, 500.0, 100)));
			DirectionPlan source = StoreDirectionPlan();
			ToolResult result =
				Inspection.RefineFromEvaluation(
					"apv-visual-acceptance-refine-pass",
					source.PlanId,
					evaluation["evaluationId"] as string,
					Session.Revision);
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

			ToolResult savePlanResult =
				Inspection.PrepareSavePlan(
					"apv-visual-acceptance-loop-save-plan",
					Session.Revision,
					new[] { new SaveTargetInput { scenePath = TEMP_SCENE_PATH } });
			Dictionary<string, object> savePlan = ResultData(savePlanResult);
			ToolResult saveResult =
				Inspection.ApplySavePlan(
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
			ToolResult bakeResult = StartApv(apvPlan);
			Assert.That(ResultData(bakeResult)["jobStatus"],
				Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.SUCCEEDED.ToString()));

			CaptureEvidenceRecord capture = StoreFakeCapture();
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

			DirectionPlan source = StoreDirectionPlan();
			ToolResult refine =
				Inspection.RefineFromEvaluation(
					"apv-visual-acceptance-loop-refine",
					source.PlanId,
					evaluation["evaluationId"] as string,
					Session.Revision);
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
			AdaptiveProbeVolumeBakeSession.EnvironmentOverrideForTests = input =>
				new ApvEnvironment
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
			AdaptiveProbeVolumeBakeSession.StartOverrideForTests = plan =>
				new ApvBackendState { Started = true };
			AdaptiveProbeVolumeBakeSession.IsRunningOverrideForTests = isRunning;
			AdaptiveProbeVolumeBakeSession.CancelOverrideForTests = cancel;
			AdaptiveProbeVolumeBakeSession.OutputSnapshotOverrideForTests = plan => snapshot();
		}

		private static ApvBakePlanInput CreateApvInput(
			string[] scenes,
			string scenario)
		{
			return new ApvBakePlanInput
			{
				bakingSetAssetPath = FAKE_BAKING_SET_PATH,
				lightingScenario = scenario,
				scenePaths = scenes,
				outputAssetRoots = new[] { "Assets" },
				timeoutSeconds = 300
			};
		}

		private static ToolResult PrepareApv(
			ApvBakePlanInput input)
		{
			return Inspection.PrepareApvBakePlan(
				"apv-visual-acceptance-apv-prepare",
				Session.Revision,
				input);
		}

		private static ToolResult StartApv(
			Dictionary<string, object> plan)
		{
			return Inspection.StartApvBake(
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

		private static AcceptanceProfileInput CreateProfileInput()
		{
			return new AcceptanceProfileInput
			{
				profileName = "ApvVisualAcceptance Visual Acceptance",
				minimumPassScore = 70.0,
				criteria = new[]
				{
					new AcceptanceCriterionInput
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
				performanceBudget = new PerformanceBudgetInput
				{
					maxCpuFrameMs = 16.7,
					maxGpuFrameMs = 16.7,
					maxMemoryMb = 1024.0,
					maxDrawCalls = 500,
					required = true
				}
			};
		}

		private static ToolResult PrepareProfile(
			AcceptanceProfileInput input)
		{
			return Inspection.PrepareAcceptanceProfile(
				"apv-visual-acceptance-profile",
				Session.Revision,
				input);
		}

		private static EvaluationMeasurementInput Measurement(
			string criterionId,
			double score)
		{
			return new EvaluationMeasurementInput
			{
				criterionId = criterionId,
				score = score,
				confidence = 0.95,
				summary = "External visual evaluator measurement.",
				evidence = new[] { "Color/Depth/Object ID bundle reviewed." }
			};
		}

		private static PerformanceMeasurementInput Performance(
			double cpu,
			double gpu,
			double memory,
			int drawCalls)
		{
			return new PerformanceMeasurementInput
			{
				cpuFrameMs = cpu,
				gpuFrameMs = gpu,
				memoryMb = memory,
				drawCalls = drawCalls,
				source = "ApvVisualAcceptance Test Measurement"
			};
		}

		private static ToolResult Evaluate(
			CaptureEvidenceRecord capture,
			string profileId,
			EvaluationMeasurementInput[] measurements,
			PerformanceMeasurementInput performance)
		{
			return Inspection.EvaluateCapture(
				"apv-visual-acceptance-evaluate",
				capture.CaptureId,
				Session.Revision,
				capture.EvidenceDigest,
				profileId,
				measurements,
				performance);
		}

		private static CaptureEvidenceRecord StoreFakeCapture()
		{
			WriteObjectIdMap();
			CaptureEvidenceRecord capture =
				new CaptureEvidenceRecord
				{
					Revision = Session.Revision,
					CameraObjectId = "GlobalObjectId_V1-TestCamera",
					CameraSceneHandle = IdentityCompatibility.GetSceneToken(SceneManager.GetActiveScene()),
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
			CaptureEvidenceSession.StoreCaptureForTests(capture);
			return capture;
		}

		private static CaptureArtifactRecord Artifact(
			string channel,
			string outputPath)
		{
			return new CaptureArtifactRecord
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
			List<ObjectIdEntry> entries =
				new List<ObjectIdEntry>
				{
					new ObjectIdEntry
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

		private static DirectionPlan StoreDirectionPlan()
		{
			DirectionPlan plan = new DirectionPlan
			{
				Revision = Session.Revision,
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
			Session.StorePlan(plan);
			return plan;
		}

		private static Dictionary<string, object> ResultData(
			ToolResult result)
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