#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityGraphicsMcp;

namespace UnityAgentMcp
{
	[InitializeOnLoad]
	public sealed class UnityAgentMcpRuntime
	{
		private const string CATALOG_PATH = "Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpCatalog.json";
		private static readonly UnityAgentMcpRuntime _instance = new UnityAgentMcpRuntime();

		private readonly AgentDelegateRegistry _delegateRegistry;
		private readonly AgentExecutionHistoryStore _historyStore;
		private readonly AgentExecutionTraceStore _traceStore;
		private readonly AgentWorkflowValidator _workflowValidator;
		private readonly AgentApprovalService _approvalService;
		private readonly AgentGraphCompiler _graphCompiler;
		private readonly AgentExecutionEngine _executionEngine;
		private AgentCatalogSnapshot _catalog;
		private string _catalogError;

		internal static Func<DateTime> UtcNowOverrideForTests { get; set; }
		internal static string CatalogPathOverrideForTests { get; set; }

		public static UnityAgentMcpRuntime Instance => _instance;

		internal static string[] RegisteredDomainDelegateNamesForTests => _instance._delegateRegistry.RegisteredNames.ToArray();
		internal static string HistoryPathForTests => _instance._historyStore.HistoryPath;
		internal static string TracePathForTests => _instance._traceStore.TracePath;

		static UnityAgentMcpRuntime()
		{
			EditorApplication.update += _instance.Tick;
			AssemblyReloadEvents.beforeAssemblyReload += () => _instance.InterruptRunning("AGENT-EXECUTION-INTERRUPTED", "DOMAIN_RELOAD");
			CompilationPipeline.compilationStarted += _ => _instance.InterruptRunning("AGENT-EXECUTION-INTERRUPTED", "COMPILATION_STARTED");
			EditorApplication.playModeStateChanged += state =>
			{
				if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
				{
					_instance.InterruptRunning("AGENT-EXECUTION-INTERRUPTED", state.ToString());
				}
			};
			EditorApplication.quitting += () => _instance.InterruptRunning("AGENT-EXECUTION-INTERRUPTED", "EDITOR_QUITTING");
		}

		private UnityAgentMcpRuntime()
		{
			_delegateRegistry = AgentDelegateRegistry.Discover();
			_historyStore = new AgentExecutionHistoryStore();
			_traceStore = new AgentExecutionTraceStore();
			_historyStore.Load();
			LoadCatalog();
			_workflowValidator = new AgentWorkflowValidator(_catalog);
			_approvalService = new AgentApprovalService(_catalog, () => UtcNow);
			_graphCompiler = new AgentGraphCompiler(_workflowValidator, _approvalService, () => UtcNow);
			_executionEngine = new AgentExecutionEngine(
				_delegateRegistry,
				_approvalService,
				_historyStore,
				_traceStore,
				() => UtcNow);
		}

		private static DateTime UtcNow => UtcNowOverrideForTests?.Invoke() ?? DateTime.UtcNow;

		public JObject InspectCapabilities()
		{
			if (_catalog == null)
			{
				return Error("AGENT-CATALOG-INVALID", _catalogError);
			}

			return Success(new JObject
			{
				["catalogPath"] = CATALOG_PATH,
				["domains"] = JArray.FromObject(_catalog.BuildPublicDomains()),
				["directUnityMutation"] = false,
				["defaultExecutionTimeoutSeconds"] = AgentExecutionEngine.DEFAULT_EXECUTION_TIMEOUT_SECONDS,
				["maxExecutionTimeoutSeconds"] = AgentExecutionEngine.MAX_EXECUTION_TIMEOUT_SECONDS,
				["cooperativeExecution"] = true,
				["integrationCandidateExecutionEnabled"] = true
			});
		}

		public JObject ValidateWorkflow(UnityAgentMcpStepInput[] steps)
		{
			if (_catalog == null)
			{
				return Error("AGENT-CATALOG-INVALID", _catalogError);
			}
			if (!_workflowValidator.TryValidate(steps, out List<UnityAgentMcpStepInput> normalized, out string errorCode, out string message))
			{
				return Error(errorCode, message);
			}
			return Success(new JObject
			{
				["valid"] = true,
				["stepCount"] = normalized.Count,
				["domains"] = new JArray(normalized.Select(value => value.domainId).Distinct(StringComparer.Ordinal))
			});
		}

		public JObject CompileGraph(long expectedRevision, UnityAgentMcpStepInput[] steps)
		{
			if (expectedRevision != Session.Revision)
			{
				return Error("AGENT-REVISION-CHANGED", "Graph作成前にEditor Revisionが変更されました。");
			}
			if (!TryGetCurrentCatalog(out AgentCatalogSnapshot currentCatalog, out JObject catalogError))
			{
				return catalogError;
			}
			if (!_graphCompiler.TryCompile(expectedRevision, steps, currentCatalog, out UnityAgentMcpCompiledGraph graph, out _, out string errorCode, out string message))
			{
				return Error(errorCode, message);
			}
			return Success(new JObject
			{
				["graphId"] = graph.graphId,
				["catalogSchemaVersion"] = graph.catalogSchemaVersion,
				["catalogFingerprint"] = graph.catalogFingerprint,
				["expectedRevision"] = expectedRevision,
				["stepCount"] = graph.steps.Count,
				["requiredApprovalGroups"] = new JArray(graph.requiredApprovalGroups),
				["defaultExecutionTimeoutSeconds"] = AgentExecutionEngine.DEFAULT_EXECUTION_TIMEOUT_SECONDS
			});
		}

		public JObject PreviewExecution(string graphId)
		{
			if (!TryGetCurrentGraph(graphId, out UnityAgentMcpCompiledGraph graph, out JObject error))
			{
				return error;
			}
			return Success(new JObject
			{
				["graphId"] = graph.graphId,
				["catalogSchemaVersion"] = graph.catalogSchemaVersion,
				["catalogFingerprint"] = graph.catalogFingerprint,
				["expectedRevision"] = graph.expectedRevision,
				["status"] = graph.requiredApprovalGroups.Count == 0
					? E_AGENT_EXECUTION_STATUS.PREVIEW.ToString()
					: E_AGENT_EXECUTION_STATUS.AWAITING_APPROVAL.ToString(),
				["requiredApprovalGroups"] = new JArray(graph.requiredApprovalGroups),
				["steps"] = JArray.FromObject(graph.steps.Select(value => new
				{
					value.stepId,
					value.domainId,
					value.toolName,
					value.toolGroup,
					mutation = _approvalService.RequiresApproval(value)
				})),
				["directUnityMutation"] = false
			});
		}

		public JObject SubmitApproval(string graphId, string[] approvedGroups, string confirmation)
		{
			if (!TryGetCurrentGraph(graphId, out UnityAgentMcpCompiledGraph graph, out JObject error))
			{
				return error;
			}
			if (!_approvalService.TrySubmit(graph, approvedGroups, confirmation, out string approvalToken, out string errorCode, out string message))
			{
				return Error(errorCode, message);
			}
			return Success(new JObject
			{
				["graphId"] = graph.graphId,
				["approvalToken"] = approvalToken,
				["expiresAtUtc"] = graph.approvalExpiresAtUtc.ToString("O")
			});
		}

		public JObject StartExecution(string graphId, long currentRevision, string approvalToken, int timeoutSeconds = AgentExecutionEngine.DEFAULT_EXECUTION_TIMEOUT_SECONDS)
		{
			if (!TryGetCurrentGraph(graphId, out UnityAgentMcpCompiledGraph graph, out JObject error))
			{
				return error;
			}
			if (!_approvalService.TryValidateStart(graph, approvalToken, out string approvalErrorCode, out string approvalMessage))
			{
				return Error(approvalErrorCode, approvalMessage);
			}
			if (!_executionEngine.TryStart(graph, currentRevision, timeoutSeconds, out UnityAgentMcpExecutionRecord execution, out string errorCode, out string message))
			{
				return Error(errorCode, message);
			}
			EditorApplication.QueuePlayerLoopUpdate();
			return _executionEngine.BuildPayload(execution);
		}

		public JObject GetExecutionStatus(string executionId)
		{
			return _executionEngine.TryGet(executionId, out UnityAgentMcpExecutionRecord execution)
				? _executionEngine.BuildPayload(execution)
				: Error("AGENT-EXECUTION-NOT-FOUND", "Executionが見つかりません。");
		}

		public JObject CancelExecution(string executionId)
		{
			if (!_executionEngine.TryCancel(executionId, out UnityAgentMcpExecutionRecord execution, out string errorCode, out string message))
			{
				return Error(errorCode, message);
			}
			return Success(new JObject
			{
				["executionId"] = execution.executionId,
				["cancelRequested"] = true,
				["status"] = execution.status.ToString()
			});
		}

		public void NotifyClientDisconnected()
		{
			InterruptRunning("AGENT-CLIENT-DISCONNECTED", "MCP_CLIENT_DISCONNECTED");
		}

		public JObject GetExecutionHistory(int maxItems)
		{
			int count = Mathf.Clamp(maxItems, 1, 100);
			return Success(new JObject
			{
				["items"] = _historyStore.GetItems(count),
				["total"] = _historyStore.Count
			});
		}

		public JObject GetErrorCatalog()
		{
			return Success(new JObject
			{
				["errors"] = new JArray
				{
					ErrorEntry("AGENT-CATALOG-INVALID", false),
					ErrorEntry("AGENT-CATALOG-CHANGED", false),
					ErrorEntry("AGENT-DOMAIN-NOT-OPERATIONAL", false),
					ErrorEntry("AGENT-TOOL-GROUP-MISSING", false),
					ErrorEntry("AGENT-GRAPH-CYCLE", false),
					ErrorEntry("AGENT-GRAPH-TOO-LARGE", false),
					ErrorEntry("AGENT-APPROVAL-MISSING-OR-EXPIRED", true),
					ErrorEntry("AGENT-REVISION-CHANGED", true),
					ErrorEntry("AGENT-DELEGATE-NOT-REGISTERED", false),
					ErrorEntry("AGENT-DELEGATE-RESULT-MALFORMED", false),
					ErrorEntry("AGENT-DELEGATE-RESULT-AMBIGUOUS", false),
					ErrorEntry("AGENT-HISTORY-PERSISTENCE-FAILED", true),
					ErrorEntry("AGENT-TRACE-PERSISTENCE-FAILED", true),
					ErrorEntry("AGENT-EXECUTION-INTERRUPTED", true),
					ErrorEntry("AGENT-EXECUTION-TIMEOUT", true),
					ErrorEntry("AGENT-CLIENT-DISCONNECTED", true),
					ErrorEntry("AGENT-TIMEOUT-INVALID", false)
				}
			});
		}

		internal void ProcessPendingExecutionsForTests()
		{
			_executionEngine.Tick();
		}

		internal void ResetExecutionsForTests()
		{
			_executionEngine.ResetForTests();
			_graphCompiler.Reset();
			UtcNowOverrideForTests = null;
			CatalogPathOverrideForTests = null;
		}

		internal void ResetPersistenceForTests()
		{
			_historyStore.ResetForTests();
			_traceStore.ResetForTests();
		}

		private void Tick()
		{
			_executionEngine.Tick();
		}

		private void InterruptRunning(string errorCode, string reason)
		{
			_executionEngine.InterruptRunning(errorCode, reason);
		}

		private bool TryGetCurrentGraph(string graphId, out UnityAgentMcpCompiledGraph graph, out JObject error)
		{
			if (!_graphCompiler.TryGet(graphId, out graph))
			{
				error = Error("AGENT-GRAPH-NOT-FOUND", "Compiled Graphが見つかりません。");
				return false;
			}
			if (graph.expectedRevision != Session.Revision)
			{
				error = Error("AGENT-REVISION-CHANGED", "Graph作成後にEditor Revisionが変更されました。");
				return false;
			}
			if (!TryGetCurrentCatalog(out _, out error))
			{
				return false;
			}
			error = null;
			return true;
		}

		private bool TryGetCurrentCatalog(out AgentCatalogSnapshot current, out JObject error)
		{
			current = null;
			if (_catalog == null)
			{
				error = Error("AGENT-CATALOG-INVALID", _catalogError);
				return false;
			}
			string path = string.IsNullOrEmpty(CatalogPathOverrideForTests) ? CATALOG_PATH : CatalogPathOverrideForTests;
			if (!AgentCatalogService.TryLoad(
				Path.GetFullPath(path),
				_delegateRegistry.RegisteredNames,
				out current,
				out _catalogError) ||
				!string.Equals(current.Fingerprint, _catalog.Fingerprint, StringComparison.Ordinal))
			{
				error = Error("AGENT-CATALOG-CHANGED", "Runtime起動時からCatalogが変更されたか、読み込めませんでした。");
				return false;
			}
			error = null;
			return true;
		}

		private void LoadCatalog()
		{
			string path = string.IsNullOrEmpty(CatalogPathOverrideForTests) ? CATALOG_PATH : CatalogPathOverrideForTests;
			if (!AgentCatalogService.TryLoad(
				Path.GetFullPath(path),
				_delegateRegistry.RegisteredNames,
				out _catalog,
				out _catalogError))
			{
				_catalog = null;
			}
		}

		private static JObject Success(JObject data)
		{
			data["success"] = true;
			return data;
		}

		private static JObject Error(string code, string message)
		{
			return new JObject
			{
				["success"] = false,
				["errorCode"] = code,
				["message"] = message ?? string.Empty
			};
		}

		private static JObject ErrorEntry(string code, bool retryable)
		{
			return new JObject
			{
				["code"] = code,
				["retryable"] = retryable
			};
		}
	}
}

#endif
