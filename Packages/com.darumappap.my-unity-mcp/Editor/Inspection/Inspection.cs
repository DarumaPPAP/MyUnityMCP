#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityGraphicsMcp
{
	public enum E_MCP_TOOL_STATUS
	{
		SUCCESS,
		PARTIAL,
		INVALID_REQUEST,
		UNSUPPORTED,
		UNVERIFIED,
		BACKEND_NOT_IMPLEMENTED,
		READ_ONLY_CONTRACT_VIOLATION,
		SESSION_EXPIRED,
		STALE_SNAPSHOT,
		STALE_DURING_SCAN,
		EDITOR_RELOADING,
		FAILED
	}

	public enum E_GRAPHICS_RULE_KIND
	{
		INVARIANT,
		POLICY,
		HEURISTIC
	}

	public enum E_FINDING_SEVERITY
	{
		INFO,
		WARNING,
		ERROR
	}

	public enum E_FINDING_CONFIDENCE
	{
		CONFIRMED,
		LIKELY,
		UNVERIFIED
	}

	public sealed class ToolResult
	{
		public string schemaVersion { get; set; } = "1.1";
		public string tool { get; set; }
		public string requestId { get; set; }
		public string sessionId { get; set; }
		public long revision { get; set; }
		public string status { get; set; }
		public string summary { get; set; }
		public object data { get; set; }
		public StructuredError error { get; set; }
		public ExecutionMetadata execution { get; set; }
		public List<Issue> issues { get; set; } = new List<Issue>();

		public bool IsSuccessful =>
			status == E_MCP_TOOL_STATUS.SUCCESS.ToString() ||
			status == E_MCP_TOOL_STATUS.PARTIAL.ToString();
	}

	public sealed class Issue
	{
		public string code { get; set; }
		public string message { get; set; }
		public object evidence { get; set; }
	}

	public sealed class Finding
	{
		public string ruleId { get; set; }
		public string kind { get; set; }
		public string severity { get; set; }
		public string confidence { get; set; }
		public string message { get; set; }
		public List<string> affectedObjectIds { get; set; } = new List<string>();
		public Dictionary<string, object> evidence { get; set; } = new Dictionary<string, object>();
	}

	public sealed class SceneItem
	{
		public string category { get; set; }
		public string objectId { get; set; }
		public string idStability { get; set; }
		public string name { get; set; }
		public string hierarchyPath { get; set; }
		public string scenePath { get; set; }
		public Dictionary<string, object> values { get; set; } = new Dictionary<string, object>();
	}

	public sealed class SceneSnapshot
	{
		public string SnapshotId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public Dictionary<string, object> Summary { get; set; } = new Dictionary<string, object>();
		public List<SceneItem> Items { get; set; } = new List<SceneItem>();
	}

	/// <summary>
	/// Graphics DomainのRead-only Project / Scene解析とValidationを所有します。
	/// </summary>
	public static partial class Inspection
	{
		private const int DEFAULT_PAGE_SIZE = 50;
		private const int MAX_PAGE_SIZE = 200;
		private const int SPECIAL_LIGHTMAP_INDEX = 0xFFFE;

		private static readonly HashSet<string> RELEVANT_PACKAGE_NAMES = new HashSet<string>
		{
			"com.coplaydev.unity-mcp",
			"com.unity.render-pipelines.core",
			"com.unity.render-pipelines.universal",
			"com.unity.render-pipelines.high-definition",
			"com.unity.cinemachine",
			"com.unity.timeline",
			"com.unity.visualeffectgraph",
			"com.unity.addressables",
			"com.unity.inputsystem"
		};

		public static ToolResult InspectProject(string requestId)
		{
			return ExecuteReadOnly(
				"graphics.inspect_project",
				requestId,
				delegate
				{
					Dictionary<string, object> pipeline = InspectRenderPipeline();
					Dictionary<string, object> data = new Dictionary<string, object>
					{
						{ "unityVersion", Application.unityVersion },
						{ "activeBuildTarget", EditorUserBuildSettings.activeBuildTarget.ToString() },
						{ "colorSpace", PlayerSettings.colorSpace.ToString() },
						{ "scriptingBackend", ResolveScriptingBackend() },
						{ "graphicsApis", ResolveGraphicsApis() },
						{ "installedBuildTargets", ResolveInstalledBuildTargets() },
						{ "renderPipeline", pipeline },
						{ "loadedScenes", InspectLoadedScenes() },
						{ "relevantPackages", InspectRelevantPackages() }
					};

					return CreateResult(
						"graphics.inspect_project",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"対象Unity Projectの環境情報をRead-onlyで取得しました。",
						data);
				});
		}

		public static ToolResult InspectScene(
			string requestId,
			bool includeInactive,
			int maxItems,
			string[] sections,
			string snapshotId,
			string cursor)
		{
			return ExecuteReadOnly(
				"graphics.inspect_scene",
				requestId,
				delegate
				{
					int pageSize = Mathf.Clamp(maxItems <= 0 ? DEFAULT_PAGE_SIZE : maxItems, 1, MAX_PAGE_SIZE);
					int cursorIndex;
					if (!TryParseCursor(cursor, out cursorIndex))
					{
						return CreateResult(
							"graphics.inspect_scene",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"cursorは0以上の整数で指定してください。",
							null);
					}

					SceneSnapshot snapshot;
					if (!string.IsNullOrWhiteSpace(snapshotId))
					{
						E_MCP_TOOL_STATUS failureStatus;
						if (!Session.TryGetSnapshot(snapshotId, out snapshot, out failureStatus))
						{
							return CreateResult(
								"graphics.inspect_scene",
								requestId,
								failureStatus,
								"指定されたSnapshotは現在のEditor SessionまたはRevisionでは利用できません。",
								new Dictionary<string, object> { { "snapshotId", snapshotId } });
						}
					}
					else
					{
						long startRevision = Session.Revision;
						snapshot = BuildSceneSnapshot(includeInactive, NormalizeSections(sections), startRevision);

						if (startRevision != Session.Revision)
						{
							return CreateResult(
								"graphics.inspect_scene",
								requestId,
								E_MCP_TOOL_STATUS.STALE_DURING_SCAN,
								"Scene解析中にProject状態が変更されたため結果を破棄しました。",
								null);
						}

						Session.StoreSnapshot(snapshot);
					}

					if (cursorIndex > snapshot.Items.Count)
					{
						return CreateResult(
							"graphics.inspect_scene",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"cursorがSnapshotの項目数を超えています。",
							new Dictionary<string, object>
							{
								{ "cursor", cursorIndex },
								{ "totalItems", snapshot.Items.Count }
							});
					}

					List<SceneItem> items = snapshot.Items
						.Skip(cursorIndex)
						.Take(pageSize)
						.ToList();

					int nextIndex = cursorIndex + items.Count;
					string nextCursor = nextIndex < snapshot.Items.Count ? nextIndex.ToString() : null;

					Dictionary<string, object> data = new Dictionary<string, object>
					{
						{ "snapshotId", snapshot.SnapshotId },
						{ "snapshotRevision", snapshot.Revision },
						{ "cursor", cursorIndex },
						{ "nextCursor", nextCursor },
						{ "pageSize", pageSize },
						{ "totalItems", snapshot.Items.Count },
						{ "summary", snapshot.Summary },
						{ "items", items }
					};

					return CreateResult(
						"graphics.inspect_scene",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Loaded SceneのGraphics状態をRead-onlyで取得しました。",
						data);
				});
		}

		public static ToolResult ValidateScene(
			string requestId,
			bool includeInactive)
		{
			return ExecuteReadOnly(
				"graphics.validate_scene",
				requestId,
				delegate
				{
					List<Finding> findings = BuildValidationFindings(includeInactive);
					Dictionary<string, object> counts = new Dictionary<string, object>
					{
						{ "error", findings.Count(item => item.severity == E_FINDING_SEVERITY.ERROR.ToString()) },
						{ "warning", findings.Count(item => item.severity == E_FINDING_SEVERITY.WARNING.ToString()) },
						{ "info", findings.Count(item => item.severity == E_FINDING_SEVERITY.INFO.ToString()) }
					};

					E_MCP_TOOL_STATUS status = findings.Any(item =>
						item.severity == E_FINDING_SEVERITY.ERROR.ToString())
						? E_MCP_TOOL_STATUS.PARTIAL
						: E_MCP_TOOL_STATUS.SUCCESS;

					return CreateResult(
						"graphics.validate_scene",
						requestId,
						status,
						"Graphics Validationを完了しました。",
						new Dictionary<string, object>
						{
							{ "counts", counts },
							{ "findings", findings }
						});
				});
		}

		private static ToolResult ExecuteReadOnly(
			string toolName,
			string requestId,
			Func<ToolResult> operation)
		{
			string normalizedRequestId = string.IsNullOrWhiteSpace(requestId)
				? Guid.NewGuid().ToString("N")
				: requestId;

			if (!Session.IsMainThread)
			{
				return CreateResult(
					toolName,
					normalizedRequestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Unity Editor APIはMain Threadで実行する必要があります。",
					null);
			}

			if (Session.IsReloading)
			{
				return CreateResult(
					toolName,
					normalizedRequestId,
					E_MCP_TOOL_STATUS.EDITOR_RELOADING,
					"Unity EditorがCompileまたはDomain Reload中です。",
					null);
			}

			ReadOnlyGuard guard = Session.BeginReadOnlyGuard();

			try
			{
				ToolResult result = operation();
				Dictionary<string, object> violationEvidence;
				if (guard.HasViolation(out violationEvidence))
				{
					return CreateResult(
						toolName,
						normalizedRequestId,
						E_MCP_TOOL_STATUS.READ_ONLY_CONTRACT_VIOLATION,
						"Read-only Toolの実行前後でScene Dirty状態またはUndo Groupが変化しました。",
						violationEvidence);
				}

				return result;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return CreateResult(
					toolName,
					normalizedRequestId,
					E_MCP_TOOL_STATUS.FAILED,
					"Graphics Inspection中に例外が発生しました。",
					new Dictionary<string, object>
					{
						{ "exceptionType", exception.GetType().FullName },
						{ "message", exception.Message }
					});
			}
		}

		private static ToolResult CreateResult(
			string toolName,
			string requestId,
			E_MCP_TOOL_STATUS status,
			string summary,
			object data)
		{
			return new ToolResult
			{
				tool = toolName,
				requestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId,
				sessionId = Session.SessionId,
				revision = Session.Revision,
				status = status.ToString(),
				summary = summary,
				data = data
			};
		}
	}
}

#endif
