#if UNITY_EDITOR

using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

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

	internal interface IUnityAddressablesMcpBackend
	{
		UnityDomainMcpResult Inspect(PackageInfo package);
		UnityDomainMcpResult PrepareEntry(string assetPath, string groupName, string address, string[] labels, long? expectedRevision);
		UnityDomainMcpResult ApplyEntry(string planId, long? currentRevision, string approvalToken);
		UnityDomainMcpResult PrepareContentBuild(long? expectedRevision);
		UnityDomainMcpResult BuildContent(string planId, long? currentRevision, string approvalToken);
	}

	public static class UnityAddressablesMcpRuntime
	{
		private const string PACKAGE_NAME = "com.unity.addressables";
		private const string BACKEND_TYPE_NAME = "UnityAddressablesMcp.UnityAddressablesMcpBackend, MyUnityMcp.Addressables.Editor";

		public static UnityDomainMcpResult Inspect()
		{
			PackageInfo package = GetPackage();
			IUnityAddressablesMcpBackend backend = GetBackend();
			if (backend != null)
			{
				return backend.Inspect(package);
			}
			return Unsupported("addressables.inspect", package);
		}

		public static UnityDomainMcpResult PrepareEntry(string assetPath, string groupName, string address, string[] labels, long? expectedRevision)
		{
			IUnityAddressablesMcpBackend backend = GetBackend();
			return backend == null
				? Unsupported("addressables.prepare_entry", GetPackage())
				: backend.PrepareEntry(assetPath, groupName, address, labels, expectedRevision);
		}

		public static UnityDomainMcpResult ApplyEntry(string planId, long? currentRevision, string approvalToken)
		{
			IUnityAddressablesMcpBackend backend = GetBackend();
			return backend == null
				? Unsupported("addressables.apply_entry", GetPackage())
				: backend.ApplyEntry(planId, currentRevision, approvalToken);
		}

		public static UnityDomainMcpResult PrepareContentBuild(long? expectedRevision)
		{
			IUnityAddressablesMcpBackend backend = GetBackend();
			return backend == null
				? Unsupported("addressables.prepare_content_build", GetPackage())
				: backend.PrepareContentBuild(expectedRevision);
		}

		public static UnityDomainMcpResult BuildContent(string planId, long? currentRevision, string approvalToken)
		{
			IUnityAddressablesMcpBackend backend = GetBackend();
			return backend == null
				? Unsupported("addressables.build_content", GetPackage())
				: backend.BuildContent(planId, currentRevision, approvalToken);
		}

		public static UnityDomainMcpResult GetSupportMatrix()
		{
			PackageInfo package = GetPackage();
			bool backendCompiled = GetBackend() != null;
			E_DOMAIN_TOOL_STATUS status = package == null
				? E_DOMAIN_TOOL_STATUS.UNSUPPORTED
				: backendCompiled ? E_DOMAIN_TOOL_STATUS.UNVERIFIED : E_DOMAIN_TOOL_STATUS.BACKEND_NOT_IMPLEMENTED;
			return UnityDomainMcpCommon.Result("addressables.get_support_matrix", status, "AddressablesMCPの対応状況です。", new JObject
			{
				["packageInstalled"] = package != null,
				["packageVersion"] = package?.version,
				["backendCompiled"] = backendCompiled,
				["implemented"] = new JArray("settings inspection", "entry preview", "approval-gated entry mutation", "content build preview", "approval-gated content build"),
				["automaticSettingsCreation"] = false,
				["automaticSave"] = false,
				["unverified"] = new JArray("remote content update", "platform-specific content build", "Addressables 2.x compatibility matrix")
			});
		}

		private static PackageInfo GetPackage()
		{
			return PackageInfo.GetAllRegisteredPackages().FirstOrDefault(value => string.Equals(value.name, PACKAGE_NAME, StringComparison.Ordinal));
		}

		private static IUnityAddressablesMcpBackend GetBackend()
		{
			Type type = Type.GetType(BACKEND_TYPE_NAME, false);
			return type == null ? null : Activator.CreateInstance(type) as IUnityAddressablesMcpBackend;
		}

		private static UnityDomainMcpResult Unsupported(string tool, PackageInfo package)
		{
			string summary = package == null
				? "Addressables Packageが導入されていません。"
				: "Addressables Packageは導入されていますが、対応Backendを読み込めません。";
			return UnityDomainMcpCommon.Result(tool, E_DOMAIN_TOOL_STATUS.UNSUPPORTED, summary, new JObject
			{
				["packageInstalled"] = package != null,
				["packageVersion"] = package?.version,
				["backendCompiled"] = false,
				["settingsAutoCreated"] = false
			});
		}
	}
}

#endif
