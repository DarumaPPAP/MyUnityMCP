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

		[Test]
		public void InspectCapabilities_LoadsCatalogAndKeepsDirectMutationDisabled()
		{
			JObject result = UnityAgentMcpRuntime.Instance.InspectCapabilities();

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("directUnityMutation"), Is.False);
			Assert.That(result.Value<bool>("cooperativeExecution"), Is.True);
			Assert.That(result.Value<int>("defaultExecutionTimeoutSeconds"), Is.GreaterThan(0));
			Assert.That(result["domains"]?.Any(), Is.True);
		}

		[Test]
		public void ValidateWorkflow_RejectsDesignOnlyDomain()
		{
			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[]
			{
				new UnityAgentMcpStepInput
				{
					stepId = "ui",
					domainId = "unity_ui_mcp",
					toolName = "ui.inspect",
					toolGroup = "inspect"
				}
			});

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-DOMAIN-NOT-OPERATIONAL"));
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
		public void MutationGroup_RequiresApprovalAndStillUsesRegisteredDelegateOnly()
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
			Assert.That(delegated.Value<string>("status"), Is.EqualTo("FAILED"));
			Assert.That(delegated.Value<string>("errorCode"), Is.EqualTo("AGENT-DELEGATE-NOT-REGISTERED"));
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

			Assert.That(result.Value<string>("status"), Is.EqualTo("PARTIAL"));
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
				"Packages/com.darumappap.my-unity-mcp/Editor/Development/Agent/UnityAgentMcpTools.cs");

			Assert.That(source.Split(new[] {"[McpForUnityTool("}, StringSplitOptions.None).Length - 1, Is.EqualTo(10));
			Assert.That(source.Split(new[] {"AutoRegister = false"}, StringSplitOptions.None).Length - 1, Is.EqualTo(10));
		}

		[Test]
		public void ControlPlaneSource_DoesNotCallDirectMutationApis()
		{
			string source = File.ReadAllText(
				"Packages/com.darumappap.my-unity-mcp/Editor/Development/Agent/UnityAgentMcpRuntime.cs");

			Assert.That(source, Does.Not.Contain("Undo.RecordObject"));
			Assert.That(source, Does.Not.Contain("EditorUtility.SetDirty"));
			Assert.That(source, Does.Not.Contain("AssetDatabase.CreateAsset"));
			Assert.That(source, Does.Not.Contain("EditorSceneManager.SaveScene"));
		}
	}
}

#endif