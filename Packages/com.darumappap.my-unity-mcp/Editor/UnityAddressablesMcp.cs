#if UNITY_EDITOR

using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
#if MYUNITYMCP_ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace UnityAddressablesMcp
{
	[McpForUnityTool("addressables.inspect", Description = "Addressables Package、Settings、Profile、GroupをRead-onlyで取得します。", AutoRegister = false, Group = "addressables")]
	public static class AddressablesInspectTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityAddressablesMcpRuntime.Inspect());
	}

	[McpForUnityTool("addressables.prepare_entry", Description = "Asset GUID、Group、Address、Labelを検証し、承認待ちEntry Mutation Planを作成します。", AutoRegister = false, Group = "addressables")]
	public static class AddressablesPrepareEntryTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Assets配下のAsset Path。", Required = true)] public string assetPath { get; set; }
			[ToolParameter("既存Addressables Group名。", Required = true)] public string groupName { get; set; }
			[ToolParameter("設定するAddress。", Required = true)] public string address { get; set; }
			[ToolParameter("設定するLabel。", Required = false)] public string[] labels { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? expectedRevision { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityAddressablesMcpRuntime.PrepareEntry(value.assetPath, value.groupName, value.address, value.labels, value.expectedRevision));
	}

	[McpForUnityTool("addressables.apply_entry", Description = "承認済みPlanをAddressables Public APIで適用します。自動SaveやContent Buildは行いません。", AutoRegister = false, Group = "addressables")]
	public static class AddressablesApplyEntryTool
	{
		public sealed class Parameters
		{
			[ToolParameter("addressables.prepare_entryが返したPlan ID。", Required = true)] public string planId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? currentRevision { get; set; }
			[ToolParameter("Approval Token。", Required = true)] public string approvalToken { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityAddressablesMcpRuntime.ApplyEntry(value.planId, value.currentRevision, value.approvalToken));
	}

	[McpForUnityTool("addressables.prepare_content_build", Description = "Active Profile／Builderを検証し、承認待ちAddressables Content Build Planを作成します。", AutoRegister = false, Group = "addressables")]
	public static class AddressablesPrepareContentBuildTool
	{
		public sealed class Parameters
		{
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? expectedRevision { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityAddressablesMcpRuntime.PrepareContentBuild(value.expectedRevision));
	}

	[McpForUnityTool("addressables.build_content", Description = "承認済みPlanでAddressableAssetSettings.BuildPlayerContentを実行します。", AutoRegister = false, Group = "addressables")]
	public static class AddressablesBuildContentTool
	{
		public sealed class Parameters
		{
			[ToolParameter("addressables.prepare_content_buildが返したPlan ID。", Required = true)] public string planId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? currentRevision { get; set; }
			[ToolParameter("Approval Token。", Required = true)] public string approvalToken { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityAddressablesMcpRuntime.BuildContent(value.planId, value.currentRevision, value.approvalToken));
	}

	[McpForUnityTool("addressables.get_support_matrix", Description = "Package導入状態とAddressablesMCPの実装・未検証範囲を取得します。", AutoRegister = false, Group = "addressables")]
	public static class AddressablesGetSupportMatrixTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityAddressablesMcpRuntime.GetSupportMatrix());
	}

	public static class UnityAddressablesMcpRuntime
	{
		private const string DOMAIN_ID = "unity_addressables_mcp";
		private const string PACKAGE_NAME = "com.unity.addressables";

		public static UnityDomainMcpResult Inspect()
		{
			PackageInfo package = PackageInfo.GetAllRegisteredPackages().FirstOrDefault(value => string.Equals(value.name, PACKAGE_NAME, StringComparison.Ordinal));
#if MYUNITYMCP_ADDRESSABLES
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
#else
			return UnityDomainMcpCommon.Result("addressables.inspect", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Packageが導入されていません。", new JObject
			{
				["packageInstalled"] = package != null,
				["packageVersion"] = package?.version,
				["backendCompiled"] = false,
				["settingsAutoCreated"] = false
			});
#endif
		}

		public static UnityDomainMcpResult PrepareEntry(string assetPath, string groupName, string address, string[] labels, long? expectedRevision)
		{
#if MYUNITYMCP_ADDRESSABLES
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
#else
			return UnityDomainMcpCommon.Error("addressables.prepare_entry", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Packageが導入されていません。");
#endif
		}

		public static UnityDomainMcpResult ApplyEntry(string planId, long? currentRevision, string approvalToken)
		{
#if MYUNITYMCP_ADDRESSABLES
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
#else
			return UnityDomainMcpCommon.Error("addressables.apply_entry", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Packageが導入されていません。");
#endif
		}

		public static UnityDomainMcpResult PrepareContentBuild(long? expectedRevision)
		{
#if MYUNITYMCP_ADDRESSABLES
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
#else
			return UnityDomainMcpCommon.Error("addressables.prepare_content_build", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Packageが導入されていません。");
#endif
		}

		public static UnityDomainMcpResult BuildContent(string planId, long? currentRevision, string approvalToken)
		{
#if MYUNITYMCP_ADDRESSABLES
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
#else
			return UnityDomainMcpCommon.Error("addressables.build_content", E_DOMAIN_TOOL_STATUS.UNSUPPORTED, "Addressables Packageが導入されていません。");
#endif
		}

		public static UnityDomainMcpResult GetSupportMatrix()
		{
			PackageInfo package = PackageInfo.GetAllRegisteredPackages().FirstOrDefault(value => string.Equals(value.name, PACKAGE_NAME, StringComparison.Ordinal));
			return UnityDomainMcpCommon.Result("addressables.get_support_matrix", package == null ? E_DOMAIN_TOOL_STATUS.UNSUPPORTED : E_DOMAIN_TOOL_STATUS.UNVERIFIED, "AddressablesMCPの対応状況です。", new JObject
			{
				["packageInstalled"] = package != null,
				["packageVersion"] = package?.version,
#if MYUNITYMCP_ADDRESSABLES
				["backendCompiled"] = true,
#else
				["backendCompiled"] = false,
#endif
				["implemented"] = new JArray("settings inspection", "entry preview", "approval-gated entry mutation", "content build preview", "approval-gated content build"),
				["automaticSettingsCreation"] = false,
				["automaticSave"] = false,
				["unverified"] = new JArray("remote content update", "platform-specific content build", "Addressables 2.x compatibility matrix")
			});
		}
	}
}

#endif
