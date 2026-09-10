#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGraphicsMcp
{
	public enum E_UNITY_API_PATCH_BUCKET
	{
		BASE,
		UNITY_6000_4,
		UNITY_6000_5,
		UNITY_6000_7
	}

	public enum E_UNITY_API_SOURCE_STATUS
	{
		CONFIRMED,
		PLANNED
	}

	public sealed class UnityApiCompatibilityRule
	{
		public string ruleId { get; set; }
		public E_UNITY_API_PATCH_BUCKET patchBucket { get; set; }
		public string category { get; set; }
		public string legacyApi { get; set; }
		public string replacement { get; set; }
		public string preferredFrom { get; set; }
		public string warningFrom { get; set; }
		public string errorFrom { get; set; }
		public string removedFrom { get; set; }
		public string behaviorChangeFrom { get; set; }
		public E_UNITY_API_SOURCE_STATUS sourceStatus { get; set; }
		public string note { get; set; }
	}

	/// <summary>
	/// Unity API更新をVersionごとのFile分岐ではなく、Base + 6.4 + 6.5 + 6.7の保守Bucketへ集約します。
	/// patchBucketは保守単位、preferred/warning/error/removed/behaviorChangeFromは実際の適用開始Versionです。
	/// そのため6.6由来の変更を6000.7 Bucketへ集約しつつ、6.6で必要な判定を失いません。
	/// </summary>
	public static class ApiCompatibility
	{
		private static readonly Version VERSION_6000_0 = new Version(6000, 0, 0);

		private static readonly List<UnityApiCompatibilityRule> RULES =
			new List<UnityApiCompatibilityRule>
			{
				// Base: 古いUnityでも利用できる現行形へ先行移行します。
				CreateRule(
					"BASE-COMPONENT-GETCOMPONENT",
					E_UNITY_API_PATCH_BUCKET.BASE,
					"Core",
					"Component/GameObject renderer, camera, audio, collider等のLegacy shortcut",
					"GetComponent<T>()またはキャッシュ参照",
					"6000.0", null, "6000.5", "6000.5", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"Version Patchへ先送りせずBaseコードを現行形へ寄せます。"),
				CreateRule(
					"BASE-GAMEOBJECT-ACTIVE",
					E_UNITY_API_PATCH_BUCKET.BASE,
					"Core",
					"GameObject.active / SetActiveRecursively",
					"activeSelf / activeInHierarchy / SetActive",
					"6000.0", null, "6000.5", null, null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"activeSelfとactiveInHierarchyは意味が異なるため自動置換せず用途を選択します。"),
				CreateRule(
					"BASE-UITOOLKIT-UXML-AUTHORING",
					E_UNITY_API_PATCH_BUCKET.BASE,
					"UI",
					"UxmlFactory / UxmlTraits",
					"UxmlElement / UxmlAttributeによる属性ベースAuthoring",
					"6000.0", null, null, "6000.6", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"Unity 6.0から利用できる現行UXML方式をBaseで優先します。"),
				CreateRule(
					"BASE-URP-RENDERGRAPH",
					E_UNITY_API_PATCH_BUCKET.BASE,
					"Rendering",
					"URP Compatibility Mode前提のScriptableRenderPass",
					"RecordRenderGraphベースのRenderGraph実装",
					"6000.0", null, null, "6000.4", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"URP新規実装はCompatibility Modeへ戻さずRenderGraphをBaseとします。"),

				// Unity 6.4 maintenance bucket.
				CreateRule(
					"UNITY-6000-4-OBJECT-ENTITY-ID",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_4,
					"Core",
					"Object.GetInstanceID() / int InstanceID依存",
					"Object.GetEntityId() / EntityId",
					"6000.2", "6000.4", "6000.5", null, null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"EntityIdはDictionary/SetのKeyとしてそのまま扱い、intへ戻さない方針です。"),
				CreateRule(
					"UNITY-6000-4-EDITOR-PING-ENTITY-ID",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_4,
					"Editor",
					"EditorUtility.PingObject(int)",
					"EditorUtility.PingObject(EntityId)",
					"6000.4", "6000.4", "6000.5", null, null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"Editor拡張のint InstanceID経路をEntityIdへ切り替えます。"),
				CreateRule(
					"UNITY-6000-4-ASSET-PREVIEW-ENTITY-ID",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_4,
					"Editor",
					"AssetPreview.IsLoadingAssetPreview(int)",
					"AssetPreview.IsLoadingAssetPreview(EntityId)",
					"6000.4", "6000.4", "6000.5", null, null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"Asset Preview監視のID型を更新します。"),
				CreateRule(
					"UNITY-6000-4-SERIALIZED-PROPERTY-ENTITY-ID",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_4,
					"Editor",
					"SerializedProperty.objectReferenceInstanceIDValue",
					"SerializedProperty.objectReferenceEntityIdValue",
					"6000.4", "6000.4", "6000.5", null, null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"独自InspectorのObject参照ID取得を更新します。"),
				CreateRule(
					"UNITY-6000-4-EDITOR-WINDOW-CALLBACK-ENTITY-ID",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_4,
					"Editor",
					"hierarchyWindowItemOnGUI / projectWindowItemInstanceOnGUI",
					"hierarchyWindowItemOnGUIEntityId / projectWindowItemInstanceOnGUIEntityId",
					"6000.4", "6000.4", "6000.5", null, null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"Hierarchy / Project Window拡張をEntityId callbackへ移行します。"),
				CreateRule(
					"UNITY-6000-4-ENTITY-ID-RAW-DATA",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_4,
					"Core",
					"EntityId.GetRawData() / EntityId.Equals(int)",
					"EntityId.ToULong() / EntityId同士の比較",
					"6000.4", "6000.4", null, null, null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"EntityIdとintの混在を禁止します。"),
				CreateRule(
					"UNITY-6000-4-URP-COMPATIBILITY-MODE",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_4,
					"Rendering",
					"URP_COMPATIBILITY_MODE / Compatibility Mode",
					"RecordRenderGraph",
					"6000.0", null, null, "6000.4", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"6000.4以降ではCompatibility Modeの存在確認へFallbackしません。"),
				CreateRule(
					"UNITY-6000-4-SCENE-HANDLE-RAW-DATA",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_4,
					"Core",
					"SceneHandleとint/uintの暗黙変換",
					"SceneHandle.GetRawData() / SceneHandle.FromRawData(ulong)、またはMyUnityMCP Session Token",
					"6000.4", "6000.4", "6000.5", null, null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"6000.4.12f1でCS0618 warning、6000.5.5f1でCS0619 errorをEditor CI実測。6.7 manual testでもerrorを確認。"),

				// Unity 6.5 maintenance bucket.
				CreateRule(
					"UNITY-6000-5-LEGACY-COMPONENT-REMOVAL",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_5,
					"Core",
					"Component/GameObject Legacy shortcuts",
					"GetComponent<T>()",
					"6000.0", null, "6000.5", "6000.5", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"Base Modernizationで先に除去し、6.5では残存をBlocking扱いします。"),
				CreateRule(
					"UNITY-6000-5-ENTITIES-FOREACH",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_5,
					"ECS",
					"Entities.ForEach / Job.WithCode",
					"IJobEntity / SystemAPI.Query",
					"6000.0", null, null, "6000.5", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"Entities Package Versionも同時に確認します。"),
				CreateRule(
					"UNITY-6000-5-ENTITIES-ASPECTS",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_5,
					"ECS",
					"Deprecated Aspects / IAspectQuery",
					"SystemAPI.Query等の現行Entities API",
					"6000.0", null, null, "6000.5", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"Package APIはEditor Versionだけで断定しません。"),
				CreateRule(
					"UNITY-6000-5-MODEL-IMPORTER",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_5,
					"Editor",
					"isFileScaleUsed / normalImportMode / optimizeMesh",
					"scaleInFile / importNormals / optimizeMeshOnImport",
					"6000.0", null, null, "6000.5", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"AssetPostprocessorとImporter自動化を優先監査します。"),
				CreateRule(
					"UNITY-6000-5-LEGACY-XR",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_5,
					"XR",
					"Legacy XR Module依存",
					"OpenXR / 各XR Plug-in",
					"6000.0", null, null, "6000.5", null,
					E_UNITY_API_SOURCE_STATUS.CONFIRMED,
					"XRはPackage VersionとFeature設定を併せて検証します。"),

				// Unity 6.7 roll-up bucket. 6.6由来の変更もここで保守します。
				CreateRule(
					"UNITY-6000-7-ROLLUP-UNITY-64",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"Build",
					"UNITY_64",
					"IntPtr.Sizeによるbitness判定",
					"6000.6", "6000.6", null, null, null,
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"6.6由来の変更を独立Patchにせず6.7 Roll-upで管理します。"),
				CreateRule(
					"UNITY-6000-7-ROLLUP-DEVELOPMENT-BUILD",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"Build",
					"DEVELOPMENT_BUILDの多目的利用",
					"UNITY_ENABLE_CHECKS / UNITY_INCLUDE_INSTRUMENTATION等へ用途分離",
					"6000.6", "6000.6", null, null, null,
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"安全性CheckとInstrumentationを同一条件にしません。"),
				CreateRule(
					"UNITY-6000-7-ROLLUP-UXML-FACTORY",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"UI",
					"UxmlFactory / UxmlTraits",
					"UxmlElement / UxmlAttribute",
					"6000.0", null, null, "6000.6", null,
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"Baseで先行移行し、6.6残存はRoll-up Bucketで検出します。"),
				CreateRule(
					"UNITY-6000-7-ROLLUP-DYNAMIC-BATCHING",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"Rendering",
					"Dynamic Batching依存",
					"SRP Batcher / GPU Instancing / Static Batchingを計測して選択",
					"6000.6", null, null, "6000.6", null,
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"置換方式は一律に決めず実機計測を要求します。"),
				CreateRule(
					"UNITY-6000-7-ROLLUP-HIERARCHY-API",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"Editor",
					"Unity.Hierarchy obsolete API群",
					"各Obsolete messageで指定された現行Hierarchy API",
					"6000.6", null, "6000.6", "6000.7", null,
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"6.6でError化、6.7で完全削除予定のため同一Roll-upで追跡します。"),
				CreateRule(
					"UNITY-6000-7-INPUT-SYSTEM-DETECTION",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"Build",
					"com.unity.inputsystem Package存在 / asmdef Version Define判定",
					"ENABLE_INPUT_SYSTEM",
					"6000.7", null, null, null, "6000.7",
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"Input System built-in化に備えPackage存在をCapability判定に使いません。"),
				CreateRule(
					"UNITY-6000-7-AR-MODULE",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"XR",
					"PlayerSettings.Android.ARCoreEnabled / ARCoreUpdate",
					"ARCore XR Plug-in / 不要な旧呼び出し削除",
					"6000.7", null, null, "6000.7", null,
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"AR Module removalは6.7へ延期された予定変更として扱います。"),
				CreateRule(
					"UNITY-6000-7-RENDERGRAPH-Y-FLIP",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"Rendering",
					"RenderGraph手動Y-flip workaround",
					"UvOrigin対応済みHelperへ委譲",
					"6000.7", null, null, null, "6000.7",
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"二重Y-flipによる上下反転を監査します。"),
				CreateRule(
					"UNITY-6000-7-RENDERGRAPH-BLIT-SLICE",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"Rendering",
					"RenderGraph Blit destination slice既定値0前提",
					"既定値-1を理解し、array slice 0が必要なら明示指定",
					"6000.7", null, null, null, "6000.7",
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"Texture Array / XR Textureを重点検証します。"),
				CreateRule(
					"UNITY-6000-7-NETCODE-CONFIG",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"ECS",
					"EntityManager.CreateSingleton<NetcodeConfig>()",
					"SystemAPI.GetSingleton<NetcodeConfig>()",
					"6000.7", null, "6000.7", null, null,
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"Netcode for Entities Package Versionも同時に確認します。"),
				CreateRule(
					"UNITY-6000-7-LEGACY-API-SWEEP",
					E_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
					"Core",
					"Unity 6.0以前からobsoleteの旧API群",
					"各Obsolete messageで指定された現行API",
					"6000.7", null, "6000.7", null, null,
					E_UNITY_API_SOURCE_STATUS.PLANNED,
					"大量Error化に備え、更新前にObsolete warningをゼロへ近づけます。")
			};

		public static IReadOnlyList<UnityApiCompatibilityRule> Rules => RULES;

		public static Dictionary<string, object> BuildProjectSummary(string unityVersion)
		{
			Version targetVersion = ParseUnityVersion(unityVersion);
			List<UnityApiCompatibilityRule> applicableRules = GetApplicableRules(targetVersion);
			List<string> maintenanceBuckets = ResolveMaintenanceBuckets(applicableRules);
			List<Dictionary<string, object>> ruleSummaries =
				new List<Dictionary<string, object>>(applicableRules.Count);
			int confirmedCount = 0;
			int plannedCount = 0;

			foreach (UnityApiCompatibilityRule rule in applicableRules)
			{
				if (rule.sourceStatus == E_UNITY_API_SOURCE_STATUS.CONFIRMED)
				{
					confirmedCount++;
				}
				else
				{
					plannedCount++;
				}

				ruleSummaries.Add(new Dictionary<string, object>
				{
					{ "ruleId", rule.ruleId },
					{ "patchBucket", rule.patchBucket.ToString() },
					{ "category", rule.category },
					{ "state", ResolveState(targetVersion, rule) },
					{ "legacyApi", rule.legacyApi },
					{ "replacement", rule.replacement },
					{ "sourceStatus", rule.sourceStatus.ToString() }
				});
			}

			return new Dictionary<string, object>
			{
				{ "schemaVersion", "1.0" },
				{ "unityVersion", unityVersion },
				{ "normalizedVersion", targetVersion.ToString(3) },
				{ "maintenanceBuckets", maintenanceBuckets },
				{ "confirmedRuleCount", confirmedCount },
				{ "plannedRuleCount", plannedCount },
				{ "rules", ruleSummaries },
				{ "policy", "BASE_THEN_6000_4_THEN_6000_5_THEN_6000_7_ROLLUP" }
			};
		}

		public static List<UnityApiCompatibilityRule> GetApplicableRules(string unityVersion)
		{
			return GetApplicableRules(ParseUnityVersion(unityVersion));
		}

		public static Version ParseUnityVersion(string unityVersion)
		{
			if (string.IsNullOrWhiteSpace(unityVersion))
			{
				return VERSION_6000_0;
			}

			string[] parts = unityVersion.Trim().Split('.');
			int major = parts.Length > 0 ? ParseNumericPrefix(parts[0]) : 6000;
			int minor = parts.Length > 1 ? ParseNumericPrefix(parts[1]) : 0;
			int patch = parts.Length > 2 ? ParseNumericPrefix(parts[2]) : 0;

			if (major <= 0)
			{
				major = 6000;
			}

			return new Version(major, Math.Max(0, minor), Math.Max(0, patch));
		}

		private static List<UnityApiCompatibilityRule> GetApplicableRules(Version targetVersion)
		{
			List<UnityApiCompatibilityRule> result = new List<UnityApiCompatibilityRule>();
			foreach (UnityApiCompatibilityRule rule in RULES)
			{
				Version preferredVersion = ParseOptionalVersion(rule.preferredFrom);
				Version warningVersion = ParseOptionalVersion(rule.warningFrom);
				Version errorVersion = ParseOptionalVersion(rule.errorFrom);
				Version removedVersion = ParseOptionalVersion(rule.removedFrom);
				Version behaviorVersion = ParseOptionalVersion(rule.behaviorChangeFrom);

				Version firstRelevantVersion = MinVersion(
					preferredVersion,
					warningVersion,
					errorVersion,
					removedVersion,
					behaviorVersion);

				if (firstRelevantVersion == null || targetVersion >= firstRelevantVersion)
				{
					result.Add(rule);
				}
			}

			return result;
		}

		private static List<string> ResolveMaintenanceBuckets(
			List<UnityApiCompatibilityRule> applicableRules)
		{
			List<string> result = new List<string>();
			foreach (E_UNITY_API_PATCH_BUCKET bucket in Enum.GetValues(typeof(E_UNITY_API_PATCH_BUCKET)))
			{
				for (int index = 0; index < applicableRules.Count; index++)
				{
					if (applicableRules[index].patchBucket == bucket)
					{
						result.Add(bucket.ToString());
						break;
					}
				}
			}

			return result;
		}

		private static string ResolveState(Version targetVersion, UnityApiCompatibilityRule rule)
		{
			if (IsReached(targetVersion, rule.removedFrom))
			{
				return rule.sourceStatus == E_UNITY_API_SOURCE_STATUS.PLANNED
					? "PLANNED_REMOVED"
					: "REMOVED";
			}

			if (IsReached(targetVersion, rule.errorFrom))
			{
				return rule.sourceStatus == E_UNITY_API_SOURCE_STATUS.PLANNED
					? "PLANNED_ERROR"
					: "ERROR";
			}

			if (IsReached(targetVersion, rule.behaviorChangeFrom))
			{
				return rule.sourceStatus == E_UNITY_API_SOURCE_STATUS.PLANNED
					? "PLANNED_BEHAVIOR_CHANGE"
					: "BEHAVIOR_CHANGE";
			}

			if (IsReached(targetVersion, rule.warningFrom))
			{
				return rule.sourceStatus == E_UNITY_API_SOURCE_STATUS.PLANNED
					? "PLANNED_WARNING"
					: "WARNING";
			}

			return rule.sourceStatus == E_UNITY_API_SOURCE_STATUS.PLANNED
				? "PLANNED_PREFERRED"
				: "PREFERRED";
		}

		private static bool IsReached(Version targetVersion, string version)
		{
			Version parsed = ParseOptionalVersion(version);
			return parsed != null && targetVersion >= parsed;
		}

		private static Version ParseOptionalVersion(string version)
		{
			return string.IsNullOrWhiteSpace(version) ? null : ParseUnityVersion(version);
		}

		private static Version MinVersion(params Version[] versions)
		{
			Version result = null;
			foreach (Version version in versions)
			{
				if (version != null && (result == null || version < result))
				{
					result = version;
				}
			}
			return result;
		}

		private static int ParseNumericPrefix(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return 0;
			}

			int length = 0;
			while (length < value.Length && char.IsDigit(value[length]))
			{
				length++;
			}

			if (length == 0)
			{
				return 0;
			}

			int result;
			return int.TryParse(value.Substring(0, length), out result) ? result : 0;
		}

		private static UnityApiCompatibilityRule CreateRule(
			string ruleId,
			E_UNITY_API_PATCH_BUCKET patchBucket,
			string category,
			string legacyApi,
			string replacement,
			string preferredFrom,
			string warningFrom,
			string errorFrom,
			string removedFrom,
			string behaviorChangeFrom,
			E_UNITY_API_SOURCE_STATUS sourceStatus,
			string note)
		{
			return new UnityApiCompatibilityRule
			{
				ruleId = ruleId,
				patchBucket = patchBucket,
				category = category,
				legacyApi = legacyApi,
				replacement = replacement,
				preferredFrom = preferredFrom,
				warningFrom = warningFrom,
				errorFrom = errorFrom,
				removedFrom = removedFrom,
				behaviorChangeFrom = behaviorChangeFrom,
				sourceStatus = sourceStatus,
				note = note
			};
		}
	}
}

#endif