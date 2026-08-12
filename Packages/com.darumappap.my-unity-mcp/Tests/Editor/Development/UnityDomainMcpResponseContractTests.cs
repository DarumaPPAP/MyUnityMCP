#if UNITY_EDITOR

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityDomainMcp;
using UnityGraphicsMcp;
using UnityUiMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityDomainMcpResponseContractTests
	{
		[Test]
		public void StaleRevisionError_StaysInsideBridgeSafeResultEnvelope()
		{
			long currentRevision = Session.Revision;
			object rawResult = UiPrepareRectTransformTool.HandleCommand(new JObject
			{
				["targetObjectId"] = string.Empty,
				["anchoredPosition"] = new JObject
				{
					["x"] = 1.0f,
					["y"] = 2.0f
				},
				["expectedRevision"] = currentRevision + 1
			});
			JObject serialized = JObject.FromObject(rawResult);

			Assert.That(serialized.Value<bool>("success"), Is.False, serialized.ToString());
			Assert.That(serialized.Value<string>("status"), Is.EqualTo(E_DOMAIN_TOOL_STATUS.STALE_REVISION.ToString()));
			Assert.That(serialized.Value<string>("errorCode"), Is.EqualTo("ui.prepare_rect_transform:STALE_REVISION"));
			Assert.That(serialized.Value<string>("errorMessage"), Is.Not.Empty);
			Assert.That(serialized["error"], Is.Null, "Top-level error triggers MCP Bridge error-envelope conversion and drops status/errorCode.");
			Assert.That(serialized.Value<long>("revision"), Is.EqualTo(currentRevision));
			Assert.That(serialized["data"]?.Type, Is.EqualTo(JTokenType.Null));
		}
	}
}

#endif
