#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityGraphicsMcp;

namespace UnityAgentMcp
{
	internal sealed class AgentExecutionEngine
	{
		internal const int DEFAULT_EXECUTION_TIMEOUT_SECONDS = 60;
		internal const int MAX_EXECUTION_TIMEOUT_SECONDS = 3600;

		private readonly AgentDelegateRegistry _delegateRegistry;
		private readonly AgentApprovalService _approvalService;
		private readonly AgentExecutionHistoryStore _historyStore;
		private readonly AgentExecutionTraceStore _traceStore;
		private readonly Func<DateTime> _utcNow;
		private readonly Dictionary<string, UnityAgentMcpExecutionRecord> _executions =
			new Dictionary<string, UnityAgentMcpExecutionRecord>(StringComparer.Ordinal);

		internal AgentExecutionEngine(
			AgentDelegateRegistry delegateRegistry,
			AgentApprovalService approvalService,
			AgentExecutionHistoryStore historyStore,
			AgentExecutionTraceStore traceStore,
			Func<DateTime> utcNow)
		{
			_delegateRegistry = delegateRegistry;
			_approvalService = approvalService;
			_historyStore = historyStore;
			_traceStore = traceStore;
			_utcNow = utcNow;
		}

		internal bool TryStart(
			UnityAgentMcpCompiledGraph graph,
			long currentRevision,
			int timeoutSeconds,
			out UnityAgentMcpExecutionRecord execution,
			out string errorCode,
			out string message)
		{
			execution = null;
			errorCode = null;
			message = null;
			if (graph == null)
			{
				return Fail("AGENT-GRAPH-NOT-FOUND", "Compiled Graphが見つかりません。", out errorCode, out message);
			}
			if (graph.expectedRevision != currentRevision || currentRevision != Session.Revision)
			{
				return Fail("AGENT-REVISION-CHANGED", "Preview後にEditor Revisionが変更されました。", out errorCode, out message);
			}
			if (timeoutSeconds < 1 || timeoutSeconds > MAX_EXECUTION_TIMEOUT_SECONDS)
			{
				return Fail("AGENT-TIMEOUT-INVALID", $"timeoutSecondsは1～{MAX_EXECUTION_TIMEOUT_SECONDS}で指定してください。", out errorCode, out message);
			}

			DateTime startedAtUtc = _utcNow();
			execution = new UnityAgentMcpExecutionRecord
			{
				executionId = $"agent-exec-{Guid.NewGuid():N}",
				graphId = graph.graphId,
				catalogSchemaVersion = graph.catalogSchemaVersion,
				catalogFingerprint = graph.catalogFingerprint,
				status = E_AGENT_EXECUTION_STATUS.RUNNING,
				startedAtUtc = startedAtUtc,
				deadlineUtc = startedAtUtc.AddSeconds(timeoutSeconds),
				timeoutSeconds = timeoutSeconds,
				expectedRevision = graph.expectedRevision,
				orderedSteps = AgentWorkflowValidator.TopologicalOrder(graph.steps).ToList(),
				message = "Execution accepted and queued."
			};
			_executions[execution.executionId] = execution;
			AppendTrace(execution, "EXECUTION_STARTED", null, null, null, Session.Revision, 0.0);
			return true;
		}

		internal bool TryGet(string executionId, out UnityAgentMcpExecutionRecord execution)
		{
			return _executions.TryGetValue(executionId ?? string.Empty, out execution);
		}

		internal bool TryCancel(string executionId, out UnityAgentMcpExecutionRecord execution, out string errorCode, out string message)
		{
			errorCode = null;
			message = null;
			if (!TryGet(executionId, out execution))
			{
				return Fail("AGENT-EXECUTION-NOT-FOUND", "Executionが見つかりません。", out errorCode, out message);
			}
			if (execution.status != E_AGENT_EXECUTION_STATUS.RUNNING)
			{
				return Fail("AGENT-EXECUTION-NOT-CANCELLABLE", "Running状態のExecutionだけをCancelできます。", out errorCode, out message);
			}
			execution.cancelRequested = true;
			CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.CANCELLED, null, "Cancellation was accepted at a safe Agent step boundary.");
			return true;
		}

		internal void Tick()
		{
			foreach (UnityAgentMcpExecutionRecord execution in _executions.Values
				.Where(value => value.status == E_AGENT_EXECUTION_STATUS.RUNNING)
				.ToArray())
			{
				Advance(execution);
			}
		}

		internal void InterruptRunning(string errorCode, string reason)
		{
			foreach (UnityAgentMcpExecutionRecord execution in _executions.Values
				.Where(value => value.status == E_AGENT_EXECUTION_STATUS.RUNNING)
				.ToArray())
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.INTERRUPTED, errorCode, reason);
			}
		}

		internal JObject BuildPayload(UnityAgentMcpExecutionRecord execution)
		{
			return new JObject
			{
				["success"] = execution.status == E_AGENT_EXECUTION_STATUS.RUNNING || execution.status == E_AGENT_EXECUTION_STATUS.SUCCEEDED,
				["executionSucceeded"] = execution.status == E_AGENT_EXECUTION_STATUS.SUCCEEDED,
				["executionId"] = execution.executionId,
				["graphId"] = execution.graphId,
				["catalogSchemaVersion"] = execution.catalogSchemaVersion,
				["catalogFingerprint"] = execution.catalogFingerprint,
				["expectedRevision"] = execution.expectedRevision,
				["status"] = execution.status.ToString(),
				["startedAtUtc"] = execution.startedAtUtc == default ? null : execution.startedAtUtc.ToString("O"),
				["completedAtUtc"] = execution.completedAtUtc == default ? null : execution.completedAtUtc.ToString("O"),
				["timeoutSeconds"] = execution.timeoutSeconds,
				["completedStepCount"] = execution.stepResults.Count,
				["totalStepCount"] = execution.orderedSteps.Count,
				["errorCode"] = execution.errorCode,
				["message"] = execution.message,
				["stepResults"] = new JArray(execution.stepResults)
			};
		}

		internal void ResetForTests()
		{
			foreach (UnityAgentMcpExecutionRecord execution in _executions.Values
				.Where(value => value.status == E_AGENT_EXECUTION_STATUS.RUNNING)
				.ToArray())
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.CANCELLED, null, "Test reset.");
			}
			_executions.Clear();
		}

		private void Advance(UnityAgentMcpExecutionRecord execution)
		{
			if (execution == null || execution.status != E_AGENT_EXECUTION_STATUS.RUNNING)
			{
				return;
			}
			if (execution.cancelRequested)
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.CANCELLED, null, "Cancellation was requested before the next safe step.");
				return;
			}
			if (_utcNow() > execution.deadlineUtc)
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.INTERRUPTED, "AGENT-EXECUTION-TIMEOUT", "Execution exceeded the cooperative timeout before the next step.");
				return;
			}
			if (Session.Revision != execution.expectedRevision)
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.INTERRUPTED, "AGENT-REVISION-CHANGED", "Editor Revision changed before the next delegated step.");
				return;
			}
			if (execution.nextStepIndex >= execution.orderedSteps.Count)
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.SUCCEEDED, null, "Execution completed.");
				return;
			}

			UnityAgentMcpStepInput step = execution.orderedSteps[execution.nextStepIndex];
			DateTime stepStartedAtUtc = _utcNow();
			AppendTrace(execution, "STEP_STARTED", step, null, null, Session.Revision, 0.0);
			long revisionBefore = Session.Revision;
			JObject stepResult = DelegateStep(step);
			DateTime stepCompletedAtUtc = _utcNow();
			double durationMs = Math.Max(0.0, (stepCompletedAtUtc - stepStartedAtUtc).TotalMilliseconds);
			stepResult["stepId"] = step.stepId;
			stepResult["domainId"] = step.domainId;
			stepResult["toolName"] = step.toolName;
			execution.stepResults.Add(stepResult);
			string resultCode = stepResult.Value<string>("resultCode") ?? E_AGENT_STEP_OUTCOME.AMBIGUOUS.ToString();
			execution.stepSummaries.Add(new AgentExecutionStepSummary
			{
				stepId = step.stepId,
				domainId = step.domainId,
				toolName = step.toolName,
				resultCode = resultCode,
				durationMs = durationMs
			});
			AppendTrace(execution, "STEP_COMPLETED", step, resultCode, resultCode, Session.Revision, durationMs);

			if (_utcNow() > execution.deadlineUtc)
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.INTERRUPTED, "AGENT-EXECUTION-TIMEOUT", "A delegated step exceeded the cooperative timeout.");
				return;
			}

			E_AGENT_STEP_OUTCOME outcome = ParseOutcome(resultCode);
			if (outcome != E_AGENT_STEP_OUTCOME.SUCCEEDED)
			{
				bool priorSuccess = execution.stepSummaries
					.Take(execution.stepSummaries.Count - 1)
					.Any(value => value.resultCode == E_AGENT_STEP_OUTCOME.SUCCEEDED.ToString());
				E_AGENT_EXECUTION_STATUS terminalStatus = priorSuccess
					? E_AGENT_EXECUTION_STATUS.PARTIAL
					: E_AGENT_EXECUTION_STATUS.FAILED;
				CompleteExecution(
					execution,
					terminalStatus,
					stepResult.Value<string>("errorCode") ?? "AGENT-DELEGATE-FAILED",
					stepResult.Value<string>("message") ?? "Delegated tool reported failure.");
				return;
			}

			long revisionAfter = Session.Revision;
			if (_approvalService.RequiresApproval(step))
			{
				execution.expectedRevision = revisionAfter;
			}
			else if (revisionAfter != revisionBefore)
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.INTERRUPTED, "AGENT-UNEXPECTED-MUTATION", "Read-only delegated step changed the Editor Revision.");
				return;
			}

			execution.nextStepIndex++;
			if (execution.nextStepIndex >= execution.orderedSteps.Count)
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.SUCCEEDED, null, "Execution completed.");
			}
		}

		private JObject DelegateStep(UnityAgentMcpStepInput step)
		{
			if (!_delegateRegistry.TryInvoke(step.toolName, step.parameters ?? new JObject(), out object result, out Exception exception))
			{
				return Failure("FAILED", "AGENT-DELEGATE-NOT-REGISTERED", $"Agent delegate対象外です: {step.toolName}", null);
			}
			if (exception != null)
			{
				Exception actual = exception is System.Reflection.TargetInvocationException invocation && invocation.InnerException != null
					? invocation.InnerException
					: exception;
				return Failure("FAILED", "AGENT-DELEGATE-FAILED", actual.Message, null);
			}

			JToken delegated;
			try
			{
				delegated = result == null ? JValue.CreateNull() : JToken.FromObject(result);
			}
			catch (Exception caught)
			{
				return Failure("AMBIGUOUS", "AGENT-DELEGATE-RESULT-MALFORMED", caught.Message, null);
			}

			AgentNormalizedResult normalized = AgentResultNormalizer.Normalize(delegated);
			bool success = normalized.Outcome == E_AGENT_STEP_OUTCOME.SUCCEEDED;
			JObject response = new JObject
			{
				["success"] = success,
				["resultCode"] = normalized.Outcome.ToString(),
				["delegatedResult"] = delegated
			};
			if (!success)
			{
				response["errorCode"] = normalized.ErrorCode ?? FallbackErrorCode(normalized.Outcome);
				response["message"] = normalized.Message ?? "Delegated tool reported failure.";
			}
			return response;
		}

		private void CompleteExecution(UnityAgentMcpExecutionRecord execution, E_AGENT_EXECUTION_STATUS status, string errorCode, string message)
		{
			if (execution == null || execution.status != E_AGENT_EXECUTION_STATUS.RUNNING)
			{
				return;
			}
			execution.status = status;
			execution.errorCode = errorCode;
			execution.message = message;
			execution.completedAtUtc = _utcNow();
			if (!execution.terminalTraceWritten)
			{
				execution.terminalTraceWritten = true;
				AppendTrace(execution, TerminalEvent(status), null, errorCode, status.ToString(), Session.Revision, Math.Max(0.0, (execution.completedAtUtc - execution.startedAtUtc).TotalMilliseconds));
			}
			if (!execution.historyPersisted)
			{
				execution.historyPersisted = true;
				_historyStore.TryAppend(execution);
			}
		}

		private void AppendTrace(UnityAgentMcpExecutionRecord execution, string eventName, UnityAgentMcpStepInput step, string resultCode, string traceResultCode, long revision, double durationMs)
		{
			_traceStore.TryAppend(new AgentTraceEvent
			{
				schemaVersion = 1,
				timestampUtc = _utcNow(),
				executionId = execution.executionId,
				graphId = execution.graphId,
				stepId = step?.stepId,
				domainId = step?.domainId,
				toolName = step?.toolName,
				eventName = eventName,
				revision = revision,
				resultCode = traceResultCode ?? resultCode,
				durationMs = durationMs
			});
		}

		private static E_AGENT_STEP_OUTCOME ParseOutcome(string resultCode)
		{
			return Enum.TryParse(resultCode, true, out E_AGENT_STEP_OUTCOME outcome)
				? outcome
				: E_AGENT_STEP_OUTCOME.AMBIGUOUS;
		}

		private static string TerminalEvent(E_AGENT_EXECUTION_STATUS status)
		{
			switch (status)
			{
				case E_AGENT_EXECUTION_STATUS.SUCCEEDED: return "EXECUTION_COMPLETED";
				case E_AGENT_EXECUTION_STATUS.PARTIAL: return "EXECUTION_PARTIAL";
				case E_AGENT_EXECUTION_STATUS.CANCELLED: return "EXECUTION_CANCELLED";
				case E_AGENT_EXECUTION_STATUS.INTERRUPTED: return "EXECUTION_INTERRUPTED";
				default: return "EXECUTION_FAILED";
			}
		}

		private static JObject Failure(string resultCode, string errorCode, string message, JToken delegated)
		{
			return new JObject
			{
				["success"] = false,
				["resultCode"] = resultCode,
				["errorCode"] = errorCode,
				["message"] = message,
				["delegatedResult"] = delegated ?? JValue.CreateNull()
			};
		}

		private static string FallbackErrorCode(E_AGENT_STEP_OUTCOME outcome)
		{
			switch (outcome)
			{
				case E_AGENT_STEP_OUTCOME.PARTIAL: return "AGENT-DELEGATE-PARTIAL";
				case E_AGENT_STEP_OUTCOME.UNSUPPORTED: return "AGENT-DELEGATE-UNSUPPORTED";
				case E_AGENT_STEP_OUTCOME.AMBIGUOUS: return "AGENT-DELEGATE-RESULT-AMBIGUOUS";
				default: return "AGENT-DELEGATE-FAILED";
			}
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
