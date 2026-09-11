#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityAgentMcp
{
	internal sealed class AgentWorkflowValidator
	{
		internal const int MAX_GRAPH_STEPS = 64;

		private readonly AgentCatalogSnapshot _catalog;

		internal AgentWorkflowValidator(AgentCatalogSnapshot catalog)
		{
			_catalog = catalog;
		}

		internal bool TryValidate(
			UnityAgentMcpStepInput[] steps,
			out List<UnityAgentMcpStepInput> normalized,
			out string errorCode,
			out string message)
		{
			normalized = (steps ?? Array.Empty<UnityAgentMcpStepInput>())
				.Where(value => value != null)
				.ToList();
			errorCode = null;
			message = null;

			if (_catalog == null)
			{
				return Fail("AGENT-CATALOG-INVALID", "Catalogが読み込まれていません。", out errorCode, out message);
			}
			if (normalized.Count == 0)
			{
				return Fail("AGENT-WORKFLOW-EMPTY", "WorkflowにStepがありません。", out errorCode, out message);
			}
			if (normalized.Count > MAX_GRAPH_STEPS)
			{
				return Fail("AGENT-GRAPH-TOO-LARGE", $"WorkflowのStep数は{MAX_GRAPH_STEPS}以下で指定してください。", out errorCode, out message);
			}

			HashSet<string> stepIds = new HashSet<string>(StringComparer.Ordinal);
			foreach (UnityAgentMcpStepInput step in normalized)
			{
				if (string.IsNullOrWhiteSpace(step.stepId) || !stepIds.Add(step.stepId))
				{
					return Fail("AGENT-STEP-ID-INVALID", "Step IDが空または重複しています。", out errorCode, out message);
				}
				if (!_catalog.TryGetDomain(step.domainId, out AgentCatalogDomainDefinition domain))
				{
					return Fail("AGENT-DOMAIN-NOT-FOUND", $"DomainがCatalogにありません: {step.domainId}", out errorCode, out message);
				}
				if (!_catalog.TryGetTool(step.toolName, out AgentCatalogToolDefinition tool) ||
					!string.Equals(tool.domainId, domain.domainId, StringComparison.Ordinal))
				{
					return Fail("AGENT-TOOL-NOT-DECLARED", $"ToolがDomain Catalogにありません: {step.toolName}", out errorCode, out message);
				}
				if (!_catalog.GetCanonicalGroups(domain).Contains(step.toolGroup, StringComparer.Ordinal))
				{
					return Fail("AGENT-TOOL-GROUP-MISSING", $"Tool GroupがDomainにありません: {step.toolGroup}", out errorCode, out message);
				}
				if (!string.Equals(tool.group, step.toolGroup, StringComparison.Ordinal))
				{
					return Fail("AGENT-TOOL-GROUP-MISMATCH", $"Tool GroupがCatalog定義と一致しません: {step.toolName}", out errorCode, out message);
				}
				if (!IsExecutableDomainStatus(domain.status))
				{
					return Fail("AGENT-DOMAIN-NOT-OPERATIONAL", $"Domainは実行可能ではありません: {step.domainId}", out errorCode, out message);
				}
				if (domain.directUnityMutationAllowed)
				{
					return Fail("AGENT-DIRECT-MUTATION-FORBIDDEN", "Control Plane DomainはUnity APIを直接Mutationできません。", out errorCode, out message);
				}
				step.dependsOn = step.dependsOn ?? Array.Empty<string>();
				step.parameters = step.parameters ?? new Newtonsoft.Json.Linq.JObject();
			}

			foreach (UnityAgentMcpStepInput step in normalized)
			{
				string missingDependency = (step.dependsOn ?? Array.Empty<string>())
					.FirstOrDefault(value => !stepIds.Contains(value));
				if (!string.IsNullOrEmpty(missingDependency))
				{
					return Fail("AGENT-DEPENDENCY-NOT-FOUND", $"依存Stepがありません: {missingDependency}", out errorCode, out message);
				}
			}

			if (HasCycle(normalized))
			{
				return Fail("AGENT-GRAPH-CYCLE", "Workflow GraphにCycleがあります。", out errorCode, out message);
			}
			return true;
		}

		internal static IEnumerable<UnityAgentMcpStepInput> TopologicalOrder(List<UnityAgentMcpStepInput> steps)
		{
			HashSet<string> emitted = new HashSet<string>(StringComparer.Ordinal);
			while (emitted.Count < steps.Count)
			{
				UnityAgentMcpStepInput next = steps.First(value =>
					!emitted.Contains(value.stepId) &&
					(value.dependsOn ?? Array.Empty<string>()).All(emitted.Contains));
				emitted.Add(next.stepId);
				yield return next;
			}
		}

		private static bool IsExecutableDomainStatus(string status)
		{
			return string.Equals(status, "editor_operational", StringComparison.Ordinal) ||
				string.Equals(status, "integration_candidate", StringComparison.Ordinal);
		}

		private static bool HasCycle(List<UnityAgentMcpStepInput> steps)
		{
			Dictionary<string, UnityAgentMcpStepInput> map = steps.ToDictionary(value => value.stepId, StringComparer.Ordinal);
			HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

			bool Visit(string stepId)
			{
				if (visiting.Contains(stepId))
				{
					return true;
				}
				if (visited.Contains(stepId))
				{
					return false;
				}
				if (!map.TryGetValue(stepId, out UnityAgentMcpStepInput step))
				{
					return false;
				}
				visiting.Add(stepId);
				foreach (string dependency in step.dependsOn ?? Array.Empty<string>())
				{
					if (!map.ContainsKey(dependency) || Visit(dependency))
					{
						return true;
					}
				}
				visiting.Remove(stepId);
				visited.Add(stepId);
				return false;
			}

			return steps.Any(value => Visit(value.stepId));
		}

		private static bool Fail(string code, string text, out string errorCode, out string message)
		{
			errorCode = code;
			message = text;
			return false;
		}
	}
}

#endif
