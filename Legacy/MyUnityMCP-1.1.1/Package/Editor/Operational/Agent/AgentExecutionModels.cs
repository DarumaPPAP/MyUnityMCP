#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UnityAgentMcp
{
	internal sealed class UnityAgentMcpCompiledGraph
	{
		public string graphId;
		public int catalogSchemaVersion;
		public string catalogFingerprint;
		public long expectedRevision;
		public DateTime createdAtUtc;
		public List<UnityAgentMcpStepInput> steps;
		public HashSet<string> requiredApprovalGroups;
		public string approvalTokenHash;
		public DateTime approvalExpiresAtUtc;
		public bool approved;
	}

	internal sealed class AgentExecutionStepSummary
	{
		public string stepId;
		public string domainId;
		public string toolName;
		public string resultCode;
		public double durationMs;
	}

	internal sealed class UnityAgentMcpExecutionRecord
	{
		public string executionId;
		public string graphId;
		public int catalogSchemaVersion;
		public string catalogFingerprint;
		public E_AGENT_EXECUTION_STATUS status;
		public DateTime startedAtUtc;
		public DateTime completedAtUtc;
		public DateTime deadlineUtc;
		public int timeoutSeconds;
		public long expectedRevision;
		public string errorCode;
		public string message;
		public List<UnityAgentMcpStepInput> orderedSteps = new List<UnityAgentMcpStepInput>();
		public int nextStepIndex;
		public List<JObject> stepResults = new List<JObject>();
		public List<AgentExecutionStepSummary> stepSummaries = new List<AgentExecutionStepSummary>();
		public bool cancelRequested;
		public bool historyPersisted;
		public bool terminalTraceWritten;
	}

	internal sealed class AgentTraceEvent
	{
		public int schemaVersion;
		public DateTime timestampUtc;
		public string executionId;
		public string graphId;
		public string stepId;
		public string domainId;
		public string toolName;
		public string eventName;
		public long revision;
		public string resultCode;
		public double durationMs;
	}
}

#endif
