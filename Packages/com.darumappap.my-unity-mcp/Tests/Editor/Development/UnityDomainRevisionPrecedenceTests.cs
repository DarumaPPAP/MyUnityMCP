#if UNITY_EDITOR

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityDomainMcp;
using UnityGraphicsMcp;
using UnityUiMcp;

namespace MyUnityMcp.EditorTests
{
	public sealed class UnityDomainRevisionPrecedenceTests
	{
		[Test]
		public void UiPrepareRectTransform_RejectsStaleRevisionBeforeTargetResolution()
		{
			long revisionBefore = Session.Revision;
			long staleRevision = revisionBefore + 1;

			object rawResult = UiPrepareRectTransformTool.HandleCommand(new JObject
			{
				["targetObjectId"] = "not-a-real-global-object-id",
				["anchoredPosition"] = new JObject
				{
					["x"] = 1.0f,
					["y"] = 2.0f
				},
				["expectedRevision"] = staleRevision
			});

			UnityDomainMcpResult result = rawResult as UnityDomainMcpResult;
			Assert.That(result, Is.Not.Null);
			Assert.That(result.success, Is.False);
			Assert.That(result.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.STALE_REVISION.ToString()));
			Assert.That(result.errorCode, Is.EqualTo("ui.prepare_rect_transform:STALE_REVISION"));
			Assert.That(result.error, Is.TypeOf<string>());
			Assert.That(Session.Revision, Is.EqualTo(revisionBefore));
		}

		[Test]
		public void UiPrepareRectTransform_WithCurrentRevisionStillReportsMissingTarget()
		{
			long revisionBefore = Session.Revision;

			object rawResult = UiPrepareRectTransformTool.HandleCommand(new JObject
			{
				["targetObjectId"] = "not-a-real-global-object-id",
				["anchoredPosition"] = new JObject
				{
					["x"] = 1.0f,
					["y"] = 2.0f
				},
				["expectedRevision"] = revisionBefore
			});

			UnityDomainMcpResult result = rawResult as UnityDomainMcpResult;
			Assert.That(result, Is.Not.Null);
			Assert.That(result.success, Is.False);
			Assert.That(result.status, Is.EqualTo(E_DOMAIN_TOOL_STATUS.NOT_FOUND.ToString()));
			Assert.That(result.errorCode, Is.EqualTo("ui.prepare_rect_transform:NOT_FOUND"));
			Assert.That(Session.Revision, Is.EqualTo(revisionBefore));
		}
	}
}

#endif
