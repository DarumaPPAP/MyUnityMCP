#if UNITY_EDITOR

using System;
using Newtonsoft.Json.Linq;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Graph Engineering migration-only aliases for Graphics tool class renames performed on main.
	/// Candidate sources must migrate to the canonical class names before release_candidate promotion.
	/// </summary>
	[Obsolete("Graph Engineering migration bridge only. Use InspectProjectTool before promotion.")]
	internal static class GraphicsInspectProjectTool
	{
		public static object HandleCommand(JObject @params) => InspectProjectTool.HandleCommand(@params);
	}

	[Obsolete("Graph Engineering migration bridge only. Use InspectSceneTool before promotion.")]
	internal static class GraphicsInspectSceneTool
	{
		public static object HandleCommand(JObject @params) => InspectSceneTool.HandleCommand(@params);
	}

	[Obsolete("Graph Engineering migration bridge only. Use ValidateSceneTool before promotion.")]
	internal static class GraphicsValidateSceneTool
	{
		public static object HandleCommand(JObject @params) => ValidateSceneTool.HandleCommand(@params);
	}

	[Obsolete("Graph Engineering migration bridge only. Use GetExecutionHistoryTool before promotion.")]
	internal static class GraphicsGetExecutionHistoryTool
	{
		public static object HandleCommand(JObject @params) => GetExecutionHistoryTool.HandleCommand(@params);
	}

	[Obsolete("Graph Engineering migration bridge only. Use GetErrorCatalogTool before promotion.")]
	internal static class GraphicsGetErrorCatalogTool
	{
		public static object HandleCommand(JObject @params) => GetErrorCatalogTool.HandleCommand(@params);
	}

	[Obsolete("Graph Engineering migration bridge only. Use GetSupportMatrixTool before promotion.")]
	internal static class GraphicsGetSupportMatrixTool
	{
		public static object HandleCommand(JObject @params) => GetSupportMatrixTool.HandleCommand(@params);
	}
}

#endif
