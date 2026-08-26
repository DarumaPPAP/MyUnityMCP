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

			bool rootIsDomainResult = IsUnityDomainResult(root);
			JObject candidate = root;
			bool isEnvelope = false;
			bool? outerSuccess = null;
			if (!rootIsDomainResult && IsToolBridgeEnvelope(root, out JObject envelopeData))
			{
				candidate = envelopeData ?? root;
				isEnvelope = true;
				outerSuccess = ReadSuccess(root);
			}

			string status = candidate.Value<string>("status");
			bool? candidateSuccess = ReadSuccess(candidate);
			if ((rootIsDomainResult || isEnvelope) && !candidateSuccess.HasValue)
			{
				return Ambiguous("AGENT-DELEGATE-RESULT-AMBIGUOUS", "既知Result Shapeにsuccess flagがありません。");
			}
			if (string.IsNullOrWhiteSpace(status) &&
				outerSuccess == false &&
				HasEnvelopeError(root))
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

		private static bool IsUnityDomainResult(JObject root)
		{
			return root?.Value<string>("status") != null &&
				root["success"]?.Type == JTokenType.Boolean;
		}

		private static bool IsToolBridgeEnvelope(JObject root, out JObject data)
		{
			data = null;
			if (root?["success"]?.Type != JTokenType.Boolean)
			{
				return false;
			}

			if (root["data"] is JObject dataObject &&
				dataObject.Value<string>("status") != null &&
				ReadSuccess(dataObject).HasValue)
			{
				data = dataObject;
				return true;
			}

			return root.Value<bool>("success") == false && HasEnvelopeError(root);
		}

		private static bool HasEnvelopeError(JObject root)
		{
			return root?["error"] != null ||
				root?["errorCode"] != null ||
				root?["code"] != null;
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
				root?.Value<string>("code") ??
				(candidate?["error"] as JObject)?.Value<string>("code") ??
				(root?["error"] as JObject)?.Value<string>("code");
		}

		private static string ReadMessage(JObject root, JObject candidate)
		{
			return candidate?.Value<string>("message") ??
				candidate?.Value<string>("summary") ??
				candidate?.Value<string>("errorMessage") ??
				candidate?.Value<string>("error") ??
				root?.Value<string>("message") ??
				root?.Value<string>("summary") ??
				root?.Value<string>("errorMessage") ??
				root?.Value<string>("error") ??
				(candidate?["error"] as JObject)?.Value<string>("message") ??
				(root?["error"] as JObject)?.Value<string>("message");
		}
	}
}

#endif
