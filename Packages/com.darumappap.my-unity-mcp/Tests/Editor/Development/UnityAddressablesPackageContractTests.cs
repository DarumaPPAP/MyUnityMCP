#if UNITY_EDITOR && MYUNITYMCP_ADDRESSABLES

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityAddressablesMcp;
using UnityDomainMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityAddressablesPackageContractTests
	{
		[Test]
		public void AddressablesWithPackage_LoadsTypedBackend()
		{
			UnityDomainMcpResult result = UnityAddressablesMcpRuntime.Inspect();
			JObject data = result.data as JObject;

			Assert.That(result.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.SUCCESS.ToString()), result.summary);
			Assert.That(data, Is.Not.Null);
			Assert.That(data.Value<bool>("packageInstalled"), Is.True);
			Assert.That(data.Value<bool>("backendCompiled"), Is.True);
		}
	}
}

#endif
