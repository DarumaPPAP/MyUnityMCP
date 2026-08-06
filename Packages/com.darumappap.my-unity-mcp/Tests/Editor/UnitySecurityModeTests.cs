#if UNITY_EDITOR

using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityMcpSecurity;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnitySecurityModeTests
	{
		private static JObject SensitivePayload()
		{
			return new JObject
			{
				["name"] = "ConfidentialObject",
				["scenePath"] = "Assets/Company/Customer/Stage.unity",
				["operatingSystem"] = "PrivateMachineOS",
				["graphicsDeviceName"] = "PrivateGPU",
				["unityProjectId"] = "project-id",
				["organization"] = "organization-name",
				["customerName"] = "customer-name",
				["internalIssue"] = "ISSUE-1234",
				["screenshot"] = "Library/private.png",
				["operationLog"] = "private operation",
				["password"] = "never-output",
				["count"] = 42
			};
		}

		[Test]
		public void UnknownMode_DefaultsToRestricted()
		{
			UnityMcpSecurityContext context = UnityMcpSecurityPolicy.Resolve("unknown");

			Assert.That(context.Mode, Is.EqualTo(E_MCP_SECURITY_MODE.RESTRICTED));
			Assert.That(context.IncludeScreenshots, Is.False);
			Assert.That(context.IncludeOperationalDetails, Is.False);
		}

		[Test]
		public void TeamMode_RemovesMachinePathNamesAndForbiddenCollection()
		{
			UnityMcpSecurityContext context = UnityMcpSecurityPolicy.Resolve("TEAM");
			JObject redacted = (JObject)UnityMcpSecurityPolicy.Redact(SensitivePayload(), context);

			Assert.That(redacted["count"]?.Value<int>(), Is.EqualTo(42));
			Assert.That(redacted["securityMode"]?.Value<string>(), Is.EqualTo("TEAM"));
			Assert.That(redacted["name"], Is.Null);
			Assert.That(redacted["scenePath"], Is.Null);
			Assert.That(redacted["operatingSystem"], Is.Null);
			Assert.That(redacted["graphicsDeviceName"], Is.Null);
			Assert.That(redacted["unityProjectId"], Is.Null);
			Assert.That(redacted["organization"], Is.Null);
			Assert.That(redacted["customerName"], Is.Null);
			Assert.That(redacted["internalIssue"], Is.Null);
			Assert.That(redacted["screenshot"], Is.Null);
			Assert.That(redacted["operationLog"], Is.Null);
			Assert.That(redacted["password"], Is.Null);
		}

		[Test]
		public void PersonalMode_StillNeverOutputsCredentialsOrOrganizationalSecrets()
		{
			UnityMcpSecurityContext context = UnityMcpSecurityPolicy.Resolve("PERSONAL");
			JObject redacted = (JObject)UnityMcpSecurityPolicy.Redact(SensitivePayload(), context);

			Assert.That(redacted["name"]?.Value<string>(), Is.EqualTo("ConfidentialObject"));
			Assert.That(redacted["scenePath"]?.Value<string>(), Is.EqualTo("Assets/Company/Customer/Stage.unity"));
			Assert.That(redacted["password"], Is.Null);
			Assert.That(redacted["unityProjectId"], Is.Null);
			Assert.That(redacted["organization"], Is.Null);
			Assert.That(redacted["customerName"], Is.Null);
			Assert.That(redacted["internalIssue"], Is.Null);
		}

		[Test]
		public void CiMode_ProducesMinimalDeterministicPayload()
		{
			UnityMcpSecurityContext context = UnityMcpSecurityPolicy.Resolve("CI");
			JObject redacted = (JObject)UnityMcpSecurityPolicy.Redact(SensitivePayload(), context);

			Assert.That(redacted.Properties().Count(), Is.EqualTo(2));
			Assert.That(redacted["count"]?.Value<int>(), Is.EqualTo(42));
			Assert.That(redacted["securityMode"]?.Value<string>(), Is.EqualTo("CI"));
		}

		[Test]
		public void PolicyDeclaresAllForbiddenCollectionCategories()
		{
			CollectionAssert.IsSupersetOf(
				UnityMcpSecurityPolicy.ForbiddenCollectionFields,
				new[]
				{
					"credentials",
					"unity_project_id",
					"organization_information",
					"customer_names",
					"internal_issue_numbers",
					"private_screenshots",
					"operational_information"
				});
		}
	}
}

#endif
