#if UNITY_EDITOR

using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor;
using UnityEngine;

namespace UnityAudioMcp
{
	[McpForUnityTool("audio.inspect", Description = "Loaded SceneのAudioSource、AudioClip、Mixer RoutingをRead-onlyで取得します。", AutoRegister = false, Group = "audio")]
	public static class AudioInspectTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Inactive Objectを含めるか。", Required = false)] public bool? includeInactive { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityAudioMcpRuntime.Inspect(value.includeInactive ?? true));
	}

	[McpForUnityTool("audio.validate", Description = "AudioSourceのClip、Volume、Pitch、Spatial DistanceをRead-onlyで検証します。", AutoRegister = false, Group = "audio")]
	public static class AudioValidateTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityAudioMcpRuntime.Validate());
	}

	[McpForUnityTool("audio.prepare_source", Description = "AudioSourceのVolume、Pitch、Spatial Blend、Loop、Mute変更をExact Previewします。", AutoRegister = false, Group = "audio")]
	public static class AudioPrepareSourceTool
	{
		public sealed class Parameters
		{
			[ToolParameter("対象AudioSourceのGlobal Object ID。", Required = true)] public string targetObjectId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? expectedRevision { get; set; }
			[ToolParameter("Volume。0～1。", Required = false)] public float? volume { get; set; }
			[ToolParameter("Pitch。-3～3。", Required = false)] public float? pitch { get; set; }
			[ToolParameter("Spatial Blend。0～1。", Required = false)] public float? spatialBlend { get; set; }
			[ToolParameter("Loop。", Required = false)] public bool? loop { get; set; }
			[ToolParameter("Mute。", Required = false)] public bool? mute { get; set; }
			[ToolParameter("Play On Awake。", Required = false)] public bool? playOnAwake { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityAudioMcpRuntime.PrepareSource(value.targetObjectId, value.volume, value.pitch, value.spatialBlend, value.loop, value.mute, value.playOnAwake, value.expectedRevision));
	}

	[McpForUnityTool("audio.apply_source", Description = "承認済みAudioSource PlanをUndo対応で適用します。Clip／Mixer Assetは変更しません。", AutoRegister = false, Group = "audio")]
	public static class AudioApplySourceTool
	{
		public sealed class Parameters
		{
			[ToolParameter("audio.prepare_sourceが返したPlan ID。", Required = true)] public string planId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? currentRevision { get; set; }
			[ToolParameter("Approval Token。", Required = true)] public string approvalToken { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityAudioMcpRuntime.ApplySource(value.planId, value.currentRevision, value.approvalToken));
	}

	[McpForUnityTool("audio.get_support_matrix", Description = "Audio MCPの実装・未検証範囲を取得します。", AutoRegister = false, Group = "audio")]
	public static class AudioGetSupportMatrixTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityAudioMcpRuntime.GetSupportMatrix());
	}

	public static class UnityAudioMcpRuntime
	{
		private const string DOMAIN_ID = "unity_audio_mcp";

		public static UnityDomainMcpResult Inspect(bool includeInactive)
		{
			AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>()
				.Where(value => IsSceneObject(value) && (includeInactive || value.gameObject.activeInHierarchy))
				.OrderBy(value => value.gameObject.scene.path)
				.ThenBy(value => HierarchyPath(value.transform))
				.ToArray();
			return UnityDomainMcpCommon.Result("audio.inspect", E_DOMAIN_TOOL_STATUS.SUCCESS, "AudioSource構成を取得しました。", new JObject
			{
				["sources"] = new JArray(sources.Select(SourceData)),
				["listenerCount"] = Resources.FindObjectsOfTypeAll<AudioListener>().Count(IsSceneObject)
			});
		}

		public static UnityDomainMcpResult Validate()
		{
			JArray findings = new JArray();
			AudioListener[] listeners = Resources.FindObjectsOfTypeAll<AudioListener>().Where(IsSceneObject).ToArray();
			if (listeners.Length == 0)
			{
				findings.Add(new JObject { ["code"] = "AUDIO-LISTENER-MISSING", ["severity"] = "WARNING", ["message"] = "Loaded SceneにAudioListenerがありません。" });
			}
			if (listeners.Count(value => value.enabled && value.gameObject.activeInHierarchy) > 1)
			{
				findings.Add(new JObject { ["code"] = "AUDIO-LISTENER-MULTIPLE", ["severity"] = "ERROR", ["message"] = "有効なAudioListenerが複数あります。" });
			}
			foreach (AudioSource source in Resources.FindObjectsOfTypeAll<AudioSource>().Where(IsSceneObject))
			{
				if (source.playOnAwake && source.clip == null)
				{
					findings.Add(Finding("AUDIO-CLIP-MISSING", "WARNING", source, "Play On AwakeですがAudioClipがありません。"));
				}
				if (source.volume < 0f || source.volume > 1f || source.spatialBlend < 0f || source.spatialBlend > 1f || source.pitch < -3f || source.pitch > 3f)
				{
					findings.Add(Finding("AUDIO-SOURCE-RANGE-INVALID", "ERROR", source, "AudioSource値が対応範囲外です。"));
				}
				if (source.minDistance < 0f || source.maxDistance < source.minDistance)
				{
					findings.Add(Finding("AUDIO-DISTANCE-INVALID", "ERROR", source, "3D Distance設定が不正です。"));
				}
			}
			return UnityDomainMcpCommon.Result("audio.validate", findings.Any(value => value.Value<string>("severity") == "ERROR") ? E_DOMAIN_TOOL_STATUS.PARTIAL : E_DOMAIN_TOOL_STATUS.SUCCESS, "Audio Validationを完了しました。", new JObject { ["findings"] = findings, ["findingCount"] = findings.Count });
		}

		public static UnityDomainMcpResult PrepareSource(string targetObjectId, float? volume, float? pitch, float? spatialBlend, bool? loop, bool? mute, bool? playOnAwake, long? expectedRevision)
		{
			if (!UnityDomainMcpCommon.TryResolveObject(targetObjectId, out AudioSource target))
			{
				return UnityDomainMcpCommon.Error("audio.prepare_source", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "対象AudioSourceが見つかりません。");
			}
			if (!volume.HasValue && !pitch.HasValue && !spatialBlend.HasValue && !loop.HasValue && !mute.HasValue && !playOnAwake.HasValue)
			{
				return UnityDomainMcpCommon.Error("audio.prepare_source", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "変更値がありません。");
			}
			if ((volume.HasValue && (volume.Value < 0f || volume.Value > 1f)) ||
				(pitch.HasValue && (pitch.Value < -3f || pitch.Value > 3f)) ||
				(spatialBlend.HasValue && (spatialBlend.Value < 0f || spatialBlend.Value > 1f)))
			{
				return UnityDomainMcpCommon.Error("audio.prepare_source", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Volume／Pitch／Spatial Blendが範囲外です。");
			}

			return UnityDomainMcpCommon.Prepare("audio.prepare_source", DOMAIN_ID, "update_audio_source", expectedRevision, true, new JObject
			{
				["targetObjectId"] = targetObjectId,
				["baseline"] = SourceData(target),
				["requested"] = new JObject
				{
					["volume"] = volume ?? target.volume,
					["pitch"] = pitch ?? target.pitch,
					["spatialBlend"] = spatialBlend ?? target.spatialBlend,
					["loop"] = loop ?? target.loop,
					["mute"] = mute ?? target.mute,
					["playOnAwake"] = playOnAwake ?? target.playOnAwake
				},
				["clipMutation"] = false,
				["mixerAssetMutation"] = false,
				["savePerformed"] = false
			});
		}

		public static UnityDomainMcpResult ApplySource(string planId, long? currentRevision, string approvalToken)
		{
			if (!currentRevision.HasValue)
			{
				return UnityDomainMcpCommon.Error("audio.apply_source", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "currentRevisionが必要です。");
			}
			if (!UnityDomainMcpPlanStore.TryConsume("audio.apply_source", DOMAIN_ID, planId, currentRevision.Value, approvalToken, out UnityDomainMcpPlan plan, out UnityDomainMcpResult failure))
			{
				return failure;
			}
			if (!UnityDomainMcpCommon.TryResolveObject(plan.Payload.Value<string>("targetObjectId"), out AudioSource target))
			{
				return UnityDomainMcpCommon.Error("audio.apply_source", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "対象AudioSourceが見つかりません。");
			}
			JObject requested = (JObject)plan.Payload["requested"];
			Undo.RecordObject(target, "MyUnityMCP Audio Source");
			target.volume = requested.Value<float>("volume");
			target.pitch = requested.Value<float>("pitch");
			target.spatialBlend = requested.Value<float>("spatialBlend");
			target.loop = requested.Value<bool>("loop");
			target.mute = requested.Value<bool>("mute");
			target.playOnAwake = requested.Value<bool>("playOnAwake");
			UnityDomainMcpCommon.CompleteMutation(target);
			return UnityDomainMcpCommon.Result("audio.apply_source", E_DOMAIN_TOOL_STATUS.SUCCESS, "AudioSource設定を適用しました。Scene Saveは行っていません。", new JObject
			{
				["target"] = SourceData(target),
				["clipMutation"] = false,
				["mixerAssetMutation"] = false,
				["savePerformed"] = false
			});
		}

		public static UnityDomainMcpResult GetSupportMatrix()
		{
			return UnityDomainMcpCommon.Result("audio.get_support_matrix", E_DOMAIN_TOOL_STATUS.UNVERIFIED, "Audio MCPの対応状況です。", new JObject
			{
				["implemented"] = new JArray("AudioSource inspection", "AudioClip metadata", "AudioListener validation", "approval-gated AudioSource property mutation"),
				["excludedInitialMutation"] = new JArray("AudioClip replacement", "AudioMixer asset creation", "exposed parameter authoring", "audio rendering"),
				["unverified"] = new JArray("platform-specific audio output", "gamepad speaker output", "runtime audio profiling")
			});
		}

		private static JObject SourceData(AudioSource source)
		{
			AudioClip clip = source.clip;
			return new JObject
			{
				["objectId"] = UnityDomainMcpCommon.ObjectId(source),
				["name"] = source.name,
				["scenePath"] = source.gameObject.scene.path,
				["hierarchyPath"] = HierarchyPath(source.transform),
				["enabled"] = source.enabled,
				["volume"] = source.volume,
				["pitch"] = source.pitch,
				["spatialBlend"] = source.spatialBlend,
				["loop"] = source.loop,
				["mute"] = source.mute,
				["playOnAwake"] = source.playOnAwake,
				["minDistance"] = source.minDistance,
				["maxDistance"] = source.maxDistance,
				["outputMixerGroup"] = source.outputAudioMixerGroup == null ? null : source.outputAudioMixerGroup.name,
				["outputMixerAssetPath"] = source.outputAudioMixerGroup == null ? null : AssetDatabase.GetAssetPath(source.outputAudioMixerGroup.audioMixer),
				["clip"] = clip == null ? null : new JObject
				{
					["name"] = clip.name,
					["assetPath"] = AssetDatabase.GetAssetPath(clip),
					["length"] = clip.length,
					["channels"] = clip.channels,
					["frequency"] = clip.frequency,
					["samples"] = clip.samples,
					["loadState"] = clip.loadState.ToString(),
					["loadType"] = clip.loadType.ToString()
				}
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
