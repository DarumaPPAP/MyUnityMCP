#if UNITY_EDITOR

using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityAgentMcp;

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

		[Test]
		public void InspectCapabilities_LoadsCatalogAndKeepsDirectMutationDisabled()
		{
			JObject result = UnityAgentMcpRuntime.Instance.InspectCapabilities();

			Assert.That(result.Value<bool>("success"), Is.True, result.ToString());
			Assert.That(result.Value<bool>("directUnityMutation"), Is.False);
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
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(10, new[] {GraphicsInspectStep()});
			string graphId = compiled.Value<string>("graphId");

			JObject preview = UnityAgentMcpRuntime.Instance.PreviewExecution(graphId);
			JObject executed = UnityAgentMcpRuntime.Instance.StartExecution(graphId, 10, null);

			Assert.That(compiled.Value<bool>("success"), Is.True, compiled.ToString());
			Assert.That(preview.Value<string>("status"), Is.EqualTo("PREVIEW"));
			Assert.That(executed.Value<string>("status"), Is.EqualTo("SUCCEEDED"), executed.ToString());
			Assert.That(executed["stepResults"]?.Count(), Is.EqualTo(1));
		}

		[Test]
		public void StartExecution_RejectsChangedRevision()
		{
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(20, new[] {GraphicsInspectStep()});

			JObject result = UnityAgentMcpRuntime.Instance.StartExecution(
				compiled.Value<string>("graphId"),
				21,
				null);

			Assert.That(result.Value<string>("errorCode"), Is.EqualTo("AGENT-REVISION-CHANGED"));
		}

		[Test]
		public void MutationGroup_RequiresApprovalAndStillUsesRegisteredDelegateOnly()
		{
			UnityAgentMcpStepInput step = new UnityAgentMcpStepInput
			{
				stepId = "mutate",
				domainId = "unity_graphics_mcp",
				toolName = "graphics.apply_plan",
				toolGroup = "mutate",
				dependsOn = new string[0],
				parameters = new JObject()
			};
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(30, new[] {step});
			string graphId = compiled.Value<string>("graphId");

			JObject missing = UnityAgentMcpRuntime.Instance.StartExecution(graphId, 30, null);
			JObject approval = UnityAgentMcpRuntime.Instance.SubmitApproval(
				graphId,
				new[] {"mutate"},
				"APPROVE_AGENT_EXECUTION");
			JObject delegated = UnityAgentMcpRuntime.Instance.StartExecution(
				graphId,
				30,
				approval.Value<string>("approvalToken"));

			Assert.That(missing.Value<string>("errorCode"), Is.EqualTo("AGENT-APPROVAL-MISSING-OR-EXPIRED"));
			Assert.That(approval.Value<bool>("success"), Is.True);
			Assert.That(delegated.Value<string>("status"), Is.EqualTo("FAILED"));
			Assert.That(delegated.Value<string>("errorCode"), Is.EqualTo("AGENT-DELEGATE-NOT-REGISTERED"));
		}

		[Test]
		public void ExecutionHistory_ContainsCompletedExecution()
		{
			JObject compiled = UnityAgentMcpRuntime.Instance.CompileGraph(40, new[] {GraphicsInspectStep()});
			UnityAgentMcpRuntime.Instance.StartExecution(compiled.Value<string>("graphId"), 40, null);

			JObject history = UnityAgentMcpRuntime.Instance.GetExecutionHistory(100);

			Assert.That(history.Value<bool>("success"), Is.True);
			Assert.That(history.Value<int>("total"), Is.GreaterThan(0));
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
