#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpEnvironmentOperationInput
	{
		public string operationId { get; set; }
		public string operation { get; set; }
		public string targetObjectId { get; set; }
		public string targetScenePath { get; set; }
		public string name { get; set; }
		public UnityGraphicsMcpVector3Input position { get; set; }
		public UnityGraphicsMcpVector3Input eulerAngles { get; set; }
		public bool? enabled { get; set; }

		// Camera
		public string projection { get; set; }
		public float? fieldOfView { get; set; }
		public float? orthographicSize { get; set; }
		public float? nearClipPlane { get; set; }
		public float? farClipPlane { get; set; }
		public int? cullingMask { get; set; }
		public string clearFlags { get; set; }
		public UnityGraphicsMcpColorInput backgroundColor { get; set; }
		public float? depth { get; set; }
		public bool? allowHdr { get; set; }
		public bool? allowMsaa { get; set; }

		// Reflection Probe
		public string probeMode { get; set; }
		public string refreshMode { get; set; }
		public string timeSlicingMode { get; set; }
		public int? importance { get; set; }
		public float? intensity { get; set; }
		public bool? boxProjection { get; set; }
		public UnityGraphicsMcpVector3Input size { get; set; }
		public UnityGraphicsMcpVector3Input center { get; set; }
		public float? blendDistance { get; set; }
		public int? resolution { get; set; }

		// Volume
		public bool? isGlobal { get; set; }
		public float? priority { get; set; }
		public float? weight { get; set; }
		public string sharedProfileAssetPath { get; set; }
	}

	internal sealed class UnityGraphicsMcpPreparedEnvironmentOperation
	{
		public string OperationId { get; set; }
		public string Operation { get; set; }
		public string TargetObjectId { get; set; }
		public int TargetInstanceId { get; set; }
		public int TargetSceneHandle { get; set; }
		public string TargetScenePath { get; set; }
		public string ComponentKind { get; set; }
		public Dictionary<string, object> RequestedValues { get; set; } = new Dictionary<string, object>();
		public string BaselineDigest { get; set; }
		public Dictionary<string, object> BaselinePreview { get; set; }
	}

	internal sealed class UnityGraphicsMcpExecutableEnvironmentPlan
	{
		public string PlanId { get; set; }
		public string DirectionPlanId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public DateTime ExpiresUtc { get; set; }
		public string ApprovalTokenHash { get; set; }
		public string DiffDigest { get; set; }
		public bool Consumed { get; set; }
		public List<UnityGraphicsMcpPreparedEnvironmentOperation> Operations { get; set; } =
			new List<UnityGraphicsMcpPreparedEnvironmentOperation>();
	}

	internal sealed class UnityGraphicsMcpEnvironmentTransaction
	{
		public string TransactionId { get; set; }
		public string PlanId { get; set; }
		public int UndoGroup { get; set; }
		public long PostRevision { get; set; }
		public bool Invalidated { get; set; }
		public bool Undone { get; set; }
	}

	internal static class UnityGraphicsMcpEnvironmentMutationSession
	{
		private const int MAX_PLAN_COUNT = 8;
		private static readonly TimeSpan PLAN_LIFETIME = TimeSpan.FromMinutes(10.0);
		private static readonly Dictionary<string, UnityGraphicsMcpExecutableEnvironmentPlan> _plans =
			new Dictionary<string, UnityGraphicsMcpExecutableEnvironmentPlan>();
		private static UnityGraphicsMcpEnvironmentTransaction _latestTransaction;
		private static bool _isPerformingOwnedUndo;

		static UnityGraphicsMcpEnvironmentMutationSession()
		{
			EditorApplication.hierarchyChanged += InvalidateTransaction;
			EditorApplication.projectChanged += InvalidateTransaction;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			CompilationPipeline.compilationStarted += OnCompilationStarted;
			EditorApplication.quitting += Clear;
		}

		public static string StorePlan(UnityGraphicsMcpExecutableEnvironmentPlan plan)
		{
			RemoveExpiredPlans();
			while (_plans.Count >= MAX_PLAN_COUNT)
			{
				string oldestId = _plans.OrderBy(pair => pair.Value.CreatedUtc).First().Key;
				_plans.Remove(oldestId);
			}

			plan.PlanId = UnityGraphicsMcpSession.SessionId + ":environment-plan:" + Guid.NewGuid().ToString("N");
			plan.CreatedUtc = DateTime.UtcNow;
			plan.ExpiresUtc = plan.CreatedUtc + PLAN_LIFETIME;
			_plans[plan.PlanId] = plan;
			return plan.PlanId;
		}

		public static bool TryGetPlan(
			string planId,
			long expectedRevision,
			string approvalToken,
			out UnityGraphicsMcpExecutableEnvironmentPlan plan,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			plan = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;

			if (string.IsNullOrWhiteSpace(planId) ||
				!planId.StartsWith(UnityGraphicsMcpSession.SessionId + ":environment-plan:", StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Environment Planは現在のEditor Sessionに属していません。";
				return false;
			}

			RemoveExpiredPlans();
			if (!_plans.TryGetValue(planId, out plan))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "Environment Planが存在しないか有効期限切れです。";
				return false;
			}

			if (plan.Consumed)
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "Environment Planは既に使用済みです。";
				return false;
			}

			if (plan.Revision != UnityGraphicsMcpSession.Revision || expectedRevision != UnityGraphicsMcpSession.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "Environment Plan作成後にEditor Revisionが変更されました。";
				return false;
			}

			if (string.IsNullOrWhiteSpace(approvalToken) ||
				!string.Equals(plan.ApprovalTokenHash, HashText(approvalToken), StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "承認Tokenが不足しているか一致しません。";
				return false;
			}

			return true;
		}

		public static void ConsumePlan(UnityGraphicsMcpExecutableEnvironmentPlan plan)
		{
			if (plan != null)
			{
				plan.Consumed = true;
			}
		}

		public static void SetLatestTransaction(UnityGraphicsMcpEnvironmentTransaction transaction)
		{
			_latestTransaction = transaction;
		}

		public static bool TryGetLatestTransaction(
			string transactionId,
			out UnityGraphicsMcpEnvironmentTransaction transaction,
			out string failureMessage)
		{
			transaction = _latestTransaction;
			failureMessage = null;
			if (transaction == null || transaction.Undone)
			{
				failureMessage = "Undo可能なPhase 3B Transactionがありません。";
				return false;
			}
			if (!string.Equals(transaction.TransactionId, transactionId, StringComparison.Ordinal))
			{
				failureMessage = "transactionIdが直近Phase 3B Transactionと一致しません。";
				return false;
			}
			if (transaction.Invalidated)
			{
				failureMessage = "Transaction後に外部変更が検出されたため、安全なUndoを実行できません。";
				return false;
			}
			return true;
		}

		public static void BeginOwnedUndo()
		{
			_isPerformingOwnedUndo = true;
		}

		public static void EndOwnedUndo(bool succeeded)
		{
			_isPerformingOwnedUndo = false;
			if (succeeded && _latestTransaction != null)
			{
				_latestTransaction.Undone = true;
			}
		}

		public static void ClearForTests()
		{
			Clear();
		}

		public static string HashText(string value)
		{
			using (SHA256 sha256 = SHA256.Create())
			{
				byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
				StringBuilder builder = new StringBuilder(bytes.Length * 2);
				foreach (byte item in bytes)
				{
					builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
				}
				return builder.ToString();
			}
		}

		private static void RemoveExpiredPlans()
		{
			DateTime now = DateTime.UtcNow;
			foreach (string id in _plans.Where(pair => pair.Value.ExpiresUtc <= now).Select(pair => pair.Key).ToArray())
			{
				_plans.Remove(id);
			}
		}

		private static void InvalidateTransaction()
		{
			if (_latestTransaction != null && !_latestTransaction.Undone)
			{
				_latestTransaction.Invalidated = true;
			}
		}

		private static void OnUndoRedoPerformed()
		{
			if (!_isPerformingOwnedUndo)
			{
				InvalidateTransaction();
			}
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			Clear();
		}

		private static void OnCompilationStarted(object context)
		{
			Clear();
		}

		private static void Clear()
		{
			_plans.Clear();
			_latestTransaction = null;
			_isPerformingOwnedUndo = false;
		}
	}

	public static partial class UnityGraphicsMcpInspection
	{
		private const int MAX_ENVIRONMENT_OPERATION_COUNT = 48;

		public static UnityGraphicsMcpToolResult PrepareEnvironmentPlan(
			string requestId,
			string directionPlanId,
			long? expectedRevision,
			UnityGraphicsMcpEnvironmentOperationInput[] operations)
		{
			return ExecuteReadOnly(
				"graphics.prepare_environment_plan",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.prepare_environment_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}

					UnityGraphicsMcpDirectionPlan directionPlan;
					E_MCP_TOOL_STATUS failureStatus;
					if (!UnityGraphicsMcpSession.TryGetPlan(
						directionPlanId,
						expectedRevision.Value,
						out directionPlan,
						out failureStatus))
					{
						return CreateResult(
							"graphics.prepare_environment_plan",
							requestId,
							failureStatus,
							"Direction Planは現在のEditor SessionまたはRevisionでは利用できません。",
							null);
					}

					if (operations == null || operations.Length == 0 || operations.Length > MAX_ENVIRONMENT_OPERATION_COUNT)
					{
						return CreateResult(
							"graphics.prepare_environment_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"1～48件のEnvironment Operationを指定してください。",
							null);
					}

					List<UnityGraphicsMcpPreparedEnvironmentOperation> prepared = new List<UnityGraphicsMcpPreparedEnvironmentOperation>();
					List<UnityGraphicsMcpIssue> issues = new List<UnityGraphicsMcpIssue>();
					foreach (UnityGraphicsMcpEnvironmentOperationInput operation in operations)
					{
						UnityGraphicsMcpPreparedEnvironmentOperation preparedOperation;
						if (!TryPrepareEnvironmentOperation(operation, out preparedOperation, issues))
						{
							continue;
						}
						prepared.Add(preparedOperation);
					}

					if (issues.Count > 0 || prepared.Count != operations.Length)
					{
						UnityGraphicsMcpToolResult invalid = CreateResult(
							"graphics.prepare_environment_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Environment OperationをExecutable Planへ変換できませんでした。",
							null);
						invalid.issues.AddRange(issues);
						return invalid;
					}

					string approvalToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
					UnityGraphicsMcpExecutableEnvironmentPlan plan = new UnityGraphicsMcpExecutableEnvironmentPlan
					{
						DirectionPlanId = directionPlanId,
						Revision = expectedRevision.Value,
						ApprovalTokenHash = UnityGraphicsMcpEnvironmentMutationSession.HashText(approvalToken),
						Operations = prepared
					};
					plan.DiffDigest = BuildEnvironmentPlanDigest(plan);
					UnityGraphicsMcpEnvironmentMutationSession.StorePlan(plan);

					return CreateResult(
						"graphics.prepare_environment_plan",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Camera、Reflection Probe、Volume操作をRead-onlyで検証し、承認待ちEnvironment Planを作成しました。",
						new Dictionary<string, object>
						{
							{ "directionPlanId", directionPlanId },
							{ "planId", plan.PlanId },
							{ "expectedRevision", plan.Revision },
							{ "approvalToken", approvalToken },
							{ "approvalTokenExpiresUtc", plan.ExpiresUtc.ToString("O") },
							{ "diffDigest", plan.DiffDigest },
							{ "operations", BuildEnvironmentOperationPreviews(plan.Operations) },
							{ "saveMode", "NONE" },
							{ "mutationApplied", false },
							{ "savePerformed", false },
							{ "bakePerformed", false }
						});
				});
		}

		public static UnityGraphicsMcpToolResult ApplyEnvironmentPlan(
			string requestId,
			string planId,
			long? expectedRevision,
			string approvalToken,
			string saveMode)
		{
			return ExecuteMutation(
				"graphics.apply_environment_plan",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult("graphics.apply_environment_plan", requestId, E_MCP_TOOL_STATUS.INVALID_REQUEST, "expectedRevisionを指定してください。", null);
					}
					if (!string.Equals(string.IsNullOrWhiteSpace(saveMode) ? "NONE" : saveMode.Trim(), "NONE", StringComparison.OrdinalIgnoreCase))
					{
						return CreateResult("graphics.apply_environment_plan", requestId, E_MCP_TOOL_STATUS.UNSUPPORTED, "Phase 3Bで利用できるsaveModeはNONEだけです。", null);
					}

					UnityGraphicsMcpExecutableEnvironmentPlan plan;
					E_MCP_TOOL_STATUS failureStatus;
					string failureMessage;
					if (!UnityGraphicsMcpEnvironmentMutationSession.TryGetPlan(planId, expectedRevision.Value, approvalToken, out plan, out failureStatus, out failureMessage))
					{
						return CreateResult("graphics.apply_environment_plan", requestId, failureStatus, failureMessage, null);
					}

					List<UnityGraphicsMcpIssue> staleIssues = new List<UnityGraphicsMcpIssue>();
					if (!ValidateEnvironmentPlanStillMatches(plan, staleIssues) ||
						!string.Equals(plan.DiffDigest, BuildEnvironmentPlanDigest(plan), StringComparison.Ordinal))
					{
						UnityGraphicsMcpToolResult stale = CreateResult(
							"graphics.apply_environment_plan",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"Preview後に対象ComponentまたはScene状態が変化したため適用を中止しました。",
							null);
						stale.issues.AddRange(staleIssues);
						return stale;
					}

					int undoGroup = Undo.GetCurrentGroup();
					Undo.IncrementCurrentGroup();
					undoGroup = Undo.GetCurrentGroup();
					Undo.SetCurrentGroupName("MyUnityMCP Phase 3B Environment Transaction");
					List<Dictionary<string, object>> applied = new List<Dictionary<string, object>>();

					try
					{
						foreach (UnityGraphicsMcpPreparedEnvironmentOperation operation in plan.Operations)
						{
							ApplyEnvironmentOperation(operation, applied);
						}
						Undo.CollapseUndoOperations(undoGroup);
						UnityGraphicsMcpEnvironmentMutationSession.ConsumePlan(plan);
						UnityGraphicsMcpSession.NotifyMutationApplied();
						UnityGraphicsMcpEnvironmentTransaction transaction = new UnityGraphicsMcpEnvironmentTransaction
						{
							TransactionId = UnityGraphicsMcpSession.SessionId + ":environment-transaction:" + Guid.NewGuid().ToString("N"),
							PlanId = plan.PlanId,
							UndoGroup = undoGroup,
							PostRevision = UnityGraphicsMcpSession.Revision
						};
						UnityGraphicsMcpEnvironmentMutationSession.SetLatestTransaction(transaction);

						return CreateResult(
							"graphics.apply_environment_plan",
							requestId,
							E_MCP_TOOL_STATUS.SUCCESS,
							"Environment Planを一つのUnity Undo Transactionとして適用しました。",
							new Dictionary<string, object>
							{
								{ "planId", plan.PlanId },
								{ "transactionId", transaction.TransactionId },
								{ "revision", transaction.PostRevision },
								{ "appliedOperations", applied },
								{ "undoAvailable", true },
								{ "savePerformed", false },
								{ "bakePerformed", false }
							});
					}
					catch (Exception exception)
					{
						Undo.RevertAllDownToGroup(undoGroup);
						return CreateResult(
							"graphics.apply_environment_plan",
							requestId,
							E_MCP_TOOL_STATUS.FAILED,
							"Environment Transaction中に例外が発生したため、Undo Group全体をRollbackしました。 " + exception.Message,
							null);
					}
				});
		}

		public static UnityGraphicsMcpToolResult UndoLastEnvironmentTransaction(
			string requestId,
			string transactionId,
			long? expectedRevision)
		{
			return ExecuteMutation(
				"graphics.undo_last_environment_transaction",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult("graphics.undo_last_environment_transaction", requestId, E_MCP_TOOL_STATUS.INVALID_REQUEST, "expectedRevisionを指定してください。", null);
					}

					UnityGraphicsMcpEnvironmentTransaction transaction;
					string failureMessage;
					if (!UnityGraphicsMcpEnvironmentMutationSession.TryGetLatestTransaction(transactionId, out transaction, out failureMessage))
					{
						return CreateResult("graphics.undo_last_environment_transaction", requestId, E_MCP_TOOL_STATUS.INVALID_REQUEST, failureMessage, null);
					}
					if (expectedRevision.Value != UnityGraphicsMcpSession.Revision || transaction.PostRevision != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult("graphics.undo_last_environment_transaction", requestId, E_MCP_TOOL_STATUS.STALE_SNAPSHOT, "Transaction後にEditor Revisionが変更されたためUndoを拒否しました。", null);
					}
					if (Undo.GetCurrentGroup() != transaction.UndoGroup)
					{
						return CreateResult("graphics.undo_last_environment_transaction", requestId, E_MCP_TOOL_STATUS.INVALID_REQUEST, "Phase 3B TransactionがUndo Stackの最新Groupではありません。", null);
					}

					bool succeeded = false;
					UnityGraphicsMcpEnvironmentMutationSession.BeginOwnedUndo();
					try
					{
						Undo.PerformUndo();
						UnityGraphicsMcpSession.NotifyMutationApplied();
						succeeded = true;
						return CreateResult(
							"graphics.undo_last_environment_transaction",
							requestId,
							E_MCP_TOOL_STATUS.SUCCESS,
							"直近Phase 3B TransactionをUndoしました。",
							new Dictionary<string, object>
							{
								{ "transactionId", transaction.TransactionId },
								{ "revision", UnityGraphicsMcpSession.Revision },
								{ "savePerformed", false },
								{ "bakePerformed", false }
							});
					}
					finally
					{
						UnityGraphicsMcpEnvironmentMutationSession.EndOwnedUndo(succeeded);
					}
				});
		}

		private static bool TryPrepareEnvironmentOperation(
			UnityGraphicsMcpEnvironmentOperationInput input,
			out UnityGraphicsMcpPreparedEnvironmentOperation prepared,
			List<UnityGraphicsMcpIssue> issues)
		{
			prepared = null;
			if (input == null || string.IsNullOrWhiteSpace(input.operationId) || string.IsNullOrWhiteSpace(input.operation))
			{
				AddEnvironmentIssue(issues, "GFX-PHASE3B-001", "operationIdとoperationは必須です。", null);
				return false;
			}

			string operation = input.operation.Trim().ToUpperInvariant();
			string kind;
			bool create;
			switch (operation)
			{
				case "CAMERA_CREATE": kind = "CAMERA"; create = true; break;
				case "CAMERA_UPDATE": kind = "CAMERA"; create = false; break;
				case "REFLECTION_PROBE_CREATE": kind = "REFLECTION_PROBE"; create = true; break;
				case "REFLECTION_PROBE_UPDATE": kind = "REFLECTION_PROBE"; create = false; break;
				case "VOLUME_CREATE": kind = "VOLUME"; create = true; break;
				case "VOLUME_UPDATE": kind = "VOLUME"; create = false; break;
				default:
					AddEnvironmentIssue(issues, "GFX-PHASE3B-002", "未対応Operationです。", operation);
					return false;
			}

			if (kind == "VOLUME" && ResolveVolumeType() == null)
			{
				AddEnvironmentIssue(issues, "GFX-PHASE3B-003", "Volume APIを提供するRender Pipeline Core Packageが導入されていません。", operation);
				return false;
			}

			Object target = null;
			Scene targetScene = default;
			if (create)
			{
				if (!TryResolveLoadedScene(input.targetScenePath, out targetScene))
				{
					AddEnvironmentIssue(issues, "GFX-PHASE3B-004", "作成先Sceneを解決できません。", input.targetScenePath);
					return false;
				}
			}
			else
			{
				if (!TryResolveGlobalObject(input.targetObjectId, out target))
				{
					AddEnvironmentIssue(issues, "GFX-PHASE3B-005", "更新対象Objectを解決できません。", input.targetObjectId);
					return false;
				}
				if (!IsExpectedEnvironmentComponent(target, kind))
				{
					AddEnvironmentIssue(issues, "GFX-PHASE3B-006", "targetObjectIdのComponent種別がOperationと一致しません。", input.targetObjectId);
					return false;
				}
				targetScene = ((Component)target).gameObject.scene;
			}

			Dictionary<string, object> values;
			if (!TryBuildRequestedEnvironmentValues(input, kind, out values, issues))
			{
				return false;
			}

			prepared = new UnityGraphicsMcpPreparedEnvironmentOperation
			{
				OperationId = input.operationId.Trim(),
				Operation = operation,
				TargetObjectId = create ? null : input.targetObjectId,
				TargetInstanceId = create ? 0 : target.GetInstanceID(),
				TargetSceneHandle = targetScene.handle,
				TargetScenePath = targetScene.path,
				ComponentKind = kind,
				RequestedValues = values,
				BaselinePreview = create ? null : CaptureEnvironmentState((Component)target, kind)
			};
			prepared.BaselineDigest = create
				? BuildCreateBaselineDigest(targetScene, kind)
				: HashDictionary(prepared.BaselinePreview);
			return true;
		}

		private static bool TryBuildRequestedEnvironmentValues(
			UnityGraphicsMcpEnvironmentOperationInput input,
			string kind,
			out Dictionary<string, object> values,
			List<UnityGraphicsMcpIssue> issues)
		{
			values = new Dictionary<string, object>();
			AddCommonValues(input, values);

			if (kind == "CAMERA")
			{
				if (!string.IsNullOrWhiteSpace(input.projection))
				{
					string projection = input.projection.Trim().ToUpperInvariant();
					if (projection != "PERSPECTIVE" && projection != "ORTHOGRAPHIC")
					{
						AddEnvironmentIssue(issues, "GFX-PHASE3B-101", "projectionはPERSPECTIVEまたはORTHOGRAPHICです。", projection);
						return false;
					}
					values["projection"] = projection;
				}
				if (input.fieldOfView.HasValue) values["fieldOfView"] = Mathf.Clamp(input.fieldOfView.Value, 1.0f, 179.0f);
				if (input.orthographicSize.HasValue) values["orthographicSize"] = Mathf.Max(0.0001f, input.orthographicSize.Value);
				if (input.nearClipPlane.HasValue) values["nearClipPlane"] = Mathf.Max(0.0001f, input.nearClipPlane.Value);
				if (input.farClipPlane.HasValue) values["farClipPlane"] = Mathf.Max(0.0002f, input.farClipPlane.Value);
				if (input.cullingMask.HasValue) values["cullingMask"] = input.cullingMask.Value;
				if (!string.IsNullOrWhiteSpace(input.clearFlags))
				{
					CameraClearFlags parsed;
					if (!Enum.TryParse(input.clearFlags, true, out parsed))
					{
						AddEnvironmentIssue(issues, "GFX-PHASE3B-102", "clearFlagsを解釈できません。", input.clearFlags);
						return false;
					}
					values["clearFlags"] = parsed.ToString();
				}
				if (input.backgroundColor != null) values["backgroundColor"] = ToColor(input.backgroundColor);
				if (input.depth.HasValue) values["depth"] = input.depth.Value;
				if (input.allowHdr.HasValue) values["allowHdr"] = input.allowHdr.Value;
				if (input.allowMsaa.HasValue) values["allowMsaa"] = input.allowMsaa.Value;
			}
			else if (kind == "REFLECTION_PROBE")
			{
				ReflectionProbeMode probeMode;
				if (!string.IsNullOrWhiteSpace(input.probeMode))
				{
					if (!Enum.TryParse(input.probeMode, true, out probeMode))
					{
						AddEnvironmentIssue(issues, "GFX-PHASE3B-201", "probeModeを解釈できません。", input.probeMode);
						return false;
					}
					values["probeMode"] = probeMode.ToString();
				}
				ReflectionProbeRefreshMode refreshMode;
				if (!string.IsNullOrWhiteSpace(input.refreshMode))
				{
					if (!Enum.TryParse(input.refreshMode, true, out refreshMode))
					{
						AddEnvironmentIssue(issues, "GFX-PHASE3B-202", "refreshModeを解釈できません。", input.refreshMode);
						return false;
					}
					values["refreshMode"] = refreshMode.ToString();
				}
				ReflectionProbeTimeSlicingMode timeSlicing;
				if (!string.IsNullOrWhiteSpace(input.timeSlicingMode))
				{
					if (!Enum.TryParse(input.timeSlicingMode, true, out timeSlicing))
					{
						AddEnvironmentIssue(issues, "GFX-PHASE3B-203", "timeSlicingModeを解釈できません。", input.timeSlicingMode);
						return false;
					}
					values["timeSlicingMode"] = timeSlicing.ToString();
				}
				if (input.importance.HasValue) values["importance"] = Mathf.Max(0, input.importance.Value);
				if (input.intensity.HasValue) values["intensity"] = Mathf.Max(0.0f, input.intensity.Value);
				if (input.boxProjection.HasValue) values["boxProjection"] = input.boxProjection.Value;
				if (input.size != null) values["size"] = ToVector3(input.size);
				if (input.center != null) values["center"] = ToVector3(input.center);
				if (input.blendDistance.HasValue) values["blendDistance"] = Mathf.Max(0.0f, input.blendDistance.Value);
				if (input.resolution.HasValue) values["resolution"] = Mathf.Clamp(input.resolution.Value, 16, 2048);
				if (input.cullingMask.HasValue) values["cullingMask"] = input.cullingMask.Value;
			}
			else
			{
				if (input.isGlobal.HasValue) values["isGlobal"] = input.isGlobal.Value;
				if (input.priority.HasValue) values["priority"] = input.priority.Value;
				if (input.blendDistance.HasValue) values["blendDistance"] = Mathf.Max(0.0f, input.blendDistance.Value);
				if (input.weight.HasValue) values["weight"] = Mathf.Clamp01(input.weight.Value);
				if (!string.IsNullOrWhiteSpace(input.sharedProfileAssetPath))
				{
					Type profileType = ResolveVolumeProfileType();
					Object profile = profileType == null ? null : AssetDatabase.LoadAssetAtPath(input.sharedProfileAssetPath, profileType);
					if (profile == null)
					{
						AddEnvironmentIssue(issues, "GFX-PHASE3B-301", "既存VolumeProfile Assetを解決できません。新規Assetは自動生成しません。", input.sharedProfileAssetPath);
						return false;
					}
					values["sharedProfileAssetPath"] = input.sharedProfileAssetPath;
				}
			}

			return true;
		}

		private static void AddCommonValues(UnityGraphicsMcpEnvironmentOperationInput input, Dictionary<string, object> values)
		{
			if (!string.IsNullOrWhiteSpace(input.name)) values["name"] = input.name.Trim();
			if (input.position != null) values["position"] = ToVector3(input.position);
			if (input.eulerAngles != null) values["eulerAngles"] = ToVector3(input.eulerAngles);
			if (input.enabled.HasValue) values["enabled"] = input.enabled.Value;
		}

		private static bool ValidateEnvironmentPlanStillMatches(UnityGraphicsMcpExecutableEnvironmentPlan plan, List<UnityGraphicsMcpIssue> issues)
		{
			foreach (UnityGraphicsMcpPreparedEnvironmentOperation operation in plan.Operations)
			{
				bool create = operation.Operation.EndsWith("_CREATE", StringComparison.Ordinal);
				if (create)
				{
					Scene scene;
					if (!TryResolveSceneByHandle(operation.TargetSceneHandle, out scene) ||
						!string.Equals(operation.BaselineDigest, BuildCreateBaselineDigest(scene, operation.ComponentKind), StringComparison.Ordinal))
					{
						AddEnvironmentIssue(issues, "GFX-PHASE3B-401", "作成先SceneのBaselineが変化しました。", operation.OperationId);
						return false;
					}
				}
				else
				{
					Object target = EditorUtility.InstanceIDToObject(operation.TargetInstanceId);
					Component component = target as Component;
					if (component == null || !IsExpectedEnvironmentComponent(component, operation.ComponentKind) ||
						!string.Equals(operation.BaselineDigest, HashDictionary(CaptureEnvironmentState(component, operation.ComponentKind)), StringComparison.Ordinal))
					{
						AddEnvironmentIssue(issues, "GFX-PHASE3B-402", "更新対象ComponentのBaselineが変化しました。", operation.OperationId);
						return false;
					}
				}
			}
			return true;
		}

		private static void ApplyEnvironmentOperation(
			UnityGraphicsMcpPreparedEnvironmentOperation operation,
			List<Dictionary<string, object>> applied)
		{
			bool create = operation.Operation.EndsWith("_CREATE", StringComparison.Ordinal);
			Component component;
			if (create)
			{
				Scene scene;
				if (!TryResolveSceneByHandle(operation.TargetSceneHandle, out scene))
				{
					throw new InvalidOperationException("作成先SceneがLoad済みではありません。");
				}
				GameObject gameObject = new GameObject(GetString(operation.RequestedValues, "name") ?? DefaultEnvironmentObjectName(operation.ComponentKind));
				Undo.RegisterCreatedObjectUndo(gameObject, "Create " + operation.ComponentKind);
				SceneManager.MoveGameObjectToScene(gameObject, scene);
				component = AddEnvironmentComponentWithUndo(gameObject, operation.ComponentKind);
			}
			else
			{
				component = EditorUtility.InstanceIDToObject(operation.TargetInstanceId) as Component;
				if (component == null)
				{
					throw new InvalidOperationException("更新対象Componentが失われました。");
				}
				Undo.RecordObject(component, "Update " + operation.ComponentKind);
				Undo.RecordObject(component.transform, "Update " + operation.ComponentKind + " Transform");
				Undo.RecordObject(component.gameObject, "Update " + operation.ComponentKind + " Name");
			}

			ApplyCommonEnvironmentValues(component, operation.RequestedValues);
			if (operation.ComponentKind == "CAMERA") ApplyCameraValues((Camera)component, operation.RequestedValues);
			else if (operation.ComponentKind == "REFLECTION_PROBE") ApplyReflectionProbeValues((ReflectionProbe)component, operation.RequestedValues);
			else ApplyVolumeValues(component, operation.RequestedValues);
			EditorUtility.SetDirty(component);

			applied.Add(new Dictionary<string, object>
			{
				{ "operationId", operation.OperationId },
				{ "operation", operation.Operation },
				{ "objectId", GlobalObjectId.GetGlobalObjectIdSlow(component).ToString() },
				{ "componentKind", operation.ComponentKind },
				{ "after", CaptureEnvironmentState(component, operation.ComponentKind) }
			});
		}

		private static Component AddEnvironmentComponentWithUndo(GameObject gameObject, string kind)
		{
			if (kind == "CAMERA") return Undo.AddComponent<Camera>(gameObject);
			if (kind == "REFLECTION_PROBE") return Undo.AddComponent<ReflectionProbe>(gameObject);
			Type volumeType = ResolveVolumeType();
			if (volumeType == null) throw new InvalidOperationException("Volume APIが利用できません。");
			return Undo.AddComponent(gameObject, volumeType);
		}

		private static void ApplyCommonEnvironmentValues(Component component, Dictionary<string, object> values)
		{
			string name = GetString(values, "name");
			if (!string.IsNullOrWhiteSpace(name)) component.gameObject.name = name;
			if (values.ContainsKey("position")) component.transform.position = (Vector3)values["position"];
			if (values.ContainsKey("eulerAngles")) component.transform.eulerAngles = (Vector3)values["eulerAngles"];
			if (values.ContainsKey("enabled")) SetEnabled(component, (bool)values["enabled"]);
		}

		private static void ApplyCameraValues(Camera camera, Dictionary<string, object> values)
		{
			if (values.ContainsKey("projection")) camera.orthographic = GetString(values, "projection") == "ORTHOGRAPHIC";
			if (values.ContainsKey("fieldOfView")) camera.fieldOfView = Convert.ToSingle(values["fieldOfView"]);
			if (values.ContainsKey("orthographicSize")) camera.orthographicSize = Convert.ToSingle(values["orthographicSize"]);
			if (values.ContainsKey("nearClipPlane")) camera.nearClipPlane = Convert.ToSingle(values["nearClipPlane"]);
			if (values.ContainsKey("farClipPlane")) camera.farClipPlane = Convert.ToSingle(values["farClipPlane"]);
			if (camera.farClipPlane <= camera.nearClipPlane) camera.farClipPlane = camera.nearClipPlane + 0.0001f;
			if (values.ContainsKey("cullingMask")) camera.cullingMask = Convert.ToInt32(values["cullingMask"]);
			if (values.ContainsKey("clearFlags")) camera.clearFlags = (CameraClearFlags)Enum.Parse(typeof(CameraClearFlags), GetString(values, "clearFlags"));
			if (values.ContainsKey("backgroundColor")) camera.backgroundColor = (Color)values["backgroundColor"];
			if (values.ContainsKey("depth")) camera.depth = Convert.ToSingle(values["depth"]);
			if (values.ContainsKey("allowHdr")) camera.allowHDR = Convert.ToBoolean(values["allowHdr"]);
			if (values.ContainsKey("allowMsaa")) camera.allowMSAA = Convert.ToBoolean(values["allowMsaa"]);
		}

		private static void ApplyReflectionProbeValues(ReflectionProbe probe, Dictionary<string, object> values)
		{
			if (values.ContainsKey("probeMode")) probe.mode = (ReflectionProbeMode)Enum.Parse(typeof(ReflectionProbeMode), GetString(values, "probeMode"));
			if (values.ContainsKey("refreshMode")) probe.refreshMode = (ReflectionProbeRefreshMode)Enum.Parse(typeof(ReflectionProbeRefreshMode), GetString(values, "refreshMode"));
			if (values.ContainsKey("timeSlicingMode")) probe.timeSlicingMode = (ReflectionProbeTimeSlicingMode)Enum.Parse(typeof(ReflectionProbeTimeSlicingMode), GetString(values, "timeSlicingMode"));
			if (values.ContainsKey("importance")) probe.importance = Convert.ToInt32(values["importance"]);
			if (values.ContainsKey("intensity")) probe.intensity = Convert.ToSingle(values["intensity"]);
			if (values.ContainsKey("boxProjection")) probe.boxProjection = Convert.ToBoolean(values["boxProjection"]);
			if (values.ContainsKey("size")) probe.size = (Vector3)values["size"];
			if (values.ContainsKey("center")) probe.center = (Vector3)values["center"];
			if (values.ContainsKey("blendDistance")) probe.blendDistance = Convert.ToSingle(values["blendDistance"]);
			if (values.ContainsKey("resolution")) probe.resolution = Convert.ToInt32(values["resolution"]);
			if (values.ContainsKey("cullingMask")) probe.cullingMask = Convert.ToInt32(values["cullingMask"]);
		}

		private static void ApplyVolumeValues(Component volume, Dictionary<string, object> values)
		{
			SetProperty(volume, "isGlobal", values);
			SetProperty(volume, "priority", values);
			SetProperty(volume, "blendDistance", values);
			SetProperty(volume, "weight", values);
			if (values.ContainsKey("sharedProfileAssetPath"))
			{
				Type profileType = ResolveVolumeProfileType();
				Object profile = AssetDatabase.LoadAssetAtPath(GetString(values, "sharedProfileAssetPath"), profileType);
				PropertyInfo property = volume.GetType().GetProperty("sharedProfile", BindingFlags.Instance | BindingFlags.Public);
				if (property == null || !property.CanWrite) throw new InvalidOperationException("Volume.sharedProfile APIを解決できません。");
				property.SetValue(volume, profile, null);
			}
		}

		private static Dictionary<string, object> CaptureEnvironmentState(Component component, string kind)
		{
			Dictionary<string, object> state = new Dictionary<string, object>
			{
				{ "objectId", GlobalObjectId.GetGlobalObjectIdSlow(component).ToString() },
				{ "name", component.gameObject.name },
				{ "scenePath", component.gameObject.scene.path },
				{ "position", component.transform.position },
				{ "eulerAngles", component.transform.eulerAngles },
				{ "enabled", GetEnabled(component) }
			};

			if (kind == "CAMERA")
			{
				Camera camera = (Camera)component;
				state["projection"] = camera.orthographic ? "ORTHOGRAPHIC" : "PERSPECTIVE";
				state["fieldOfView"] = camera.fieldOfView;
				state["orthographicSize"] = camera.orthographicSize;
				state["nearClipPlane"] = camera.nearClipPlane;
				state["farClipPlane"] = camera.farClipPlane;
				state["cullingMask"] = camera.cullingMask;
				state["clearFlags"] = camera.clearFlags.ToString();
				state["backgroundColor"] = camera.backgroundColor;
				state["depth"] = camera.depth;
				state["allowHdr"] = camera.allowHDR;
				state["allowMsaa"] = camera.allowMSAA;
			}
			else if (kind == "REFLECTION_PROBE")
			{
				ReflectionProbe probe = (ReflectionProbe)component;
				state["probeMode"] = probe.mode.ToString();
				state["refreshMode"] = probe.refreshMode.ToString();
				state["timeSlicingMode"] = probe.timeSlicingMode.ToString();
				state["importance"] = probe.importance;
				state["intensity"] = probe.intensity;
				state["boxProjection"] = probe.boxProjection;
				state["size"] = probe.size;
				state["center"] = probe.center;
				state["blendDistance"] = probe.blendDistance;
				state["resolution"] = probe.resolution;
				state["cullingMask"] = probe.cullingMask;
			}
			else
			{
				state["isGlobal"] = GetPropertyValue(component, "isGlobal");
				state["priority"] = GetPropertyValue(component, "priority");
				state["blendDistance"] = GetPropertyValue(component, "blendDistance");
				state["weight"] = GetPropertyValue(component, "weight");
				Object profile = GetPropertyValue(component, "sharedProfile") as Object;
				state["sharedProfileAssetPath"] = profile == null ? null : AssetDatabase.GetAssetPath(profile);
			}
			return state;
		}

		private static List<Dictionary<string, object>> BuildEnvironmentOperationPreviews(List<UnityGraphicsMcpPreparedEnvironmentOperation> operations)
		{
			return operations.Select(operation => new Dictionary<string, object>
			{
				{ "operationId", operation.OperationId },
				{ "operation", operation.Operation },
				{ "componentKind", operation.ComponentKind },
				{ "targetObjectId", operation.TargetObjectId },
				{ "targetScenePath", operation.TargetScenePath },
				{ "before", operation.BaselinePreview },
				{ "requestedAfter", operation.RequestedValues }
			}).ToList();
		}

		private static string BuildEnvironmentPlanDigest(UnityGraphicsMcpExecutableEnvironmentPlan plan)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(plan.DirectionPlanId).Append('|').Append(plan.Revision);
			foreach (UnityGraphicsMcpPreparedEnvironmentOperation operation in plan.Operations)
			{
				builder.Append('|').Append(operation.OperationId)
					.Append('|').Append(operation.Operation)
					.Append('|').Append(operation.TargetObjectId)
					.Append('|').Append(operation.TargetSceneHandle)
					.Append('|').Append(operation.BaselineDigest)
					.Append('|').Append(HashDictionary(operation.RequestedValues));
			}
			return UnityGraphicsMcpEnvironmentMutationSession.HashText(builder.ToString());
		}

		private static string BuildCreateBaselineDigest(Scene scene, string kind)
		{
			return UnityGraphicsMcpEnvironmentMutationSession.HashText(
				scene.handle + "|" + scene.path + "|" + scene.rootCount + "|" + kind);
		}

		private static string HashDictionary(Dictionary<string, object> values)
		{
			if (values == null) return UnityGraphicsMcpEnvironmentMutationSession.HashText("null");
			StringBuilder builder = new StringBuilder();
			foreach (KeyValuePair<string, object> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
			{
				builder.Append(pair.Key).Append('=').Append(StableValue(pair.Value)).Append(';');
			}
			return UnityGraphicsMcpEnvironmentMutationSession.HashText(builder.ToString());
		}

		private static string StableValue(object value)
		{
			if (value == null) return "null";
			if (value is Vector3)
			{
				Vector3 vector = (Vector3)value;
				return vector.x.ToString("R", CultureInfo.InvariantCulture) + "," + vector.y.ToString("R", CultureInfo.InvariantCulture) + "," + vector.z.ToString("R", CultureInfo.InvariantCulture);
			}
			if (value is Color)
			{
				Color color = (Color)value;
				return color.r.ToString("R", CultureInfo.InvariantCulture) + "," + color.g.ToString("R", CultureInfo.InvariantCulture) + "," + color.b.ToString("R", CultureInfo.InvariantCulture) + "," + color.a.ToString("R", CultureInfo.InvariantCulture);
			}
			if (value is IFormattable) return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
			return value.ToString();
		}

		private static bool TryResolveLoadedScene(string scenePath, out Scene scene)
		{
			if (!string.IsNullOrWhiteSpace(scenePath))
			{
				for (int index = 0; index < SceneManager.sceneCount; index++)
				{
					Scene candidate = SceneManager.GetSceneAt(index);
					if (candidate.IsValid() && string.Equals(candidate.path, scenePath, StringComparison.OrdinalIgnoreCase))
					{
						scene = candidate;
						return true;
					}
				}
				scene = default;
				return false;
			}
			scene = SceneManager.GetActiveScene();
			return scene.IsValid() && scene.isLoaded;
		}

		private static bool TryResolveSceneByHandle(int handle, out Scene scene)
		{
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene candidate = SceneManager.GetSceneAt(index);
				if (candidate.IsValid() && candidate.handle == handle)
				{
					scene = candidate;
					return true;
				}
			}
			scene = default;
			return false;
		}

		private static bool TryResolveGlobalObject(string objectId, out Object target)
		{
			target = null;
			GlobalObjectId globalObjectId;
			if (string.IsNullOrWhiteSpace(objectId) || !GlobalObjectId.TryParse(objectId, out globalObjectId)) return false;
			target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
			return target != null;
		}

		private static bool IsExpectedEnvironmentComponent(Object target, string kind)
		{
			if (kind == "CAMERA") return target is Camera;
			if (kind == "REFLECTION_PROBE") return target is ReflectionProbe;
			Type volumeType = ResolveVolumeType();
			return volumeType != null && target != null && volumeType.IsInstanceOfType(target);
		}

		private static Type ResolveVolumeType()
		{
			return Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime", false);
		}

		private static Type ResolveVolumeProfileType()
		{
			return Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime", false);
		}

		private static object GetPropertyValue(object target, string propertyName)
		{
			PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
			return property == null || !property.CanRead ? null : property.GetValue(target, null);
		}

		private static void SetProperty(object target, string propertyName, Dictionary<string, object> values)
		{
			if (!values.ContainsKey(propertyName)) return;
			PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
			if (property == null || !property.CanWrite) throw new InvalidOperationException(target.GetType().FullName + "." + propertyName + " APIを解決できません。");
			object value = Convert.ChangeType(values[propertyName], property.PropertyType, CultureInfo.InvariantCulture);
			property.SetValue(target, value, null);
		}

		private static bool GetEnabled(Component component)
		{
			Behaviour behaviour = component as Behaviour;
			return behaviour == null || behaviour.enabled;
		}

		private static void SetEnabled(Component component, bool enabled)
		{
			Behaviour behaviour = component as Behaviour;
			if (behaviour != null) behaviour.enabled = enabled;
		}

		private static Vector3 ToVector3(UnityGraphicsMcpVector3Input input)
		{
			return new Vector3(input.x, input.y, input.z);
		}

		private static Color ToColor(UnityGraphicsMcpColorInput input)
		{
			return new Color(input.r, input.g, input.b, input.a);
		}

		private static string GetString(Dictionary<string, object> values, string key)
		{
			object value;
			return values.TryGetValue(key, out value) ? value as string : null;
		}

		private static string DefaultEnvironmentObjectName(string kind)
		{
			if (kind == "CAMERA") return "MCP Camera";
			if (kind == "REFLECTION_PROBE") return "MCP Reflection Probe";
			return "MCP Volume";
		}

		private static void AddEnvironmentIssue(List<UnityGraphicsMcpIssue> issues, string id, string message, string target)
		{
			issues.Add(new UnityGraphicsMcpIssue
			{
				id = id,
				severity = "ERROR",
				category = "INVARIANT",
				message = message,
				target = target
			});
		}
	}

	[McpForUnityTool(
		"graphics.prepare_environment_plan",
		Description = "Camera、Reflection Probe、Volumeの明示操作を正確な差分へ変換し、承認TokenをRead-onlyで発行します。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsPrepareEnvironmentPlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }
			[ToolParameter("graphics.compile_directionが返したDirection Plan ID。", Required = true)]
			public string directionPlanId { get; set; }
			[ToolParameter("Direction Planが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }
			[ToolParameter("CAMERA / REFLECTION_PROBE / VOLUMEのCREATEまたはUPDATE操作。", Required = true)]
			public UnityGraphicsMcpEnvironmentOperationInput[] operations { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.PrepareEnvironmentPlan(
					parameters.requestId,
					parameters.directionPlanId,
					parameters.expectedRevision,
					parameters.operations));
		}
	}

	[McpForUnityTool(
		"graphics.apply_environment_plan",
		Description = "承認済みEnvironment Planを一つのUnity Undo Transactionとして適用します。自動保存とBakeは行いません。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsApplyEnvironmentPlanTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }
			[ToolParameter("prepare_environment_planが返したPlan ID。", Required = true)]
			public string planId { get; set; }
			[ToolParameter("Planが前提とするEditor Revision。", Required = true)]
			public long? expectedRevision { get; set; }
			[ToolParameter("prepare_environment_planが返した一時承認Token。", Required = true)]
			public string approvalToken { get; set; }
			[ToolParameter("Phase 3BではNONEのみ。", Required = false)]
			public string saveMode { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.ApplyEnvironmentPlan(
					parameters.requestId,
					parameters.planId,
					parameters.expectedRevision,
					parameters.approvalToken,
					parameters.saveMode));
		}
	}

	[McpForUnityTool(
		"graphics.undo_last_environment_transaction",
		Description = "外部変更がない場合に限り、直近Phase 3B Environment Transactionを一括Undoします。",
		AutoRegister = false,
		Group = "core")]
	public static class GraphicsUndoLastEnvironmentTransactionTool
	{
		public sealed class Parameters
		{
			[ToolParameter("Request ID。", Required = false)]
			public string requestId { get; set; }
			[ToolParameter("apply_environment_planが返したTransaction ID。", Required = true)]
			public string transactionId { get; set; }
			[ToolParameter("apply_environment_planが返したRevision。", Required = true)]
			public long? expectedRevision { get; set; }
		}

		public static object HandleCommand(JObject @params)
		{
			return UnityGraphicsMcpToolBridge.Execute<Parameters>(
				@params,
				parameters => UnityGraphicsMcpInspection.UndoLastEnvironmentTransaction(
					parameters.requestId,
					parameters.transactionId,
					parameters.expectedRevision));
		}
	}
}

#endif
