#if UNITY_EDITOR

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
		private static UnityAgentMcpStepInput GraphicsInspectStep(string stepId = "inspect")
		{
			return new UnityAgentMcpStepInput
			{
				stepId = stepId,
				domainId = "unity_graphics_mcp",
				toolName = "graphics.inspect_project",
				toolGroup = "inspect",
				dependsOn = new string[0],
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
				dependsOn = dependsOn ?? new string[0],
				parameters = new JObject()
			};
		}

		[Test]
		public void InspectCapabilities_LoadsCatalogAndKeepsDirectMutationDisabled()
		{
			JObject result = UnityAgentMcpRuntime.Instance.InspectCapabilities();

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("directUnityMutation"), Is.False);
			Assert.That(result.Value<int>("executionTimeoutSeconds"), Is.GreaterThan(0));
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
			UnityAgentMcpStepInput first = GraphicsInspectStep("first");
			UnityAgentMcpStepInput second = GraphicsInspectStep("second");
			first.dependsOn = new[] {"second"};
			second.dependsOn = new[] {"first"};

			JObject result = UnityAgentMcpRuntime.Instance.ValidateWorkflow(new[] {first, second});

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-GRAPH-CYCLE"));
		}

		[Test]
		public void CompilePreviewStart_DelegatesExistingGraphicsInspection()
		{
			long revision = UnityGraphicsMcpSession.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			string graphId = compiled.Value<string>("graphId");

			JObject preview = UnityAgentMcpRuntime.Instance.PreviewExecution(graphId);
			JObject executed = UnityAgentMcpRuntime.Instance.StartExecution(graphId, revision, null);

			Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());
			Assert.That(preview.Value<string>("status"), Is.EqualTo("PREVIEW"));
			Assert.That(executed.Value<string>("status"), Is.EqualTo("SUCCEEDED"), executed.ToString());
			Assert.That(executed["stepResults"]?.Count(), Is.EqualTo(1));
		}

		[Test]
		public void CompileGraph_RejectsRevisionThatIsNotCurrentEditorRevision()
		{
			long revision = UnityGraphicsMcpSession.Revision;

			JObject result = UnityAgentMcpRuntime.Instance.CompileGraph(revision + 1, new[] {GraphicsInspectStep()});

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void StartExecution_RejectsChangedCallerRevision()
		{
			long revision = UnityGraphicsMcpSession.Revision;
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
			long revision = UnityGraphicsMcpSession.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			UnityGraphicsMcpSession.NotifyMutationApplied();

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
			long revision = UnityGraphicsMcpSession.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			UnityGraphicsMcpSession.NotifyMutationApplied();

			JObject result = UnityAgentMcpRuntime.Instance.PreviewExecution(compiled.Value<string>("graphId"));

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void MutationGroup_RequiresApprovalAndStillUsesRegisteredDelegateOnly()
		{
			long revision = UnityGraphicsMcpSession.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsMutationStep()});
			string graphId = compiled.Value<string>("graphId");

			JObject missing = UnityAgentMcpRuntime.Instance.StartExecution(graphId, revision, null);
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");
			JObject delegated = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				revision,
				approval.Value<string>("approvalToken"));

			Assert.That(missing.Value<string>("errorCode"), Is.EqualTo("AGENT-APPROVAL-MISSING-OR-EXPIRED"));
			Assert.That(approval.Value<bool>("success"), Is.True);
			Assert.That(delegated.Value<string>("status"), Is.EqualTo("FAILED"));
			Assert.That(delegated.Value<string>("errorCode"), Is.EqualTo("AGENT-DELEGATE-NOT-REGISTERED"));
		}

		[Test]
		public void LaterDelegateFailure_ProducesPartialInsteadOfFalseSuccess()
		{
			long revision = UnityGraphicsMcpSession.Revision;
			UnityAgentMcpStepInput inspect = GraphicsInspectStep("inspect");
			UnityAgentMcpStepInput mutate = GraphicsMutationStep("mutate", "inspect");
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {inspect, mutate});
			string graphId = compiled.Value<string>("graphId");
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");

			JObject result = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				revision,
				approval.Value<string>("approvalToken"));

			Assert.That(result.Value<bool>("success"), Is.False);
			Assert.That(result.Value<string>("status"), Is.EqualTo("PARTIAL"));
			Assert.That(result["stepResults"]?.Count(), Is.EqualTo(2));
		}

		[Test]
		public void ExecutionHistory_ContainsCompletedExecution()
		{
			long revision = UnityGraphicsMcpSession.Revision;
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(revision, new[] {GraphicsInspectStep()});
			UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), revision, null);

			JObject history = UnityAgentMcpRuntime.Instance.GetExecutionHistory(100);

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
		}

		[Test]
		public void AgentTools_AreDefaultDisabled()
		{
			string source = File.ReadAllText(
				"Packages/com.darumappap.my-unity-mcp/Editor/UnityAgentMcpTools.cs");

			Assert.That(source.Split(new[] {"[McpForUnityTool("}, System.StringSplitOptions.None).Length - 1, Is.EqualTo(10));
			Assert.That(source.Split(new[] {"AutoRegister = false"}, System.StringSplitOptions.None).Length - 1, Is.EqualTo(10));
		}

		[Test]
		public void ControlPlaneSource_DoesNotCallDirectMutationApis()
		{
			string source = File.ReadAllText(
				"Packages/com.darumappap.my-unity-mcp/Editor/UnityAgentMcpRuntime.cs");

			Assert.That(source, Does.Not.Contain("Undo.RecordObject"));
			Assert.That(source, Does.Not.Contain("EditorUtility.SetDirty"));
			Assert.That(source, Does.Not.Contain("AssetDatabase.CreateAsset"));
			Assert.That(source, Does.Not.Contain("EditorSceneManager.SaveScene"));
		}
	}
}

#endif
