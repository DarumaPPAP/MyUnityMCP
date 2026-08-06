#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UnityMcpSecurity
{
	public enum E_MCP_SECURITY_MODE
	{
		PERSONAL,
		TEAM,
		RESTRICTED,
		CI
	}

	public sealed class UnityMcpSecurityContext
	{
		public E_MCP_SECURITY_MODE Mode { get; }
		public bool IncludeMachineDetails { get; }
		public bool IncludeProjectPaths { get; }
		public bool IncludeObjectNames { get; }
		public bool IncludeScreenshots { get; }
		public bool IncludeOperationalDetails { get; }

		internal UnityMcpSecurityContext(
			E_MCP_SECURITY_MODE mode,
			bool includeMachineDetails,
			bool includeProjectPaths,
			bool includeObjectNames,
			bool includeScreenshots,
			bool includeOperationalDetails)
		{
			Mode = mode;
			IncludeMachineDetails = includeMachineDetails;
			IncludeProjectPaths = includeProjectPaths;
			IncludeObjectNames = includeObjectNames;
			IncludeScreenshots = includeScreenshots;
			IncludeOperationalDetails = includeOperationalDetails;
		}

		public JObject Describe()
		{
			return new JObject
			{
				["mode"] = Mode.ToString(),
				["includeMachineDetails"] = IncludeMachineDetails,
				["includeProjectPaths"] = IncludeProjectPaths,
				["includeObjectNames"] = IncludeObjectNames,
				["includeScreenshots"] = IncludeScreenshots,
				["includeOperationalDetails"] = IncludeOperationalDetails,
				["forbiddenCollection"] = new JArray(UnityMcpSecurityPolicy.ForbiddenCollectionFields)
			};
		}
	}

	public static class UnityMcpSecurityPolicy
	{
		public static readonly string[] ForbiddenCollectionFields =
		{
			"credentials",
			"authentication_tokens",
			"unity_project_id",
			"organization_information",
			"customer_names",
			"internal_issue_numbers",
			"private_screenshots",
			"operational_information"
		};

		private static readonly HashSet<string> SENSITIVE_KEYS = new HashSet<string>(
			new[]
			{
				"credential",
				"credentials",
				"secret",
				"token",
				"password",
				"unityProjectId",
				"organization",
				"organizationId",
				"customer",
				"customerName",
				"issueId",
				"internalIssue",
				"screenshot",
				"operationLog"
			},
			StringComparer.OrdinalIgnoreCase);

		public static UnityMcpSecurityContext Resolve(string requestedMode)
		{
			if (!Enum.TryParse(requestedMode ?? string.Empty, true, out E_MCP_SECURITY_MODE mode))
			{
				mode = E_MCP_SECURITY_MODE.RESTRICTED;
			}

			switch (mode)
			{
				case E_MCP_SECURITY_MODE.PERSONAL:
					return new UnityMcpSecurityContext(mode, true, true, true, true, true);
				case E_MCP_SECURITY_MODE.TEAM:
					return new UnityMcpSecurityContext(mode, false, false, false, false, false);
				case E_MCP_SECURITY_MODE.CI:
					return new UnityMcpSecurityContext(mode, false, false, false, false, false);
				default:
					return new UnityMcpSecurityContext(E_MCP_SECURITY_MODE.RESTRICTED, false, false, true, false, false);
			}
		}

		public static JToken Redact(JToken value, UnityMcpSecurityContext context)
		{
			if (value == null)
			{
				return JValue.CreateNull();
			}
			if (context == null)
			{
				context = Resolve(null);
			}

			if (value is JObject sourceObject)
			{
				JObject result = new JObject();
				foreach (JProperty property in sourceObject.Properties())
				{
					if (SENSITIVE_KEYS.Contains(property.Name))
					{
						continue;
					}
					if (!context.IncludeMachineDetails && IsMachineDetail(property.Name))
					{
						continue;
					}
					if (!context.IncludeProjectPaths && IsPath(property.Name))
					{
						continue;
					}
					if (!context.IncludeObjectNames && IsObjectName(property.Name))
					{
						continue;
					}
					if (!context.IncludeScreenshots && IsScreenshot(property.Name))
					{
						continue;
					}
					if (!context.IncludeOperationalDetails && IsOperational(property.Name))
					{
						continue;
					}
					result[property.Name] = Redact(property.Value, context);
				}
				result["securityMode"] = context.Mode.ToString();
				return result;
			}

			if (value is JArray sourceArray)
			{
				JArray result = new JArray();
				foreach (JToken item in sourceArray)
				{
					result.Add(Redact(item, context));
				}
				return result;
			}

			return value.DeepClone();
		}

		private static bool IsMachineDetail(string key)
		{
			return key.IndexOf("operatingSystem", StringComparison.OrdinalIgnoreCase) >= 0 ||
				key.IndexOf("graphicsDevice", StringComparison.OrdinalIgnoreCase) >= 0 ||
				key.IndexOf("machine", StringComparison.OrdinalIgnoreCase) >= 0 ||
				key.IndexOf("deviceName", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsPath(string key)
		{
			return key.EndsWith("Path", StringComparison.OrdinalIgnoreCase) ||
				key.EndsWith("Paths", StringComparison.OrdinalIgnoreCase) ||
				key.IndexOf("projectRoot", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsObjectName(string key)
		{
			return string.Equals(key, "name", StringComparison.OrdinalIgnoreCase) ||
				key.EndsWith("Name", StringComparison.OrdinalIgnoreCase) ||
				key.IndexOf("hierarchy", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsScreenshot(string key)
		{
			return key.IndexOf("screenshot", StringComparison.OrdinalIgnoreCase) >= 0 ||
				key.IndexOf("imagePath", StringComparison.OrdinalIgnoreCase) >= 0 ||
				key.IndexOf("capturePath", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsOperational(string key)
		{
			return key.IndexOf("operation", StringComparison.OrdinalIgnoreCase) >= 0 ||
				key.IndexOf("executionHistory", StringComparison.OrdinalIgnoreCase) >= 0 ||
				key.IndexOf("workflowRun", StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}

#endif
