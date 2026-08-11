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
	public sealed class UnityGraphicsMcpIntegrationHardeningTests
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
			UnityGraphicsMcpSession.ClearSnapshots();
			UnityGraphicsMcpSession.ClearPlans();
			UnityGraphicsMcpSaveEvaluationSession.ClearForTests();
			UnityGraphicsMcpDependencyBakeSession.ClearForTests();
			UnityGraphicsMcpCaptureEvidenceSession.ClearForTests();
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.ClearForTests();
			UnityGraphicsMcpVisualAcceptanceSession.ClearForTests();
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			AssetDatabase.DeleteAsset("Assets/MyUnityMcpIntegrationHardening");
			_storageRoot = AbsoluteProjectPath(
				"Library/MyUnityMCP/IntegrationHardeningTests/Execution");
			UnityGraphicsMcpExecutionHardening.ResetForTests(_storageRoot);
			DeletePath(AbsoluteProjectPath(
				"Library/MyUnityMCP/IntegrationHardeningTests"));
			Directory.CreateDirectory(_storageRoot);
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
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			AssetDatabase.DeleteAsset("Assets/MyUnityMcpIntegrationHardening");
			DeletePath(AbsoluteProjectPath(
				"Library/MyUnityMCP/IntegrationHardeningTests"));
			UnityGraphicsMcpExecutionHardening.RestoreAfterTests();
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
				typeof(GraphicsGetExecutionStatusTool),
				typeof(GraphicsCancelExecutionTool),
				typeof(GraphicsGetExecutionHistoryTool),
				typeof(GraphicsGetErrorCatalogTool),
				typeof(GraphicsGetSupportMatrixTool)
			};
			foreach (Type type in types)
			{
				Assert.That(GetToolAttribute(type).AutoRegister, Is.False, type.Name);
			}
		}

		[Test]
		public void ToolBridge_SuccessAddsExecutionMetadataAndPersistentHistory()
		{
			object response = UnityGraphicsMcpToolBridge.Execute<DummyParameters>(
				new JObject { ["requestId"] = "hardening-success" },
				parameters => UnityGraphicsMcpInspection.CreateHardeningResult(
					"graphics.hardening_success",
					parameters.requestId,
					E_MCP_TOOL_STATUS.SUCCESS,
					"success",
					new Dictionary<string, object>()));

			Assert.That(response, Is.Not.Null);
			Assert.That(response.GetType().Name, Is.EqualTo("SuccessResponse"));
			List<UnityGraphicsMcpExecutionRecord> history =
				UnityGraphicsMcpExecutionHardening.GetHistory(null, 10);
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
			UnityGraphicsMcpExecutionScope scope =
				UnityGraphicsMcpExecutionHardening.Begin(
					"graphics.failed_contract",
					"hardening-failure");
			UnityGraphicsMcpToolResult failed =
				UnityGraphicsMcpInspection.CreateHardeningResult(
					"graphics.failed_contract",
					"hardening-failure",
					E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
					"Snapshot is stale.",
					new Dictionary<string, object>
					{
						{ "failureCode", "MCP_STALE_SNAPSHOT" }
					});
			failed = UnityGraphicsMcpExecutionHardening.Complete(scope, failed);

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
			UnityGraphicsMcpExecutionScope scope =
				UnityGraphicsMcpExecutionHardening.Begin(
					"graphics.progress_contract",
					"hardening-progress");
			Assert.That(UnityGraphicsMcpExecutionHardening.ReportProgress(
				scope.ExecutionId, 20.0, "INSPECT", "Inspect complete."), Is.True);
			Assert.That(UnityGraphicsMcpExecutionHardening.ReportProgress(
				scope.ExecutionId, 10.0, "STALE", "Must not move backward."), Is.True);
			Assert.That(UnityGraphicsMcpExecutionHardening.ReportProgress(
				scope.ExecutionId, 75.0, "BAKE", "Bake complete."), Is.True);

			UnityGraphicsMcpExecutionRecord record;
			Assert.That(UnityGraphicsMcpExecutionHardening.TryGetExecution(
				scope.ExecutionId, out record), Is.True);
			Assert.That(record.progress, Is.EqualTo(75.0));
			Assert.That(record.progressEvents.Count, Is.EqualTo(4));
		}

		[Test]
		public void Cancellation_RequestIsCooperativeAndPersisted()
		{
			UnityGraphicsMcpExecutionScope scope =
				UnityGraphicsMcpExecutionHardening.Begin(
					"graphics.cancellation_contract",
					"hardening-cancel");
			Assert.That(UnityGraphicsMcpExecutionHardening.RequestCancellation(
				scope.ExecutionId,
				"EXECUTION_CANCEL_REQUESTED",
				"test cancellation"), Is.True);
			Assert.That(UnityGraphicsMcpExecutionHardening.IsCancellationRequested(
				scope.ExecutionId), Is.True);
			Assert.Throws<OperationCanceledException>(() =>
				UnityGraphicsMcpExecutionHardening.ThrowIfCancellationRequested(
					scope.ExecutionId));
		}

		[Test]
		public void Timeout_FinalizesExecutionWithStructuredReason()
		{
			DateTime now = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);
			UnityGraphicsMcpExecutionHardening.UtcNowOverrideForTests = () => now;
			UnityGraphicsMcpExecutionScope scope =
				UnityGraphicsMcpExecutionHardening.Begin(
					"graphics.timeout_contract",
					"hardening-timeout",
					1);
			now = now.AddSeconds(2.0);
			UnityGraphicsMcpExecutionHardening.TickForTests();

			UnityGraphicsMcpExecutionRecord record;
			Assert.That(UnityGraphicsMcpExecutionHardening.TryGetExecution(
				scope.ExecutionId, out record), Is.True);
			Assert.That(record.state,
				Is.EqualTo(E_MCP_EXECUTION_STATE.TIMED_OUT.ToString()));
			Assert.That(record.errorCode, Is.EqualTo("EXECUTION_TIMED_OUT"));
		}

		[Test]
		public void RestartRecovery_MarksPersistedActiveExecutionInterrupted()
		{
			UnityGraphicsMcpExecutionScope scope =
				UnityGraphicsMcpExecutionHardening.Begin(
					"graphics.restart_contract",
					"hardening-restart");
			Assert.That(File.Exists(Path.Combine(
				_storageRoot,
				"active-executions.json")), Is.True);
			UnityGraphicsMcpExecutionHardening.SimulateProcessLossForTests();
			UnityGraphicsMcpExecutionHardening.RecoverForTests("UNITY_RESTARTED");

			UnityGraphicsMcpExecutionRecord record;
			Assert.That(UnityGraphicsMcpExecutionHardening.TryGetExecution(
				scope.ExecutionId, out record), Is.True);
			Assert.That(record.state,
				Is.EqualTo(E_MCP_EXECUTION_STATE.INTERRUPTED.ToString()));
			Assert.That(record.errorCode, Is.EqualTo("UNITY_RESTARTED"));
		}

		[Test]
		public void ArtifactRetention_DeletesExpiredOwnedArtifactsOnly()
		{
			string root =
				UnityGraphicsMcpExecutionHardening.OwnedArtifactRootForTests();
			Directory.CreateDirectory(root);
			string expired = Path.Combine(root, "expired.txt");
			string current = Path.Combine(root, "current.txt");
			File.WriteAllText(expired, "expired");
			File.WriteAllText(current, "current");
			File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddDays(-20.0));
			File.SetLastWriteTimeUtc(current, DateTime.UtcNow);

			UnityGraphicsMcpExecutionHardening.PruneRetentionForTests();

			Assert.That(File.Exists(expired), Is.False);
			Assert.That(File.Exists(current), Is.True);
		}

		[Test]
		public void SupportMatrix_IsFixedAndDoesNotPromoteUnverifiedTargets()
		{
			Dictionary<string, object> matrix =
				UnityGraphicsMcpExecutionHardening.BuildSupportMatrix();
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
				UnityGraphicsMcpExecutionHardening.GetErrorCatalog()
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
			List<UnityGraphicsMcpExecutionRecord> records =
				new List<UnityGraphicsMcpExecutionRecord>();
			for (int index = 1; index <= 100; index++)
			{
				records.Add(new UnityGraphicsMcpExecutionRecord
				{
					durationMs = index
				});
			}
			Dictionary<string, object> summary =
				UnityGraphicsMcpExecutionHardening.BuildPerformanceSummary(records);
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

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.InspectScene(
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
			UnityGraphicsMcpSession.NotifyMutationApplied();
			int rootCountBeforeApply = SceneManager.GetActiveScene().rootCount;

			UnityGraphicsMcpToolResult result = ApplyLightPlan(
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

			UnityGraphicsMcpToolResult result = ApplyLightPlan(
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
			UnityGraphicsMcpExecutableLightPlan storedPlan;
			E_MCP_TOOL_STATUS failureStatus;
			string failureMessage;
			Assert.That(UnityGraphicsMcpMutationSession.TryGetPlan(
				data["planId"] as string,
				Convert.ToInt64(data["expectedRevision"]),
				data["approvalToken"] as string,
				out storedPlan,
				out failureStatus,
				out failureMessage), Is.True, failureMessage);
			storedPlan.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1.0);

			UnityGraphicsMcpToolResult result = ApplyLightPlan(
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
			UnityGraphicsMcpExecutionScope scope =
				UnityGraphicsMcpExecutionHardening.Begin(
					"graphics.start_apv_bake",
					"hardening-bake-output");

			UnityGraphicsMcpToolResult result = StartApv(plan);
			result = UnityGraphicsMcpExecutionHardening.Complete(scope, result);

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

			UnityGraphicsMcpToolResult result = PrepareApv();

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
			UnityGraphicsMcpSession.NotifyMutationApplied();
			bool dirtyBefore = SceneManager.GetActiveScene().isDirty;

			UnityGraphicsMcpToolResult result =
				UnityGraphicsMcpInspection.CaptureEvidence(
					"hardening-deleted-camera",
					cameraId,
					UnityGraphicsMcpSession.Revision,
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
			UnityGraphicsMcpExecutionScope scope =
				UnityGraphicsMcpExecutionHardening.Begin(
					"graphics.lifecycle_fault",
					"hardening-lifecycle-" + code.ToLowerInvariant());

			if (code == "MCP_CLIENT_DISCONNECTED")
			{
				UnityGraphicsMcpExecutionLifecycle.NotifyClientDisconnected("mcp-client");
			}
			else if (code == "UNITY_RESTARTED")
			{
				UnityGraphicsMcpExecutionHardening.SimulateProcessLossForTests();
				UnityGraphicsMcpExecutionHardening.RecoverForTests(code);
			}
			else
			{
				UnityGraphicsMcpExecutionHardening.InterruptAllForTests(code);
			}

			UnityGraphicsMcpExecutionRecord record;
			Assert.That(UnityGraphicsMcpExecutionHardening.TryGetExecution(
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
			UnityGraphicsMcpToolResult project =
				UnityGraphicsMcpInspection.InspectProject(
					"hardening-e2e-project",
					new[] { "PC" },
					new[] { "No automatic save" });
			Assert.That(project.IsSuccessful, Is.True, project.summary);

			UnityGraphicsMcpToolResult inspect =
				UnityGraphicsMcpInspection.InspectScene(
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
			UnityGraphicsMcpToolResult applied = ApplyLightPlan(
				"hardening-e2e-apply",
				mutationPlan,
				mutationPlan["approvalToken"] as string);
			Assert.That(applied.IsSuccessful, Is.True, applied.summary);
			Assert.That(UnityEngine.Object.FindObjectsByType<Light>(
				FindObjectsInactive.Include,
				FindObjectsSortMode.None).Length, Is.EqualTo(1));

			UnityGraphicsMcpToolResult savePlanResult =
				UnityGraphicsMcpInspection.PrepareSavePlan(
					"hardening-e2e-save-plan",
					UnityGraphicsMcpSession.Revision,
					new[]
					{
						new UnityGraphicsMcpSaveTargetInput
						{
							scenePath = TEMP_SCENE_PATH
						}
					});
			Dictionary<string, object> savePlan = ResultData(savePlanResult);
			UnityGraphicsMcpToolResult saved =
				UnityGraphicsMcpInspection.ApplySavePlan(
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
			UnityGraphicsMcpToolResult baked = StartApv(apvPlan);
			Assert.That(ResultData(baked)["jobStatus"],
				Is.EqualTo(E_GRAPHICS_APV_JOB_STATUS.SUCCEEDED.ToString()));

			UnityGraphicsMcpCaptureEvidenceRecord capture = StoreFakeCapture(camera);
			Assert.That(capture.Artifacts.Select(item => item.Channel),
				Is.SupersetOf(new[] { "COLOR", "LINEAR_DEPTH", "OBJECT_ID" }));
			Dictionary<string, object> profile = ResultData(
				UnityGraphicsMcpInspection.PrepareAcceptanceProfile(
					"hardening-e2e-profile",
					UnityGraphicsMcpSession.Revision,
					CreateProfileInput()));
			UnityGraphicsMcpToolResult evaluated =
				UnityGraphicsMcpInspection.EvaluateCapture(
					"hardening-e2e-evaluate",
					capture.CaptureId,
					UnityGraphicsMcpSession.Revision,
					capture.EvidenceDigest,
					profile["profileId"] as string,
					new[] { Measurement("composition", 25.0) },
					Performance());
			Dictionary<string, object> evaluation = ResultData(evaluated);
			Assert.That(evaluation["decision"],
				Is.EqualTo(E_GRAPHICS_VISUAL_EVALUATION_DECISION.FAILED.ToString()));

			UnityGraphicsMcpDirectionPlan source = StoreDirectionPlan();
			UnityGraphicsMcpToolResult refined =
				UnityGraphicsMcpInspection.RefineFromEvaluation(
					"hardening-e2e-refine",
					source.PlanId,
					evaluation["evaluationId"] as string,
					UnityGraphicsMcpSession.Revision);
			Assert.That(refined.IsSuccessful, Is.True, refined.summary);
			Assert.That(ResultData(refined)["planId"] as string, Is.Not.Empty);
		}

		private static Dictionary<string, object> PrepareLightCreatePlan(string requestId)
		{
			UnityGraphicsMcpToolResult direction =
				UnityGraphicsMcpInspection.CompileDirection(
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
			UnityGraphicsMcpToolResult prepare =
				UnityGraphicsMcpInspection.PrepareLightPlan(
					requestId + "-prepare",
					directionData["planId"] as string,
					Convert.ToInt64(directionData["expectedRevision"]),
					new[]
					{
						new UnityGraphicsMcpLightOperationInput
						{
							operationId = "create-key",
							operation = "LIGHT_CREATE",
							name = "Hardening Key Light",
							lightType = "Directional",
							color = new UnityGraphicsMcpColorInput
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

		private static UnityGraphicsMcpToolResult ApplyLightPlan(
			string requestId,
			Dictionary<string, object> data,
			string token)
		{
			return UnityGraphicsMcpInspection.ApplyPlan(
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
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.EnvironmentOverrideForTests = input =>
				new UnityGraphicsMcpApvEnvironment
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
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.StartOverrideForTests = plan =>
				new UnityGraphicsMcpApvBackendState { Started = true };
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.IsRunningOverrideForTests = isRunning;
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.CancelOverrideForTests = cancel;
			UnityGraphicsMcpAdaptiveProbeVolumeBakeSession.OutputSnapshotOverrideForTests =
				plan => snapshot();
		}

		private static UnityGraphicsMcpToolResult PrepareApv()
		{
			return UnityGraphicsMcpInspection.PrepareApvBakePlan(
				"hardening-apv-prepare",
				UnityGraphicsMcpSession.Revision,
				new UnityGraphicsMcpApvBakePlanInput
				{
					bakingSetAssetPath = FAKE_BAKING_SET_PATH,
					lightingScenario = "Day",
					scenePaths = new[] { TEMP_SCENE_PATH },
					outputAssetRoots = new[] { "Assets" },
					timeoutSeconds = 300
				});
		}

		private static UnityGraphicsMcpToolResult StartApv(
			Dictionary<string, object> plan)
		{
			return UnityGraphicsMcpInspection.StartApvBake(
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

		private static UnityGraphicsMcpCaptureEvidenceRecord StoreFakeCapture(Camera camera)
		{
			string mapPath = AbsoluteProjectPath(MAP_RELATIVE_PATH);
			Directory.CreateDirectory(Path.GetDirectoryName(mapPath));
			File.WriteAllText(mapPath, "[]");
			UnityGraphicsMcpCaptureEvidenceRecord capture =
				new UnityGraphicsMcpCaptureEvidenceRecord
				{
					Revision = UnityGraphicsMcpSession.Revision,
					CameraObjectId = GlobalObjectId.GetGlobalObjectIdSlow(camera).ToString(),
					CameraSceneHandle = UnityGraphicsMcpIdentityCompatibility.GetSceneToken(camera.gameObject.scene),
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

		private static UnityGraphicsMcpAcceptanceProfileInput CreateProfileInput()
		{
			return new UnityGraphicsMcpAcceptanceProfileInput
			{
				profileName = "Integration Hardening Acceptance",
				minimumPassScore = 70.0,
				criteria = new[]
				{
					new UnityGraphicsMcpAcceptanceCriterionInput
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

		private static UnityGraphicsMcpEvaluationMeasurementInput Measurement(
			string criterionId,
			double score)
		{
			return new UnityGraphicsMcpEvaluationMeasurementInput
			{
				criterionId = criterionId,
				score = score,
				confidence = 0.95,
				summary = "External evaluator measurement.",
				evidence = new[] { "Capture bundle reviewed." }
			};
		}

		private static UnityGraphicsMcpPerformanceMeasurementInput Performance()
		{
			return new UnityGraphicsMcpPerformanceMeasurementInput
			{
				cpuFrameMs = 10.0,
				gpuFrameMs = 11.0,
				memoryMb = 500.0,
				drawCalls = 100,
				source = "Integration Hardening Test"
			};
		}

		private static UnityGraphicsMcpDirectionPlan StoreDirectionPlan()
		{
			UnityGraphicsMcpDirectionPlan plan = new UnityGraphicsMcpDirectionPlan
			{
				Revision = UnityGraphicsMcpSession.Revision,
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
			UnityGraphicsMcpSession.StorePlan(plan);
			return plan;
		}

		private static UnityGraphicsMcpVector3Input Vector(float x, float y, float z)
		{
			return new UnityGraphicsMcpVector3Input { x = x, y = y, z = z };
		}

		private static Dictionary<string, object> ResultData(
			UnityGraphicsMcpToolResult result)
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