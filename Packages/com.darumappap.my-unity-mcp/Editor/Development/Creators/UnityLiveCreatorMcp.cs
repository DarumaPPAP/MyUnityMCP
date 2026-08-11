#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityAgentMcp;

namespace UnityLiveCreatorMcp
{
	public sealed class UnityLiveCreatorCueInput
	{
		public string cueId;
		public double atSeconds;
		public string domainId;
		public string toolName;
		public string toolGroup;
		public JObject parameters;
		public bool requiresOperatorApproval;
		public string recoveryCueId;
	}

	[McpForUnityTool("live.compile_show", Description = "Live Cue SheetをDomain Tool Schedule、Operator Gate、Recovery RouteへCompileします。", AutoRegister = false, Group = "creator")]
	public static class LiveCompileShowTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Live Show Goal。", Required = true)] public string goal { get; set; }
			[ToolParameter("Cue Sheet。", Required = true)] public UnityLiveCreatorCueInput[] cues { get; set; }
			[ToolParameter("最大Duration秒。", Required = true)] public double? durationSeconds { get; set; }
			[ToolParameter("無人実行を許可するか。常にfalseのみ。", Required = false)] public bool? unattended { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityLiveCreatorRuntime.Execute<Parameters>(@params, value => UnityLiveCreatorRuntime.CompileShow(value.goal, value.cues, value.durationSeconds, value.unattended ?? false));
	}

	[McpForUnityTool("live.preview_show", Description = "Live Show Schedule、Blocking Condition、Operator GateをRead-onlyで表示します。", AutoRegister = false, Group = "creator")]
	public static class LivePreviewShowTool
	{
		public sealed class Parameters
		{
			[ToolParameter("live.compile_showが返したShow ID。", Required = true)] public string showId { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityLiveCreatorRuntime.Execute<Parameters>(@params, value => UnityLiveCreatorRuntime.Preview(value.showId));
	}

	[McpForUnityTool("live.create_operator_handoff", Description = "Show Scheduleを人間Operator用のCue／Abort／Recovery Handoffへ変換します。", AutoRegister = false, Group = "creator")]
	public static class LiveCreateOperatorHandoffTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Show ID。", Required = true)] public string showId { get; set; }
			[ToolParameter("Operator名。秘密情報は指定しない。", Required = false)] public string operatorLabel { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityLiveCreatorRuntime.Execute<Parameters>(@params, value => UnityLiveCreatorRuntime.CreateOperatorHandoff(value.showId, value.operatorLabel));
	}

	internal static class UnityLiveCreatorRuntime
	{
		private static readonly Dictionary<string, JObject> _shows = new Dictionary<string, JObject>(StringComparer.Ordinal);

		public static object Execute<T>(JObject @params, Func<T, JObject> operation) where T : new()
		{
			try
			{
				T value = @params == null || !@params.HasValues ? new T() : @params.ToObject<T>();
				return operation(value ?? new T());
			}
			catch (Exception exception)
			{
				return Error("LIVE-REQUEST-INVALID", exception.Message);
			}
		}

		public static JObject CompileShow(string goal, UnityLiveCreatorCueInput[] cues, double? durationSeconds, bool unattended)
		{
			if (string.IsNullOrWhiteSpace(goal))
			{
				return Error("LIVE-GOAL-MISSING", "goalが必要です。");
			}
			if (unattended)
			{
				return Error("LIVE-UNATTENDED-FORBIDDEN", "LiveCreatorの無人実行は禁止です。");
			}
			if (!durationSeconds.HasValue || durationSeconds.Value <= 0.0 || double.IsNaN(durationSeconds.Value) || double.IsInfinity(durationSeconds.Value))
			{
				return Error("LIVE-DURATION-INVALID", "正のdurationSecondsが必要です。");
			}
			List<UnityLiveCreatorCueInput> normalized = (cues ?? Array.Empty<UnityLiveCreatorCueInput>()).Where(value => value != null).OrderBy(value => value.atSeconds).ToList();
			if (normalized.Count == 0 || normalized.Count > 512)
			{
				return Error("LIVE-CUE-COUNT-INVALID", "1～512 Cueを指定してください。");
			}
			if (normalized.Any(value => string.IsNullOrWhiteSpace(value.cueId) || value.atSeconds < 0.0 || value.atSeconds > durationSeconds.Value || string.IsNullOrWhiteSpace(value.domainId) || string.IsNullOrWhiteSpace(value.toolName) || string.IsNullOrWhiteSpace(value.toolGroup)))
			{
				return Error("LIVE-CUE-INVALID", "Cue ID、時刻、Domain、Tool、Tool Groupを確認してください。");
			}
			if (normalized.GroupBy(value => value.cueId, StringComparer.Ordinal).Any(value => value.Count() > 1))
			{
				return Error("LIVE-CUE-ID-DUPLICATE", "cueIdが重複しています。");
			}
			HashSet<string> cueIds = new HashSet<string>(normalized.Select(value => value.cueId), StringComparer.Ordinal);
			string missingRecovery = normalized.Select(value => value.recoveryCueId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && !cueIds.Contains(value));
			if (!string.IsNullOrEmpty(missingRecovery))
			{
				return Error("LIVE-RECOVERY-CUE-MISSING", $"Recovery Cueが存在しません: {missingRecovery}");
			}

			JObject capabilities = UnityAgentMcpRuntime.Instance.InspectCapabilities();
			JArray domains = (JArray)capabilities["domains"] ?? new JArray();
			JArray blocking = new JArray();
			JArray compiledCues = new JArray();
			foreach (UnityLiveCreatorCueInput cue in normalized)
			{
				JToken domain = domains.FirstOrDefault(value => string.Equals(value.Value<string>("domainId"), cue.domainId, StringComparison.Ordinal));
				bool operational = string.Equals(domain?.Value<string>("status"), "editor_operational", StringComparison.Ordinal);
				bool declaredTool = domain?["tools"]?.Values<string>().Contains(cue.toolName, StringComparer.Ordinal) ?? false;
				if (!operational)
				{
					blocking.Add($"{cue.cueId}: domain {cue.domainId} is not editor_operational");
				}
				else if (!declaredTool)
				{
					blocking.Add($"{cue.cueId}: tool {cue.toolName} is not declared");
				}
				compiledCues.Add(new JObject
				{
					["cueId"] = cue.cueId,
					["atSeconds"] = cue.atSeconds,
					["domainId"] = cue.domainId,
					["toolName"] = cue.toolName,
					["toolGroup"] = cue.toolGroup,
					["parameters"] = cue.parameters ?? new JObject(),
					["requiresOperatorApproval"] = cue.requiresOperatorApproval,
					["recoveryCueId"] = cue.recoveryCueId,
					["executionReady"] = operational && declaredTool
				});
			}

			string showId = $"live-show-{Guid.NewGuid():N}";
			JObject show = new JObject
			{
				["success"] = true,
				["showId"] = showId,
				["goal"] = goal,
				["durationSeconds"] = durationSeconds.Value,
				["cues"] = compiledCues,
				["blockingConditions"] = blocking,
				["executionReady"] = blocking.Count == 0,
				["unattended"] = false,
				["directUnityMutation"] = false,
				["operatorRequired"] = true,
				["abortPolicy"] = "operator_or_domain_failure_stops_next_side_effect_cue"
			};
			_shows[showId] = show;
			return show;
		}

		public static JObject Preview(string showId)
		{
			return _shows.TryGetValue(showId ?? string.Empty, out JObject show)
				? new JObject { ["success"] = true, ["show"] = show.DeepClone(), ["executionStarted"] = false }
				: Error("LIVE-SHOW-NOT-FOUND", "Showが見つかりません。");
		}

		public static JObject CreateOperatorHandoff(string showId, string operatorLabel)
		{
			if (!_shows.TryGetValue(showId ?? string.Empty, out JObject show))
			{
				return Error("LIVE-SHOW-NOT-FOUND", "Showが見つかりません。");
			}
			return new JObject
			{
				["success"] = true,
				["showId"] = showId,
				["handoffStatus"] = "OPERATOR_REVIEW_REQUIRED",
				["operatorLabel"] = string.IsNullOrWhiteSpace(operatorLabel) ? "operator" : operatorLabel,
				["cues"] = show["cues"]?.DeepClone(),
				["blockingConditions"] = show["blockingConditions"]?.DeepClone(),
				["operatorChecklist"] = new JArray(
					"Cue順序と時刻を確認",
					"Side Effect CueのApprovalを確認",
					"Recovery Cueを確認",
					"Abort手順を確認",
					"実行前に対象Scene／Revisionを確認"),
				["automaticGoLive"] = false,
				["operatorApproval"] = "PENDING_HUMAN"
			};
		}

		private static JObject Error(string code, string message)
		{
			return new JObject { ["success"] = false, ["errorCode"] = code, ["message"] = message };
		}
	}
}

#endif
