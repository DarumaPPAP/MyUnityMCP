#if UNITY_EDITOR

using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityUiMcp
{
	public sealed class UnityUiMcpVector2Input
	{
		public float x;
		public float y;
	}

	[McpForUnityTool("ui.inspect", Description = "Loaded SceneのCanvas、RectTransform、UIDocumentをRead-onlyで取得します。", AutoRegister = false, Group = "ui")]
	public static class UiInspectTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Inactive Objectを含めるか。", Required = false)] public bool? includeInactive { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityUiMcpRuntime.Inspect(value.includeInactive ?? true));
	}

	[McpForUnityTool("ui.validate", Description = "Canvas重複、無効なRectTransform、UIDocument設定不足をRead-onlyで検証します。", AutoRegister = false, Group = "ui")]
	public static class UiValidateTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityUiMcpRuntime.Validate());
	}

	[McpForUnityTool("ui.prepare_rect_transform", Description = "RectTransformのAnchored Position、Size、Anchor、PivotをExact Previewし、承認Tokenを発行します。", AutoRegister = false, Group = "ui")]
	public static class UiPrepareRectTransformTool
	{
		public sealed class Parameters
		{
			[ToolParameter("対象RectTransformのGlobal Object ID。", Required = true)] public string targetObjectId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? expectedRevision { get; set; }
			[ToolParameter("Anchored Position。", Required = false)] public UnityUiMcpVector2Input anchoredPosition { get; set; }
			[ToolParameter("Size Delta。", Required = false)] public UnityUiMcpVector2Input sizeDelta { get; set; }
			[ToolParameter("Anchor Min。", Required = false)] public UnityUiMcpVector2Input anchorMin { get; set; }
			[ToolParameter("Anchor Max。", Required = false)] public UnityUiMcpVector2Input anchorMax { get; set; }
			[ToolParameter("Pivot。", Required = false)] public UnityUiMcpVector2Input pivot { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityUiMcpRuntime.PrepareRectTransform(value.targetObjectId, value.anchoredPosition, value.sizeDelta, value.anchorMin, value.anchorMax, value.pivot, value.expectedRevision));
	}

	[McpForUnityTool("ui.apply_rect_transform", Description = "承認済みRectTransform PlanをUndo対応で適用します。Saveは行いません。", AutoRegister = false, Group = "ui")]
	public static class UiApplyRectTransformTool
	{
		public sealed class Parameters
		{
			[ToolParameter("ui.prepare_rect_transformが返したPlan ID。", Required = true)] public string planId { get; set; }
			[ToolParameter("現在のEditor Revision。", Required = true)] public long? currentRevision { get; set; }
			[ToolParameter("Approval Token。", Required = true)] public string approvalToken { get; set; }
		}
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, value => UnityUiMcpRuntime.ApplyRectTransform(value.planId, value.currentRevision, value.approvalToken));
	}

	[McpForUnityTool("ui.get_support_matrix", Description = "UI MCPのUGUI／UI Toolkit対応範囲を取得します。", AutoRegister = false, Group = "ui")]
	public static class UiGetSupportMatrixTool
	{
		public sealed class Parameters { }
		public static object HandleCommand(JObject @params) => UnityDomainMcpCommon.Execute<Parameters>(@params, _ => UnityUiMcpRuntime.GetSupportMatrix());
	}

	public static class UnityUiMcpRuntime
	{
		private const string DOMAIN_ID = "unity_ui_mcp";

		public static UnityDomainMcpResult Inspect(bool includeInactive)
		{
			Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>()
				.Where(value => IsSceneObject(value) && (includeInactive || value.gameObject.activeInHierarchy))
				.OrderBy(value => value.gameObject.scene.path)
				.ThenBy(value => HierarchyPath(value.transform))
				.ToArray();
			RectTransform[] rectTransforms = Resources.FindObjectsOfTypeAll<RectTransform>()
				.Where(value => IsSceneObject(value) && (includeInactive || value.gameObject.activeInHierarchy))
				.OrderBy(value => value.gameObject.scene.path)
				.ThenBy(value => HierarchyPath(value))
				.ToArray();
			UIDocument[] documents = Resources.FindObjectsOfTypeAll<UIDocument>()
				.Where(value => IsSceneObject(value) && (includeInactive || value.gameObject.activeInHierarchy))
				.OrderBy(value => value.gameObject.scene.path)
				.ThenBy(value => HierarchyPath(value.transform))
				.ToArray();

			return UnityDomainMcpCommon.Result("ui.inspect", E_DOMAIN_TOOL_STATUS.SUCCESS, "Scene UIを取得しました。", new JObject
			{
				["canvases"] = new JArray(canvases.Select(value => new JObject
				{
					["objectId"] = UnityDomainMcpCommon.ObjectId(value),
					["name"] = value.name,
					["scenePath"] = value.gameObject.scene.path,
					["hierarchyPath"] = HierarchyPath(value.transform),
					["renderMode"] = value.renderMode.ToString(),
					["sortingOrder"] = value.sortingOrder,
					["overrideSorting"] = value.overrideSorting,
					["enabled"] = value.enabled
				})),
				["rectTransforms"] = new JArray(rectTransforms.Select(RectTransformData)),
				["uiDocuments"] = new JArray(documents.Select(value => new JObject
				{
					["objectId"] = UnityDomainMcpCommon.ObjectId(value),
					["name"] = value.name,
					["scenePath"] = value.gameObject.scene.path,
					["hierarchyPath"] = HierarchyPath(value.transform),
					["panelSettings"] = value.panelSettings == null ? null : AssetDatabase.GetAssetPath(value.panelSettings),
					["sourceAsset"] = value.visualTreeAsset == null ? null : AssetDatabase.GetAssetPath(value.visualTreeAsset),
					["sortingOrder"] = value.sortingOrder,
					["enabled"] = value.enabled
				})),
				["screenSpaceOverlayHandledByUnity"] = true
			});
		}

		public static UnityDomainMcpResult Validate()
		{
			JArray findings = new JArray();
			foreach (UIDocument document in Resources.FindObjectsOfTypeAll<UIDocument>().Where(IsSceneObject))
			{
				if (document.panelSettings == null)
				{
					findings.Add(Finding("UI-DOCUMENT-PANEL-MISSING", "ERROR", document, "UIDocumentにPanelSettingsがありません。"));
				}
				if (document.visualTreeAsset == null)
				{
					findings.Add(Finding("UI-DOCUMENT-TREE-MISSING", "WARNING", document, "UIDocumentにVisualTreeAssetがありません。"));
				}
			}
			foreach (RectTransform rectTransform in Resources.FindObjectsOfTypeAll<RectTransform>().Where(IsSceneObject))
			{
				if (rectTransform.anchorMin.x > rectTransform.anchorMax.x || rectTransform.anchorMin.y > rectTransform.anchorMax.y)
				{
					findings.Add(Finding("UI-ANCHOR-RANGE-INVALID", "ERROR", rectTransform, "anchorMinがanchorMaxを超えています。"));
				}
				if (!Finite(rectTransform.anchoredPosition) || !Finite(rectTransform.sizeDelta))
				{
					findings.Add(Finding("UI-RECT-NON-FINITE", "ERROR", rectTransform, "RectTransformに非有限値があります。"));
				}
			}
			return UnityDomainMcpCommon.Result("ui.validate", findings.Any(value => value.Value<string>("severity") == "ERROR") ? E_DOMAIN_TOOL_STATUS.PARTIAL : E_DOMAIN_TOOL_STATUS.SUCCESS, "UI Validationを完了しました。", new JObject
			{
				["findingCount"] = findings.Count,
				["findings"] = findings
			});
		}

		public static UnityDomainMcpResult PrepareRectTransform(
			string targetObjectId,
			UnityUiMcpVector2Input anchoredPosition,
			UnityUiMcpVector2Input sizeDelta,
			UnityUiMcpVector2Input anchorMin,
			UnityUiMcpVector2Input anchorMax,
			UnityUiMcpVector2Input pivot,
			long? expectedRevision)
		{
			if (!UnityDomainMcpCommon.TryResolveObject(targetObjectId, out RectTransform target))
			{
				return UnityDomainMcpCommon.Error("ui.prepare_rect_transform", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "対象RectTransformが見つかりません。");
			}
			if (anchoredPosition == null && sizeDelta == null && anchorMin == null && anchorMax == null && pivot == null)
			{
				return UnityDomainMcpCommon.Error("ui.prepare_rect_transform", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "変更値がありません。");
			}
			Vector2 requestedAnchorMin = anchorMin == null ? target.anchorMin : ToVector2(anchorMin);
			Vector2 requestedAnchorMax = anchorMax == null ? target.anchorMax : ToVector2(anchorMax);
			Vector2 requestedPivot = pivot == null ? target.pivot : ToVector2(pivot);
			if (!Finite(requestedAnchorMin) || !Finite(requestedAnchorMax) || !Finite(requestedPivot) ||
				requestedAnchorMin.x > requestedAnchorMax.x || requestedAnchorMin.y > requestedAnchorMax.y ||
				requestedAnchorMin.x < 0f || requestedAnchorMin.y < 0f || requestedAnchorMax.x > 1f || requestedAnchorMax.y > 1f ||
				requestedPivot.x < 0f || requestedPivot.x > 1f || requestedPivot.y < 0f || requestedPivot.y > 1f)
			{
				return UnityDomainMcpCommon.Error("ui.prepare_rect_transform", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Anchor／Pivot値が不正です。");
			}

			return UnityDomainMcpCommon.Prepare("ui.prepare_rect_transform", DOMAIN_ID, "update_rect_transform", expectedRevision, true, new JObject
			{
				["targetObjectId"] = targetObjectId,
				["baseline"] = RectTransformData(target),
				["requested"] = new JObject
				{
					["anchoredPosition"] = VectorData(anchoredPosition == null ? target.anchoredPosition : ToVector2(anchoredPosition)),
					["sizeDelta"] = VectorData(sizeDelta == null ? target.sizeDelta : ToVector2(sizeDelta)),
					["anchorMin"] = VectorData(requestedAnchorMin),
					["anchorMax"] = VectorData(requestedAnchorMax),
					["pivot"] = VectorData(requestedPivot)
				},
				["savePerformed"] = false
			});
		}

		public static UnityDomainMcpResult ApplyRectTransform(string planId, long? currentRevision, string approvalToken)
		{
			if (!currentRevision.HasValue)
			{
				return UnityDomainMcpCommon.Error("ui.apply_rect_transform", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "currentRevisionが必要です。");
			}
			if (!UnityDomainMcpPlanStore.TryConsume("ui.apply_rect_transform", DOMAIN_ID, planId, currentRevision.Value, approvalToken, out UnityDomainMcpPlan plan, out UnityDomainMcpResult failure))
			{
				return failure;
			}
			if (!UnityDomainMcpCommon.TryResolveObject(plan.Payload.Value<string>("targetObjectId"), out RectTransform target))
			{
				return UnityDomainMcpCommon.Error("ui.apply_rect_transform", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "対象RectTransformが見つかりません。");
			}
			JObject requested = (JObject)plan.Payload["requested"];
			Undo.RecordObject(target, "MyUnityMCP UI RectTransform");
			target.anchoredPosition = ReadVector(requested["anchoredPosition"]);
			target.sizeDelta = ReadVector(requested["sizeDelta"]);
			target.anchorMin = ReadVector(requested["anchorMin"]);
			target.anchorMax = ReadVector(requested["anchorMax"]);
			target.pivot = ReadVector(requested["pivot"]);
			UnityDomainMcpCommon.CompleteMutation(target);
			return UnityDomainMcpCommon.Result("ui.apply_rect_transform", E_DOMAIN_TOOL_STATUS.SUCCESS, "RectTransformを適用しました。Scene Saveは行っていません。", new JObject
			{
				["target"] = RectTransformData(target),
				["savePerformed"] = false
			});
		}

		public static UnityDomainMcpResult GetSupportMatrix()
		{
			return UnityDomainMcpCommon.Result("ui.get_support_matrix", E_DOMAIN_TOOL_STATUS.UNVERIFIED, "UI MCPの対応状況です。", new JObject
			{
				["implemented"] = new JArray("Canvas inspection", "RectTransform inspection and validation", "UIDocument inspection", "approval-gated RectTransform mutation"),
				["screenSpaceOverlay"] = "standard_unity_rendering_not_reimplemented",
				["unverified"] = new JArray("TextMeshPro semantic inspection", "complex layout component editing", "runtime UI interaction automation")
			});
		}

		private static JObject RectTransformData(RectTransform value)
		{
			return new JObject
			{
				["objectId"] = UnityDomainMcpCommon.ObjectId(value),
				["name"] = value.name,
				["scenePath"] = value.gameObject.scene.path,
				["hierarchyPath"] = HierarchyPath(value),
				["anchoredPosition"] = VectorData(value.anchoredPosition),
				["sizeDelta"] = VectorData(value.sizeDelta),
				["anchorMin"] = VectorData(value.anchorMin),
				["anchorMax"] = VectorData(value.anchorMax),
				["pivot"] = VectorData(value.pivot),
				["active"] = value.gameObject.activeSelf
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
				["hierarchyPath"] = HierarchyPath(target.transform),
				["scenePath"] = target.gameObject.scene.path
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

		private static bool Finite(Vector2 value)
		{
			return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
		}

		private static Vector2 ToVector2(UnityUiMcpVector2Input value)
		{
			return new Vector2(value.x, value.y);
		}

		private static JObject VectorData(Vector2 value)
		{
			return new JObject { ["x"] = value.x, ["y"] = value.y };
		}

		private static Vector2 ReadVector(JToken value)
		{
			return new Vector2(value.Value<float>("x"), value.Value<float>("y"));
		}
	}
}

#endif
