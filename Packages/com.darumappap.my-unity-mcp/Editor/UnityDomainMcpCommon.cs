#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityGraphicsMcp;
using Object = UnityEngine.Object;

namespace UnityDomainMcp
{
	public enum E_DOMAIN_TOOL_STATUS
	{
		SUCCESS,
		PARTIAL,
		INVALID_REQUEST,
		UNSUPPORTED,
		UNVERIFIED,
		BACKEND_NOT_IMPLEMENTED,
		STALE_REVISION,
		APPROVAL_REQUIRED,
		APPROVAL_EXPIRED,
		NOT_FOUND,
		FAILED
	}

	public sealed class UnityDomainMcpResult
	{
		public string schemaVersion = "1.0";
		public string tool;
		public string status;
		public string summary;
		public object data;
		public JObject error;
		public long revision;
		public bool success;
	}

	internal sealed class UnityDomainMcpPlan
	{
		public string PlanId;
		public string DomainId;
		public string Operation;
		public long ExpectedRevision;
		public DateTime CreatedUtc;
		public DateTime ExpiresUtc;
		public bool RequiresApproval;
		public string ApprovalTokenHash;
		public bool Consumed;
		public JObject Payload;
	}

	internal static class UnityDomainMcpPlanStore
	{
		private const int MAX_PLAN_COUNT = 64;
		private static readonly TimeSpan PLAN_LIFETIME = TimeSpan.FromMinutes(10.0);
		private static readonly Dictionary<string, UnityDomainMcpPlan> _plans =
			new Dictionary<string, UnityDomainMcpPlan>(StringComparer.Ordinal);

		static UnityDomainMcpPlanStore()
		{
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			CompilationPipeline.compilationStarted += _ => Clear();
			EditorApplication.playModeStateChanged += _ => Clear();
			EditorApplication.quitting += Clear;
		}

		public static UnityDomainMcpResult Prepare(
			string tool,
			string domainId,
			string operation,
			long expectedRevision,
			bool requiresApproval,
			JObject payload)
		{
			RemoveExpired();
			while (_plans.Count >= MAX_PLAN_COUNT)
			{
				string oldest = _plans.OrderBy(value => value.Value.CreatedUtc).First().Key;
				_plans.Remove(oldest);
			}

			string approvalToken = requiresApproval
				? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
				: null;
			UnityDomainMcpPlan plan = new UnityDomainMcpPlan
			{
				PlanId = $"{domainId}:plan:{Guid.NewGuid():N}",
				DomainId = domainId,
				Operation = operation,
				ExpectedRevision = expectedRevision,
				CreatedUtc = DateTime.UtcNow,
				ExpiresUtc = DateTime.UtcNow + PLAN_LIFETIME,
				RequiresApproval = requiresApproval,
				ApprovalTokenHash = Hash(approvalToken),
				Payload = payload ?? new JObject()
			};
			_plans[plan.PlanId] = plan;

			return UnityDomainMcpCommon.Result(
				tool,
				E_DOMAIN_TOOL_STATUS.SUCCESS,
				"操作をRead-onlyで検証し、実行Planを作成しました。",
				new JObject
				{
					["planId"] = plan.PlanId,
					["domainId"] = plan.DomainId,
					["operation"] = plan.Operation,
					["expectedRevision"] = plan.ExpectedRevision,
					["approvalRequired"] = requiresApproval,
					["approvalToken"] = approvalToken,
					["expiresAtUtc"] = plan.ExpiresUtc.ToString("O"),
					["payload"] = plan.Payload,
					["mutationApplied"] = false
				});
		}

		public static bool TryConsume(
			string tool,
			string domainId,
			string planId,
			long currentRevision,
			string approvalToken,
			out UnityDomainMcpPlan plan,
			out UnityDomainMcpResult failure)
		{
			RemoveExpired();
			plan = null;
			failure = null;
			if (string.IsNullOrWhiteSpace(planId) || !_plans.TryGetValue(planId, out plan))
			{
				failure = UnityDomainMcpCommon.Error(tool, E_DOMAIN_TOOL_STATUS.NOT_FOUND, "Planが存在しないか期限切れです。");
				return false;
			}
			if (!string.Equals(plan.DomainId, domainId, StringComparison.Ordinal))
			{
				failure = UnityDomainMcpCommon.Error(tool, E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "PlanのDomainが一致しません。");
				return false;
			}
			if (plan.Consumed)
			{
				failure = UnityDomainMcpCommon.Error(tool, E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Planは使用済みです。");
				return false;
			}
			if (plan.ExpectedRevision != currentRevision || currentRevision != UnityGraphicsMcpSession.Revision)
			{
				failure = UnityDomainMcpCommon.Error(tool, E_DOMAIN_TOOL_STATUS.STALE_REVISION, "Preview後にEditor Revisionが変更されました。");
				return false;
			}
			if (plan.RequiresApproval)
			{
				if (DateTime.UtcNow > plan.ExpiresUtc)
				{
					failure = UnityDomainMcpCommon.Error(tool, E_DOMAIN_TOOL_STATUS.APPROVAL_EXPIRED, "Approval Tokenが期限切れです。");
					return false;
				}
				if (string.IsNullOrWhiteSpace(approvalToken) || !string.Equals(Hash(approvalToken), plan.ApprovalTokenHash, StringComparison.Ordinal))
				{
					failure = UnityDomainMcpCommon.Error(tool, E_DOMAIN_TOOL_STATUS.APPROVAL_REQUIRED, "Approval Tokenが不足しているか一致しません。");
					return false;
				}
			}
			plan.Consumed = true;
			return true;
		}

		public static void ClearForTests()
		{
			Clear();
		}

		private static string Hash(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			using (SHA256 sha256 = SHA256.Create())
			{
				byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
				return string.Concat(bytes.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
			}
		}

		private static void RemoveExpired()
		{
			DateTime now = DateTime.UtcNow;
			foreach (string key in _plans.Where(value => value.Value.ExpiresUtc <= now).Select(value => value.Key).ToArray())
			{
				_plans.Remove(key);
			}
		}

		private static void Clear()
		{
			_plans.Clear();
		}
	}

	internal static class UnityDomainMcpCommon
	{
		public static object Execute<T>(JObject @params, Func<T, UnityDomainMcpResult> operation) where T : new()
		{
			try
			{
				T parameters = @params == null || !@params.HasValues ? new T() : @params.ToObject<T>();
				return operation(parameters ?? new T());
			}
			catch (Exception exception)
			{
				return Error("domain.request", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, exception.Message);
			}
		}

		public static UnityDomainMcpResult Result(string tool, E_DOMAIN_TOOL_STATUS status, string summary, object data)
		{
			return new UnityDomainMcpResult
			{
				tool = tool,
				status = status.ToString(),
				summary = summary,
				data = data,
				revision = UnityGraphicsMcpSession.Revision,
				success = status == E_DOMAIN_TOOL_STATUS.SUCCESS || status == E_DOMAIN_TOOL_STATUS.PARTIAL
			};
		}

		public static UnityDomainMcpResult Error(string tool, E_DOMAIN_TOOL_STATUS status, string message)
		{
			UnityDomainMcpResult result = Result(tool, status, message, null);
			result.error = new JObject
			{
				["code"] = $"{tool}:{status}",
				["message"] = message
			};
			return result;
		}

		public static bool TryResolveObject<T>(string globalObjectId, out T target) where T : Object
		{
			target = null;
			if (string.IsNullOrWhiteSpace(globalObjectId) || !GlobalObjectId.TryParse(globalObjectId, out GlobalObjectId id))
			{
				return false;
			}
			target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T;
			return target != null;
		}

		public static string ObjectId(Object target)
		{
			return target == null ? null : GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
		}

		public static UnityDomainMcpResult Prepare(
			string tool,
			string domainId,
			string operation,
			long? expectedRevision,
			bool requiresApproval,
			JObject payload)
		{
			if (!expectedRevision.HasValue)
			{
				return Error(tool, E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "expectedRevisionを指定してください。");
			}
			if (expectedRevision.Value != UnityGraphicsMcpSession.Revision)
			{
				return Error(tool, E_DOMAIN_TOOL_STATUS.STALE_REVISION, "expectedRevisionが現在のEditor Revisionと一致しません。");
			}
			return UnityDomainMcpPlanStore.Prepare(tool, domainId, operation, expectedRevision.Value, requiresApproval, payload);
		}

		public static void CompleteMutation(Object target)
		{
			if (target != null)
			{
				EditorUtility.SetDirty(target);
			}
			UnityGraphicsMcpSession.NotifyMutationApplied();
		}
	}
}

#endif
