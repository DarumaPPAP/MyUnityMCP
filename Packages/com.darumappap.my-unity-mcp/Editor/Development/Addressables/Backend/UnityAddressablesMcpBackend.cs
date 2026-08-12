#if UNITY_EDITOR && MYUNITYMCP_ADDRESSABLES

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace UnityAddressablesMcp
{
	internal sealed class UnityAddressablesMcpBackend : IUnityAddressablesMcpBackend
	{
		private const string DOMAIN_ID = "unity_addressables_mcp";

		public UnityDomainMcpResult Inspect(bool packageInstalled, string packageVersion)
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
			return UnityAddressablesMcpBridge.Result("addressables.inspect", E_DOMAIN_TOOL_STATUS.SUCCESS, "Addressables環境を取得しました。", new JObject
			{
				["packageInstalled"] = packageInstalled,
				["packageVersion"] = packageVersion,
				["backendCompiled"] = true,
				["settingsExists"] = settings != null,
				["activeProfileId"] = settings?.activeProfileId,
				["activeProfileName"] = settings == null ? null : settings.profileSettings.GetProfileName(settings.activeProfileId),
				["activeBuilder"] = settings?.ActivePlayerDataBuilder == null ? null : settings.ActivePlayerDataBuilder.Name,
				["groups"] = groups,
				["settingsAutoCreated"] = false,
				["contentBuildAvailableThroughMcp"] = false
			});
		}

		public UnityDomainMcpResult PrepareEntry(string assetPath, string groupName, string address, string[] labels, long? expectedRevision)
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				return UnityAddressablesMcpBridge.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Settingsが存在しません。自動生成は行いません。");
			}
			if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
			{
				return UnityAddressablesMcpBridge.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "assetPathが存在しません。");
			}
			AddressableAssetGroup group = settings.FindGroup(groupName);
			if (group == null)
			{
				return UnityAddressablesMcpBridge.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "指定Groupが存在しません。自動生成は行いません。");
			}
			if (group.ReadOnly)
			{
				return UnityAddressablesMcpBridge.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Read-only Groupは変更できません。");
			}
			if (string.IsNullOrWhiteSpace(address))
			{
				return UnityAddressablesMcpBridge.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "addressが必要です。");
			}
			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			return UnityAddressablesMcpBridge.Prepare("addressables.prepare_entry", DOMAIN_ID, "create_or_move_entry", expectedRevision, true, new JObject
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
			if (!UnityAddressablesMcpBridge.TryConsume("addressables.apply_entry", DOMAIN_ID, planId, currentRevision, approvalToken, out UnityAddressablesMcpApprovedPlan plan, out UnityDomainMcpResult failure))
			{
				return failure;
			}
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				return UnityAddressablesMcpBridge.Error("addressables.apply_entry", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Settingsが存在しません。");
			}
			AddressableAssetGroup group = settings.FindGroup(plan.Payload.Value<string>("groupName"));
			if (group == null || group.ReadOnly)
			{
				return UnityAddressablesMcpBridge.Error("addressables.apply_entry", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "対象Groupが変更されたか利用できません。");
			}
			Undo.RecordObject(settings, "MyUnityMCP Addressables Entry");
			AddressableAssetEntry entry = settings.CreateOrMoveEntry(plan.Payload.Value<string>("assetGuid"), group, false, false);
			entry.address = plan.Payload.Value<string>("address");
			foreach (string label in plan.Payload["labels"].Values<string>())
			{
				entry.SetLabel(label, true, true, false);
			}
			EditorUtility.SetDirty(settings);
			UnityAddressablesMcpBridge.CompleteMutation(settings);
			return UnityAddressablesMcpBridge.Result("addressables.apply_entry", E_DOMAIN_TOOL_STATUS.SUCCESS, "Addressables Entryを更新しました。SettingsはDirtyですが自動Saveしていません。", new JObject
			{
				["assetGuid"] = entry.guid,
				["address"] = entry.address,
				["groupName"] = group.Name,
				["labels"] = new JArray(entry.labels),
				["savePerformed"] = false,
				["contentBuildPerformed"] = false
			});
		}
	}
}

#endif
