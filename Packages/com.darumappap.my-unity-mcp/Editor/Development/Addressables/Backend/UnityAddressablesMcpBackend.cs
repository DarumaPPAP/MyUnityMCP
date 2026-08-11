#if UNITY_EDITOR && MYUNITYMCP_ADDRESSABLES

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityAddressablesMcp
{
	internal sealed class UnityAddressablesMcpBackend : IUnityAddressablesMcpBackend
	{
		private const string DOMAIN_ID = "unity_addressables_mcp";

		public UnityDomainMcpResult Inspect(PackageInfo package)
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			JArray groups = new JArray();
			if (settings != null)
			{
				foreach (AddressableAssetGroup group in settings.groups.Where(value => value != null))
				{
					groups.Add(new JObject
					{
						["name"] = group.Name,
						["guid"] = group.Guid,
						["entryCount"] = group.entries.Count,
						["readOnly"] = group.ReadOnly
					});
				}
			}
			return UnityDomainMcpCommon.Result("addressables.inspect", E_DOMAIN_TOOL_STATUS.SUCCESS, "Addressables環境を取得しました。", new JObject
			{
				["packageInstalled"] = package != null,
				["packageVersion"] = package?.version,
				["backendCompiled"] = true,
				["settingsExists"] = settings != null,
				["activeProfileId"] = settings?.activeProfileId,
				["activeProfileName"] = settings == null ? null : settings.profileSettings.GetProfileName(settings.activeProfileId),
				["activeBuilder"] = settings?.ActivePlayerDataBuilder == null ? null : settings.ActivePlayerDataBuilder.Name,
				["groups"] = groups,
				["settingsAutoCreated"] = false
			});
		}

		public UnityDomainMcpResult PrepareEntry(string assetPath, string groupName, string address, string[] labels, long? expectedRevision)
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				return UnityDomainMcpCommon.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Settingsが存在しません。自動生成は行いません。");
			}
			if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
			{
				return UnityDomainMcpCommon.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "assetPathが存在しません。");
			}
			AddressableAssetGroup group = settings.FindGroup(groupName);
			if (group == null)
			{
				return UnityDomainMcpCommon.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "指定Groupが存在しません。自動生成は行いません。");
			}
			if (group.ReadOnly)
			{
				return UnityDomainMcpCommon.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Read-only Groupは変更できません。");
			}
			if (string.IsNullOrWhiteSpace(address))
			{
				return UnityDomainMcpCommon.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "addressが必要です。");
			}
			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			return UnityDomainMcpCommon.Prepare("addressables.prepare_entry", DOMAIN_ID, "create_or_move_entry", expectedRevision, true, new JObject
			{
				["assetPath"] = assetPath,
				["assetGuid"] = guid,
				["groupName"] = groupName,
				["address"] = address,
				["labels"] = labels == null ? new JArray() : new JArray(labels.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()),
				["savePerformed"] = false,
				["contentBuildPerformed"] = false
			});
		}

		public UnityDomainMcpResult ApplyEntry(string planId, long? currentRevision, string approvalToken)
		{
			if (!currentRevision.HasValue)
			{
				return UnityDomainMcpCommon.Error("addressables.apply_entry", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "currentRevisionが必要です。");
			}
			if (!UnityDomainMcpPlanStore.TryConsume("addressables.apply_entry", DOMAIN_ID, planId, currentRevision.Value, approvalToken, out UnityDomainMcpPlan plan, out UnityDomainMcpResult failure))
			{
				return failure;
			}
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				return UnityDomainMcpCommon.Error("addressables.apply_entry", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Settingsが存在しません。");
			}
			AddressableAssetGroup group = settings.FindGroup(plan.Payload.Value<string>("groupName"));
			if (group == null || group.ReadOnly)
			{
				return UnityDomainMcpCommon.Error("addressables.apply_entry", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "対象Groupが変更されたか利用できません。");
			}
			Undo.RecordObject(settings, "MyUnityMCP Addressables Entry");
			AddressableAssetEntry entry = settings.CreateOrMoveEntry(plan.Payload.Value<string>("assetGuid"), group, false, false);
			entry.address = plan.Payload.Value<string>("address");
			foreach (string label in plan.Payload["labels"].Values<string>())
			{
				entry.SetLabel(label, true, true, false);
			}
			EditorUtility.SetDirty(settings);
			UnityDomainMcpCommon.CompleteMutation(settings);
			return UnityDomainMcpCommon.Result("addressables.apply_entry", E_DOMAIN_TOOL_STATUS.SUCCESS, "Addressables Entryを更新しました。SettingsはDirtyですが自動Saveしていません。", new JObject
			{
				["assetGuid"] = entry.guid,
				["address"] = entry.address,
				["groupName"] = group.Name,
				["labels"] = new JArray(entry.labels),
				["savePerformed"] = false,
				["contentBuildPerformed"] = false
			});
		}

		public UnityDomainMcpResult PrepareContentBuild(long? expectedRevision)
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				return UnityDomainMcpCommon.Error("addressables.prepare_content_build", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Settingsが存在しません。");
			}
			if (settings.ActivePlayerDataBuilder == null)
			{
				return UnityDomainMcpCommon.Error("addressables.prepare_content_build", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Active Player Data Builderが設定されていません。");
			}
			return UnityDomainMcpCommon.Prepare("addressables.prepare_content_build", DOMAIN_ID, "build_player_content", expectedRevision, true, new JObject
			{
				["activeProfileId"] = settings.activeProfileId,
				["activeProfileName"] = settings.profileSettings.GetProfileName(settings.activeProfileId),
				["activeBuilder"] = settings.ActivePlayerDataBuilder.Name,
				["groupCount"] = settings.groups.Count(value => value != null),
				["settingsAssetPath"] = AssetDatabase.GetAssetPath(settings)
			});
		}

		public UnityDomainMcpResult BuildContent(string planId, long? currentRevision, string approvalToken)
		{
			if (!currentRevision.HasValue)
			{
				return UnityDomainMcpCommon.Error("addressables.build_content", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "currentRevisionが必要です。");
			}
			if (!UnityDomainMcpPlanStore.TryConsume("addressables.build_content", DOMAIN_ID, planId, currentRevision.Value, approvalToken, out UnityDomainMcpPlan plan, out UnityDomainMcpResult failure))
			{
				return failure;
			}
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null || settings.ActivePlayerDataBuilder == null)
			{
				return UnityDomainMcpCommon.Error("addressables.build_content", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables SettingsまたはBuilderが利用できません。");
			}
			if (!string.Equals(settings.activeProfileId, plan.Payload.Value<string>("activeProfileId"), StringComparison.Ordinal) ||
				!string.Equals(settings.ActivePlayerDataBuilder.Name, plan.Payload.Value<string>("activeBuilder"), StringComparison.Ordinal))
			{
				return UnityDomainMcpCommon.Error("addressables.build_content", E_DOMAIN_TOOL_STATUS.STALE_REVISION, "Preview後にProfileまたはBuilderが変更されました。");
			}
			AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
			bool success = string.IsNullOrEmpty(result.Error);
			return UnityDomainMcpCommon.Result("addressables.build_content", success ? E_DOMAIN_TOOL_STATUS.SUCCESS : E_DOMAIN_TOOL_STATUS.FAILED, success ? "Addressables Content Buildが成功しました。" : "Addressables Content Buildが失敗しました。", new JObject
			{
				["success"] = success,
				["error"] = result.Error,
				["profile"] = settings.profileSettings.GetProfileName(settings.activeProfileId),
				["builder"] = settings.ActivePlayerDataBuilder.Name
			});
		}
	}
}

#endif
