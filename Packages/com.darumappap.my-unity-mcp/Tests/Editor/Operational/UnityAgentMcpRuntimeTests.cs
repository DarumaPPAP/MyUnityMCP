#if UNITY_EDITOR

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityAgentMcp;
using UnityGraphicsMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityAgentMcpRuntimeTests
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

		private static UnityAgentMcpStepInput GraphicsInspectStep(string stepId = "inspect", params string[] dependsOn)
		{
			return new UnityAgentMcpStepInput
			{
				stepId = stepId,
				domainId = "unity_graphics_mcp",
				toolName = "graphics.inspect_project",
				toolGroup = "inspect",
				dependsOn = dependsOn ?? Array.Empty<string>(),
				parameters = new JObject()
			};
		}

		private static UnityAgentMcpStepInput GraphicsMutationStep(string stepId = "mutate", params string[] dependsOn)
		{
			return new UnityAgentMcpStepInput
			{
				stepId = stepId,
				domainId = "unity_graphics_mcp",
				toolName = "graphics.apply_plan",
				toolGroup = "mutate",
				dependsOn = dependsOn ?? Array.Empty<string>(),
				parameters = new JObject()
			};
		}

		private static UnityAgentMcpStepInput ProfilerInspectStep(string stepId = "profiler")
		{
			return new UnityAgentMcpStepInput
			{
				stepId = stepId,
				domainId = "unity_profiler_mcp",
				toolName = "profiler.inspect_environment",
				toolGroup = "profiler",
				dependsOn = Array.Empty<string>(),
				parameters = new JObject()
			};
		}

		private static string CatalogJson()
		{
			return File.ReadAllText("Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpCatalog.json");
		}

		private static bool TryParseCatalog(JObject root, out AgentCatalogSnapshot snapshot, out string error)
		{
			return AgentCatalogService.TryParse(
				root.ToString(),
				UnityAgentMcpRuntime.RegisteredDomainDelegateNamesForTests,
				out snapshot,
				out error);
		}

		[Test]
		public void InspectCapabilities_LoadsCatalogAndKeepsDirectMutationDisabled()
		{
			JObject result = UnityAgentMcpRuntime.Instance.InspectCapabilities();

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("directUnityMutation"), Is.False);
			Assert.That(result.Value<bool>("cooperativeExecution"), Is.True);
			Assert.That(result.Value<bool>("integrationCandidateExecutionEnabled"), Is.True);
			Assert.That(result.Value<int>("defaultExecutionTimeoutSeconds"), Is.GreaterThan(0));
			Assert.That(result["domains"]?.Any(), Is.True);
		}

		[Test]
		public void CatalogV5_ProjectsLegacyInspectCapabilitiesShapeForAllDomains()
		{
			JObject result = UnityAgentMcpRuntime.Instance.InspectCapabilities();
			JArray domains = (JArray)result["domains"];
			JArray catalogDomains = (JArray)JObject.Parse(CatalogJson())["domains"];
			string[] expectedDomainIds =
			{
				"unity_graphics_mcp", "unity_profiler_mcp", "unity_addressables_mcp", "unity_ui_mcp",
				"unity_animation_mcp", "unity_audio_mcp", "unity_cinematic_mcp"
			};
			string[][] expectedGroups =
			{
				new[] {"inspect", "plan", "mutate", "save", "bake", "capture", "evaluate_and_refine", "execution"},
				new[] {"profiler"},
				new[] {"addressables"},
				new[] {"ui"},
				new[] {"animation"},
				new[] {"audio"},
				new[] {"cinematic"}
			};
			string[] expectedFields = {"domainId", "status", "toolGroups", "tools", "directUnityMutationAllowed"};

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(domains.Count, Is.EqualTo(7));
			for (int index = 0; index < domains.Count; index++)
			{
				JObject domain = (JObject)domains[index];
				JObject catalogDomain = (JObject)catalogDomains[index];
				Assert.That(domain.Properties().Select(value => value.Name).OrderBy(value => value).ToArray(), Is.EqualTo(expectedFields.OrderBy(value => value).ToArray()));
				Assert.That(domain["domainId"]?.Type, Is.EqualTo(JTokenType.String));
				Assert.That(domain.Value<string>("domainId"), Is.EqualTo(expectedDomainIds[index]));
				Assert.That(domain["status"]?.Type, Is.EqualTo(JTokenType.String));
				Assert.That(domain["toolGroups"]?.Type, Is.EqualTo(JTokenType.Array));
				Assert.That(domain["toolGroups"].All(value => value.Type == JTokenType.String), Is.True);
				Assert.That(domain["toolGroups"].Values<string>().ToArray(), Is.EqualTo(expectedGroups[index]));
				Assert.That(domain["tools"]?.Type, Is.EqualTo(JTokenType.Array));
				Assert.That(domain["tools"].All(value => value.Type == JTokenType.String), Is.True);
				Assert.That(domain["tools"].Values<string>().ToArray(), Is.EqualTo(catalogDomain["tools"].Values<JObject>().Select(value => value.Value<string>("name")).ToArray()));
				Assert.That(domain["directUnityMutationAllowed"]?.Type, Is.EqualTo(JTokenType.Boolean));
				Assert.That(domain.Value<bool>("directUnityMutationAllowed"), Is.False);
			}
			Assert.That(result.ToString(), Does.Not.Contain("policy"));
		}

		[Test]
		public void CatalogV5_PreservesApprovalSet()
		{
			Assert.That(TryParseCatalog(JObject.Parse(CatalogJson()), out AgentCatalogSnapshot snapshot, out string error), Is.True, error);
			string[] expected =
			{
				"graphics.apply_plan", "graphics.undo_last_transaction", "graphics.apply_environment_plan",
				"graphics.undo_last_environment_transaction", "graphics.apply_save_plan", "graphics.bake_dependencies",
				"graphics.start_apv_bake", "graphics.get_apv_bake_status", "graphics.cancel_apv_bake",
				"addressables.apply_entry", "ui.apply_rect_transform", "animation.apply_parameter",
				"audio.apply_source", "cinematic.apply_director"
			};

			Assert.That(snapshot.ToolIndex.Values.Where(value => value.policy.approvalRequired).Select(value => value.name).ToArray(), Is.EqualTo(expected));
		}

		[Test]
		public void CatalogV5_RejectsDuplicateTool()
		{
			JObject root = JObject.Parse(CatalogJson());
			JArray tools = (JArray)root["domains"][0]["tools"];
			tools.Add(tools[0].DeepClone());

			Assert.That(TryParseCatalog(root, out _, out string error), Is.False);
			Assert.That(error, Does.Contain("duplicate tool"));
		}

		[Test]
		public void CatalogV5_RejectsUnknownAndMissingRegisteredTools()
		{
			JObject unknownTool = JObject.Parse(CatalogJson());
			((JObject)unknownTool["domains"][0]["tools"][0])["name"] = "graphics.unknown";
			Assert.That(TryParseCatalog(unknownTool, out _, out string unknownError), Is.False);
			Assert.That(unknownError, Does.Contain("registered delegate"));
		}

		[Test]
		public void CatalogV5_RejectsMissingPolicy()
		{
			JObject root = JObject.Parse(CatalogJson());
			((JObject)root["domains"][0]["tools"][0]).Remove("policy");

			Assert.That(TryParseCatalog(root, out _, out string error), Is.False);
			Assert.That(error, Does.Contain("policy"));
		}

		[Test]
		public void CatalogV5_RejectsInvalidEffectAndRetryPolicy()
		{
			JObject invalidEffect = JObject.Parse(CatalogJson());
			((JObject)invalidEffect["domains"][0]["tools"][0]["policy"])["effect"] = "execution_control";
			Assert.That(TryParseCatalog(invalidEffect, out _, out string effectError), Is.False);
			Assert.That(effectError, Does.Contain("effect"));

			JObject invalidRetry = JObject.Parse(CatalogJson());
			((JObject)invalidRetry["domains"][0]["tools"][0]["policy"])["retryPolicy"] = "safe_retry";
			Assert.That(TryParseCatalog(invalidRetry, out _, out string retryError), Is.False);
			Assert.That(retryError, Does.Contain("retryPolicy"));
		}

		[Test]
		public void CatalogV5_RejectsLegacyDomainToolGroupsAndCreatorObjects()
		{
			JObject legacyDomain = JObject.Parse(CatalogJson());
			legacyDomain["domains"][0]["toolGroups"] = new JArray("inspect");
			Assert.That(TryParseCatalog(legacyDomain, out _, out string domainError), Is.False);
			Assert.That(domainError, Does.Contain("toolGroups"));

			JObject creatorObjects = JObject.Parse(CatalogJson());
			creatorObjects["creators"][0]["tools"][0] = new JObject { ["name"] = "world.compile_workflow" };
			Assert.That(TryParseCatalog(creatorObjects, out _, out string creatorError), Is.False);
			Assert.That(creatorError, Does.Contain("creator tools"));
		}

		[Test]
		public void CatalogV5_RejectsCreatorDirectMutation()
		{
			JObject creatorMutation = JObject.Parse(CatalogJson());
			creatorMutation["creators"][0]["directUnityMutationAllowed"] = true;

			Assert.That(TryParseCatalog(creatorMutation, out _, out string error), Is.False);
			Assert.That(error, Does.Contain("creator"));
		}

		[Test]
		public void ValidateWorkflow_AcceptsIntegrationCandidateDomain()
		{
			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[]
			{
				new UnityAgentMcpStepInput
				{
					stepId = "ui",
					domainId = "unity_ui_mcp",
					toolName = "ui.inspect",
					toolGroup = "ui"
				}
			});

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("valid"), Is.True);
		}

		[Test]
		public void ValidateWorkflow_RejectsMissingToolGroup()
		{
			UnityAgentMcpStepInput step = GraphicsInspectStep();
			step.toolGroup = "missing";

			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[] {step});

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-TOOL-GROUP-MISSING"));
		}

		[Test]
		public void ValidateWorkflow_RejectsKnownButWrongCanonicalToolGroup()
		{
			UnityAgentMcpStepInput step = GraphicsInspectStep();
			step.toolGroup = "mutate";

			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[] {step});

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-TOOL-GROUP-MISMATCH"));
		}

		[Test]
		public void ValidateWorkflow_RejectsCycle()
		{
			UnityAgentMcpStepInput first = GraphicsInspectStep("first", "second");
			UnityAgentMcpStepInput second = GraphicsInspectStep("second", "first");

			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[] {first, second});

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-GRAPH-CYCLE"));
		}

		[Test]
		public void CompilePreviewStart_DelegatesExistingGraphicsInspectionCooperatively()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			string graphId = compiled.Value<string>("graphId");
			JObject preview = UnityAgentMcpRuntime.Instance.PreviewExecution(graphId);
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(graphId, revision, null);

			Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());
			Assert.That(preview.Value<string>("status"), Is.EqualTo("PREVIEW"));
			Assert.That(started.Value<string>("status"), Is.EqualTo("RUNNING"), started.ToString());

			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject completed = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));
			Assert.That(completed.Value<string>("status"), Is.EqualTo("SUCCEEDED"), completed.ToString());
			Assert.That(completed.Value<bool>("executionSucceeded"), Is.True);
			Assert.That(completed["stepResults"]?.Count(), Is.EqualTo(1));
		}

		[Test]
		public void IntegrationCandidate_DelegatesProfilerInspection()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {ProfilerInspectStep()});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null);

			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject completed = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());
			Assert.That(completed.Value<string>("status"), Is.EqualTo("SUCCEEDED"), completed.ToString());
			Assert.That(completed.Value<bool>("executionSucceeded"), Is.True);
		}

		[Test]
		public void CompileGraph_RejectsRevisionThatIsNotCurrentEditorRevision()
		{
			long revision = Session.Revision;
			JObject result = UnityAgentMcpRuntime.Instance.CompileGraph(revision + 1, new[] {GraphicsInspectStep()});

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void StartExecution_RejectsChangedCallerRevision()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			JObject result = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision + 1,
				null);

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void StartExecution_RejectsActualEditorRevisionChangeEvenWithOldCallerValue()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			Session.NotifyMutationApplied();
			JObject result = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null);

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void PreviewExecution_RejectsGraphAfterEditorRevisionChanges()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			Session.NotifyMutationApplied();
			JObject result = UnityAgentMcpRuntime.Instance.PreviewExecution(compiled.Value<string>("graphId"));

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void MutationGroup_RequiresAgentApprovalThenDelegatesToDomainSafety()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsMutationStep()});
			string graphId = compiled.Value<string>("graphId");

			JObject missing = UnityAgentMcpRuntime.Instance.StartExecution(graphId, revision, null);
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				revision,
				approval.Value<string>("approvalToken"));
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject delegated = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(missing.Value<string>("errorCode"), Is.EqualTo("AGENT-APPROVAL-MISSING-OR-EXPIRED"));
			Assert.That(approval.Value<bool>("success"), Is.True);
			Assert.That(delegated.Value<string>("status"), Is.EqualTo("FAILED"), delegated.ToString());
			Assert.That(delegated.Value<string>("errorCode"), Is.Not.EqualTo("AGENT-DELEGATE-NOT-REGISTERED"));
		}

		[Test]
		public void Approval_ExpiresBeforeExecutionStarts()
		{
			DateTime now = DateTime.UtcNow;
			UnityAgentMcpRuntime.UtcNowOverrideForTests = () => now;
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsMutationStep()});
			string graphId = compiled.Value<string>("graphId");
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");
			now = now.AddMinutes(11);
			JObject result = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				revision,
				approval.Value<string>("approvalToken"));

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-APPROVAL-MISSING-OR-EXPIRED"));
		}

		[Test]
		public void LaterDelegateFailure_ProducesPartialInsteadOfFalseSuccess()
		{
			long revision = Session.Revision;
			UnityAgentMcpStepInput inspect = GraphicsInspectStep("inspect");
			UnityAgentMcpStepInput mutate = GraphicsMutationStep("mutate", "inspect");
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {inspect, mutate});
			string graphId = compiled.Value<string>("graphId");
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				revision,
				approval.Value<string>("approvalToken"));

			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject result = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(result.Value<string>("status"), Is.EqualTo("PARTIAL"), result.ToString());
			Assert.That(result.Value<bool>("executionSucceeded"), Is.False);
			Assert.That(result["stepResults"]?.Count(), Is.EqualTo(2));
		}

		[Test]
		public void CancelExecution_StopsQueuedExecutionBeforeDelegation()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[]
			{
				GraphicsInspectStep("first"),
				GraphicsInspectStep("second", "first")
			});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null);
			JObject cancelled = UnityAgentMcpRuntime.Instance.CancelExecution(started.Value<string>("executionId"));
			JObject status = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(started.Value<string>("status"), Is.EqualTo("RUNNING"));
			Assert.That(cancelled.Value<bool>("success"), Is.True);
			Assert.That(cancelled.Value<string>("status"), Is.EqualTo("CANCELLED"));
			Assert.That(status.Value<string>("status"), Is.EqualTo("CANCELLED"));
			Assert.That(status.Value<int>("completedStepCount"), Is.Zero);
		}

		[Test]
		public void ExecutionTimeout_InterruptsBeforeNextStep()
		{
			DateTime now = DateTime.UtcNow;
			UnityAgentMcpRuntime.UtcNowOverrideForTests = () => now;
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null,
				1);
			now = now.AddSeconds(2);
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject status = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(status.Value<string>("status"), Is.EqualTo("INTERRUPTED"));
			Assert.That(status.Value<string>("errorCode"), Is.EqualTo("AGENT-EXECUTION-TIMEOUT"));
			Assert.That(status.Value<int>("completedStepCount"), Is.Zero);
		}

		[Test]
		public void ClientDisconnect_InterruptsRunningExecution()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				revision,
				null);

			UnityAgentMcpRuntime.Instance.NotifyClientDisconnected();
			JObject status = UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId"));

			Assert.That(status.Value<string>("status"), Is.EqualTo("INTERRUPTED"));
			Assert.That(status.Value<string>("errorCode"), Is.EqualTo("AGENT-CLIENT-DISCONNECTED"));
		}

		[Test]
		public void ExecutionHistory_ContainsCompletedExecution()
		{
			long revision = Session.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			JObject started = UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), revision, null);
			UnityAgentMcpRuntime.Instance.ProcessPendingExecutionsForTests();
			JObject history = UnityAgentMcpRuntime.Instance.GetExecutionHistory(100);

			Assert.That(UnityAgentMcpRuntime.Instance.GetExecutionStatus(started.Value<string>("executionId")).Value<string>("status"), Is.EqualTo("SUCCEEDED"));
			Assert.That(history.Value<bool>("success"), Is.True);
			Assert.That(history.Value<int>("total"), Is.GreaterThan(0));
		}

		[Test]
		public void ErrorCatalog_DeclaresTimeoutAndDisconnectRecovery()
		{
			JObject result = UnityAgentMcpRuntime.Instance.GetErrorCatalog();
			string[] codes = result["errors"]?.Values<JObject>()
				.Select(value => value.Value<string>("code"))
				.ToArray();

			CollectionAssert.Contains(codes, "AGENT-EXECUTION-TIMEOUT");
			CollectionAssert.Contains(codes, "AGENT-CLIENT-DISCONNECTED");
			CollectionAssert.Contains(codes, "AGENT-TIMEOUT-INVALID");
		}

		[Test]
		public void AgentTools_AreDefaultDisabled()
		{
			string source = File.ReadAllText(
				"Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpTools.cs");

			Assert.That(source.Split(new[] {"[McpForUnityTool("}, StringSplitOptions.None).Length - 1, Is.EqualTo(10));
			Assert.That(source.Split(new[] {"AutoRegister = false"}, StringSplitOptions.None).Length - 1, Is.EqualTo(10));
		}

		[Test]
		public void ControlPlaneSource_DoesNotCallDirectMutationApis()
		{
			string source = File.ReadAllText(
				"Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpRuntime.cs");

			Assert.That(source, Does.Not.Contain("Undo.RecordObject"));
			Assert.That(source, Does.Not.Contain("EditorUtility.SetDirty"));
			Assert.That(source, Does.Not.Contain("AssetDatabase.CreateAsset"));
			Assert.That(source, Does.Not.Contain("EditorSceneManager.SaveScene"));
		}
	}
}

#endif
