#if UNITY_EDITOR

using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace UnityCinematicMcp
{
	[McpForUnityTool("cinematic.inspect", Description = "Loaded SceneのPlayableDirector、Playable Asset、Output BindingをRead-onlyで取得します。", AutoRegister = false, Group = "cinematic")]
	public static class CinematicInspectTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Inactive Objectを含めるか。", Required = false)] public bool? includeInactive { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityCinematicMcpRuntime.Inspect(value.includeInactive ?? true));
	}

	[McpForUnityTool("cinematic.validate", Description = "Playable Asset欠落、Binding欠落、Time範囲をRead-onlyで検証します。", AutoRegister = false, Group = "cinematic")]
	public static class CinematicValidateTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityCinematicMcpRuntime.Validate());
	}

	[McpForUnityTool("cinematic.prepare_director", Description = "PlayableDirectorのInitial Time、Update Mode、Wrap Mode、Play On Awake変更をExact Previewします。", AutoRegister = false, Group = "cinematic")]
	public static class CinematicPrepareDirectorTool
	{
		public sealed class Parameters
		{
			[ToolParameter("対象PlayableDirectorのGlobal Object ID。", Required = true)] public string targetObjectId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? expectedRevision { get; set; }
			[ToolParameter("Initial Time。", Required = false)] public double? initialTime { get; set; }
			[ToolParameter("DSPClock／GameTime／UnscaledGameTime／Manual。", Required = false)] public string timeUpdateMode { get; set; }
			[ToolParameter("Hold／Loop／None。", Required = false)] public string extrapolationMode { get; set; }
			[ToolParameter("Play On Awake。", Required = false)] public bool? playOnAwake { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityCinematicMcpRuntime.PrepareDirector(value.targetObjectId, value.initialTime, value.timeUpdateMode, value.extrapolationMode, value.playOnAwake, value.expectedRevision));
	}

	[McpForUnityTool("cinematic.apply_director", Description = "承認済みPlayableDirector PlanをUndo対応で適用します。Playable AssetとBindingは変更しません。", AutoRegister = false, Group = "cinematic")]
	public static class CinematicApplyDirectorTool
	{
		public sealed class Parameters
		{
			[ToolParameter("cinematic.prepare_directorが返したPlan ID。", Required = true)] public string planId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? currentRevision { get; set; }
			[ToolParameter("Approval Token。", Required = true)] public string approvalToken { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityCinematicMcpRuntime.ApplyDirector(value.planId, value.currentRevision, value.approvalToken));
	}

	[McpForUnityTool("cinematic.get_support_matrix", Description = "Cinematic MCPのCore Playables対応とOptional Package範囲を取得します。", AutoRegister = false, Group = "cinematic")]
	public static class CinematicGetSupportMatrixTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityCinematicMcpRuntime.GetSupportMatrix());
	}

	public static class UnityCinematicMcpRuntime
	{
		private const string DOMAIN_ID = "unity_cinematic_mcp";

		public static UnityDomainMcpResult Inspect(bool includeInactive)
		{
			PlayableDirector[] directors = Resources.FindObjectsOfTypeAll<PlayableDirector>()
				.Where(value => IsSceneObject(value) && (includeInactive || value.gameObject.activeInHierarchy))
				.OrderBy(value => value.gameObject.scene.path)
				.ThenBy(value => HierarchyPath(value.transform))
				.ToArray();
			return UnityDomainMcpCommon.Result("cinematic.inspect", E_DOMAIN_TOOL_STATUS.SUCCESS, "PlayableDirector構成を取得しました。", new JObject
			{
				["directors"] = new JArray(directors.Select(DirectorData))
			});
		}

		public static UnityDomainMcpResult Validate()
		{
			JArray findings = new JArray();
			foreach (PlayableDirector director in Resources.FindObjectsOfTypeAll<PlayableDirector>().Where(IsSceneObject))
			{
				if (director.playableAsset == null)
				{
					findings.Add(Finding("CINEMATIC-ASSET-MISSING", "WARNING", director, "PlayableDirectorにPlayable Assetがありません。"));
					continue;
				}
				if (double.IsNaN(director.initialTime) || double.IsInfinity(director.initialTime) || director.initialTime < 0.0 || (director.duration > 0.0 && director.initialTime > director.duration))
				{
					findings.Add(Finding("CINEMATIC-INITIAL-TIME-INVALID", "ERROR", director, "Initial TimeがPlayable Duration範囲外です。"));
				}
				foreach (PlayableBinding binding in director.playableAsset.outputs)
				{
					if (binding.sourceObject != null && director.GetGenericBinding(binding.sourceObject) == null)
					{
						findings.Add(new JObject
						{
							["code"] = "CINEMATIC-BINDING-MISSING",
							["severity"] = "WARNING",
							["message"] = $"Playable Output Bindingが未設定です: {binding.streamName}",
							["objectId"] = UnityDomainMcpCommon.ObjectId(director),
							["streamName"] = binding.streamName,
							["targetType"] = binding.outputTargetType?.FullName
						});
					}
				}
			}
			return UnityDomainMcpCommon.Result("cinematic.validate", findings.Any(value => value.Value<string>("severity") == "ERROR") ? E_DOMAIN_TOOL_STATUS.PARTIAL : E_DOMAIN_TOOL_STATUS.SUCCESS, "Cinematic Validationを完了しました。", new JObject { ["findings"] = findings, ["findingCount"] = findings.Count });
		}

		public static UnityDomainMcpResult PrepareDirector(string targetObjectId, double? initialTime, string timeUpdateMode, string extrapolationMode, bool? playOnAwake, long? expectedRevision)
		{
			if (!UnityDomainMcpCommon.TryResolveObject(targetObjectId, out PlayableDirector target))
			{
				return UnityDomainMcpCommon.Error("cinematic.prepare_director", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "対象PlayableDirectorが見つかりません。");
			}
			if (!initialTime.HasValue && string.IsNullOrWhiteSpace(timeUpdateMode) && string.IsNullOrWhiteSpace(extrapolationMode) && !playOnAwake.HasValue)
			{
				return UnityDomainMcpCommon.Error("cinematic.prepare_director", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "変更値がありません。");
			}
			double resolvedInitialTime = initialTime ?? target.initialTime;
			if (double.IsNaN(resolvedInitialTime) || double.IsInfinity(resolvedInitialTime) || resolvedInitialTime < 0.0 || (target.duration > 0.0 && resolvedInitialTime > target.duration))
			{
				return UnityDomainMcpCommon.Error("cinematic.prepare_director", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "initialTimeがPlayable Duration範囲外です。");
			}
			DirectorUpdateMode resolvedUpdate = target.timeUpdateMode;
			if (!string.IsNullOrWhiteSpace(timeUpdateMode) && !Enum.TryParse(timeUpdateMode, true, out resolvedUpdate))
			{
				return UnityDomainMcpCommon.Error("cinematic.prepare_director", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "timeUpdateModeが不正です。");
			}
			DirectorWrapMode resolvedWrap = target.extrapolationMode;
			if (!string.IsNullOrWhiteSpace(extrapolationMode) && !Enum.TryParse(extrapolationMode, true, out resolvedWrap))
			{
				return UnityDomainMcpCommon.Error("cinematic.prepare_director", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "extrapolationModeが不正です。");
			}
			return UnityDomainMcpCommon.Prepare("cinematic.prepare_director", DOMAIN_ID, "update_playable_director", expectedRevision, true, new JObject
			{
				["targetObjectId"] = targetObjectId,
				["baseline"] = DirectorData(target),
				["requested"] = new JObject
				{
					["initialTime"] = resolvedInitialTime,
					["timeUpdateMode"] = resolvedUpdate.ToString(),
					["extrapolationMode"] = resolvedWrap.ToString(),
					["playOnAwake"] = playOnAwake ?? target.playOnAwake
				},
				["playableAssetMutation"] = false,
				["bindingMutation"] = false,
				["savePerformed"] = false
			});
		}

		public static UnityDomainMcpResult ApplyDirector(string planId, long? currentRevision, string approvalToken)
		{
			if (!currentRevision.HasValue)
			{
				return UnityDomainMcpCommon.Error("cinematic.apply_director", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "currentRevisionが必要です。");
			}
			if (!UnityDomainMcpPlanStore.TryConsume("cinematic.apply_director", DOMAIN_ID, planId, currentRevision.Value, approvalToken, out UnityDomainMcpPlan plan, out UnityDomainMcpResult failure))
			{
				return failure;
			}
			if (!UnityDomainMcpCommon.TryResolveObject(plan.Payload.Value<string>("targetObjectId"), out PlayableDirector target))
			{
				return UnityDomainMcpCommon.Error("cinematic.apply_director", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "対象PlayableDirectorが見つかりません。");
			}
			JObject requested = (JObject)plan.Payload["requested"];
			Undo.RecordObject(target, "MyUnityMCP Playable Director");
			target.initialTime = requested.Value<double>("initialTime");
			target.timeUpdateMode = (DirectorUpdateMode)Enum.Parse(typeof(DirectorUpdateMode), requested.Value<string>("timeUpdateMode"));
			target.extrapolationMode = (DirectorWrapMode)Enum.Parse(typeof(DirectorWrapMode), requested.Value<string>("extrapolationMode"));
			target.playOnAwake = requested.Value<bool>("playOnAwake");
			UnityDomainMcpCommon.CompleteMutation(target);
			return UnityDomainMcpCommon.Result("cinematic.apply_director", E_DOMAIN_TOOL_STATUS.SUCCESS, "PlayableDirector設定を適用しました。Scene Saveは行っていません。", new JObject
			{
				["target"] = DirectorData(target),
				["playableAssetMutation"] = false,
				["bindingMutation"] = false,
				["savePerformed"] = false
			});
		}

		public static UnityDomainMcpResult GetSupportMatrix()
		{
			return UnityDomainMcpCommon.Result("cinematic.get_support_matrix", E_DOMAIN_TOOL_STATUS.UNVERIFIED, "Cinematic MCPの対応状況です。", new JObject
			{
				["implemented"] = new JArray("PlayableDirector inspection", "Playable output binding validation", "approval-gated director property mutation"),
				["coreModule"] = "UnityEngine.DirectorModule",
				["optionalPackagesUnverified"] = new JArray("com.unity.timeline track authoring", "com.unity.cinemachine camera authoring"),
				["excludedInitialMutation"] = new JArray("track creation", "clip creation", "binding mutation", "Cinemachine shot mutation")
			});
		}

		private static JObject DirectorData(PlayableDirector director)
		{
			JArray outputs = new JArray();
			if (director.playableAsset != null)
			{
				foreach (PlayableBinding binding in director.playableAsset.outputs)
				{
					UnityEngine.Object target = binding.sourceObject == null ? null : director.GetGenericBinding(binding.sourceObject);
					outputs.Add(new JObject
					{
						["streamName"] = binding.streamName,
						["sourceObject"] = binding.sourceObject == null ? null : binding.sourceObject.name,
						["targetType"] = binding.outputTargetType?.FullName,
						["boundObjectId"] = UnityDomainMcpCommon.ObjectId(target),
						["boundObjectName"] = target == null ? null : target.name
					});
				}
			}
			return new JObject
			{
				["objectId"] = UnityDomainMcpCommon.ObjectId(director),
				["name"] = director.name,
				["scenePath"] = director.gameObject.scene.path,
				["hierarchyPath"] = HierarchyPath(director.transform),
				["enabled"] = director.enabled,
				["playableAsset"] = director.playableAsset == null ? null : director.playableAsset.name,
				["playableAssetPath"] = director.playableAsset == null ? null : AssetDatabase.GetAssetPath(director.playableAsset),
				["duration"] = director.duration,
				["initialTime"] = director.initialTime,
				["currentTime"] = director.time,
				["state"] = director.state.ToString(),
				["timeUpdateMode"] = director.timeUpdateMode.ToString(),
				["extrapolationMode"] = director.extrapolationMode.ToString(),
				["playOnAwake"] = director.playOnAwake,
				["outputs"] = outputs
			};
		}

		private static JObject Finding(string code, string severity, Component target, string message)
		{
			return new JObject
			{
				["code"] = code,
				["severity"] = severity,
				["message"] = message,
				["objectId"] = UnityDomainMcpCommon.ObjectId(target),
				["scenePath"] = target.gameObject.scene.path,
				["hierarchyPath"] = HierarchyPath(target.transform)
			};
		}

		private static bool IsSceneObject(Component value)
		{
			return value != null && value.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(value);
		}

		private static string HierarchyPath(Transform transform)
		{
			return transform.parent == null ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;
		}
	}
}

#endif
