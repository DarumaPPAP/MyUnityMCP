#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityGraphicsMcp;

namespace UnityAgentMcp
{
	public enum E_AGENT_EXECUTION_STATUS
	{
		PREVIEW,
		AWAITING_APPROVAL,
		RUNNING,
		SUCCEEDED,
		PARTIAL,
		FAILED,
		CANCELLED,
		INTERRUPTED
	}

	public sealed class UnityAgentMcpStepInput
	{
		public string stepId;
		public string domainId;
		public string toolName;
		public string toolGroup;
		public string[] dependsOn;
		public JObject parameters;
	}

	public sealed class UnityAgentMcpCatalogData
	{
		public int schemaVersion;
		public UnityAgentMcpDomainData[] domains;
	}

	public sealed class UnityAgentMcpDomainData
	{
		public string domainId;
		public string status;
		public string[] toolGroups;
		public string[] tools;
		public bool directUnityMutationAllowed;
	}

	internal sealed class UnityAgentMcpCompiledGraph
	{
		public string graphId;
		public long expectedRevision;
		public DateTime createdAtUtc;
		public List<UnityAgentMcpStepInput> steps;
		public HashSet<string> requiredApprovalGroups;
		public string approvalToken;
		public DateTime approvalExpiresAtUtc;
		public bool approved;
	}

	internal sealed class UnityAgentMcpExecutionRecord
	{
		public string executionId;
		public string graphId;
		public E_AGENT_EXECUTION_STATUS status;
		public DateTime startedAtUtc;
		public DateTime completedAtUtc;
		public string errorCode;
		public string message;
		public List<JObject> stepResults = new List<JObject>();
		public bool cancelRequested;
	}

	[InitializeOnLoad]
	public sealed class UnityAgentMcpRuntime
	{
		private const string CATALOG_PATH = "Packages/com.darumappap.my-unity-mcp/Editor/UnityAgentMcpCatalog.json";
		private const string HISTORY_PATH = "Library/MyUnityMCP/AgentExecution/history.jsonl";
		private const int APPROVAL_TTL_MINUTES = 10;

		private static readonly HashSet<string> APPROVAL_GROUPS = new HashSet<string>(StringComparer.Ordinal)
		{
			"mutate",
			"save",
			"bake",
			"build",
			"content_build"
		};

		private static readonly Dictionary<string, Func<JObject, object>> GRAPHICS_DELEGATES =
			new Dictionary<string, Func<JObject, object>>(StringComparer.Ordinal)
			{
				{"graphics.inspect_project", GraphicsInspectProjectTool.HandleCommand},
				{"graphics.inspect_scene", GraphicsInspectSceneTool.HandleCommand},
				{"graphics.validate_scene", GraphicsValidateSceneTool.HandleCommand},
				{"graphics.get_execution_history", GraphicsGetExecutionHistoryTool.HandleCommand},
				{"graphics.get_error_catalog", GraphicsGetErrorCatalogTool.HandleCommand},
				{"graphics.get_support_matrix", GraphicsGetSupportMatrixTool.HandleCommand}
			};

		private static readonly UnityAgentMcpRuntime _instance = new UnityAgentMcpRuntime();

		private readonly Dictionary<string, UnityAgentMcpCompiledGraph> _graphs =
			new Dictionary<string, UnityAgentMcpCompiledGraph>(StringComparer.Ordinal);
		private readonly Dictionary<string, UnityAgentMcpExecutionRecord> _executions =
			new Dictionary<string, UnityAgentMcpExecutionRecord>(StringComparer.Ordinal);
		private readonly List<JObject> _history = new List<JObject>();
		private UnityAgentMcpCatalogData _catalog;
		private string _catalogError;

		public static UnityAgentMcpRuntime Instance => _instance;

		static UnityAgentMcpRuntime()
		{
			AssemblyReloadEvents.beforeAssemblyReload += () => _instance.InterruptRunning("DOMAIN_RELOAD");
			CompilationPipeline.compilationStarted += _ => _instance.InterruptRunning("COMPILATION_STARTED");
			EditorApplication.quitting += () => _instance.InterruptRunning("EDITOR_QUITTING");
		}

		private UnityAgentMcpRuntime()
		{
			LoadCatalog();
			LoadHistory();
		}

		public JObject InspectCapabilities()
		{
			if (_catalog == null)
			{
				return Error("AGENT-CATALOG-INVALID", _catalogError);
			}

			return Success(new JObject
			{
				["catalogPath"] = CATALOG_PATH,
				["domains"] = JArray.FromObject(_catalog.domains ?? Array.Empty<UnityAgentMcpDomainData>()),
				["directUnityMutation"] = false
			});
		}

		public JObject ValidateWorkflow(UnityAgentMcpStepInput[] steps)
		{
			if (!TryValidateSteps(steps, out List<UnityAgentMcpStepInput> normalized, out JObject error))
			{
				return error;
			}

			return Success(new JObject
			{
				["valid"] = true,
				["stepCount"] = normalized.Count,
				["domains"] = new JArray(normalized.Select(value => value.domainId).Distinct())
			});
		}

		public JObject CompileGraph(long expectedRevision, UnityAgentMcpStepInput[] steps)
		{
			if (!TryValidateSteps(steps, out List<UnityAgentMcpStepInput> normalized, out JObject error))
			{
				return error;
			}

			string graphId = $"agent-graph-{Guid.NewGuid():N}";
			UnityAgentMcpCompiledGraph graph = new UnityAgentMcpCompiledGraph
			{
				graphId = graphId,
				expectedRevision = expectedRevision,
				createdAtUtc = DateTime.UtcNow,
				steps = normalized,
				requiredApprovalGroups = new HashSet<string>(
					normalized.Where(value => APPROVAL_GROUPS.Contains(value.toolGroup))
						.Select(value => value.toolGroup),
					StringComparer.Ordinal)
			};
			_graphs[graphId] = graph;

			return Success(new JObject
			{
				["graphId"] = graphId,
				["expectedRevision"] = expectedRevision,
				["stepCount"] = normalized.Count,
				["requiredApprovalGroups"] = new JArray(graph.requiredApprovalGroups)
			});
		}

		public JObject PreviewExecution(string graphId)
		{
			if (!TryGetGraph(graphId, out UnityAgentMcpCompiledGraph graph, out JObject error))
			{
				return error;
			}

			return Success(new JObject
			{
				["graphId"] = graph.graphId,
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
					mutation = APPROVAL_GROUPS.Contains(value.toolGroup)
				})),
				["directUnityMutation"] = false
			});
		}

		public JObject SubmitApproval(string graphId, string[] approvedGroups, string confirmation)
		{
			if (!TryGetGraph(graphId, out UnityAgentMcpCompiledGraph graph, out JObject error))
			{
				return error;
			}
			if (!string.Equals(confirmation, "APPROVE_AGENT_EXECUTION", StringComparison.Ordinal))
			{
				return Error("AGENT-APPROVAL-CONFIRMATION-INVALID", "明示確認文字列が一致しません。");
			}

			HashSet<string> approved = new HashSet<string>(approvedGroups ?? Array.Empty<string>(), StringComparer.Ordinal);
			if (!graph.requiredApprovalGroups.IsSubsetOf(approved))
			{
				return Error("AGENT-APPROVAL-INCOMPLETE", "必要なTool Groupがすべて承認されていません。");
			}

			graph.approved = true;
			graph.approvalToken = Guid.NewGuid().ToString("N");
			graph.approvalExpiresAtUtc = DateTime.UtcNow.AddMinutes(APPROVAL_TTL_MINUTES);
			return Success(new JObject
			{
				["graphId"] = graph.graphId,
				["approvalToken"] = graph.approvalToken,
				["expiresAtUtc"] = graph.approvalExpiresAtUtc.ToString("O")
			});
		}

		public JObject StartExecution(string graphId, long currentRevision, string approvalToken)
		{
			if (!TryGetGraph(graphId, out UnityAgentMcpCompiledGraph graph, out JObject error))
			{
				return error;
			}
			if (graph.expectedRevision != currentRevision)
			{
				return Error("AGENT-REVISION-CHANGED", "Preview後にRevisionが変更されました。");
			}
			if (graph.requiredApprovalGroups.Count > 0)
			{
				if (!graph.approved || DateTime.UtcNow > graph.approvalExpiresAtUtc)
				{
					return Error("AGENT-APPROVAL-MISSING-OR-EXPIRED", "承認が存在しないか期限切れです。");
				}
				if (!string.Equals(approvalToken, graph.approvalToken, StringComparison.Ordinal))
				{
					return Error("AGENT-APPROVAL-TOKEN-MISMATCH", "Approval Tokenが一致しません。");
				}
			}

			UnityAgentMcpExecutionRecord execution = new UnityAgentMcpExecutionRecord
			{
				executionId = $"agent-exec-{Guid.NewGuid():N}",
				graphId = graph.graphId,
				status = E_AGENT_EXECUTION_STATUS.RUNNING,
				startedAtUtc = DateTime.UtcNow
			};
			_executions[execution.executionId] = execution;

			foreach (UnityAgentMcpStepInput step in TopologicalOrder(graph.steps))
			{
				if (execution.cancelRequested)
				{
					execution.status = E_AGENT_EXECUTION_STATUS.CANCELLED;
					execution.message = "Cancellation was requested before the next safe step.";
					break;
				}

				JObject stepResult = DelegateStep(step);
				stepResult["stepId"] = step.stepId;
				execution.stepResults.Add(stepResult);
				if (!(stepResult.Value<bool?>("success") ?? false))
				{
					execution.status = execution.stepResults.Count > 1
						? E_AGENT_EXECUTION_STATUS.PARTIAL
						: E_AGENT_EXECUTION_STATUS.FAILED;
					execution.errorCode = stepResult.Value<string>("errorCode") ?? "AGENT-DELEGATE-FAILED";
					execution.message = stepResult.Value<string>("message") ?? "Delegated tool failed.";
					break;
				}
			}

			if (execution.status == E_AGENT_EXECUTION_STATUS.RUNNING)
			{
				execution.status = E_AGENT_EXECUTION_STATUS.SUCCEEDED;
				execution.message = "Execution completed.";
			}
			execution.completedAtUtc = DateTime.UtcNow;
			PersistHistory(execution);
			return ExecutionPayload(execution);
		}

		public JObject GetExecutionStatus(string executionId)
		{
			return _executions.TryGetValue(executionId ?? string.Empty, out UnityAgentMcpExecutionRecord execution)
				? ExecutionPayload(execution)
				: Error("AGENT-EXECUTION-NOT-FOUND", "Executionが見つかりません。");
		}

		public JObject CancelExecution(string executionId)
		{
			if (!_executions.TryGetValue(executionId ?? string.Empty, out UnityAgentMcpExecutionRecord execution))
			{
				return Error("AGENT-EXECUTION-NOT-FOUND", "Executionが見つかりません。");
			}
			if (execution.status != E_AGENT_EXECUTION_STATUS.RUNNING)
			{
				return Error("AGENT-EXECUTION-NOT-CANCELLABLE", "Running状態のExecutionだけをCancelできます。");
			}
			execution.cancelRequested = true;
			return Success(new JObject
			{
				["executionId"] = execution.executionId,
				["cancelRequested"] = true
			});
		}

		public JObject GetExecutionHistory(int maxItems)
		{
			int count = Mathf.Clamp(maxItems, 1, 100);
			return Success(new JObject
			{
				["items"] = new JArray(_history.Skip(Math.Max(0, _history.Count - count))),
				["total"] = _history.Count
			});
		}

		public JObject GetErrorCatalog()
		{
			return Success(new JObject
			{
				["errors"] = new JArray
				{
					ErrorEntry("AGENT-CATALOG-INVALID", false),
					ErrorEntry("AGENT-DOMAIN-NOT-OPERATIONAL", false),
					ErrorEntry("AGENT-TOOL-GROUP-MISSING", false),
					ErrorEntry("AGENT-GRAPH-CYCLE", false),
					ErrorEntry("AGENT-APPROVAL-MISSING-OR-EXPIRED", true),
					ErrorEntry("AGENT-REVISION-CHANGED", true),
					ErrorEntry("AGENT-DELEGATE-NOT-REGISTERED", false),
					ErrorEntry("AGENT-EXECUTION-INTERRUPTED", true)
				}
			});
		}

		private bool TryValidateSteps(
			UnityAgentMcpStepInput[] steps,
			out List<UnityAgentMcpStepInput> normalized,
			out JObject error)
		{
			normalized = (steps ?? Array.Empty<UnityAgentMcpStepInput>())
				.Where(value => value != null)
				.ToList();
			error = null;
			if (_catalog == null)
			{
				error = Error("AGENT-CATALOG-INVALID", _catalogError);
				return false;
			}
			if (normalized.Count == 0)
			{
				error = Error("AGENT-WORKFLOW-EMPTY", "WorkflowにStepがありません。");
				return false;
			}

			Dictionary<string, UnityAgentMcpDomainData> domains = _catalog.domains.ToDictionary(value => value.domainId, StringComparer.Ordinal);
			HashSet<string> stepIds = new HashSet<string>(StringComparer.Ordinal);
			foreach (UnityAgentMcpStepInput step in normalized)
			{
				if (string.IsNullOrWhiteSpace(step.stepId) || !stepIds.Add(step.stepId))
				{
					error = Error("AGENT-STEP-ID-INVALID", "Step IDが空または重複しています。");
					return false;
				}
				if (!domains.TryGetValue(step.domainId ?? string.Empty, out UnityAgentMcpDomainData domain))
				{
					error = Error("AGENT-DOMAIN-NOT-FOUND", $"DomainがCatalogにありません: {step.domainId}");
					return false;
				}
				if (!string.Equals(domain.status, "editor_operational", StringComparison.Ordinal))
				{
					error = Error("AGENT-DOMAIN-NOT-OPERATIONAL", $"Domainは実行可能ではありません: {step.domainId}");
					return false;
				}
				if (!(domain.toolGroups ?? Array.Empty<string>()).Contains(step.toolGroup, StringComparer.Ordinal))
				{
					error = Error("AGENT-TOOL-GROUP-MISSING", $"Tool GroupがDomainにありません: {step.toolGroup}");
					return false;
				}
				if (!(domain.tools ?? Array.Empty<string>()).Contains(step.toolName, StringComparer.Ordinal))
				{
					error = Error("AGENT-TOOL-NOT-DECLARED", $"ToolがDomain Catalogにありません: {step.toolName}");
					return false;
				}
				if (domain.directUnityMutationAllowed)
				{
					error = Error("AGENT-DIRECT-MUTATION-FORBIDDEN", "Control Plane DomainはUnity APIを直接Mutationできません。");
					return false;
				}
				step.dependsOn = step.dependsOn ?? Array.Empty<string>();
				step.parameters = step.parameters ?? new JObject();
			}

			foreach (UnityAgentMcpStepInput step in normalized)
			{
				string missingDependency = (step.dependsOn ?? Array.Empty<string>())
					.FirstOrDefault(value => !stepIds.Contains(value));
				if (!string.IsNullOrEmpty(missingDependency))
				{
					error = Error("AGENT-DEPENDENCY-NOT-FOUND", $"依存Stepがありません: {missingDependency}");
					return false;
				}
			}

			if (HasCycle(normalized))
			{
				error = Error("AGENT-GRAPH-CYCLE", "Workflow GraphにCycleがあります。");
				return false;
			}
			return true;
		}

		private JObject DelegateStep(UnityAgentMcpStepInput step)
		{
			if (!string.Equals(step.domainId, "unity_graphics_mcp", StringComparison.Ordinal))
			{
				return Error("AGENT-DOMAIN-DELEGATE-MISSING", $"Delegateがありません: {step.domainId}");
			}
			if (!GRAPHICS_DELEGATES.TryGetValue(step.toolName, out Func<JObject, object> handler))
			{
				return Error("AGENT-DELEGATE-NOT-REGISTERED", $"Agent delegate対象外です: {step.toolName}");
			}

			try
			{
				object result = handler(step.parameters ?? new JObject());
				JToken delegated = result == null ? JValue.CreateNull() : JToken.FromObject(result);
				bool delegatedSuccess = delegated.Type == JTokenType.Object
					? delegated.Value<bool?>("success") ??
					  !string.Equals(delegated.Value<string>("status"), "ERROR", StringComparison.OrdinalIgnoreCase)
					: result != null;
				if (!delegatedSuccess)
				{
					return new JObject
					{
						["success"] = false,
						["errorCode"] = delegated.Value<string>("errorCode") ?? "AGENT-DELEGATE-FAILED",
						["message"] = delegated.Value<string>("message") ?? "Delegated tool reported failure.",
						["delegatedResult"] = delegated
					};
				}
				return new JObject
				{
					["success"] = true,
					["domainId"] = step.domainId,
					["toolName"] = step.toolName,
					["delegatedResult"] = delegated
				};
			}
			catch (Exception exception)
			{
				return Error("AGENT-DELEGATE-FAILED", exception.Message);
			}
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

		private static IEnumerable<UnityAgentMcpStepInput> TopologicalOrder(List<UnityAgentMcpStepInput> steps)
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

		private bool TryGetGraph(string graphId, out UnityAgentMcpCompiledGraph graph, out JObject error)
		{
			if (!_graphs.TryGetValue(graphId ?? string.Empty, out graph))
			{
				error = Error("AGENT-GRAPH-NOT-FOUND", "Compiled Graphが見つかりません。");
				return false;
			}
			error = null;
			return true;
		}

		private void LoadCatalog()
		{
			try
			{
				string absolutePath = Path.GetFullPath(CATALOG_PATH);
				_catalog = JsonConvert.DeserializeObject<UnityAgentMcpCatalogData>(File.ReadAllText(absolutePath));
				if (_catalog?.domains == null || _catalog.domains.Length == 0)
				{
					throw new InvalidDataException("Catalog domain list is empty.");
				}
			}
			catch (Exception exception)
			{
				_catalog = null;
				_catalogError = exception.Message;
			}
		}

		private void LoadHistory()
		{
			try
			{
				if (!File.Exists(HISTORY_PATH))
				{
					return;
				}
				foreach (string line in File.ReadLines(HISTORY_PATH).Reverse().Take(1000).Reverse())
				{
					if (!string.IsNullOrWhiteSpace(line))
					{
						_history.Add(JObject.Parse(line));
					}
				}
			}
			catch (Exception exception)
			{
				_catalogError = string.IsNullOrEmpty(_catalogError)
					? $"History load failed: {exception.Message}"
					: _catalogError;
			}
		}

		private void PersistHistory(UnityAgentMcpExecutionRecord execution)
		{
			JObject payload = ExecutionPayload(execution);
			_history.Add(payload);
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(HISTORY_PATH));
				File.AppendAllText(HISTORY_PATH, payload.ToString(Formatting.None) + Environment.NewLine);
			}
			catch (Exception exception)
			{
				execution.status = E_AGENT_EXECUTION_STATUS.PARTIAL;
				execution.errorCode = "AGENT-HISTORY-PERSISTENCE-FAILED";
				execution.message = exception.Message;
			}
		}

		private void InterruptRunning(string reason)
		{
			foreach (UnityAgentMcpExecutionRecord execution in _executions.Values.Where(value => value.status == E_AGENT_EXECUTION_STATUS.RUNNING))
			{
				execution.status = E_AGENT_EXECUTION_STATUS.INTERRUPTED;
				execution.errorCode = "AGENT-EXECUTION-INTERRUPTED";
				execution.message = reason;
				execution.completedAtUtc = DateTime.UtcNow;
				PersistHistory(execution);
			}
		}

		private static JObject ExecutionPayload(UnityAgentMcpExecutionRecord execution)
		{
			return new JObject
			{
				["success"] = execution.status == E_AGENT_EXECUTION_STATUS.SUCCEEDED,
				["executionId"] = execution.executionId,
				["graphId"] = execution.graphId,
				["status"] = execution.status.ToString(),
				["startedAtUtc"] = execution.startedAtUtc == default ? null : execution.startedAtUtc.ToString("O"),
				["completedAtUtc"] = execution.completedAtUtc == default ? null : execution.completedAtUtc.ToString("O"),
				["errorCode"] = execution.errorCode,
				["message"] = execution.message,
				["stepResults"] = new JArray(execution.stepResults)
			};
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
