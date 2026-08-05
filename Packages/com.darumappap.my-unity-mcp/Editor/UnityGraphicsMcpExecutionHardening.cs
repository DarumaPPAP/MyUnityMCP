#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public enum E_MCP_EXECUTION_STATE
	{
		RUNNING,
		SUCCEEDED,
		PARTIAL,
		FAILED,
		CANCELLED,
		TIMED_OUT,
		INTERRUPTED
	}

	public sealed class UnityGraphicsMcpStructuredError
	{
		public string code { get; set; }
		public string category { get; set; }
		public string message { get; set; }
		public bool retryable { get; set; }
		public string retryAction { get; set; }
		public string remediation { get; set; }
		public Dictionary<string, object> details { get; set; } =
			new Dictionary<string, object>();
	}

	public sealed class UnityGraphicsMcpExecutionMetadata
	{
		public string executionId { get; set; }
		public string traceId { get; set; }
		public string state { get; set; }
		public string startedUtc { get; set; }
		public string completedUtc { get; set; }
		public double durationMs { get; set; }
		public double progress { get; set; }
		public int timeoutSeconds { get; set; }
		public bool cancellationRequested { get; set; }
		public long managedMemoryDeltaBytes { get; set; }
		public string progressMode { get; set; }
		public string historyPath { get; set; }
		public string tracePath { get; set; }
		public int artifactRetentionDays { get; set; }
	}

	public sealed class UnityGraphicsMcpProgressEvent
	{
		public string timestampUtc { get; set; }
		public double progress { get; set; }
		public string stage { get; set; }
		public string message { get; set; }
	}

	public sealed class UnityGraphicsMcpExecutionRecord
	{
		public string executionId { get; set; }
		public string traceId { get; set; }
		public string tool { get; set; }
		public string requestId { get; set; }
		public string clientId { get; set; }
		public string sessionId { get; set; }
		public long revision { get; set; }
		public string state { get; set; }
		public string status { get; set; }
		public string summary { get; set; }
		public string errorCode { get; set; }
		public string startedUtc { get; set; }
		public string completedUtc { get; set; }
		public double durationMs { get; set; }
		public double progress { get; set; }
		public int timeoutSeconds { get; set; }
		public bool cancellationRequested { get; set; }
		public long managedMemoryStartBytes { get; set; }
		public long managedMemoryEndBytes { get; set; }
		public int loadedSceneCount { get; set; }
		public int rootObjectCount { get; set; }
		public List<UnityGraphicsMcpProgressEvent> progressEvents { get; set; } =
			new List<UnityGraphicsMcpProgressEvent>();
	}

	public sealed class UnityGraphicsMcpToolTraceEntry
	{
		public string timestampUtc { get; set; }
		public string executionId { get; set; }
		public string traceId { get; set; }
		public string tool { get; set; }
		public string requestId { get; set; }
		public string eventName { get; set; }
		public string state { get; set; }
		public double progress { get; set; }
		public string errorCode { get; set; }
		public string message { get; set; }
	}

	public sealed class UnityGraphicsMcpErrorCatalogEntry
	{
		public string code { get; set; }
		public string category { get; set; }
		public bool retryable { get; set; }
		public string retryAction { get; set; }
		public string remediation { get; set; }
	}

	internal sealed class UnityGraphicsMcpExecutionScope
	{
		public string ExecutionId { get; set; }
		public Stopwatch Stopwatch { get; set; }
		public bool Completed { get; set; }
	}

	[InitializeOnLoad]
	internal static class UnityGraphicsMcpExecutionHardening
	{
		private const int DEFAULT_TIMEOUT_SECONDS = 60;
		private const int MAX_TIMEOUT_SECONDS = 3600;
		private const int MAX_HISTORY_COUNT = 1000;
		private const int HISTORY_RETENTION_DAYS = 30;
		private const int ARTIFACT_RETENTION_DAYS = 14;
		private const string DEFAULT_CLIENT_ID = "mcp-client";

		private static readonly object _sync = new object();
		private static readonly Dictionary<string, UnityGraphicsMcpExecutionRecord> _active =
			new Dictionary<string, UnityGraphicsMcpExecutionRecord>(StringComparer.Ordinal);
		private static readonly List<UnityGraphicsMcpExecutionRecord> _history =
			new List<UnityGraphicsMcpExecutionRecord>();
		private static readonly Dictionary<string, UnityGraphicsMcpErrorCatalogEntry> _errorCatalog =
			BuildErrorCatalog();

		internal static Func<DateTime> UtcNowOverrideForTests { get; set; }
		internal static string StorageRootOverrideForTests { get; set; }

		static UnityGraphicsMcpExecutionHardening()
		{
			EnsureStorage();
			LoadHistory();
			RecoverInterruptedExecutions("UNITY_RESTARTED");
			EditorApplication.update += Tick;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			CompilationPipeline.compilationStarted += OnCompilationStarted;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
			EditorApplication.quitting += OnEditorQuitting;
			EditorSceneManager.sceneOpened += OnSceneOpened;
			EditorSceneManager.sceneClosed += OnSceneClosed;
			EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
		}

		public static UnityGraphicsMcpExecutionScope Begin(
			string tool,
			string requestId,
			int? timeoutSeconds = null,
			string clientId = null)
		{
			DateTime now = UtcNow();
			string normalizedRequestId = string.IsNullOrWhiteSpace(requestId)
				? Guid.NewGuid().ToString("N")
				: requestId;
			int timeout = Mathf.Clamp(
				timeoutSeconds ?? DEFAULT_TIMEOUT_SECONDS,
				1,
				MAX_TIMEOUT_SECONDS);
			UnityGraphicsMcpExecutionRecord record = new UnityGraphicsMcpExecutionRecord
			{
				executionId = UnityGraphicsMcpSession.SessionId +
					":execution:" + Guid.NewGuid().ToString("N"),
				traceId = Guid.NewGuid().ToString("N"),
				tool = string.IsNullOrWhiteSpace(tool) ? "unknown" : tool,
				requestId = normalizedRequestId,
				clientId = string.IsNullOrWhiteSpace(clientId) ? DEFAULT_CLIENT_ID : clientId,
				sessionId = UnityGraphicsMcpSession.SessionId,
				revision = UnityGraphicsMcpSession.Revision,
				state = E_MCP_EXECUTION_STATE.RUNNING.ToString(),
				status = E_MCP_EXECUTION_STATE.RUNNING.ToString(),
				startedUtc = FormatUtc(now),
				progress = 0.0,
				timeoutSeconds = timeout,
				managedMemoryStartBytes = GC.GetTotalMemory(false),
				loadedSceneCount = SceneManager.sceneCount,
				rootObjectCount = CountRootObjects()
			};
			record.progressEvents.Add(new UnityGraphicsMcpProgressEvent
			{
				timestampUtc = FormatUtc(now),
				progress = 0.0,
				stage = "STARTED",
				message = "Tool execution started."
			});

			lock (_sync)
			{
				_active[record.executionId] = record;
				PersistActive();
			}

			AppendTrace(record, "STARTED", null);
			return new UnityGraphicsMcpExecutionScope
			{
				ExecutionId = record.executionId,
				Stopwatch = Stopwatch.StartNew()
			};
		}

		public static UnityGraphicsMcpToolResult Complete(
			UnityGraphicsMcpExecutionScope scope,
			UnityGraphicsMcpToolResult result)
		{
			if (result == null)
			{
				return null;
			}

			if (result.execution != null)
			{
				return result;
			}

			if (scope == null)
			{
				scope = Begin(result.tool, result.requestId);
			}

			UnityGraphicsMcpExecutionRecord record;
			UnityGraphicsMcpExecutionRecord completedRecord = null;
			lock (_sync)
			{
				if (!_active.TryGetValue(scope.ExecutionId, out record))
				{
					completedRecord = _history.LastOrDefault(item =>
						item.executionId == scope.ExecutionId);
					if (completedRecord == null)
					{
						record = CreateDetachedRecord(scope.ExecutionId, result);
					}
				}
			}

			if (completedRecord != null)
			{
				scope.Stopwatch?.Stop();
				result.status = E_MCP_TOOL_STATUS.FAILED.ToString();
				result.summary = completedRecord.summary;
				result.error = BuildStructuredError(completedRecord);
				result.execution = BuildMetadata(completedRecord);
				scope.Completed = true;
				return result;
			}

			if (!string.IsNullOrWhiteSpace(result.tool))
			{
				record.tool = result.tool;
			}
			if (!string.IsNullOrWhiteSpace(result.requestId))
			{
				record.requestId = result.requestId;
			}

			scope.Stopwatch?.Stop();
			record.durationMs = scope.Stopwatch == null
				? 0.0
				: Math.Round(scope.Stopwatch.Elapsed.TotalMilliseconds, 3);
			record.managedMemoryEndBytes = GC.GetTotalMemory(false);
			record.completedUtc = FormatUtc(UtcNow());
			record.progress = 100.0;
			record.status = result.status;
			record.summary = result.summary;
			record.errorCode = ResolveFailureCode(result);
			record.state = ResolveExecutionState(
				result,
				record.errorCode).ToString();
			record.progressEvents.Add(new UnityGraphicsMcpProgressEvent
			{
				timestampUtc = record.completedUtc,
				progress = 100.0,
				stage = record.state,
				message = result.summary
			});

			result.error = BuildStructuredError(result, record.errorCode);
			result.execution = BuildMetadata(record);
			scope.Completed = true;

			lock (_sync)
			{
				_active.Remove(record.executionId);
				_history.Add(CloneRecord(record));
				TrimHistoryInMemory();
				PersistActive();
				AppendHistory(record);
			}

			AppendTrace(record, "COMPLETED", result.summary);
			AppendStructuredLog(record);
			PruneRetentionIfNeeded();
			return result;
		}

		public static bool ReportProgress(
			string executionId,
			double progress,
			string stage,
			string message)
		{
			UnityGraphicsMcpExecutionRecord record;
			lock (_sync)
			{
				if (string.IsNullOrWhiteSpace(executionId) ||
					!_active.TryGetValue(executionId, out record))
				{
					return false;
				}

				record.progress = Math.Max(record.progress, Math.Min(99.0, Math.Max(0.0, progress)));
				record.progressEvents.Add(new UnityGraphicsMcpProgressEvent
				{
					timestampUtc = FormatUtc(UtcNow()),
					progress = record.progress,
					stage = string.IsNullOrWhiteSpace(stage) ? "PROGRESS" : stage,
					message = message
				});
				PersistActive();
			}

			AppendTrace(record, "PROGRESS", message);
			return true;
		}

		public static bool RequestCancellation(
			string executionId,
			string reasonCode,
			string message)
		{
			UnityGraphicsMcpExecutionRecord record;
			lock (_sync)
			{
				if (string.IsNullOrWhiteSpace(executionId) ||
					!_active.TryGetValue(executionId, out record))
				{
					return false;
				}

				record.cancellationRequested = true;
				record.errorCode = string.IsNullOrWhiteSpace(reasonCode)
					? "EXECUTION_CANCEL_REQUESTED"
					: reasonCode;
				record.progressEvents.Add(new UnityGraphicsMcpProgressEvent
				{
					timestampUtc = FormatUtc(UtcNow()),
					progress = record.progress,
					stage = "CANCELLATION_REQUESTED",
					message = message
				});
				PersistActive();
			}

			AppendTrace(record, "CANCELLATION_REQUESTED", message);
			return true;
		}

		public static bool IsCancellationRequested(string executionId)
		{
			lock (_sync)
			{
				UnityGraphicsMcpExecutionRecord record;
				return !string.IsNullOrWhiteSpace(executionId) &&
					_active.TryGetValue(executionId, out record) &&
					record.cancellationRequested;
			}
		}

		public static void ThrowIfCancellationRequested(string executionId)
		{
			if (IsCancellationRequested(executionId))
			{
				throw new OperationCanceledException(
					"MyUnityMCP execution cancellation was requested.");
			}
		}

		public static bool TryGetExecution(
			string executionId,
			out UnityGraphicsMcpExecutionRecord record)
		{
			record = null;
			if (string.IsNullOrWhiteSpace(executionId))
			{
				return false;
			}

			lock (_sync)
			{
				UnityGraphicsMcpExecutionRecord active;
				if (_active.TryGetValue(executionId, out active))
				{
					record = CloneRecord(active);
					return true;
				}

				UnityGraphicsMcpExecutionRecord history = _history
					.LastOrDefault(item => item.executionId == executionId);
				if (history != null)
				{
					record = CloneRecord(history);
					return true;
				}
			}

			return false;
		}

		public static List<UnityGraphicsMcpExecutionRecord> GetHistory(
			string tool,
			int maxEntries)
		{
			int count = Mathf.Clamp(maxEntries <= 0 ? 50 : maxEntries, 1, 200);
			lock (_sync)
			{
				IEnumerable<UnityGraphicsMcpExecutionRecord> query = _history;
				if (!string.IsNullOrWhiteSpace(tool))
				{
					query = query.Where(item => string.Equals(
						item.tool,
						tool,
						StringComparison.Ordinal));
				}

				return query
					.OrderByDescending(item => item.startedUtc)
					.Take(count)
					.Select(CloneRecord)
					.ToList();
			}
		}

		public static List<UnityGraphicsMcpErrorCatalogEntry> GetErrorCatalog()
		{
			return _errorCatalog.Values
				.OrderBy(item => item.code, StringComparer.Ordinal)
				.Select(CloneErrorCatalogEntry)
				.ToList();
		}

		public static void NotifyClientDisconnected(string clientId)
		{
			string targetClient = string.IsNullOrWhiteSpace(clientId)
				? DEFAULT_CLIENT_ID
				: clientId;
			InterruptMatching(
				item => string.Equals(item.clientId, targetClient, StringComparison.Ordinal),
				"MCP_CLIENT_DISCONNECTED",
				"MCP client disconnected while the operation was running.");
		}

		public static Dictionary<string, object> BuildSupportMatrix()
		{
			return new Dictionary<string, object>
			{
				{ "contractVersion", "1.0" },
				{ "packageVersion", "1.0.0" },
				{ "editorOnly", true },
				{ "minimumUnityVersion", "6000.0" },
				{ "verifiedUnityVersion", "6000.0.75f1" },
				{ "verifiedHost", "GitHub Actions Ubuntu BatchMode NoGraphics" },
				{ "pipelines", new List<Dictionary<string, object>>
					{
						Capability("Inspection / Planning", "Built-in, URP, HDRP", "SUPPORTED"),
						Capability("Light / Camera / Reflection Probe Mutation", "Built-in, URP, HDRP", "SUPPORTED"),
						Capability("Volume Mutation", "URP, HDRP", "SUPPORTED_WHEN_VOLUME_API_RESOLVES"),
						Capability("Dependency Bake", "Capability-dependent", "SUPPORTED_BY_EXPLICIT_BACKEND"),
						Capability("Capture Evidence", "Built-in, URP, HDRP", "SUPPORTED_WITH_GRAPHICS_DEVICE"),
						Capability("APV Bake", "URP, HDRP", "SUPPORTED_WHEN_APV_BACKEND_RESOLVES"),
						Capability("Visual Acceptance / Refine", "Pipeline-independent", "SUPPORTED")
					} },
				{ "execution", new Dictionary<string, object>
					{
						{ "timeout", "COOPERATIVE" },
						{ "cancellation", "COOPERATIVE; native backend cancellation when available" },
						{ "progress", "POLL graphics.get_execution_status and JSONL trace" },
						{ "domainReloadRecovery", true },
						{ "unityRestartRecovery", true },
						{ "mcpDisconnect", "Adapter must invoke NotifyClientDisconnected" }
					} },
				{ "retention", new Dictionary<string, object>
					{
						{ "executionHistoryDays", HISTORY_RETENTION_DAYS },
						{ "ownedArtifactDays", ARTIFACT_RETENTION_DAYS },
						{ "ciEvidenceDays", 90 }
					} },
				{ "notVerified", new[]
					{
						"Player runtime execution",
						"Target device execution",
						"Every Unity 6000.x patch",
						"External MCP transport disconnect callback on every client"
					} }
			};
		}

		public static Dictionary<string, object> BuildPerformanceSummary(
			IEnumerable<UnityGraphicsMcpExecutionRecord> records)
		{
			List<double> durations = records == null
				? new List<double>()
				: records.Select(item => item.durationMs).OrderBy(value => value).ToList();
			if (durations.Count == 0)
			{
				return new Dictionary<string, object>
				{
					{ "sampleCount", 0 },
					{ "p50DurationMs", 0.0 },
					{ "p95DurationMs", 0.0 },
					{ "maxDurationMs", 0.0 }
				};
			}

			return new Dictionary<string, object>
			{
				{ "sampleCount", durations.Count },
				{ "p50DurationMs", Percentile(durations, 0.50) },
				{ "p95DurationMs", Percentile(durations, 0.95) },
				{ "maxDurationMs", durations[durations.Count - 1] }
			};
		}

		public static UnityGraphicsMcpToolResult GetExecutionStatus(
			string requestId,
			string executionId)
		{
			return UnityGraphicsMcpInspection.ExecuteHardeningReadOnly(
				"graphics.get_execution_status",
				requestId,
				delegate
				{
					UnityGraphicsMcpExecutionRecord record;
					if (!TryGetExecution(executionId, out record))
					{
						return UnityGraphicsMcpInspection.CreateHardeningResult(
							"graphics.get_execution_status",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"指定Execution IDはActiveまたは保持履歴に存在しません。",
							new Dictionary<string, object> { { "executionId", executionId } });
					}

					return UnityGraphicsMcpInspection.CreateHardeningResult(
						"graphics.get_execution_status",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Execution状態を取得しました。",
						new Dictionary<string, object>
						{
							{ "execution", record },
							{ "pollAgain", record.state == E_MCP_EXECUTION_STATE.RUNNING.ToString() }
						});
				});
		}

		public static UnityGraphicsMcpToolResult CancelExecution(
			string requestId,
			string executionId,
			string reason)
		{
			return UnityGraphicsMcpInspection.ExecuteHardeningReadOnly(
				"graphics.cancel_execution",
				requestId,
				delegate
				{
					bool accepted = RequestCancellation(
						executionId,
						"EXECUTION_CANCEL_REQUESTED",
						string.IsNullOrWhiteSpace(reason)
							? "Cancellation requested by MCP client."
							: reason);
					return UnityGraphicsMcpInspection.CreateHardeningResult(
						"graphics.cancel_execution",
						requestId,
						accepted ? E_MCP_TOOL_STATUS.SUCCESS : E_MCP_TOOL_STATUS.INVALID_REQUEST,
						accepted
							? "Cancellationを要求しました。対象処理は次の安全なCancellation Pointで停止します。"
							: "指定Execution IDはActiveではありません。",
						new Dictionary<string, object>
						{
							{ "executionId", executionId },
							{ "cancellationAccepted", accepted },
							{ "cancellationMode", "COOPERATIVE" }
						});
				});
		}

		public static UnityGraphicsMcpToolResult GetExecutionHistory(
			string requestId,
			string tool,
			int maxEntries)
		{
			return UnityGraphicsMcpInspection.ExecuteHardeningReadOnly(
				"graphics.get_execution_history",
				requestId,
				delegate
				{
					List<UnityGraphicsMcpExecutionRecord> records = GetHistory(tool, maxEntries);
					return UnityGraphicsMcpInspection.CreateHardeningResult(
						"graphics.get_execution_history",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"保持中のExecution履歴とPerformance集計を取得しました。",
						new Dictionary<string, object>
						{
							{ "records", records },
							{ "performance", BuildPerformanceSummary(records) },
							{ "historyRetentionDays", HISTORY_RETENTION_DAYS },
							{ "historyPath", RelativeHistoryPath() },
							{ "tracePath", RelativeTracePath() }
						});
				});
		}

		public static UnityGraphicsMcpToolResult GetErrorCatalog(string requestId)
		{
			return UnityGraphicsMcpInspection.ExecuteHardeningReadOnly(
				"graphics.get_error_catalog",
				requestId,
				delegate
				{
					return UnityGraphicsMcpInspection.CreateHardeningResult(
						"graphics.get_error_catalog",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Hardening Error Code一覧を取得しました。",
						new Dictionary<string, object>
						{
							{ "errors", GetErrorCatalog() },
							{ "defaultRetryEntryPoint", "graphics.inspect_project" }
						});
				});
		}

		public static UnityGraphicsMcpToolResult GetSupportMatrix(string requestId)
		{
			return UnityGraphicsMcpInspection.ExecuteHardeningReadOnly(
				"graphics.get_support_matrix",
				requestId,
				delegate
				{
					return UnityGraphicsMcpInspection.CreateHardeningResult(
						"graphics.get_support_matrix",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"固定Support Matrixを取得しました。",
						BuildSupportMatrix());
				});
		}

		internal static void TickForTests()
		{
			Tick();
		}

		internal static void InterruptAllForTests(string code)
		{
			InterruptMatching(item => true, code, code);
		}

		internal static void RecoverForTests(string code)
		{
			RecoverInterruptedExecutions(code);
		}

		internal static void SimulateProcessLossForTests()
		{
			lock (_sync)
			{
				_active.Clear();
			}
		}

		internal static void PruneRetentionForTests()
		{
			lock (_sync)
			{
				TrimHistoryInMemory();
				RewriteJsonLines(HistoryPath(), _history);
				PruneOwnedArtifacts(UtcNow().AddDays(-ARTIFACT_RETENTION_DAYS));
			}
		}

		internal static string OwnedArtifactRootForTests()
		{
			return OwnedArtifactRoot();
		}

		internal static void ResetForTests(string storageRoot)
		{
			lock (_sync)
			{
				StorageRootOverrideForTests = storageRoot;
				UtcNowOverrideForTests = null;
				_active.Clear();
				_history.Clear();
				if (Directory.Exists(StorageRoot()))
				{
					Directory.Delete(StorageRoot(), true);
				}
				EnsureStorage();
			}
		}

		internal static void RestoreAfterTests()
		{
			lock (_sync)
			{
				_active.Clear();
				_history.Clear();
				StorageRootOverrideForTests = null;
				UtcNowOverrideForTests = null;
				EnsureStorage();
				LoadHistory();
			}
		}

		private static void Tick()
		{
			List<string> timedOutIds = new List<string>();
			DateTime now = UtcNow();
			lock (_sync)
			{
				foreach (UnityGraphicsMcpExecutionRecord record in _active.Values)
				{
					DateTime started;
					if (DateTime.TryParse(
						record.startedUtc,
						CultureInfo.InvariantCulture,
						DateTimeStyles.RoundtripKind,
						out started) &&
						now >= started.AddSeconds(record.timeoutSeconds))
					{
						timedOutIds.Add(record.executionId);
					}
				}
			}

			foreach (string executionId in timedOutIds)
			{
				FinalizeInterrupted(
					executionId,
					E_MCP_EXECUTION_STATE.TIMED_OUT,
					"EXECUTION_TIMED_OUT",
					"Execution exceeded its timeout. Restart from the retry action in the structured error.");
			}
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode ||
				state == PlayModeStateChange.EnteredPlayMode)
			{
				InterruptMatching(
					item => true,
					"PLAY_MODE_TRANSITION",
					"Play Mode transition interrupted the Editor operation.");
			}
		}

		private static void OnCompilationStarted(object context)
		{
			InterruptMatching(
				item => true,
				"COMPILE_STARTED",
				"Script compilation interrupted the Editor operation.");
		}

		private static void OnBeforeAssemblyReload()
		{
			InterruptMatching(
				item => true,
				"DOMAIN_RELOAD",
				"Domain Reload interrupted the Editor operation.");
		}

		private static void OnEditorQuitting()
		{
			InterruptMatching(
				item => true,
				"UNITY_SHUTDOWN",
				"Unity Editor shutdown interrupted the operation.");
		}

		private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
		{
			InterruptMatching(
				item => true,
				"MULTI_SCENE_CONFIGURATION_CHANGED",
				"Loaded Scene configuration changed while the operation was running.");
		}

		private static void OnSceneClosed(Scene scene)
		{
			InterruptMatching(
				item => true,
				"SCENE_CLOSED",
				"A loaded Scene was closed while the operation was running.");
		}

		private static void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
		{
			InterruptMatching(
				item => true,
				"MULTI_SCENE_CONFIGURATION_CHANGED",
				"Active Scene changed while the operation was running.");
		}

		private static void InterruptMatching(
			Func<UnityGraphicsMcpExecutionRecord, bool> predicate,
			string code,
			string message)
		{
			List<string> ids;
			lock (_sync)
			{
				ids = _active.Values
					.Where(predicate)
					.Select(item => item.executionId)
					.ToList();
			}

			foreach (string executionId in ids)
			{
				FinalizeInterrupted(
					executionId,
					E_MCP_EXECUTION_STATE.INTERRUPTED,
					code,
					message);
			}
		}

		private static void FinalizeInterrupted(
			string executionId,
			E_MCP_EXECUTION_STATE state,
			string code,
			string message)
		{
			UnityGraphicsMcpExecutionRecord record;
			lock (_sync)
			{
				if (!_active.TryGetValue(executionId, out record))
				{
					return;
				}

				DateTime completed = UtcNow();
				DateTime started;
				record.completedUtc = FormatUtc(completed);
				record.durationMs = DateTime.TryParse(
					record.startedUtc,
					CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind,
					out started)
					? Math.Max(0.0, (completed - started).TotalMilliseconds)
					: 0.0;
				record.managedMemoryEndBytes = GC.GetTotalMemory(false);
				record.state = state.ToString();
				record.status = E_MCP_TOOL_STATUS.FAILED.ToString();
				record.errorCode = code;
				record.summary = message;
				record.progressEvents.Add(new UnityGraphicsMcpProgressEvent
				{
					timestampUtc = record.completedUtc,
					progress = record.progress,
					stage = state.ToString(),
					message = message
				});
				_active.Remove(executionId);
				_history.Add(CloneRecord(record));
				TrimHistoryInMemory();
				PersistActive();
				AppendHistory(record);
			}

			AppendTrace(record, state.ToString(), message);
			AppendStructuredLog(record);
		}

		private static void RecoverInterruptedExecutions(string code)
		{
			string path = ActivePath();
			if (!File.Exists(path))
			{
				return;
			}

			try
			{
				List<UnityGraphicsMcpExecutionRecord> records =
					JsonConvert.DeserializeObject<List<UnityGraphicsMcpExecutionRecord>>(
						File.ReadAllText(path));
				if (records != null)
				{
					foreach (UnityGraphicsMcpExecutionRecord record in records)
					{
						record.state = E_MCP_EXECUTION_STATE.INTERRUPTED.ToString();
						record.status = E_MCP_TOOL_STATUS.FAILED.ToString();
						record.errorCode = code;
						record.summary = "Previous Unity process ended before the operation completed.";
						record.completedUtc = FormatUtc(UtcNow());
						record.managedMemoryEndBytes = GC.GetTotalMemory(false);
						_history.Add(CloneRecord(record));
						AppendHistory(record);
						AppendTrace(record, "RECOVERED_AS_INTERRUPTED", record.summary);
						AppendStructuredLog(record);
					}
				}
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogWarning(
					"MyUnityMCP execution recovery failed: " + exception.Message);
			}
			finally
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
				TrimHistoryInMemory();
			}
		}

		private static UnityGraphicsMcpExecutionMetadata BuildMetadata(
			UnityGraphicsMcpExecutionRecord record)
		{
			return new UnityGraphicsMcpExecutionMetadata
			{
				executionId = record.executionId,
				traceId = record.traceId,
				state = record.state,
				startedUtc = record.startedUtc,
				completedUtc = record.completedUtc,
				durationMs = record.durationMs,
				progress = record.progress,
				timeoutSeconds = record.timeoutSeconds,
				cancellationRequested = record.cancellationRequested,
				managedMemoryDeltaBytes =
					record.managedMemoryEndBytes - record.managedMemoryStartBytes,
				progressMode = "POLL_AND_JSONL_TRACE",
				historyPath = RelativeHistoryPath(),
				tracePath = RelativeTracePath(),
				artifactRetentionDays = ARTIFACT_RETENTION_DAYS
			};
		}

		private static UnityGraphicsMcpStructuredError BuildStructuredError(
			UnityGraphicsMcpToolResult result,
			string code)
		{
			if (result == null || result.IsSuccessful)
			{
				return null;
			}

			UnityGraphicsMcpErrorCatalogEntry catalog;
			if (!_errorCatalog.TryGetValue(code ?? string.Empty, out catalog))
			{
				catalog = Catalog(
					code ?? "MCP_FAILED",
					"INTERNAL",
					true,
					"Inspect current state and restart from the last successful checkpoint.",
					"Read the Tool Call Trace and do not reuse stale Plan, Token, Capture, or Job IDs.");
			}

			UnityGraphicsMcpStructuredError error = new UnityGraphicsMcpStructuredError
			{
				code = catalog.code,
				category = catalog.category,
				message = result.summary,
				retryable = catalog.retryable,
				retryAction = catalog.retryAction,
				remediation = catalog.remediation
			};
			error.details["status"] = result.status;
			error.details["sessionId"] = result.sessionId;
			error.details["revision"] = result.revision;
			return error;
		}

		private static UnityGraphicsMcpStructuredError BuildStructuredError(
			UnityGraphicsMcpExecutionRecord record)
		{
			UnityGraphicsMcpErrorCatalogEntry catalog;
			if (!_errorCatalog.TryGetValue(record.errorCode ?? string.Empty, out catalog))
			{
				catalog = Catalog(
					record.errorCode ?? "MCP_FAILED",
					"INTERNAL",
					true,
					"Inspect current state and restart from the last successful checkpoint.",
					"Read the Tool Call Trace and do not reuse stale IDs.");
			}

			UnityGraphicsMcpStructuredError error = new UnityGraphicsMcpStructuredError
			{
				code = catalog.code,
				category = catalog.category,
				message = record.summary,
				retryable = catalog.retryable,
				retryAction = catalog.retryAction,
				remediation = catalog.remediation
			};
			error.details["executionId"] = record.executionId;
			error.details["traceId"] = record.traceId;
			error.details["state"] = record.state;
			return error;
		}

		private static string ResolveFailureCode(UnityGraphicsMcpToolResult result)
		{
			if (result == null || result.IsSuccessful)
			{
				return null;
			}

			Dictionary<string, object> data = result.data as Dictionary<string, object>;
			object failureCode;
			if (data != null &&
				data.TryGetValue("failureCode", out failureCode) &&
				failureCode != null &&
				!string.IsNullOrWhiteSpace(failureCode.ToString()))
			{
				return failureCode.ToString();
			}

			UnityGraphicsMcpIssue issue = result.issues == null
				? null
				: result.issues.FirstOrDefault(item => item != null && !string.IsNullOrWhiteSpace(item.code));
			if (issue != null)
			{
				return issue.code;
			}

			string summary = result.summary ?? string.Empty;
			if (summary.IndexOf("承認Token", StringComparison.Ordinal) >= 0 &&
				(summary.IndexOf("一致しません", StringComparison.Ordinal) >= 0 ||
				 summary.IndexOf("不足", StringComparison.Ordinal) >= 0))
			{
				return "APPROVAL_TOKEN_MISMATCH";
			}
			if (summary.IndexOf("有効期限切れ", StringComparison.Ordinal) >= 0)
			{
				return "PLAN_EXPIRED";
			}
			if (summary.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 &&
				(summary.IndexOf("存在しません", StringComparison.Ordinal) >= 0 ||
				 summary.IndexOf("解決", StringComparison.Ordinal) >= 0))
			{
				return "CAMERA_NOT_FOUND";
			}
			if (result.status == E_MCP_TOOL_STATUS.UNSUPPORTED.ToString() &&
				summary.IndexOf("Pipeline", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "UNSUPPORTED_PIPELINE";
			}
			if (summary.IndexOf("Artifact", StringComparison.OrdinalIgnoreCase) >= 0 &&
				(summary.IndexOf("不足", StringComparison.Ordinal) >= 0 ||
				 summary.IndexOf("存在しません", StringComparison.Ordinal) >= 0))
			{
				return "OUTPUT_ASSET_MISSING";
			}

			return "MCP_" + (result.status ?? E_MCP_TOOL_STATUS.FAILED.ToString());
		}

		private static E_MCP_EXECUTION_STATE ResolveExecutionState(
			UnityGraphicsMcpToolResult result,
			string failureCode)
		{
			if (result == null)
			{
				return E_MCP_EXECUTION_STATE.FAILED;
			}
			if (result.status == E_MCP_TOOL_STATUS.SUCCESS.ToString())
			{
				return E_MCP_EXECUTION_STATE.SUCCEEDED;
			}
			if (result.status == E_MCP_TOOL_STATUS.PARTIAL.ToString())
			{
				return E_MCP_EXECUTION_STATE.PARTIAL;
			}
			if (string.Equals(
				failureCode,
				"EXECUTION_CANCEL_REQUESTED",
				StringComparison.Ordinal))
			{
				return E_MCP_EXECUTION_STATE.CANCELLED;
			}
			return E_MCP_EXECUTION_STATE.FAILED;
		}

		private static Dictionary<string, UnityGraphicsMcpErrorCatalogEntry> BuildErrorCatalog()
		{
			UnityGraphicsMcpErrorCatalogEntry[] entries =
			{
				Catalog("MCP_INVALID_REQUEST", "REQUEST", true, "Correct parameters and retry the same Tool.", "Do not change the Scene unless the correction requires a new Snapshot."),
				Catalog("MCP_UNSUPPORTED", "COMPATIBILITY", false, "Select a supported capability from graphics.get_support_matrix.", "Do not emulate unsupported Pipeline or backend output."),
				Catalog("MCP_UNVERIFIED", "COMPATIBILITY", true, "Run the operation in a verified Editor and graphics device environment.", "Treat unverified as unknown, not success."),
				Catalog("MCP_BACKEND_NOT_IMPLEMENTED", "BACKEND", false, "Install or implement the required explicit backend.", "Do not create placeholder output assets."),
				Catalog("MCP_READ_ONLY_CONTRACT_VIOLATION", "SAFETY", true, "Restore the Scene and repeat Inspect from a clean checkpoint.", "Investigate the Tool that dirtied Scene, Asset, or Undo state."),
				Catalog("MCP_SESSION_EXPIRED", "CONCURRENCY", true, "Restart from Inspect and create new Snapshot, Plan, Token, and Job IDs.", "IDs are valid only in the Editor Session that created them."),
				Catalog("MCP_STALE_SNAPSHOT", "CONCURRENCY", true, "Inspect again, create a new Snapshot, and prepare a new Plan.", "Never reuse the previous approval token."),
				Catalog("MCP_STALE_DURING_SCAN", "CONCURRENCY", true, "Wait for Editor changes to settle and rerun Inspect.", "Do not use partial scan output."),
				Catalog("MCP_EDITOR_RELOADING", "EDITOR_LIFECYCLE", true, "Wait for Compile and Domain Reload to finish, then restart from Inspect.", "Discard IDs created before reload."),
				Catalog("MCP_FAILED", "INTERNAL", true, "Inspect current state and restart from the last successful checkpoint.", "Read the structured log and Tool Call Trace before retrying."),
				Catalog("EXECUTION_CANCEL_REQUESTED", "CANCELLATION", true, "Wait for cancellation completion, then restart from the last successful checkpoint.", "Do not force-close Unity while an atomic mutation rollback is in progress."),
				Catalog("EXECUTION_TIMED_OUT", "TIMEOUT", true, "Inspect current state and retry with a justified timeout.", "Do not blindly increase timeout without checking the trace stage."),
				Catalog("DOMAIN_RELOAD", "EDITOR_LIFECYCLE", true, "Restart from Inspect after reload.", "Transient IDs and approval tokens are invalid."),
				Catalog("COMPILE_STARTED", "EDITOR_LIFECYCLE", true, "Wait for compilation to finish and restart from Inspect.", "Do not continue an old workflow after scripts changed."),
				Catalog("PLAY_MODE_TRANSITION", "EDITOR_LIFECYCLE", true, "Return to stable Edit Mode and restart from Inspect.", "Editor mutation Tools are Edit Mode only."),
				Catalog("SCENE_CLOSED", "CONCURRENCY", true, "Load the required Scene set and restart from Inspect.", "Verify no output Asset was partially committed."),
				Catalog("MULTI_SCENE_CONFIGURATION_CHANGED", "CONCURRENCY", true, "Inspect the new loaded Scene set and prepare a new Plan.", "Previous Scene baselines are stale."),
				Catalog("MCP_CLIENT_DISCONNECTED", "TRANSPORT", true, "Reconnect the MCP client and query execution history before retrying.", "Do not assume the previous call failed before checking history."),
				Catalog("UNITY_SHUTDOWN", "EDITOR_LIFECYCLE", true, "Restart Unity and query execution history.", "Recovered active operations are marked interrupted."),
				Catalog("UNITY_RESTARTED", "EDITOR_LIFECYCLE", true, "Query execution history, inspect current state, and restart from the last successful checkpoint.", "Do not reuse pre-restart transient IDs."),
				Catalog("APV_BAKE_NO_OUTPUT_DIFF", "OUTPUT", true, "Validate Baking Set, Lighting Scenario, and output roots, then prepare a new Bake Plan.", "A completed backend call without output diff is failure."),
				Catalog("CAMERA_NOT_FOUND", "OUTPUT", true, "Inspect Scene and select an existing Camera GlobalObjectId.", "Prepare a new Capture after any Camera change."),
				Catalog("OUTPUT_ASSET_MISSING", "OUTPUT", true, "Delete incomplete temporary output and rerun from Prepare Bake or Capture.", "Never accept a manifest with missing artifacts."),
				Catalog("APPROVAL_TOKEN_MISMATCH", "AUTHORIZATION", true, "Prepare a new Plan and use only its matching one-time token.", "Never reuse or guess approval tokens."),
				Catalog("PLAN_EXPIRED", "AUTHORIZATION", true, "Prepare a new Plan from a current Snapshot.", "Expired Plans and tokens are not renewable."),
				Catalog("UNSUPPORTED_PIPELINE", "COMPATIBILITY", false, "Select a supported capability or Pipeline from the Support Matrix.", "Do not run APV Bake on Built-in Pipeline.")
			};
			return entries.ToDictionary(item => item.code, item => item, StringComparer.Ordinal);
		}

		private static UnityGraphicsMcpErrorCatalogEntry Catalog(
			string code,
			string category,
			bool retryable,
			string retryAction,
			string remediation)
		{
			return new UnityGraphicsMcpErrorCatalogEntry
			{
				code = code,
				category = category,
				retryable = retryable,
				retryAction = retryAction,
				remediation = remediation
			};
		}

		private static UnityGraphicsMcpErrorCatalogEntry CloneErrorCatalogEntry(
			UnityGraphicsMcpErrorCatalogEntry item)
		{
			return Catalog(
				item.code,
				item.category,
				item.retryable,
				item.retryAction,
				item.remediation);
		}

		private static UnityGraphicsMcpExecutionRecord CreateDetachedRecord(
			string executionId,
			UnityGraphicsMcpToolResult result)
		{
			return new UnityGraphicsMcpExecutionRecord
			{
				executionId = executionId,
				traceId = Guid.NewGuid().ToString("N"),
				tool = result.tool,
				requestId = result.requestId,
				clientId = DEFAULT_CLIENT_ID,
				sessionId = result.sessionId,
				revision = result.revision,
				state = E_MCP_EXECUTION_STATE.RUNNING.ToString(),
				status = E_MCP_EXECUTION_STATE.RUNNING.ToString(),
				startedUtc = FormatUtc(UtcNow()),
				timeoutSeconds = DEFAULT_TIMEOUT_SECONDS,
				managedMemoryStartBytes = GC.GetTotalMemory(false),
				loadedSceneCount = SceneManager.sceneCount,
				rootObjectCount = CountRootObjects()
			};
		}

		private static UnityGraphicsMcpExecutionRecord CloneRecord(
			UnityGraphicsMcpExecutionRecord record)
		{
			return JsonConvert.DeserializeObject<UnityGraphicsMcpExecutionRecord>(
				JsonConvert.SerializeObject(record));
		}

		private static Dictionary<string, object> Capability(
			string capability,
			string pipeline,
			string support)
		{
			return new Dictionary<string, object>
			{
				{ "capability", capability },
				{ "pipeline", pipeline },
				{ "support", support }
			};
		}

		private static double Percentile(List<double> values, double percentile)
		{
			if (values == null || values.Count == 0)
			{
				return 0.0;
			}
			double position = (values.Count - 1) * percentile;
			int lower = Mathf.FloorToInt((float)position);
			int upper = Mathf.CeilToInt((float)position);
			if (lower == upper)
			{
				return values[lower];
			}
			double weight = position - lower;
			return values[lower] + ((values[upper] - values[lower]) * weight);
		}

		private static int CountRootObjects()
		{
			int count = 0;
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (scene.IsValid() && scene.isLoaded)
				{
					count += scene.rootCount;
				}
			}
			return count;
		}

		private static void EnsureStorage()
		{
			Directory.CreateDirectory(StorageRoot());
			Directory.CreateDirectory(OwnedArtifactRoot());
		}

		private static string StorageRoot()
		{
			if (!string.IsNullOrWhiteSpace(StorageRootOverrideForTests))
			{
				return StorageRootOverrideForTests;
			}
			return Path.Combine(
				Directory.GetParent(Application.dataPath).FullName,
				"Library",
				"MyUnityMCP",
				"Execution");
		}

		private static string HistoryPath()
		{
			return Path.Combine(StorageRoot(), "execution-history.jsonl");
		}

		private static string TracePath()
		{
			return Path.Combine(StorageRoot(), "tool-call-trace.jsonl");
		}

		private static string StructuredLogPath()
		{
			return Path.Combine(StorageRoot(), "structured-log.jsonl");
		}

		private static string ActivePath()
		{
			return Path.Combine(StorageRoot(), "active-executions.json");
		}

		private static string OwnedArtifactRoot()
		{
			return Path.Combine(StorageRoot(), "Artifacts");
		}

		private static string RelativeHistoryPath()
		{
			return "Library/MyUnityMCP/Execution/execution-history.jsonl";
		}

		private static string RelativeTracePath()
		{
			return "Library/MyUnityMCP/Execution/tool-call-trace.jsonl";
		}

		private static void PersistActive()
		{
			EnsureStorage();
			string path = ActivePath();
			if (_active.Count == 0)
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
				return;
			}

			WriteAtomic(
				path,
				JsonConvert.SerializeObject(_active.Values.ToList(), Formatting.Indented));
		}

		private static void AppendHistory(UnityGraphicsMcpExecutionRecord record)
		{
			AppendJsonLine(HistoryPath(), record);
		}

		private static void AppendTrace(
			UnityGraphicsMcpExecutionRecord record,
			string eventName,
			string message)
		{
			if (record == null)
			{
				return;
			}
			AppendJsonLine(
				TracePath(),
				new UnityGraphicsMcpToolTraceEntry
				{
					timestampUtc = FormatUtc(UtcNow()),
					executionId = record.executionId,
					traceId = record.traceId,
					tool = record.tool,
					requestId = record.requestId,
					eventName = eventName,
					state = record.state,
					progress = record.progress,
					errorCode = record.errorCode,
					message = message
				});
		}

		private static void AppendStructuredLog(UnityGraphicsMcpExecutionRecord record)
		{
			AppendJsonLine(
				StructuredLogPath(),
				new Dictionary<string, object>
				{
					{ "timestampUtc", FormatUtc(UtcNow()) },
					{ "level", record.state == E_MCP_EXECUTION_STATE.SUCCEEDED.ToString() ? "INFO" : "ERROR" },
					{ "event", "TOOL_EXECUTION_COMPLETED" },
					{ "executionId", record.executionId },
					{ "traceId", record.traceId },
					{ "tool", record.tool },
					{ "requestId", record.requestId },
					{ "sessionId", record.sessionId },
					{ "revision", record.revision },
					{ "state", record.state },
					{ "status", record.status },
					{ "errorCode", record.errorCode },
					{ "durationMs", record.durationMs },
					{ "managedMemoryDeltaBytes", record.managedMemoryEndBytes - record.managedMemoryStartBytes },
					{ "summary", record.summary }
				});
		}

		private static void AppendJsonLine(string path, object value)
		{
			try
			{
				EnsureStorage();
				File.AppendAllText(
					path,
					JsonConvert.SerializeObject(value, Formatting.None) + Environment.NewLine);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogWarning(
					"MyUnityMCP structured log write failed: " + exception.Message);
			}
		}

		private static void LoadHistory()
		{
			string path = HistoryPath();
			if (!File.Exists(path))
			{
				return;
			}

			try
			{
				foreach (string line in File.ReadLines(path))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}
					UnityGraphicsMcpExecutionRecord record =
						JsonConvert.DeserializeObject<UnityGraphicsMcpExecutionRecord>(line);
					if (record != null)
					{
						_history.Add(record);
					}
				}
				TrimHistoryInMemory();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogWarning(
					"MyUnityMCP execution history load failed: " + exception.Message);
			}
		}

		private static void TrimHistoryInMemory()
		{
			DateTime threshold = UtcNow().AddDays(-HISTORY_RETENTION_DAYS);
			_history.RemoveAll(item =>
			{
				DateTime started;
				return DateTime.TryParse(
					item.startedUtc,
					CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind,
					out started) &&
					started < threshold;
			});
			if (_history.Count > MAX_HISTORY_COUNT)
			{
				_history.RemoveRange(0, _history.Count - MAX_HISTORY_COUNT);
			}
		}

		private static void PruneRetentionIfNeeded()
		{
			if (_history.Count % 25 != 0)
			{
				return;
			}

			lock (_sync)
			{
				TrimHistoryInMemory();
				RewriteJsonLines(HistoryPath(), _history);
				PruneOwnedArtifacts(UtcNow().AddDays(-ARTIFACT_RETENTION_DAYS));
			}
		}

		private static void PruneOwnedArtifacts(DateTime thresholdUtc)
		{
			string root = OwnedArtifactRoot();
			if (!Directory.Exists(root))
			{
				return;
			}

			foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
			{
				if (File.GetLastWriteTimeUtc(file) < thresholdUtc)
				{
					File.Delete(file);
				}
			}
			foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
				.OrderByDescending(item => item.Length))
			{
				if (!Directory.EnumerateFileSystemEntries(directory).Any())
				{
					Directory.Delete(directory);
				}
			}
		}

		private static void RewriteJsonLines<T>(string path, IEnumerable<T> records)
		{
			string content = string.Join(
				Environment.NewLine,
				records.Select(item => JsonConvert.SerializeObject(item, Formatting.None)));
			if (!string.IsNullOrEmpty(content))
			{
				content += Environment.NewLine;
			}
			WriteAtomic(path, content);
		}

		private static void WriteAtomic(string path, string content)
		{
			EnsureStorage();
			string temporary = path + ".tmp";
			File.WriteAllText(temporary, content ?? string.Empty);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			File.Move(temporary, path);
		}

		private static DateTime UtcNow()
		{
			return UtcNowOverrideForTests == null
				? DateTime.UtcNow
				: UtcNowOverrideForTests();
		}

		private static string FormatUtc(DateTime value)
		{
			return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
		}
	}

	public static partial class UnityGraphicsMcpInspection
	{
		internal static UnityGraphicsMcpToolResult ExecuteHardeningReadOnly(
			string toolName,
			string requestId,
			Func<UnityGraphicsMcpToolResult> operation)
		{
			return ExecuteReadOnly(toolName, requestId, operation);
		}

		internal static UnityGraphicsMcpToolResult CreateHardeningResult(
			string toolName,
			string requestId,
			E_MCP_TOOL_STATUS status,
			string summary,
			object data)
		{
			return CreateResult(toolName, requestId, status, summary, data);
		}
	}
}

#endif