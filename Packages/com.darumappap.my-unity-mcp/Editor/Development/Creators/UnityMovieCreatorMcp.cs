#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityAgentMcp;

namespace UnityMovieCreatorMcp
{
	public sealed class UnityMovieCreatorShotInput
	{
		public string shotId;
		public double durationSeconds;
		public string directorObjectId;
		public string visualGoal;
		public string[] acceptanceCriteria;
	}

	[McpForUnityTool("movie.compile_production", Description = "Shot ListをGraphics／Cinematic Domainへ委譲するProduction GraphとHuman Gateへ変換します。", AutoRegister = false, Group = "creator")]
	public static class MovieCompileProductionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Movie Goal。", Required = true)] public string goal { get; set; }
			[ToolParameter("Shot List。", Required = true)] public UnityMovieCreatorShotInput[] shots { get; set; }
			[ToolParameter("対象Platform。", Required = false)] public string[] targetPlatforms { get; set; }
			[ToolParameter("禁止変更。", Required = false)] public string[] prohibitedChanges { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityMovieCreatorRuntime.Execute<Parameters>(@params, value => UnityMovieCreatorRuntime.CompileProduction(value.goal, value.shots, value.targetPlatforms, value.prohibitedChanges));
	}

	[McpForUnityTool("movie.preview_production", Description = "Movie Production Graph、Shot順序、Human Gate、未検証BackendをRead-onlyで表示します。", AutoRegister = false, Group = "creator")]
	public static class MoviePreviewProductionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("movie.compile_productionが返したProduction ID。", Required = true)] public string productionId { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityMovieCreatorRuntime.Execute<Parameters>(@params, value => UnityMovieCreatorRuntime.Preview(value.productionId));
	}

	[McpForUnityTool("movie.create_review_handoff", Description = "Movie ProductionをShot単位のHuman Visual Review Handoffへ変換します。自動合格しません。", AutoRegister = false, Group = "creator")]
	public static class MovieCreateReviewHandoffTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Production ID。", Required = true)] public string productionId { get; set; }
			[ToolParameter("Capture／Review Evidence ID。", Required = false)] public string[] evidenceIds { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityMovieCreatorRuntime.Execute<Parameters>(@params, value => UnityMovieCreatorRuntime.CreateReviewHandoff(value.productionId, value.evidenceIds));
	}

	internal static class UnityMovieCreatorRuntime
	{
		private static readonly Dictionary<string, JObject> _productions = new Dictionary<string, JObject>(StringComparer.Ordinal);

		public static object Execute<T>(JObject @params, Func<T, JObject> operation) where T : new()
		{
			try
			{
				T value = @params == null || !@params.HasValues ? new T() : @params.ToObject<T>();
				return operation(value ?? new T());
			}
			catch (Exception exception)
			{
				return Error("MOVIE-REQUEST-INVALID", exception.Message);
			}
		}

		public static JObject CompileProduction(string goal, UnityMovieCreatorShotInput[] shots, string[] targetPlatforms, string[] prohibitedChanges)
		{
			if (string.IsNullOrWhiteSpace(goal))
			{
				return Error("MOVIE-GOAL-MISSING", "goalが必要です。");
			}
			List<UnityMovieCreatorShotInput> normalized = (shots ?? Array.Empty<UnityMovieCreatorShotInput>()).Where(value => value != null).ToList();
			if (normalized.Count == 0 || normalized.Count > 256)
			{
				return Error("MOVIE-SHOT-COUNT-INVALID", "1～256 Shotを指定してください。");
			}
			if (normalized.Any(value => string.IsNullOrWhiteSpace(value.shotId) || value.durationSeconds <= 0.0 || double.IsNaN(value.durationSeconds) || double.IsInfinity(value.durationSeconds)))
			{
				return Error("MOVIE-SHOT-INVALID", "shotIdと正のdurationSecondsが必要です。");
			}
			if (normalized.GroupBy(value => value.shotId, StringComparer.Ordinal).Any(value => value.Count() > 1))
			{
				return Error("MOVIE-SHOT-ID-DUPLICATE", "shotIdが重複しています。");
			}

			JObject capabilities = UnityAgentMcpRuntime.Instance.InspectCapabilities();
			JToken cinematic = capabilities["domains"]?.FirstOrDefault(value => string.Equals(value.Value<string>("domainId"), "unity_cinematic_mcp", StringComparison.Ordinal));
			bool cinematicOperational = string.Equals(cinematic?.Value<string>("status"), "editor_operational", StringComparison.Ordinal);
			string productionId = $"movie-production-{Guid.NewGuid():N}";
			double cursor = 0.0;
			JArray compiledShots = new JArray();
			foreach (UnityMovieCreatorShotInput shot in normalized)
			{
				compiledShots.Add(new JObject
				{
					["shotId"] = shot.shotId,
					["startSeconds"] = cursor,
					["durationSeconds"] = shot.durationSeconds,
					["endSeconds"] = cursor + shot.durationSeconds,
					["directorObjectId"] = shot.directorObjectId,
					["visualGoal"] = shot.visualGoal,
					["acceptanceCriteria"] = shot.acceptanceCriteria == null ? new JArray() : new JArray(shot.acceptanceCriteria),
					["domainSteps"] = new JArray("graphics.inspect_scene", "graphics.compile_direction", "cinematic.inspect", "cinematic.validate", "graphics.capture_evidence", "human_visual_review"),
					["automaticVisualAcceptance"] = false
				});
				cursor += shot.durationSeconds;
			}

			JObject production = new JObject
			{
				["success"] = true,
				["productionId"] = productionId,
				["goal"] = goal,
				["durationSeconds"] = cursor,
				["targetPlatforms"] = targetPlatforms == null ? new JArray() : new JArray(targetPlatforms),
				["prohibitedChanges"] = prohibitedChanges == null ? new JArray() : new JArray(prohibitedChanges),
				["shots"] = compiledShots,
				["executionReady"] = cinematicOperational,
				["blockingConditions"] = cinematicOperational ? new JArray() : new JArray("unity_cinematic_mcp is not editor_operational"),
				["directUnityMutation"] = false,
				["humanGates"] = new JArray("mutation approval", "save approval", "capture review", "final movie review")
			};
			_productions[productionId] = production;
			return production;
		}

		public static JObject Preview(string productionId)
		{
			return _productions.TryGetValue(productionId ?? string.Empty, out JObject production)
				? new JObject { ["success"] = true, ["production"] = production.DeepClone(), ["mutationApplied"] = false }
				: Error("MOVIE-PRODUCTION-NOT-FOUND", "Productionが見つかりません。");
		}

		public static JObject CreateReviewHandoff(string productionId, string[] evidenceIds)
		{
			if (!_productions.TryGetValue(productionId ?? string.Empty, out JObject production))
			{
				return Error("MOVIE-PRODUCTION-NOT-FOUND", "Productionが見つかりません。");
			}
			return new JObject
			{
				["success"] = true,
				["productionId"] = productionId,
				["handoffStatus"] = "HUMAN_VISUAL_REVIEW_REQUIRED",
				["evidenceIds"] = evidenceIds == null ? new JArray() : new JArray(evidenceIds),
				["shotReview"] = new JArray(((JArray)production["shots"]).Select(value => new JObject
				{
					["shotId"] = value.Value<string>("shotId"),
					["visualGoal"] = value.Value<string>("visualGoal"),
					["acceptanceCriteria"] = value["acceptanceCriteria"]?.DeepClone(),
					["decision"] = "PENDING_HUMAN"
				})),
				["automaticVisualAcceptance"] = false,
				["finalApproval"] = "PENDING_HUMAN"
			};
		}

		private static JObject Error(string code, string message)
		{
			return new JObject { ["success"] = false, ["errorCode"] = code, ["message"] = message };
		}
	}
}

#endif
