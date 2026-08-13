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
		public void StaleRevisionError_StoresMachineFieldsInsideBridgePreservedData()
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
			JObject data = serialized["data"] as JObject;

			Assert.That(serialized.Value<bool>("success"), Is.False, serialized.ToString());
			Assert.That(serialized.Value<string>("message"), Is.Not.Empty);
			Assert.That(serialized["error"], Is.Null, "Top-level error is reserved by MCPResponse and must not carry structured domain metadata.");
			Assert.That(data, Is.Not.Null, serialized.ToString());
			Assert.That(data.Value<string>("status"), Is.EqualTo(E_DOMAIN_TOOL_STATUS.STALE_REVISION.ToString()));
			Assert.That(data.Value<string>("errorCode"), Is.EqualTo("ui.prepare_rect_transform:STALE_REVISION"));
			Assert.That(data.Value<string>("errorMessage"), Is.Not.Empty);
			Assert.That(data.Value<long>("revision"), Is.EqualTo(currentRevision));
			Assert.That(serialized.Value<long>("revision"), Is.EqualTo(currentRevision));
		}
	}
}

#endif
