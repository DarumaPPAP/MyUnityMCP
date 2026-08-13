#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityDomainMcp;

namespace UnityProfilerMcp
{
	public sealed class UnityProfilerMcpCounterInput
	{
		public string counterId;
		public string category;
		public string name;
	}

	internal sealed class UnityProfilerMcpRecorderEntry
	{
		public string CounterId;
		public string Category;
		public string Name;
		public string Unit;
		public ProfilerRecorder Recorder;
		public List<long> Samples = new List<long>();
	}

	internal sealed class UnityProfilerMcpCaptureSession
	{
		public string CaptureId;
		public string Status;
		public int WarmupFrames;
		public int SampleFrames;
		public int ObservedFrames;
		public DateTime StartedUtc;
		public DateTime CompletedUtc;
		public string ErrorCode;
		public string Message;
		public bool CancelRequested;
		public JObject Environment;
		public List<UnityProfilerMcpRecorderEntry> Recorders = new List<UnityProfilerMcpRecorderEntry>();
	}

	[McpForUnityTool("profiler.inspect_environment", Description = "Profiler Capture環境をRead-onlyで取得します。Editor値をTarget Device性能として表現しません。", AutoRegister = false, Group = "profiler")]
	public static class ProfilerInspectEnvironmentTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityProfilerMcpRuntime.InspectEnvironment());
	}

	[McpForUnityTool("profiler.inspect_counters", Description = "MyUnityMCPが対応するProfilerRecorder Counterの現在利用可否をRead-onlyで検査します。", AutoRegister = false, Group = "profiler")]
	public static class ProfilerInspectCountersTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityProfilerMcpRuntime.InspectCounters());
	}

	[McpForUnityTool("profiler.prepare_capture", Description = "Warmup、Sample数、Counterを検証し、Capture PlanをRead-onlyで作成します。", AutoRegister = false, Group = "profiler")]
	public static class ProfilerPrepareCaptureTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Sample Frame数。1～3600。", Required = true)] public int? sampleFrames { get; set; }
			[ToolParameter("CaptureするCounter。", Required = true)] public UnityProfilerMcpCounterInput[] counters { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? expectedRevision { get; set; }
			[ToolParameter("Warmup Frame数。0～600。", Required = false)] public int? warmupFrames { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityProfilerMcpRuntime.PrepareCapture(value.warmupFrames ?? 30, value.sampleFrames, value.counters, value.expectedRevision));
	}

	[McpForUnityTool("profiler.start_capture", Description = "承認済みProfiler Capture Planを開始します。Project Assetは変更しません。", AutoRegister = false, Group = "profiler")]
	public static class ProfilerStartCaptureTool
	{
		public sealed class Parameters
		{
			[ToolParameter("profiler.prepare_captureが返したPlan ID。", Required = true)] public string planId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? currentRevision { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityProfilerMcpRuntime.StartCapture(value.planId, value.currentRevision));
	}

	[McpForUnityTool("profiler.get_capture_status", Description = "Profiler Captureの進捗とCounter状態を取得します。", AutoRegister = false, Group = "profiler")]
	public static class ProfilerGetCaptureStatusTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Capture ID。", Required = true)] public string captureId { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityProfilerMcpRuntime.GetStatus(value.captureId));
	}

	[McpForUnityTool("profiler.cancel_capture", Description = "Running中のProfiler Captureを協調Cancelします。", AutoRegister = false, Group = "profiler")]
	public static class ProfilerCancelCaptureTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Capture ID。", Required = true)] public string captureId { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityProfilerMcpRuntime.Cancel(value.captureId));
	}

	[McpForUnityTool("profiler.summarize_capture", Description = "完了済みCaptureをMedian、p95、maxへ集計します。", AutoRegister = false, Group = "profiler")]
	public static class ProfilerSummarizeCaptureTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Capture ID。", Required = true)] public string captureId { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityProfilerMcpRuntime.Summarize(value.captureId));
	}

	[McpForUnityTool("profiler.compare_baseline", Description = "同一EnvironmentのCapture SummaryをBaselineと比較します。異なる環境は拒否します。", AutoRegister = false, Group = "profiler")]
	public static class ProfilerCompareBaselineTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Baseline Summary。", Required = true)] public JObject baseline { get; set; }
			[ToolParameter("Candidate Summary。", Required = true)] public JObject candidate { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityProfilerMcpRuntime.CompareBaseline(value.baseline, value.candidate));
	}

	public static class UnityProfilerMcpRuntime
	{
		private const string DOMAIN_ID = "unity_profiler_mcp";
		private static readonly Dictionary<string, UnityProfilerMcpCaptureSession> _sessions = new Dictionary<string, UnityProfilerMcpCaptureSession>(StringComparer.Ordinal);
		private static readonly UnityProfilerMcpCounterInput[] _supportedCounters =
		{
			new UnityProfilerMcpCounterInput {counterId = "main_thread_time", category = "Internal", name = "Main Thread"},
			new UnityProfilerMcpCounterInput {counterId = "system_used_memory", category = "Memory", name = "System Used Memory"},
			new UnityProfilerMcpCounterInput {counterId = "gc_reserved_memory", category = "Memory", name = "GC Reserved Memory"},
			new UnityProfilerMcpCounterInput {counterId = "total_used_memory", category = "Memory", name = "Total Used Memory"},
			new UnityProfilerMcpCounterInput {counterId = "draw_calls_count", category = "Render", name = "Draw Calls Count"},
			new UnityProfilerMcpCounterInput {counterId = "triangles_count", category = "Render", name = "Triangles Count"}
		};

		static UnityProfilerMcpRuntime()
		{
			EditorApplication.update += Update;
			AssemblyReloadEvents.beforeAssemblyReload += () => InterruptAll("DOMAIN_RELOAD");
			CompilationPipeline.compilationStarted += _ => InterruptAll("COMPILATION_STARTED");
			EditorApplication.playModeStateChanged += state =>
			{
				if (state == PlayModeStateChange.ExitingPlayMode)
				{
					InterruptAll("PLAY_MODE_EXIT");
				}
			};
			EditorApplication.quitting += () => InterruptAll("EDITOR_QUITTING");
		}

		public static UnityDomainMcpResult InspectEnvironment()
		{
			return UnityDomainMcpCommon.Result("profiler.inspect_environment", E_DOMAIN_TOOL_STATUS.SUCCESS, "Editor Capture環境を取得しました。", EnvironmentMetadata());
		}

		public static UnityDomainMcpResult InspectCounters()
		{
			JArray counters = new JArray();
			foreach (UnityProfilerMcpCounterInput counter in _supportedCounters)
			{
				using (ProfilerRecorder recorder = ProfilerRecorder.StartNew(Category(counter.category), counter.name, 1))
				{
					counters.Add(new JObject
					{
						["counterId"] = counter.counterId,
						["category"] = counter.category,
						["name"] = counter.name,
						["available"] = recorder.Valid,
						["unit"] = recorder.Valid ? recorder.UnitType.ToString() : null
					});
				}
			}
			return UnityDomainMcpCommon.Result("profiler.inspect_counters", E_DOMAIN_TOOL_STATUS.SUCCESS, "対応Counterの利用可否を検査しました。", new JObject { ["counters"] = counters });
		}

		public static UnityDomainMcpResult PrepareCapture(int warmupFrames, int? sampleFrames, UnityProfilerMcpCounterInput[] counters, long? expectedRevision)
		{
			if (!sampleFrames.HasValue || sampleFrames.Value < 1 || sampleFrames.Value > 3600)
			{
				return UnityDomainMcpCommon.Error("profiler.prepare_capture", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "sampleFramesは1～3600で指定してください。");
			}
			if (warmupFrames < 0 || warmupFrames > 600)
			{
				return UnityDomainMcpCommon.Error("profiler.prepare_capture", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "warmupFramesは0～600で指定してください。");
			}
			if (counters == null || counters.Length == 0 || counters.Length > 16)
			{
				return UnityDomainMcpCommon.Error("profiler.prepare_capture", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "1～16個のCounterを指定してください。");
			}
			foreach (UnityProfilerMcpCounterInput counter in counters)
			{
				if (counter == null || string.IsNullOrWhiteSpace(counter.counterId) || string.IsNullOrWhiteSpace(counter.category) || string.IsNullOrWhiteSpace(counter.name))
				{
					return UnityDomainMcpCommon.Error("profiler.prepare_capture", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Counter定義が不完全です。");
				}
			}
			return UnityDomainMcpCommon.Prepare("profiler.prepare_capture", DOMAIN_ID, "capture", expectedRevision, false, new JObject
			{
				["warmupFrames"] = warmupFrames,
				["sampleFrames"] = sampleFrames.Value,
				["counters"] = JArray.FromObject(counters),
				["environment"] = EnvironmentMetadata(),
				["targetDeviceClaim"] = false
			});
		}

		public static UnityDomainMcpResult StartCapture(string planId, long? currentRevision)
		{
			if (!currentRevision.HasValue)
			{
				return UnityDomainMcpCommon.Error("profiler.start_capture", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "currentRevisionが必要です。");
			}
			if (!UnityDomainMcpPlanStore.TryConsume("profiler.start_capture", DOMAIN_ID, planId, currentRevision.Value, null, out UnityDomainMcpPlan plan, out UnityDomainMcpResult failure))
			{
				return failure;
			}

			UnityProfilerMcpCaptureSession session = new UnityProfilerMcpCaptureSession
			{
				CaptureId = $"profiler-capture-{Guid.NewGuid():N}",
				Status = "RUNNING",
				WarmupFrames = plan.Payload.Value<int>("warmupFrames"),
				SampleFrames = plan.Payload.Value<int>("sampleFrames"),
				StartedUtc = DateTime.UtcNow,
				Environment = (JObject)plan.Payload["environment"]
			};

			foreach (JToken token in plan.Payload["counters"] ?? new JArray())
			{
				UnityProfilerMcpCounterInput counter = token.ToObject<UnityProfilerMcpCounterInput>();
				ProfilerRecorder recorder = ProfilerRecorder.StartNew(Category(counter.category), counter.name, Math.Max(1, session.SampleFrames));
				if (!recorder.Valid)
				{
					recorder.Dispose();
					Dispose(session);
					return UnityDomainMcpCommon.Error("profiler.start_capture", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, $"Counterを利用できません: {counter.category}/{counter.name}");
				}
				session.Recorders.Add(new UnityProfilerMcpRecorderEntry
				{
					CounterId = counter.counterId,
					Category = counter.category,
					Name = counter.name,
					Unit = recorder.UnitType.ToString(),
					Recorder = recorder
				});
			}
			_sessions[session.CaptureId] = session;
			return UnityDomainMcpCommon.Result("profiler.start_capture", E_DOMAIN_TOOL_STATUS.SUCCESS, "Profiler Captureを開始しました。", StatusPayload(session));
		}

		public static UnityDomainMcpResult GetStatus(string captureId)
		{
			return _sessions.TryGetValue(captureId ?? string.Empty, out UnityProfilerMcpCaptureSession session)
				? UnityDomainMcpCommon.Result("profiler.get_capture_status", E_DOMAIN_TOOL_STATUS.SUCCESS, "Capture状態を取得しました。", StatusPayload(session))
				: UnityDomainMcpCommon.Error("profiler.get_capture_status", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "Captureが見つかりません。");
		}

		public static UnityDomainMcpResult Cancel(string captureId)
		{
			if (!_sessions.TryGetValue(captureId ?? string.Empty, out UnityProfilerMcpCaptureSession session))
			{
				return UnityDomainMcpCommon.Error("profiler.cancel_capture", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "Captureが見つかりません。");
			}
			if (session.Status != "RUNNING")
			{
				return UnityDomainMcpCommon.Error("profiler.cancel_capture", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Running中のCaptureだけをCancelできます。");
			}
			session.CancelRequested = true;
			return UnityDomainMcpCommon.Result("profiler.cancel_capture", E_DOMAIN_TOOL_STATUS.SUCCESS, "Cancellationを要求しました。", new JObject { ["captureId"] = captureId, ["cancelRequested"] = true });
		}

		public static UnityDomainMcpResult Summarize(string captureId)
		{
			if (!_sessions.TryGetValue(captureId ?? string.Empty, out UnityProfilerMcpCaptureSession session))
			{
				return UnityDomainMcpCommon.Error("profiler.summarize_capture", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "Captureが見つかりません。");
			}
			if (session.Status != "COMPLETED")
			{
				return UnityDomainMcpCommon.Error("profiler.summarize_capture", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "完了済みCaptureだけを集計できます。");
			}
			JObject summary = BuildSummary(session);
			return UnityDomainMcpCommon.Result("profiler.summarize_capture", E_DOMAIN_TOOL_STATUS.SUCCESS, "Captureを集計しました。", summary);
		}

		public static UnityDomainMcpResult CompareBaseline(JObject baseline, JObject candidate)
		{
			if (baseline == null || candidate == null)
			{
				return UnityDomainMcpCommon.Error("profiler.compare_baseline", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "BaselineとCandidateが必要です。");
			}
			string baselineFingerprint = baseline.SelectToken("environment.fingerprint")?.Value<string>();
			string candidateFingerprint = candidate.SelectToken("environment.fingerprint")?.Value<string>();
			if (string.IsNullOrEmpty(baselineFingerprint) || baselineFingerprint != candidateFingerprint)
			{
				return UnityDomainMcpCommon.Error("profiler.compare_baseline", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "異なるEnvironmentのCaptureは比較できません。");
			}

			JArray comparisons = new JArray();
			foreach (JProperty candidateMetric in ((JObject)candidate["metrics"] ?? new JObject()).Properties())
			{
				JToken baselineMetric = baseline["metrics"]?[candidateMetric.Name];
				if (baselineMetric == null)
				{
					continue;
				}
				double baselineP95 = baselineMetric.Value<double>("p95");
				double candidateP95 = candidateMetric.Value.Value<double>("p95");
				comparisons.Add(new JObject
				{
					["counterId"] = candidateMetric.Name,
					["baselineP95"] = baselineP95,
					["candidateP95"] = candidateP95,
					["delta"] = candidateP95 - baselineP95,
					["deltaPercent"] = baselineP95 == 0.0 ? null : ((candidateP95 - baselineP95) / baselineP95) * 100.0
				});
			}
			return UnityDomainMcpCommon.Result("profiler.compare_baseline", E_DOMAIN_TOOL_STATUS.SUCCESS, "同一EnvironmentのBaseline比較を完了しました。", new JObject
			{
				["environmentFingerprint"] = baselineFingerprint,
				["comparisons"] = comparisons,
				["targetDeviceClaim"] = false
			});
		}

		internal static JObject SummarizeValues(IEnumerable<long> values)
		{
			long[] sorted = (values ?? Enumerable.Empty<long>()).OrderBy(value => value).ToArray();
			if (sorted.Length == 0)
			{
				return new JObject { ["sampleCount"] = 0, ["median"] = 0, ["p95"] = 0, ["max"] = 0 };
			}
			int medianIndex = (sorted.Length - 1) / 2;
			int p95Index = Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95) - 1);
			return new JObject
			{
				["sampleCount"] = sorted.Length,
				["median"] = sorted[medianIndex],
				["p95"] = sorted[p95Index],
				["max"] = sorted[sorted.Length - 1]
			};
		}

		private static void Update()
		{
			foreach (UnityProfilerMcpCaptureSession session in _sessions.Values.Where(value => value.Status == "RUNNING").ToArray())
			{
				if (session.CancelRequested)
				{
					Complete(session, "CANCELLED", "Capture was cancelled.");
					continue;
				}
				session.ObservedFrames++;
				if (session.ObservedFrames <= session.WarmupFrames)
				{
					continue;
				}
				foreach (UnityProfilerMcpRecorderEntry entry in session.Recorders)
				{
					entry.Samples.Add(entry.Recorder.LastValue);
				}
				if (session.Recorders.All(value => value.Samples.Count >= session.SampleFrames))
				{
					Complete(session, "COMPLETED", "Capture completed.");
				}
			}
		}

		private static void Complete(UnityProfilerMcpCaptureSession session, string status, string message)
		{
			session.Status = status;
			session.Message = message;
			session.CompletedUtc = DateTime.UtcNow;
			Dispose(session);
		}

		private static void Dispose(UnityProfilerMcpCaptureSession session)
		{
			foreach (UnityProfilerMcpRecorderEntry entry in session.Recorders)
			{
				entry.Recorder.Dispose();
			}
		}

		private static void InterruptAll(string reason)
		{
			foreach (UnityProfilerMcpCaptureSession session in _sessions.Values.Where(value => value.Status == "RUNNING").ToArray())
			{
				session.ErrorCode = "PROFILER-CAPTURE-INTERRUPTED";
				Complete(session, "INTERRUPTED", reason);
			}
		}

		private static JObject BuildSummary(UnityProfilerMcpCaptureSession session)
		{
			JObject metrics = new JObject();
			foreach (UnityProfilerMcpRecorderEntry entry in session.Recorders)
			{
				JObject metric = SummarizeValues(entry.Samples);
				metric["category"] = entry.Category;
				metric["name"] = entry.Name;
				metric["unit"] = entry.Unit;
				metrics[entry.CounterId] = metric;
			}
			return new JObject
			{
				["captureId"] = session.CaptureId,
				["environment"] = session.Environment,
				["metrics"] = metrics,
				["scope"] = "UNITY_EDITOR_LOCAL",
				["targetDeviceClaim"] = false
			};
		}

		private static JObject StatusPayload(UnityProfilerMcpCaptureSession session)
		{
			return new JObject
			{
				["captureId"] = session.CaptureId,
				["status"] = session.Status,
				["warmupFrames"] = session.WarmupFrames,
				["sampleFrames"] = session.SampleFrames,
				["observedFrames"] = session.ObservedFrames,
				["collectedSamples"] = session.Recorders.Count == 0 ? 0 : session.Recorders.Min(value => value.Samples.Count),
				["errorCode"] = session.ErrorCode,
				["message"] = session.Message,
				["environment"] = session.Environment,
				["targetDeviceClaim"] = false
			};
		}

		private static JObject EnvironmentMetadata()
		{
			string fingerprint = string.Join("|", new[]
			{
				Application.unityVersion,
				SystemInfo.operatingSystem,
				SystemInfo.graphicsDeviceName,
				SystemInfo.graphicsDeviceType.ToString(),
				EditorUserBuildSettings.activeBuildTarget.ToString()
			});
			return new JObject
			{
				["fingerprint"] = fingerprint,
				["unityVersion"] = Application.unityVersion,
				["operatingSystem"] = SystemInfo.operatingSystem,
				["graphicsDeviceName"] = SystemInfo.graphicsDeviceName,
				["graphicsDeviceType"] = SystemInfo.graphicsDeviceType.ToString(),
				["activeBuildTarget"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
				["isEditor"] = true,
				["scope"] = "UNITY_EDITOR_LOCAL",
				["targetDeviceClaim"] = false
			};
		}

		private static ProfilerCategory Category(string value)
		{
			switch ((value ?? string.Empty).Trim().ToUpperInvariant())
			{
				case "MEMORY": return ProfilerCategory.Memory;
				case "RENDER": return ProfilerCategory.Render;
				case "INTERNAL": return ProfilerCategory.Internal;
				default: return new ProfilerCategory(value);
			}
		}
	}
}

#endif
