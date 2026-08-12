#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
		public string approvalTokenHash;
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
		public DateTime deadlineUtc;
		public int timeoutSeconds;
		public long expectedRevision;
		public string errorCode;
		public string message;
		public List<UnityAgentMcpStepInput> orderedSteps = new List<UnityAgentMcpStepInput>();
		public int nextStepIndex;
		public List<JObject> stepResults = new List<JObject>();
		public bool cancelRequested;
		public bool historyPersisted;
	}

	[InitializeOnLoad]
	public sealed class UnityAgentMcpRuntime
	{
		private const string CATALOG_PATH = "Packages/com.darumappap.my-unity-mcp/Editor/Development/Agent/UnityAgentMcpCatalog.json";
		private const string HISTORY_PATH = "Library/MyUnityMCP/AgentExecution/history.jsonl";
		private const int APPROVAL_TTL_MINUTES = 10;
		private const int DEFAULT_EXECUTION_TIMEOUT_SECONDS = 60;
		private const int MAX_EXECUTION_TIMEOUT_SECONDS = 3600;

		private static readonly HashSet<string> APPROVAL_GROUPS = new HashSet<string>(StringComparer.Ordinal)
		{
			"mutate",
			"save",
			"bake"
		};

		private static readonly HashSet<string> APPROVAL_TOOLS = new HashSet<string>(StringComparer.Ordinal)
		{
			"graphics.apply_plan",
			"graphics.undo_last_transaction",
			"graphics.apply_environment_plan",
			"graphics.undo_last_environment_transaction",
			"graphics.apply_save_plan",
			"graphics.bake_dependencies",
			"graphics.start_apv_bake",
			"build.start_player",
			"addressables.apply_entry",
			"addressables.build_content",
			"ui.apply_rect_transform",
			"animation.apply_parameter",
			"audio.apply_source",
			"cinematic.apply_director"
		};

		private static readonly Dictionary<string, Func<JObject, object>> DOMAIN_DELEGATES = BuildDomainDelegates();
		private static readonly UnityAgentMcpRuntime _instance = new UnityAgentMcpRuntime();

		private readonly Dictionary<string, UnityAgentMcpCompiledGraph> _graphs =
			new Dictionary<string, UnityAgentMcpCompiledGraph>(StringComparer.Ordinal);
		private readonly Dictionary<string, UnityAgentMcpExecutionRecord> _executions =
			new Dictionary<string, UnityAgentMcpExecutionRecord>(StringComparer.Ordinal);
		private readonly List<JObject> _history = new List<JObject>();
		private UnityAgentMcpCatalogData _catalog;
		private string _catalogError;

		internal static Func<DateTime> UtcNowOverrideForTests { get; set; }

		public static UnityAgentMcpRuntime Instance => _instance;

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
			LoadCatalog();
			LoadHistory();
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
				["domains"] = JArray.FromObject(_catalog.domains ?? Array.Empty<UnityAgentMcpDomainData>()),
				["directUnityMutation"] = false,
				["defaultExecutionTimeoutSeconds"] = DEFAULT_EXECUTION_TIMEOUT_SECONDS,
				["maxExecutionTimeoutSeconds"] = MAX_EXECUTION_TIMEOUT_SECONDS,
				["cooperativeExecution"] = true,
				["integrationCandidateExecutionEnabled"] = true
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
			if (expectedRevision != Session.Revision)
			{
				return Error("AGENT-REVISION-CHANGED", "Graph作成前にEditor Revisionが変更されました。");
			}
			if (!TryValidateSteps(steps, out List<UnityAgentMcpStepInput> normalized, out JObject error))
			{
				return error;
			}

			string graphId = $"agent-graph-{Guid.NewGuid():N}";
			UnityAgentMcpCompiledGraph graph = new UnityAgentMcpCompiledGraph
			{
				graphId = graphId,
				expectedRevision = expectedRevision,
				createdAtUtc = UtcNow,
				steps = normalized,
				requiredApprovalGroups = new HashSet<string>(
					normalized.Where(RequiresApproval)
						.Select(value => value.toolGroup),
					StringComparer.Ordinal)
			};
			_graphs[graphId] = graph;

			return Success(new JObject
			{
				["graphId"] = graphId,
				["expectedRevision"] = expectedRevision,
				["stepCount"] = normalized.Count,
				["requiredApprovalGroups"] = new JArray(graph.requiredApprovalGroups),
				["defaultExecutionTimeoutSeconds"] = DEFAULT_EXECUTION_TIMEOUT_SECONDS
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
					mutation = RequiresApproval(value)
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
			if (!string.Equals(confirmation, "APPROVE_AGENT_EXECUTION", StringComparison.Ordinal))
			{
				return Error("AGENT-APPROVAL-CONFIRMATION-INVALID", "明示確認文字列が一致しません。");
			}

			HashSet<string> approved = new HashSet<string>(approvedGroups ?? Array.Empty<string>(), StringComparer.Ordinal);
			if (!graph.requiredApprovalGroups.IsSubsetOf(approved))
			{
				return Error("AGENT-APPROVAL-INCOMPLETE", "必要なTool Groupがすべて承認されていません。");
			}

			string approvalToken = Guid.NewGuid().ToString("N");
			graph.approved = true;
			graph.approvalTokenHash = HashToken(approvalToken);
			graph.approvalExpiresAtUtc = UtcNow.AddMinutes(APPROVAL_TTL_MINUTES);
			return Success(new JObject
			{
				["graphId"] = graph.graphId,
				["approvalToken"] = approvalToken,
				["expiresAtUtc"] = graph.approvalExpiresAtUtc.ToString("O")
			});
		}

		public JObject StartExecution(string graphId, long currentRevision, string approvalToken, int timeoutSeconds = DEFAULT_EXECUTION_TIMEOUT_SECONDS)
		{
			if (!TryGetGraph(graphId, out UnityAgentMcpCompiledGraph graph, out JObject error))
			{
				return error;
			}
			if (graph.expectedRevision != currentRevision || currentRevision != Session.Revision)
			{
				return Error("AGENT-REVISION-CHANGED", "Preview後にEditor Revisionが変更されました。");
			}
			if (timeoutSeconds < 1 || timeoutSeconds > MAX_EXECUTION_TIMEOUT_SECONDS)
			{
				return Error("AGENT-TIMEOUT-INVALID", $"timeoutSecondsは1～{MAX_EXECUTION_TIMEOUT_SECONDS}で指定してください。");
			}
			if (graph.requiredApprovalGroups.Count > 0)
			{
				if (!graph.approved || UtcNow > graph.approvalExpiresAtUtc)
				{
					return Error("AGENT-APPROVAL-MISSING-OR-EXPIRED", "承認が存在しないか期限切れです。");
				}
				if (string.IsNullOrWhiteSpace(approvalToken) ||
					!string.Equals(HashToken(approvalToken), graph.approvalTokenHash, StringComparison.Ordinal))
				{
					return Error("AGENT-APPROVAL-TOKEN-MISMATCH", "Approval Tokenが一致しません。");
				}
			}

			DateTime startedAtUtc = UtcNow;
			UnityAgentMcpExecutionRecord execution = new UnityAgentMcpExecutionRecord
			{
				executionId = $"agent-exec-{Guid.NewGuid():N}",
				graphId = graph.graphId,
				status = E_AGENT_EXECUTION_STATUS.RUNNING,
				startedAtUtc = startedAtUtc,
				deadlineUtc = startedAtUtc.AddSeconds(timeoutSeconds),
				timeoutSeconds = timeoutSeconds,
				expectedRevision = graph.expectedRevision,
				orderedSteps = TopologicalOrder(graph.steps).ToList(),
				message = "Execution accepted and queued."
			};
			_executions[execution.executionId] = execution;
			EditorApplication.QueuePlayerLoopUpdate();
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
			CompleteExecution(
				execution,
				E_AGENT_EXECUTION_STATUS.CANCELLED,
				null,
				"Cancellation was accepted at a safe Agent step boundary.");
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
					ErrorEntry("AGENT-EXECUTION-INTERRUPTED", true),
					ErrorEntry("AGENT-EXECUTION-TIMEOUT", true),
					ErrorEntry("AGENT-CLIENT-DISCONNECTED", true),
					ErrorEntry("AGENT-TIMEOUT-INVALID", false)
				}
			});
		}

		internal void ProcessPendingExecutionsForTests()
		{
			Tick();
		}

		internal void ResetExecutionsForTests()
		{
			foreach (UnityAgentMcpExecutionRecord execution in _executions.Values
				.Where(value => value.status == E_AGENT_EXECUTION_STATUS.RUNNING)
				.ToArray())
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.CANCELLED, null, "Test reset.");
			}
			_graphs.Clear();
			_executions.Clear();
			UtcNowOverrideForTests = null;
		}

		private void Tick()
		{
			foreach (UnityAgentMcpExecutionRecord execution in _executions.Values
				.Where(value => value.status == E_AGENT_EXECUTION_STATUS.RUNNING)
				.ToArray())
			{
				AdvanceExecution(execution);
			}
		}

		private void AdvanceExecution(UnityAgentMcpExecutionRecord execution)
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
			if (UtcNow > execution.deadlineUtc)
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
			long revisionBefore = Session.Revision;
			JObject stepResult = DelegateStep(step);
			stepResult["stepId"] = step.stepId;
			execution.stepResults.Add(stepResult);

			if (UtcNow > execution.deadlineUtc)
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.INTERRUPTED, "AGENT-EXECUTION-TIMEOUT", "A delegated step exceeded the cooperative timeout.");
				return;
			}
			if (!(stepResult.Value<bool?>("success") ?? false))
			{
				CompleteExecution(
					execution,
					execution.stepResults.Count > 1 ? E_AGENT_EXECUTION_STATUS.PARTIAL : E_AGENT_EXECUTION_STATUS.FAILED,
					stepResult.Value<string>("errorCode") ?? "AGENT-DELEGATE-FAILED",
					stepResult.Value<string>("message") ?? "Delegated tool failed.");
				return;
			}

			long revisionAfter = Session.Revision;
			if (RequiresApproval(step))
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
				if (!IsExecutableDomainStatus(domain.status))
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
			if (!DOMAIN_DELEGATES.TryGetValue(step.toolName, out Func<JObject, object> handler))
			{
				return Error("AGENT-DELEGATE-NOT-REGISTERED", $"Agent delegate対象外です: {step.toolName}");
			}

			try
			{
				object result = handler(step.parameters ?? new JObject());
				JToken delegated = result == null ? JValue.CreateNull() : JToken.FromObject(result);
				bool delegatedSuccess = IsDelegatedSuccess(delegated);
				if (!delegatedSuccess)
				{
					JToken errorToken = delegated.Type == JTokenType.Object ? delegated["error"] : null;
					string delegatedErrorCode = delegated.Type == JTokenType.Object
						? delegated.Value<string>("errorCode")
						: null;
					string delegatedMessage = delegated.Type == JTokenType.Object
						? delegated.Value<string>("message")
						: null;

					if (errorToken is JObject errorObject)
					{
						delegatedErrorCode = delegatedErrorCode ?? errorObject.Value<string>("code");
						delegatedMessage = delegatedMessage ?? errorObject.Value<string>("message");
					}
					else if (errorToken?.Type == JTokenType.String)
					{
						delegatedMessage = delegatedMessage ?? errorToken.Value<string>();
					}

					return new JObject
					{
						["success"] = false,
						["errorCode"] = delegatedErrorCode ?? "AGENT-DELEGATE-FAILED",
						["message"] = delegatedMessage ?? delegated.Value<string>("summary") ?? "Delegated tool reported failure.",
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
			catch (TargetInvocationException exception)
			{
				return Error("AGENT-DELEGATE-FAILED", exception.InnerException?.Message ?? exception.Message);
			}
			catch (Exception exception)
			{
				return Error("AGENT-DELEGATE-FAILED", exception.Message);
			}
		}

		private static Dictionary<string, Func<JObject, object>> BuildDomainDelegates()
		{
			Dictionary<string, Func<JObject, object>> handlers =
				new Dictionary<string, Func<JObject, object>>(StringComparer.Ordinal);
			System.Reflection.Assembly assembly = typeof(InspectProjectTool).Assembly;
			foreach (Type type in assembly.GetTypes())
			{
				CustomAttributeData attribute = type.GetCustomAttributesData().FirstOrDefault(value =>
					string.Equals(
						value.AttributeType.FullName,
						"MCPForUnity.Editor.Tools.McpForUnityToolAttribute",
						StringComparison.Ordinal));
				if (attribute == null || attribute.ConstructorArguments.Count == 0)
				{
					continue;
				}
				string toolName = attribute.ConstructorArguments[0].Value as string;
				if (string.IsNullOrWhiteSpace(toolName))
				{
					continue;
				}
				MethodInfo handleCommand = type.GetMethod(
					"HandleCommand",
					BindingFlags.Public | BindingFlags.Static,
					null,
					new[] {typeof(JObject)},
					null);
				if (handleCommand == null)
				{
					continue;
				}
				if (handlers.ContainsKey(toolName))
				{
					throw new InvalidOperationException($"Duplicate MCP Tool delegate: {toolName}");
				}
				MethodInfo method = handleCommand;
				handlers.Add(toolName, value => method.Invoke(null, new object[] {value}));
			}
			return handlers;
		}

		private static bool IsDelegatedSuccess(JToken delegated)
		{
			if (delegated == null || delegated.Type != JTokenType.Object)
			{
				return delegated != null && delegated.Type != JTokenType.Null;
			}

			bool? domainSuccess = delegated.Value<bool?>("success");
			if (domainSuccess.HasValue)
			{
				return domainSuccess.Value;
			}

			bool? graphicsSuccess = delegated.Value<bool?>("IsSuccessful") ??
				delegated.Value<bool?>("isSuccessful");
			if (graphicsSuccess.HasValue)
			{
				return graphicsSuccess.Value;
			}

			string status = delegated.Value<string>("status");
			return string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(status, "PARTIAL", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsExecutableDomainStatus(string status)
		{
			return string.Equals(status, "editor_operational", StringComparison.Ordinal) ||
				string.Equals(status, "integration_candidate", StringComparison.Ordinal);
		}

		private static bool RequiresApproval(UnityAgentMcpStepInput step)
		{
			return step != null &&
				(APPROVAL_GROUPS.Contains(step.toolGroup ?? string.Empty) ||
				 APPROVAL_TOOLS.Contains(step.toolName ?? string.Empty));
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

		private bool TryGetCurrentGraph(string graphId, out UnityAgentMcpCompiledGraph graph, out JObject error)
		{
			if (!TryGetGraph(graphId, out graph, out error))
			{
				return false;
			}
			if (graph.expectedRevision != Session.Revision)
			{
				error = Error("AGENT-REVISION-CHANGED", "Graph作成後にEditor Revisionが変更されました。");
				return false;
			}
			return true;
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

		private void CompleteExecution(UnityAgentMcpExecutionRecord execution, E_AGENT_EXECUTION_STATUS status, string errorCode, string message)
		{
			if (execution == null || execution.status != E_AGENT_EXECUTION_STATUS.RUNNING)
			{
				return;
			}
			execution.status = status;
			execution.errorCode = errorCode;
			execution.message = message;
			execution.completedAtUtc = UtcNow;
			if (!execution.historyPersisted)
			{
				PersistHistory(execution);
				execution.historyPersisted = true;
			}
		}

		private void PersistHistory(UnityAgentMcpExecutionRecord execution)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(HISTORY_PATH));
				JObject payload = ExecutionPayload(execution);
				File.AppendAllText(HISTORY_PATH, payload.ToString(Formatting.None) + Environment.NewLine);
				_history.Add(payload);
			}
			catch (Exception exception)
			{
				execution.status = E_AGENT_EXECUTION_STATUS.PARTIAL;
				execution.errorCode = "AGENT-HISTORY-PERSISTENCE-FAILED";
				execution.message = exception.Message;
				_history.Add(ExecutionPayload(execution));
			}
		}

		private void InterruptRunning(string errorCode, string reason)
		{
			foreach (UnityAgentMcpExecutionRecord execution in _executions.Values
				.Where(value => value.status == E_AGENT_EXECUTION_STATUS.RUNNING)
				.ToArray())
			{
				CompleteExecution(execution, E_AGENT_EXECUTION_STATUS.INTERRUPTED, errorCode, reason);
			}
		}

		private static JObject ExecutionPayload(UnityAgentMcpExecutionRecord execution)
		{
			return new JObject
			{
				["success"] = true,
				["executionSucceeded"] = execution.status == E_AGENT_EXECUTION_STATUS.SUCCEEDED,
				["executionId"] = execution.executionId,
				["graphId"] = execution.graphId,
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

		private static string HashToken(string value)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
				return BitConverter.ToString(hash).Replace("-", string.Empty);
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
