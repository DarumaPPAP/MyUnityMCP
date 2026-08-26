#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UnityAgentMcp
{
	internal enum E_AGENT_STEP_OUTCOME
	{
		SUCCEEDED,
		FAILED,
		UNSUPPORTED,
		PARTIAL,
		AMBIGUOUS
	}

	internal sealed class AgentNormalizedResult
	{
		internal E_AGENT_STEP_OUTCOME Outcome { get; set; }
		internal string ErrorCode { get; set; }
		internal string Message { get; set; }
	}

	internal static class AgentResultNormalizer
	{
		private static readonly HashSet<string> FAILURE_STATUSES = new HashSet<string>(StringComparer.Ordinal)
		{
			"INVALID_REQUEST",
			"UNVERIFIED",
			"BACKEND_NOT_IMPLEMENTED",
			"READ_ONLY_CONTRACT_VIOLATION",
			"SESSION_EXPIRED",
			"STALE_SNAPSHOT",
			"STALE_DURING_SCAN",
			"EDITOR_RELOADING",
			"STALE_REVISION",
			"APPROVAL_REQUIRED",
			"APPROVAL_EXPIRED",
			"NOT_FOUND",
			"FAILED",
		};

		internal static AgentNormalizedResult Normalize(JToken delegated)
		{
			if (delegated == null || delegated.Type == JTokenType.Null)
			{
				return Ambiguous("AGENT-DELEGATE-RESULT-MALFORMED", "DelegateがnullのResultを返しました。");
			}
			if (!(delegated is JObject root))
			{
				return Ambiguous("AGENT-DELEGATE-RESULT-MALFORMED", "Delegate Resultのshapeを解釈できません。");
			}

			JObject candidate = root;
			bool isEnvelope = root["result"] is JObject || root["data"] is JObject;
			if (root["result"] is JObject resultObject)
			{
				candidate = resultObject;
			}
			else if (root["data"] is JObject dataObject)
			{
				candidate = dataObject;
			}

			bool? outerSuccess = isEnvelope ? ReadSuccess(root) : null;
			string status = candidate.Value<string>("status");
			bool? candidateSuccess = ReadSuccess(candidate);
			if (string.IsNullOrWhiteSpace(status) &&
				outerSuccess == false &&
				(root["error"] != null || root["errorCode"] != null || root["code"] != null))
			{
				return new AgentNormalizedResult
				{
					Outcome = E_AGENT_STEP_OUTCOME.FAILED,
					ErrorCode = ReadErrorCode(root, candidate) ?? "AGENT-DELEGATE-FAILED",
					Message = ReadMessage(root, candidate) ?? "DelegateがFailureを返しました。"
				};
			}
			if (string.IsNullOrWhiteSpace(status))
			{
				return Ambiguous("AGENT-DELEGATE-RESULT-AMBIGUOUS", "Delegate Resultに既知のstatusがありません。");
			}

			string normalizedStatus = status.Trim().ToUpperInvariant();
			if (normalizedStatus == "PARTIAL")
			{
				return Result(E_AGENT_STEP_OUTCOME.PARTIAL, root, candidate);
			}
			if (normalizedStatus == "UNSUPPORTED")
			{
				return Result(E_AGENT_STEP_OUTCOME.UNSUPPORTED, root, candidate);
			}
			if (normalizedStatus == "SUCCESS" || normalizedStatus == "SUCCEEDED")
			{
				if (candidateSuccess == false || outerSuccess == false)
				{
					return Ambiguous("AGENT-DELEGATE-RESULT-CONTRADICTORY", "Delegate Resultのstatusとsuccessが矛盾しています。");
				}
				return Result(E_AGENT_STEP_OUTCOME.SUCCEEDED, root, candidate);
			}
			if (FAILURE_STATUSES.Contains(normalizedStatus))
			{
				if (candidateSuccess == true || outerSuccess == true)
				{
					return Ambiguous("AGENT-DELEGATE-RESULT-CONTRADICTORY", "Delegate Resultのfailure statusとsuccessが矛盾しています。");
				}
				return Result(E_AGENT_STEP_OUTCOME.FAILED, root, candidate);
			}
			return Ambiguous("AGENT-DELEGATE-RESULT-AMBIGUOUS", "Delegate Resultのstatusが未知です: " + status);
		}

		private static AgentNormalizedResult Result(E_AGENT_STEP_OUTCOME outcome, JObject root, JObject candidate)
		{
			return new AgentNormalizedResult
			{
				Outcome = outcome,
				ErrorCode = ReadErrorCode(root, candidate),
				Message = ReadMessage(root, candidate)
			};
		}

		private static AgentNormalizedResult Ambiguous(string errorCode, string message)
		{
			return new AgentNormalizedResult
			{
				Outcome = E_AGENT_STEP_OUTCOME.AMBIGUOUS,
				ErrorCode = errorCode,
				Message = message
			};
		}

		private static bool? ReadSuccess(JObject value)
		{
			if (value == null)
			{
				return null;
			}
			foreach (string field in new[] {"success", "IsSuccessful", "isSuccessful"})
			{
				if (value[field]?.Type == JTokenType.Boolean)
				{
					return value[field].Value<bool>();
				}
			}
			return null;
		}

		private static string ReadErrorCode(JObject root, JObject candidate)
		{
			return candidate?.Value<string>("errorCode") ??
				root?.Value<string>("errorCode") ??
				(candidate?["error"] as JObject)?.Value<string>("code") ??
				(root?["error"] as JObject)?.Value<string>("code");
		}

		private static string ReadMessage(JObject root, JObject candidate)
		{
			return candidate?.Value<string>("message") ??
				candidate?.Value<string>("summary") ??
				root?.Value<string>("message") ??
				root?.Value<string>("summary") ??
				(candidate?["error"] as JObject)?.Value<string>("message") ??
				(root?["error"] as JObject)?.Value<string>("message");
		}
	}
}

#endif
