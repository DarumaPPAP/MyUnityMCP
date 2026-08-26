#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace UnityAgentMcp
{
	internal sealed class AgentApprovalService
	{
		private const int APPROVAL_TTL_MINUTES = 10;
		private readonly AgentCatalogSnapshot _catalog;
		private readonly Func<DateTime> _utcNow;

		internal AgentApprovalService(AgentCatalogSnapshot catalog, Func<DateTime> utcNow)
		{
			_catalog = catalog;
			_utcNow = utcNow;
		}

		internal HashSet<string> RequiredApprovalGroups(IEnumerable<UnityAgentMcpStepInput> steps)
		{
			return new HashSet<string>(
				(steps ?? Enumerable.Empty<UnityAgentMcpStepInput>())
					.Select(GetApprovalGroup)
					.Where(value => !string.IsNullOrEmpty(value)),
				StringComparer.Ordinal);
		}

		internal bool RequiresApproval(UnityAgentMcpStepInput step)
		{
			return !string.IsNullOrEmpty(GetApprovalGroup(step));
		}

		internal bool TrySubmit(
			UnityAgentMcpCompiledGraph graph,
			string[] approvedGroups,
			string confirmation,
			out string approvalToken,
			out string errorCode,
			out string message)
		{
			approvalToken = null;
			errorCode = null;
			message = null;
			if (!string.Equals(confirmation, "APPROVE_AGENT_EXECUTION", StringComparison.Ordinal))
			{
				return Fail("AGENT-APPROVAL-CONFIRMATION-INVALID", "明示確認文字列が一致しません。", out errorCode, out message);
			}
			HashSet<string> approved = new HashSet<string>(approvedGroups ?? Array.Empty<string>(), StringComparer.Ordinal);
			if (!graph.requiredApprovalGroups.IsSubsetOf(approved))
			{
				return Fail("AGENT-APPROVAL-INCOMPLETE", "必要なTool Groupがすべて承認されていません。", out errorCode, out message);
			}

			approvalToken = Guid.NewGuid().ToString("N");
			graph.approved = true;
			graph.approvalTokenHash = HashToken(approvalToken);
			graph.approvalExpiresAtUtc = _utcNow().AddMinutes(APPROVAL_TTL_MINUTES);
			return true;
		}

		internal bool TryValidateStart(
			UnityAgentMcpCompiledGraph graph,
			string approvalToken,
			out string errorCode,
			out string message)
		{
			errorCode = null;
			message = null;
			if (graph.requiredApprovalGroups.Count == 0)
			{
				return true;
			}
			if (!graph.approved || _utcNow() > graph.approvalExpiresAtUtc)
			{
				return Fail("AGENT-APPROVAL-MISSING-OR-EXPIRED", "承認が存在しないか期限切れです。", out errorCode, out message);
			}
			if (string.IsNullOrWhiteSpace(approvalToken) ||
				!string.Equals(HashToken(approvalToken), graph.approvalTokenHash, StringComparison.Ordinal))
			{
				return Fail("AGENT-APPROVAL-TOKEN-MISMATCH", "Approval Tokenが一致しません。", out errorCode, out message);
			}
			return true;
		}

		private string GetApprovalGroup(UnityAgentMcpStepInput step)
		{
			if (step == null || _catalog == null || !_catalog.TryGetTool(step.toolName, out AgentCatalogToolDefinition tool))
			{
				return null;
			}
			return tool.policy != null && tool.policy.approvalRequired
				? tool.policy.approvalGroup
				: null;
		}

		private static string HashToken(string value)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
				return BitConverter.ToString(hash).Replace("-", string.Empty);
			}
		}

		private static bool Fail(string code, string text, out string errorCode, out string message)
		{
			errorCode = code;
			message = text;
			return false;
		}
	}
}

#endif
