#if UNITY_EDITOR

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityDomainMcp;
using UnityGraphicsMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityDomainMcpResponseContractTests
	{
		[Test]
		public void StaleRevisionError_UsesStringErrorAndSeparateErrorCode()
		{
			long currentRevision = Session.Revision;
			UnityDomainMcpResult result = UnityDomainMcpCommon.Prepare(
				"ui.prepare_rect_transform",
				"unity_ui_mcp",
				"update_rect_transform",
				currentRevision + 1,
				true,
				new JObject());
			JObject serialized = JObject.FromObject(result);

			Assert.That(result.success, Is.False);
			Assert.That(result.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.STALE_REVISION.ToString()));
			Assert.That(serialized["error"]?.Type, Is.EqualTo(JTokenType.String));
			Assert.That(serialized.Value<string>("error"), Is.Not.Empty);
			Assert.That(
				serialized.Value<string>("errorCode"),
				Is.EqualTo("ui.prepare_rect_transform:STALE_REVISION"));
			Assert.That(result.revision, Is.EqualTo(currentRevision));
			Assert.That(result.data, Is.Null);
		}
	}
}

#endif
