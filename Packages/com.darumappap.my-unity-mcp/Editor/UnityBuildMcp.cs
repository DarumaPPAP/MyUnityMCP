#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityBuildMcp
{
	[McpForUnityTool("build.inspect_environment", Description = "Build Target、Build Settings Scene、出力制約をRead-onlyで取得します。", AutoRegister = false, Group = "build")]
	public static class BuildInspectEnvironmentTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityBuildMcpRuntime.InspectEnvironment());
	}

	[McpForUnityTool("build.prepare_player", Description = "Scene、Target、出力先、BuildOptionsを検証し、承認待ちBuild Planを作成します。", AutoRegister = false, Group = "build")]
	public static class BuildPreparePlayerTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Build Target名。", Required = true)] public string target { get; set; }
			[ToolParameter("Build対象Scene。省略時は有効なBuild Settings Scene。", Required = false)] public string[] scenes { get; set; }
			[ToolParameter("Project相対出力Path。Builds/MyUnityMCP/配下のみ。", Required = true)] public string outputPath { get; set; }
			[ToolParameter("Development Buildか。", Required = false)] public bool? development { get; set; }
			[ToolParameter("Detailed Build Reportを有効にするか。", Required = false)] public bool? detailedReport { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? expectedRevision { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityBuildMcpRuntime.PreparePlayer(value.target, value.scenes, value.outputPath, value.development ?? false, value.detailedReport ?? true, value.expectedRevision));
	}

	[McpForUnityTool("build.start_player", Description = "承認済みBuild PlanをBuildPipeline.BuildPlayerで実行し、BuildReport Summaryを返します。", AutoRegister = false, Group = "build")]
	public static class BuildStartPlayerTool
	{
		public sealed class Parameters
		{
			[ToolParameter("build.prepare_playerが返したPlan ID。", Required = true)] public string planId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? currentRevision { get; set; }
			[ToolParameter("build.prepare_playerが返したApproval Token。", Required = true)] public string approvalToken { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityBuildMcpRuntime.StartPlayer(value.planId, value.currentRevision, value.approvalToken));
	}

	[McpForUnityTool("build.get_history", Description = "現在のEditor Sessionで実行したBuild Summaryを取得します。秘密情報や環境変数は記録しません。", AutoRegister = false, Group = "build")]
	public static class BuildGetHistoryTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityBuildMcpRuntime.GetHistory());
	}

	[McpForUnityTool("build.cancel_player", Description = "Public BuildPipelineに安全な協調Cancel APIがないため、未開始Planのみ破棄可能であることを明示します。", AutoRegister = false, Group = "build")]
	public static class BuildCancelPlayerTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityDomainMcpCommon.Error("build.cancel_player", E_DOMAIN_TOOL_STATUS.BACKEND_NOT_IMPLEMENTED, "実行中Buildの強制CancelはPublic APIで安全に提供できません。"));
	}

	[McpForUnityTool("build.get_support_matrix", Description = "BuildMCPの検証済み範囲と未検証Platformを取得します。", AutoRegister = false, Group = "build")]
	public static class BuildGetSupportMatrixTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityBuildMcpRuntime.GetSupportMatrix());
	}

	public static class UnityBuildMcpRuntime
	{
		private const string DOMAIN_ID = "unity_build_mcp";
		private const string OUTPUT_ROOT = "Builds/MyUnityMCP/";
		private static readonly List<JObject> _history = new List<JObject>();

		public static UnityDomainMcpResult InspectEnvironment()
		{
			return UnityDomainMcpCommon.Result("build.inspect_environment", E_DOMAIN_TOOL_STATUS.SUCCESS, "Build環境を取得しました。", new JObject
			{
				["activeBuildTarget"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
				["installedTargets"] = new JArray(Enum.GetNames(typeof(BuildTarget)).Where(value => !string.Equals(value, "NoTarget", StringComparison.Ordinal))),
				["enabledScenes"] = new JArray(EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path)),
				["outputRoot"] = OUTPUT_ROOT,
				["isBuildingPlayer"] = BuildPipeline.isBuildingPlayer,
				["automaticBuild"] = false
			});
		}

		public static UnityDomainMcpResult PreparePlayer(string target, string[] scenes, string outputPath, bool development, bool detailedReport, long? expectedRevision)
		{
			if (!Enum.TryParse(target, true, out BuildTarget parsedTarget) || parsedTarget == BuildTarget.NoTarget)
			{
				return UnityDomainMcpCommon.Error("build.prepare_player", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Build Targetが不正です。");
			}
			if (!TryNormalizeOutput(outputPath, out string normalizedOutput, out string outputError))
			{
				return UnityDomainMcpCommon.Error("build.prepare_player", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, outputError);
			}

			string[] resolvedScenes = scenes == null || scenes.Length == 0
				? EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray()
				: scenes.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
			if (resolvedScenes.Length == 0)
			{
				return UnityDomainMcpCommon.Error("build.prepare_player", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Build対象Sceneがありません。");
			}
			foreach (string scene in resolvedScenes)
			{
				if (!scene.StartsWith("Assets/", StringComparison.Ordinal) || !scene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || !File.Exists(scene))
				{
					return UnityDomainMcpCommon.Error("build.prepare_player", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, $"Scene Pathが不正です: {scene}");
				}
			}

			BuildOptions options = BuildOptions.None;
			if (development) options |= BuildOptions.Development;
			if (detailedReport) options |= BuildOptions.DetailedBuildReport;

			return UnityDomainMcpCommon.Prepare("build.prepare_player", DOMAIN_ID, "build_player", expectedRevision, true, new JObject
			{
				["target"] = parsedTarget.ToString(),
				["scenes"] = new JArray(resolvedScenes),
				["outputPath"] = normalizedOutput,
				["options"] = options.ToString(),
				["development"] = development,
				["detailedReport"] = detailedReport,
				["environmentVariablesCaptured"] = false,
				["secretsCaptured"] = false
			});
		}

		public static UnityDomainMcpResult StartPlayer(string planId, long? currentRevision, string approvalToken)
		{
			if (!currentRevision.HasValue)
			{
				return UnityDomainMcpCommon.Error("build.start_player", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "currentRevisionが必要です。");
			}
			if (BuildPipeline.isBuildingPlayer)
			{
				return UnityDomainMcpCommon.Error("build.start_player", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "別のPlayer Buildが実行中です。");
			}
			if (!UnityDomainMcpPlanStore.TryConsume("build.start_player", DOMAIN_ID, planId, currentRevision.Value, approvalToken, out UnityDomainMcpPlan plan, out UnityDomainMcpResult failure))
			{
				return failure;
			}

			BuildTarget target = (BuildTarget)Enum.Parse(typeof(BuildTarget), plan.Payload.Value<string>("target"));
			BuildOptions options = (BuildOptions)Enum.Parse(typeof(BuildOptions), plan.Payload.Value<string>("options"));
			string outputPath = plan.Payload.Value<string>("outputPath");
			string absoluteOutput = Path.GetFullPath(outputPath);
			string outputDirectory = Path.GetDirectoryName(absoluteOutput);
			if (!string.IsNullOrEmpty(outputDirectory))
			{
				Directory.CreateDirectory(outputDirectory);
			}

			DateTime startedUtc = DateTime.UtcNow;
			BuildReport report;
			try
			{
				report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
				{
					scenes = plan.Payload["scenes"].Values<string>().ToArray(),
					locationPathName = outputPath,
					target = target,
					options = options
				});
			}
			catch (Exception exception)
			{
				JObject exceptionRecord = new JObject
				{
					["buildId"] = $"build-{Guid.NewGuid():N}",
					["result"] = "EXCEPTION",
					["target"] = target.ToString(),
					["outputPath"] = outputPath,
					["startedUtc"] = startedUtc.ToString("O"),
					["completedUtc"] = DateTime.UtcNow.ToString("O"),
					["exceptionType"] = exception.GetType().FullName,
					["message"] = exception.Message,
					["secretsCaptured"] = false
				};
				_history.Add(exceptionRecord);
				return UnityDomainMcpCommon.Error("build.start_player", E_DOMAIN_TOOL_STATUS.FAILED, exception.Message);
			}

			BuildSummary summary = report.summary;
			JObject record = new JObject
			{
				["buildId"] = $"build-{Guid.NewGuid():N}",
				["result"] = summary.result.ToString(),
				["target"] = summary.platform.ToString(),
				["outputPath"] = outputPath,
				["totalSize"] = summary.totalSize,
				["totalTimeSeconds"] = summary.totalTime.TotalSeconds,
				["totalErrors"] = summary.totalErrors,
				["totalWarnings"] = summary.totalWarnings,
				["startedUtc"] = startedUtc.ToString("O"),
				["completedUtc"] = DateTime.UtcNow.ToString("O"),
				["secretsCaptured"] = false
			};
			_history.Add(record);
			E_DOMAIN_TOOL_STATUS status = summary.result == BuildResult.Succeeded
				? E_DOMAIN_TOOL_STATUS.SUCCESS
				: E_DOMAIN_TOOL_STATUS.FAILED;
			return UnityDomainMcpCommon.Result("build.start_player", status, $"Player Build: {summary.result}", record);
		}

		public static UnityDomainMcpResult GetHistory()
		{
			return UnityDomainMcpCommon.Result("build.get_history", E_DOMAIN_TOOL_STATUS.SUCCESS, "Build Historyを取得しました。", new JObject
			{
				["items"] = new JArray(_history),
				["environmentVariablesCaptured"] = false,
				["secretsCaptured"] = false
			});
		}

		public static UnityDomainMcpResult GetSupportMatrix()
		{
			return UnityDomainMcpCommon.Result("build.get_support_matrix", E_DOMAIN_TOOL_STATUS.UNVERIFIED, "BuildMCPの実装範囲と未検証範囲です。", new JObject
			{
				["implemented"] = new JArray("Build Settings scene inspection", "BuildPlayerOptions preview", "approval-gated BuildPipeline.BuildPlayer", "BuildReport summary"),
				["verified"] = new JArray("plan validation contracts"),
				["unverified"] = new JArray("platform-specific player builds", "Nintendo Switch", "PlayStation", "remote build farm"),
				["unsupported"] = new JArray("force cancel of running BuildPipeline.BuildPlayer")
			});
		}

		internal static bool TryNormalizeOutput(string outputPath, out string normalized, out string error)
		{
			normalized = (outputPath ?? string.Empty).Replace('\\', '/').Trim();
			error = null;
			if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized) || normalized.Contains(".."))
			{
				error = "outputPathはProject相対Pathで指定してください。";
				return false;
			}
			if (!normalized.StartsWith(OUTPUT_ROOT, StringComparison.Ordinal))
			{
				error = $"outputPathは{OUTPUT_ROOT}配下に限定されます。";
				return false;
			}
			return true;
		}
	}
}

#endif
