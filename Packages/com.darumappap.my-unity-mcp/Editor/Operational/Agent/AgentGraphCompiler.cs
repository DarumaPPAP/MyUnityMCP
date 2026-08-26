#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace UnityAgentMcp
{
	internal sealed class AgentGraphCompiler
	{
		private readonly AgentWorkflowValidator _validator;
		private readonly AgentApprovalService _approvalService;
		private readonly Func<DateTime> _utcNow;
		private readonly Dictionary<string, UnityAgentMcpCompiledGraph> _graphs =
			new Dictionary<string, UnityAgentMcpCompiledGraph>(StringComparer.Ordinal);

		internal AgentGraphCompiler(
			AgentWorkflowValidator validator,
			AgentApprovalService approvalService,
			Func<DateTime> utcNow)
		{
			_validator = validator;
			_approvalService = approvalService;
			_utcNow = utcNow;
		}

		internal bool TryCompile(
			long expectedRevision,
			UnityAgentMcpStepInput[] steps,
			AgentCatalogSnapshot catalog,
			out UnityAgentMcpCompiledGraph graph,
			out List<UnityAgentMcpStepInput> normalized,
			out string errorCode,
			out string message)
		{
			graph = null;
			if (!_validator.TryValidate(steps, out normalized, out errorCode, out message))
			{
				return false;
			}
			string graphId = $"agent-graph-{Guid.NewGuid():N}";
			graph = new UnityAgentMcpCompiledGraph
			{
				graphId = graphId,
				catalogSchemaVersion = catalog.SchemaVersion,
				catalogFingerprint = catalog.Fingerprint,
				expectedRevision = expectedRevision,
				createdAtUtc = _utcNow(),
				steps = normalized,
				requiredApprovalGroups = _approvalService.RequiredApprovalGroups(normalized)
			};
			_graphs[graphId] = graph;
			return true;
		}

		internal bool TryGet(string graphId, out UnityAgentMcpCompiledGraph graph)
		{
			return _graphs.TryGetValue(graphId ?? string.Empty, out graph);
		}

		internal void Reset()
		{
			_graphs.Clear();
		}
	}
}

#endif
