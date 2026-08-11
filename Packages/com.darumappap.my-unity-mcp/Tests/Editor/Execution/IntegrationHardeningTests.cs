#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class IntegrationHardeningTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpIntegrationHardeningTemporaryScene.unity";
		private const string FAKE_BAKING_SET_PATH =
			"Assets/MyUnityMcpIntegrationHardening/FakeBakingSet.asset";
		private const string MAP_RELATIVE_PATH =
			"Library/MyUnityMCP/IntegrationHardeningTests/object-id-map.json";

		private string _storageRoot;

		private sealed class DummyParameters
		{
			public string requestId { get; set; }
		}

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			DependencyBakeSession.ClearForTests();
			CaptureEvidenceSession.ClearForTests();
			AdaptiveProbeVolumeBakeSession.ClearForTests();
			VisualAcceptanceSession.ClearForTests();
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			AssetDatabase.DeleteAsset("Assets/MyUnityMcpIntegrationHardening");
			_storageRoot = AbsoluteProjectPath(
				"Library/MyUnityMCP/IntegrationHardeningTests/Execution");
			ExecutionHardening.ResetForTests(_storageRoot);
			DeletePath(AbsoluteProjectPath(
				"Library/MyUnityMCP/IntegrationHardeningTests"));
			Directory.CreateDirectory(_storageRoot);
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
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			AssetDatabase.DeleteAsset("Assets/MyUnityMcpIntegrationHardening");
			DeletePath(AbsoluteProjectPath(
				"Library/MyUnityMCP/IntegrationHardeningTests"));
			ExecutionHardening.RestoreAfterTests();
		}

		[Test]
		public void Bridge_DiscoversFiveExecutionHardeningTools_AndKeepsThemDisabled()
		{
			CommandRegistry.Initialize();
			string[] names =
			{
				"graphics.get_execution_status",
				"graphics.cancel_execution",
				"graphics.get_execution_history",
				"graphics.get_error_catalog",
				"graphics.get_support_matrix"
			};
			foreach (string name in names)
			{
				Assert.That(CommandRegistry.GetHandler(name), Is.Not.Null, name);
			}

			Type[] types =
			{
				typeof(GetExecutionStatusTool),
				typeof(CancelExecutionTool),
				typeof(GetExecutionHistoryTool),
				typeof(GetErrorCatalogTool),
				typeof(GetSupportMatrixTool)
			};
			foreach (Type type in types)
			{
				Assert.That(GetToolAttribute(type).AutoRegister, Is.False, type.Name);
			}
		}

		[Test]
		public void ToolBridge_SuccessAddsExecutionMetadataAndPersistentHistory()
		{
			object response = ToolBridge.Execute<DummyParameters>(
				new JObject { ["requestId"] = "hardening-success" },
				parameters => Inspection.CreateHardeningResult(
					"graphics.hardening_success",
					parameters.requestId,
					E_MCP_TOOL_STATUS.SUCCESS,
					"success",
					new Dictionary<string, object>()));

			Assert.That(response, Is.Not.Null);
			Assert.That(response.GetType().Name, Is.EqualTo("SuccessResponse"));
			List<ExecutionRecord> history =
				ExecutionHardening.GetHistory(null, 10);
			Assert.That(history.Count, Is.EqualTo(1));
			Assert.That(history[0].state,
				Is.EqualTo(E_MCP_EXECUTION_STATE.SUCCEEDED.ToString()));
			Assert.That(history[0].tool, Is.EqualTo("graphics.hardening_success"));
			Assert.That(File.Exists(Path.Combine(
				_storageRoot,
				"execution-history.jsonl")), Is.True);
			Assert.That(File.Exists(Path.Combine(
				_storageRoot,
				"tool-call-trace.jsonl")), Is.True);
			Assert.That(File.Exists(Path.Combine(
				_storageRoot,
				"structured-log.jsonl")), Is.True);
		}

		[Test]
		public void FailedExecution_ReturnsStructuredErrorAndRetryProcedure()
		{
			ExecutionScope scope =
				ExecutionHardening.Begin(
					"graphics.failed_contract",
					"hardening-failure");
			ToolResult failed =
				Inspection.CreateHardeningResult(
					"graphics.failed_contract",
					"hardening-failure",
					E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
					"Snapshot is stale.",
					new Dictionary<string, object>
					{
						{ "failureCode", "MCP_STALE_SNAPSHOT" }
					});
			failed = ExecutionHardening.Complete(scope, failed);

			Assert.That(failed.error, Is.Not.Null);
			Assert.That(failed.error.code, Is.EqualTo("MCP_STALE_SNAPSHOT"));
			Assert.That(failed.error.retryable, Is.True);
			Assert.That(failed.error.retryAction, Does.Contain("Inspect"));
			Assert.That(failed.execution.executionId, Is.Not.Empty);
			Assert.That(failed.execution.traceId, Is.Not.Empty);
		}

		[Test]
		public void Progress_IsMonotonicAndAvailableByExecutionId()
		{
			ExecutionScope scope =
				ExecutionHardening.Begin(
					"graphics.progress_contract",
					"hardening-progress");
			Assert.That(ExecutionHardening.ReportProgress(
				scope.ExecutionId, 20.0, "INSPECT", "Inspect complete."), Is.True);
			Assert.That(ExecutionHardening.ReportProgress(
				scope.ExecutionId, 10.0, "STALE", "Must not move backward."), Is.True);
			Assert.That(ExecutionHardening.ReportProgress(
				scope.ExecutionId, 75.0, "BAKE", "Bake complete."), Is.True);

			ExecutionRecord record;
			Assert.That(ExecutionHardening.TryGetExecution(
				scope.ExecutionId, out record), Is.True);
			Assert.That(record.progress, Is.EqualTo(75.0));
			Assert.That(record.progressEvents.Count, Is.EqualTo(4));
		}

		[Test]
		public void Cancellation_RequestIsCooperativeAndPersisted()
		{
			ExecutionScope scope =
				ExecutionHardening.Begin(
					"graphics.cancellation_contract",
					"hardening-cancel");
			Assert.That(ExecutionHardening.RequestCancellation(
				scope.ExecutionId,
				"EXECUTION_CANCEL_REQUESTED",
				"test cancellation"), Is.True);
			Assert.That(ExecutionHardening.IsCancellationRequested(
				scope.ExecutionId), Is.True);
			Assert.Throws<OperationCanceledException>(() =>
				ExecutionHardening.ThrowIfCancellationRequested(
					scope.ExecutionId));
		}

		[Test]
		public void Timeout_FinalizesExecutionWithStructuredReason()
		{
			DateTime now = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);
			ExecutionHardening.UtcNowOverrideForTests = () => now;
			ExecutionScope scope =
				ExecutionHardening.Begin(
					"graphics.timeout_contract",
					"hardening-timeout",
					1);
			now = now.AddSeconds(2.0);
			ExecutionHardening.TickForTests();

			ExecutionRecord record;
			Assert.That(ExecutionHardening.TryGetExecution(
				scope.ExecutionId, out record), Is.True);
			Assert.That(record.state,
				Is.EqualTo(E_MCP_EXECUTION_STATE.TIMED_OUT.ToString()));
			Assert.That(record.errorCode, Is.EqualTo("EXECUTION_TIMED_OUT"));
		}

		[Test]
		public void RestartRecovery_MarksPersistedActiveExecutionInterrupted()
		{
			ExecutionScope scope =
				ExecutionHardening.Begin(
					"graphics.restart_contract",
					"hardening-restart");
			Assert.That(File.Exists(Path.Combine(
				_storageRoot,
				"active-executions.json")), Is.True);
			ExecutionHardening.SimulateProcessLossForTests();
			ExecutionHardening.RecoverForTests("UNITY_RESTARTED");

			ExecutionRecord record;
			Assert.That(ExecutionHardening.TryGetExecution(
				scope.ExecutionId, out record), Is.True);
			Assert.That(record.state,
				Is.EqualTo(E_MCP_EXECUTION_STATE.INTERRUPTED.ToString()));
			Assert.That(record.errorCode, Is.EqualTo("UNITY_RESTARTED"));
		}

		[Test]
		public void ArtifactRetention_DeletesExpiredOwnedArtifactsOnly()
		{
			string root =
				ExecutionHardening.OwnedArtifactRootForTests();
			Directory.CreateDirectory(root);
			string expired = Path.Combine(root, "expired.txt");
			string current = Path.Combine(root, "current.txt");
			File.WriteAllText(expired, "expired");
			File.WriteAllText(current, "current");
			File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddDays(-20.0));
			File.SetLastWriteTimeUtc(current, DateTime.UtcNow);

			ExecutionHardening.PruneRetentionForTests();

			Assert.That(File.Exists(expired), Is.False);
			Assert.That(File.Exists(current), Is.True);
		}

		[Test]
		public void SupportMatrix_IsFixedAndDoesNotPromoteUnverifiedTargets()
		{
			Dictionary<string, object> matrix =
				ExecutionHardening.BuildSupportMatrix();
			Assert.That(matrix["contractVersion"], Is.EqualTo("1.0"));
			Assert.That(matrix["packageVersion"], Is.EqualTo("1.0.0"));
			Assert.That(matrix["minimumUnityVersion"], Is.EqualTo("6000.0"));
			Assert.That(matrix["verifiedUnityVersion"], Is.EqualTo("6000.0.75f1"));
			Assert.That((matrix["notVerified"] as string[]).Length,
				Is.GreaterThan(0));
		}

		[Test]
		public void ErrorCatalog_CoversAllRequiredFaultCategories()
		{
			HashSet<string> codes = new HashSet<string>(
				ExecutionHardening.GetErrorCatalog()
					.Select(item => item.code),
				StringComparer.Ordinal);
			string[] required =
			{
				"MCP_STALE_SNAPSHOT",
				"APPROVAL_TOKEN_MISMATCH",
				"PLAN_EXPIRED",
				"EXECUTION_TIMED_OUT",
				"DOMAIN_RELOAD",
				"COMPILE_STARTED",
				"PLAY_MODE_TRANSITION",
				"SCENE_CLOSED",
				"MULTI_SCENE_CONFIGURATION_CHANGED",
				"MCP_CLIENT_DISCONNECTED",
				"UNITY_RESTARTED",
				"APV_BAKE_NO_OUTPUT_DIFF",
				"OUTPUT_ASSET_MISSING",
				"CAMERA_NOT_FOUND",
				"UNSUPPORTED_PIPELINE"
			};
			Assert.That(codes, Is.SupersetOf(required));
		}

		[Test]
		public void PerformanceSummary_ComputesP50P95AndMaximum()
		{
			List<ExecutionRecord> records =
				new List<ExecutionRecord>();
			for (int index = 1; index <= 100; index++)
			{
				records.Add(new ExecutionRecord
				{
					durationMs = index
				});
			}
			Dictionary<string, object> summary =
				ExecutionHardening.BuildPerformanceSummary(records);
			Assert.That(summary["sampleCount"], Is.EqualTo(100));
			Assert.That(Convert.ToDouble(summary["p50DurationMs"]),
				Is.EqualTo(50.5).Within(0.001));
			Assert.That(Convert.ToDouble(summary["p95DurationMs"]),
				Is.EqualTo(95.05).Within(0.001));
			Assert.That(summary["maxDurationMs"], Is.EqualTo(100.0));
		}

		[Test]
		public void LargeScene_InspectionCompletesWithinBudgetWithoutDirtyingScene()
		{
			for (int index = 0; index < 400; index++)
			{
				GameObject gameObject = new GameObject("Hardening Light " + index);
				gameObject.AddComponent<Light>();
			}
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false), Is.True);
			bool dirtyBefore = scene.isDirty;
			Stopwatch stopwatch = Stopwatch.StartNew();

			ToolResult result =
				Inspection.InspectScene(
					"hardening-large-scene",
					true,
					200,
					new[] { "LIGHT" },
					null,
					null);
			stopwatch.Stop();

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));
			Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(20.0));
			Dictionary<string, object> data = ResultData(result);
			Assert.That(Convert.ToInt32(data["totalItems"]),
				Is.GreaterThanOrEqualTo(400));
		}

		[Test]
		public void PlanSceneChange_RejectsApplyWithoutAdditionalMutation()
		{
			Dictionary<string, object> plan = PrepareLightCreatePlan("hardening-stale");
			new GameObject("External Scene Change");
			Session.NotifyMutationApplied();
			int rootCountBeforeApply = SceneManager.GetActiveScene().rootCount;

			ToolResult result = ApplyLightPlan(
				"hardening-stale-apply",
				plan,
				plan["approvalToken"] as string);

			Assert.That(result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
			Assert.That(UnityEngine.Object.FindObjectsByType<Light>(
				FindObjectsInactive.Include,
				FindObjectsSortMode.None).Length, Is.EqualTo(0));
			Assert.That(SceneManager.GetActiveScene().rootCount,
				Is.EqualTo(rootCountBeforeApply));
		}

		[Test]
		public void ApprovalTokenMismatch_RejectsApplyWithoutSceneMutation()
		{
			Dictionary<string, object> plan =
				PrepareLightCreatePlan("hardening-token");
			bool dirtyBefore = SceneManager.GetActiveScene().isDirty;

			ToolResult result = ApplyLightPlan(
				"hardening-token-apply",
				plan,
				"incorrect-token");

			Assert.That(result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
			Assert.That(UnityEngine.Object.FindObjectsByType<Light>(
				FindObjectsInactive.Include,
				FindObjectsSortMode.None).Length, Is.EqualTo(0));
			Assert.That(SceneManager.GetActiveScene().isDirty,
				Is.EqualTo(dirtyBefore));
		}

		[Test]
		public void PlanExpiry_RejectsApplyWithoutSceneMutation()
		{
			Dictionary<string, object> data =
				PrepareLightCreatePlan("hardening-expired");
			ExecutableLightPlan storedPlan;
			E_MCP_TOOL_STATUS failureStatus;
			string failureMessage;
			Assert.That(MutationSession.TryGetPlan(
				data["planId"] as string,
				Convert.ToInt64(data["expectedRevision"]),
				data["approvalToken"] as string,
				out storedPlan,
				out failureStatus,
				out failureMessage), Is.True, failureMessage);
			storedPlan.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1.0);

			ToolResult result = ApplyLightPlan(
				"hardening-expired-apply",
				data,
				data["approvalToken"] as string);

			Assert.That(result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.SESSION_EXPIRED.ToString()));
			Assert.That(UnityEngine.Object.FindObjectsByType<Light>(
				FindObjectsInactive.Include,
				FindObjectsSortMode.None).Length, Is.EqualTo(0));
		}

		[Test]
		public void BakeOutputMissing_ReturnsStructuredFailureWithoutSceneDamage()
		{
			CreateSavedSceneWithCamera();
			ConfigureApvEnvironment("URP");
			ConfigureApvJob(() => false, () => Snapshot("same"), () => true);
			Dictionary<string, object> plan = ResultData(PrepareApv());
			bool dirtyBefore = SceneManager.GetActiveScene().isDirty;
			ExecutionScope scope =
				ExecutionHardening.Begin(
					"graphics.start_apv_bake",
					"hardening-bake-output");

			ToolResult result = StartApv(plan);
			result = ExecutionHardening.Complete(scope, result);

			Assert.That(result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.FAILED.ToString()));
			Assert.That(result.error.code, Is.EqualTo("APV_BAKE_NO_OUTPUT_DIFF"));
			Assert.That(SceneManager.GetActiveScene().isDirty,
				Is.EqualTo(dirtyBefore));
		}

		[Test]
		public void UnsupportedPipeline_RejectsApvPlanWithoutSceneDamage()
		{
			CreateSavedSceneWithCamera();
			ConfigureApvEnvironment("BUILT_IN");
			bool dirtyBefore = SceneManager.GetActiveScene().isDirty;

			ToolResult result = PrepareApv();

			Assert.That(result.status,
				Is.EqualTo(E_MCP_TOOL_STATUS.UNSUPPORTED.ToString()));
			Assert.That(SceneManager.GetActiveScene().isDirty,
				Is.EqualTo(dirtyBefore));
		}

		[Test]
		public void DeletedCamera_RejectsCaptureWithoutAdditionalSceneDamage()
		{
			Camera camera = CreateSavedSceneWithCamera();
			string cameraId = GlobalObjectId.GetGlobalObjectIdSlow(camera).ToString();
			UnityEngine.Object.DestroyImmediate(camera.gameObject);
			Session.NotifyMutationApplied();
			bool dirtyBefore = SceneManager.GetActiveScene().isDirty;

			ToolResult result =
				Inspection.CaptureEvidence(
					"hardening-deleted-camera",
					cameraId,
					Session.Revision,
					64,
					64,
					new[] { "COLOR" },
					"deleted-camera",
					32);

			Assert.That(result.IsSuccessful, Is.False);
			Assert.That(SceneManager.GetActiveScene().isDirty,
				Is.EqualTo(dirtyBefore));
		}

		[TestCase("DOMAIN_RELOAD")]
		[TestCase("COMPILE_STARTED")]
		[TestCase("PLAY_MODE_TRANSITION")]
		[TestCase("SCENE_CLOSED")]
		[TestCase("MULTI_SCENE_CONFIGURATION_CHANGED")]
		[TestCase("MCP_CLIENT_DISCONNECTED")]
		[TestCase("UNITY_RESTARTED")]
		public void LifecycleFault_InterruptsExecutionWithoutDirtyingScene(string code)
		{
			Scene scene = SceneManager.GetActiveScene();
			bool dirtyBefore = scene.isDirty;
			ExecutionScope scope =
				ExecutionHardening.Begin(
					"graphics.lifecycle_fault",
					"hardening-lifecycle-" + code.ToLowerInvariant());

			if (code == "MCP_CLIENT_DISCONNECTED")
			{
				ExecutionLifecycle.NotifyClientDisconnected("mcp-client");
			}
			else if (code == "UNITY_RESTARTED")
			{
				ExecutionHardening.SimulateProcessLossForTests();
				ExecutionHardening.RecoverForTests(code);
			}
			else
			{
				ExecutionHardening.InterruptAllForTests(code);
			}

			ExecutionRecord record;
			Assert.That(ExecutionHardening.TryGetExecution(
				scope.ExecutionId, out record), Is.True);
			Assert.That(record.state,
				Is.EqualTo(E_MCP_EXECUTION_STATE.INTERRUPTED.ToString()));
			Assert.That(record.errorCode, Is.EqualTo(code));
			Assert.That(scene.isDirty, Is.EqualTo(dirtyBefore));
		}

		[Test]
		public void EndToEnd_InspectSnapshotPrepareApproveApplySaveBakeCaptureEvaluateRefine_Completes()
		{
			Camera camera = CreateSavedSceneWithCamera();
			ToolResult project =
				Inspection.InspectProject(
					"hardening-e2e-project",
					new[] { "PC" },
					new[] { "No automatic save" });
			Assert.That(project.IsSuccessful, Is.True, project.summary);

			ToolResult inspect =
				Inspection.InspectScene(
					"hardening-e2e-snapshot",
					true,
					200,
					null,
					null,
					null);
			Dictionary<string, object> snapshot = ResultData(inspect);
			Assert.That(snapshot["snapshotId"] as string, Is.Not.Empty);

			Dictionary<string, object> mutationPlan =
				PrepareLightCreatePlan("hardening-e2e");
			ToolResult applied = ApplyLightPlan(
				"hardening-e2e-apply",
				mutationPlan,
				mutationPlan["approvalToken"] as string);
			Assert.That(applied.IsSuccessful, Is.True, applied.summary);
			Assert.That(UnityEngine.Object.FindObjectsByType<Light>(
				FindObjectsInactive.Include,
				FindObjectsSortMode.None).Length, Is.EqualTo(1));

			ToolResult savePlanResult =
				Inspection.PrepareSavePlan(
					"hardening-e2e-save-plan",
					Session.Revision,
					new[]
					{
						new SaveTargetInput
						{
							scenePath = TEMP_SCENE_PATH
						}
					});
			Dictionary<string, object> savePlan = ResultData(savePlanResult);
			ToolResult saved =
				Inspection.ApplySavePlan(
					"hardening-e2e-save",
					savePlan["planId"] as string,
					Convert.ToInt64(savePlan["expectedRevision"]),
					savePlan["approvalToken"] as string,
					"EXPLICIT_SCENE");
			Assert.That(saved.IsSuccessful, Is.True, saved.summary);
			Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);

			ConfigureApvEnvironment("URP");
			int outputSnapshot = 0;
			ConfigureApvJob(
				() => false,
				() => outputSnapshot++ == 0
					? Snapshot("old")
					: Snapshot("new"),
				() => true);
			Dictionary<string, object> apvPlan = ResultData(PrepareApv());
			ToolResult baked = StartApv(apvPlan);
			Assert.That(ResultData(baked)["jobStatus"],
				Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.SUCCEEDED.ToString()));

			CaptureEvidenceRecord capture = StoreFakeCapture(camera);
			Assert.That(capture.Artifacts.Select(item => item.Channel),
				Is.SupersetOf(new[] { "COLOR", "LINEAR_DEPTH", "OBJECT_ID" }));
			Dictionary<string, object> profile = ResultData(
				Inspection.PrepareAcceptanceProfile(
					"hardening-e2e-profile",
					Session.Revision,
					CreateProfileInput()));
			ToolResult evaluated =
				Inspection.EvaluateCapture(
					"hardening-e2e-evaluate",
					capture.CaptureId,
					Session.Revision,
					capture.EvidenceDigest,
					profile["profileId"] as string,
					new[] { Measurement("composition", 25.0) },
					Performance());
			Dictionary<string, object> evaluation = ResultData(evaluated);
			Assert.That(evaluation["decision"],
				Is.EqualTo(E_GRAPHICS_VISUAL_EVALUATION_DECISION.FAILED.ToString()));

			DirectionPlan source = StoreDirectionPlan();
			ToolResult refined =
				Inspection.RefineFromEvaluation(
					"hardening-e2e-refine",
					source.PlanId,
					evaluation["evaluationId"] as string,
					Session.Revision);
			Assert.That(refined.IsSuccessful, Is.True, refined.summary);
			Assert.That(ResultData(refined)["planId"] as string, Is.Not.Empty);
		}

		private static Dictionary<string, object> PrepareLightCreatePlan(string requestId)
		{
			ToolResult direction =
				Inspection.CompileDirection(
					requestId + "-direction",
					"Create one explicit key light safely.",
					null,
					null,
					null,
					null,
					new[] { "Key" },
					new[] { "Neutral" },
					null,
					null,
					null,
					new[] { "Preserve frame time" },
					new[] { "PC" },
					new[] { "Automatic Save prohibited" },
					null);
			Dictionary<string, object> directionData = ResultData(direction);
			ToolResult prepare =
				Inspection.PrepareLightPlan(
					requestId + "-prepare",
					directionData["planId"] as string,
					Convert.ToInt64(directionData["expectedRevision"]),
					new[]
					{
						new LightOperationInput
						{
							operationId = "create-key",
							operation = "LIGHT_CREATE",
							name = "Hardening Key Light",
							lightType = "Directional",
							color = new ColorInput
							{
								r = 1.0f,
								g = 0.9f,
								b = 0.8f,
								a = 1.0f
							},
							intensity = 2.0f,
							shadows = "Soft",
							position = Vector(0.0f, 3.0f, 0.0f),
							eulerAngles = Vector(45.0f, -30.0f, 0.0f),
							enabled = true
						}
					});
			Assert.That(prepare.IsSuccessful, Is.True, prepare.summary);
			return ResultData(prepare);
		}

		private static ToolResult ApplyLightPlan(
			string requestId,
			Dictionary<string, object> data,
			string token)
		{
			return Inspection.ApplyPlan(
				requestId,
				data["planId"] as string,
				Convert.ToInt64(data["expectedRevision"]),
				token,
				"NONE");
		}

		private static Camera CreateSavedSceneWithCamera()
		{
			new GameObject("Hardening Target");
			GameObject cameraObject = new GameObject("Hardening Camera");
			Camera camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.Color;
			camera.backgroundColor = Color.black;
			Assert.That(EditorSceneManager.SaveScene(
				SceneManager.GetActiveScene(),
				TEMP_SCENE_PATH,
				false), Is.True);
			return camera;
		}

		private static void ConfigureApvEnvironment(string pipelineKind)
		{
			AdaptiveProbeVolumeBakeSession.EnvironmentOverrideForTests = input =>
				new ApvEnvironment
				{
					PipelineKind = pipelineKind,
					PipelineAssetType = pipelineKind == "URP"
						? "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
						: "BuiltIn",
					BakingSetAssetPath = FAKE_BAKING_SET_PATH,
					BakingSetType = "UnityEngine.Rendering.ProbeVolumeBakingSet",
					BakingSetDigest = "fake-baking-set-digest",
					ScenePaths = new List<string> { TEMP_SCENE_PATH },
					LightingScenarios = new List<string> { "Day" },
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
			AdaptiveProbeVolumeBakeSession.OutputSnapshotOverrideForTests =
				plan => snapshot();
		}

		private static ToolResult PrepareApv()
		{
			return Inspection.PrepareApvBakePlan(
				"hardening-apv-prepare",
				Session.Revision,
				new ApvBakePlanInput
				{
					bakingSetAssetPath = FAKE_BAKING_SET_PATH,
					lightingScenario = "Day",
					scenePaths = new[] { TEMP_SCENE_PATH },
					outputAssetRoots = new[] { "Assets" },
					timeoutSeconds = 300
				});
		}

		private static ToolResult StartApv(
			Dictionary<string, object> plan)
		{
			return Inspection.StartApvBake(
				"hardening-apv-start",
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

		private static CaptureEvidenceRecord StoreFakeCapture(Camera camera)
		{
			string mapPath = AbsoluteProjectPath(MAP_RELATIVE_PATH);
			Directory.CreateDirectory(Path.GetDirectoryName(mapPath));
			File.WriteAllText(mapPath, "[]");
			CaptureEvidenceRecord capture =
				new CaptureEvidenceRecord
				{
					Revision = Session.Revision,
					CameraObjectId = GlobalObjectId.GetGlobalObjectIdSlow(camera).ToString(),
					CameraSceneHandle = IdentityCompatibility.GetSceneToken(camera.gameObject.scene),
					CameraScenePath = camera.gameObject.scene.path,
					CameraBaselineDigest = "camera-digest",
					EvidenceDigest = "hardening-evidence-" + Guid.NewGuid().ToString("N"),
					BundlePath = "Library/MyUnityMCP/IntegrationHardeningTests/Capture",
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

		private static AcceptanceProfileInput CreateProfileInput()
		{
			return new AcceptanceProfileInput
			{
				profileName = "Integration Hardening Acceptance",
				minimumPassScore = 70.0,
				criteria = new[]
				{
					new AcceptanceCriterionInput
					{
						criterionId = "composition",
						displayName = "Composition",
						weight = 1.0,
						minimumScore = 60.0,
						criticalFailureBelow = 30.0,
						required = true,
						recommendedActions = new[] { "Rebalance the composition." }
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

		private static EvaluationMeasurementInput Measurement(
			string criterionId,
			double score)
		{
			return new EvaluationMeasurementInput
			{
				criterionId = criterionId,
				score = score,
				confidence = 0.95,
				summary = "External evaluator measurement.",
				evidence = new[] { "Capture bundle reviewed." }
			};
		}

		private static PerformanceMeasurementInput Performance()
		{
			return new PerformanceMeasurementInput
			{
				cpuFrameMs = 10.0,
				gpuFrameMs = 11.0,
				memoryMb = 500.0,
				drawCalls = 100,
				source = "Integration Hardening Test"
			};
		}

		private static DirectionPlan StoreDirectionPlan()
		{
			DirectionPlan plan = new DirectionPlan
			{
				Revision = Session.Revision,
				CreatedUtc = DateTime.UtcNow,
				ProjectContext = new Dictionary<string, object>
				{
					{ "test", "integration-hardening" }
				},
				VisualIntent = new Dictionary<string, object>
				{
					{ "goal", "Close the production loop safely." }
				}
			};
			Session.StorePlan(plan);
			return plan;
		}

		private static Vector3Input Vector(float x, float y, float z)
		{
			return new Vector3Input { x = x, y = y, z = z };
		}

		private static Dictionary<string, object> ResultData(
			ToolResult result)
		{
			Dictionary<string, object> data = result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null, result.summary + "\n" +
				JsonConvert.SerializeObject(result.data));
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

		private static void DeletePath(string path)
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, true);
			}
			else if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}
}

#endif