#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class CaptureEvidenceTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpCaptureEvidenceTemporaryScene.unity";

		private readonly List<string> _captureDirectories =
			new List<string>();

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			CaptureEvidenceSession.ClearForTests();
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			_captureDirectories.Clear();
		}

		[TearDown]
		public void TearDown()
		{
			Session.ClearSnapshots();
			Session.ClearPlans();
			SaveEvaluationSession.ClearForTests();
			CaptureEvidenceSession.ClearForTests();
			Undo.ClearAll();
			RenderTexture.active = null;
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);

			foreach (string directory in _captureDirectories)
			{
				if (!string.IsNullOrWhiteSpace(directory) &&
					Directory.Exists(directory))
				{
					Directory.Delete(directory, true);
				}
			}
		}

		[Test]
		public void Bridge_DiscoversCaptureEvidenceCaptureTools_AndKeepsThemDisabled()
		{
			CommandRegistry.Initialize();

			Assert.That(CommandRegistry.GetHandler("graphics.capture_evidence"), Is.Not.Null);
			Assert.That(CommandRegistry.GetHandler("graphics.submit_visual_review"), Is.Not.Null);
			Assert.That(CommandRegistry.GetHandler("graphics.refine_from_visual_review"), Is.Not.Null);

			Assert.That(GetToolAttribute(typeof(CaptureEvidenceTool)).AutoRegister, Is.False);
			Assert.That(GetToolAttribute(typeof(SubmitVisualReviewTool)).AutoRegister, Is.False);
			Assert.That(GetToolAttribute(typeof(RefineFromVisualReviewTool)).AutoRegister, Is.False);
		}

		[Test]
		public void ObjectIdEncoding_IsDeterministic24Bit()
		{
			Color32 first = Inspection.EncodeCaptureEvidenceObjectIdForTests(1);
			Color32 second = Inspection.EncodeCaptureEvidenceObjectIdForTests(256);
			Color32 third = Inspection.EncodeCaptureEvidenceObjectIdForTests(65536);

			Assert.That(first, Is.EqualTo(new Color32(1, 0, 0, 255)));
			Assert.That(second, Is.EqualTo(new Color32(0, 1, 0, 255)));
			Assert.That(third, Is.EqualTo(new Color32(0, 0, 1, 255)));
			Assert.That(first, Is.Not.EqualTo(second));
			Assert.That(second, Is.Not.EqualTo(third));
		}

		[Test]
		public void EvidenceDigest_IsStableAcrossArtifactOrder()
		{
			CaptureEvidenceRecord first = CreateDigestRecord();
			CaptureEvidenceRecord second = CreateDigestRecord();
			second.Artifacts.Reverse();

			string firstDigest = Inspection.BuildCaptureEvidenceEvidenceDigestForTests(first);
			string secondDigest = Inspection.BuildCaptureEvidenceEvidenceDigestForTests(second);

			Assert.That(firstDigest, Is.EqualTo(secondDigest));

			second.Artifacts.Find(artifact => artifact.Channel == "COLOR").Sha256 = new string('f', 64);
			Assert.That(
				Inspection.BuildCaptureEvidenceEvidenceDigestForTests(second),
				Is.Not.EqualTo(firstDigest));
		}

		[Test]
		public void CaptureEvidence_RejectsUnsupportedChannel()
		{
			Camera camera = CreateSavedCamera();
			ToolResult result = Inspection.CaptureEvidence(
				"capture-evidence-invalid-channel",
				ObjectId(camera),
				Session.Revision,
				64,
				64,
				new[] { "NORMALS" },
				"invalid",
				32);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void CaptureEvidence_RejectsStaleRevision()
		{
			Camera camera = CreateSavedCamera();
			ToolResult result = Inspection.CaptureEvidence(
				"capture-evidence-stale",
				ObjectId(camera),
				Session.Revision + 1,
				64,
				64,
				new[] { "COLOR" },
				"stale",
				32);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
		}

		[Test]
		public void CaptureEvidence_RestoresState_AndWritesBundleWhenAvailable()
		{
			Camera camera = CreateSavedCamera();
			RenderTexture originalActive = new RenderTexture(16, 16, 0);
			originalActive.Create();
			RenderTexture.active = originalActive;
			RenderTexture originalTarget = camera.targetTexture;
			bool dirtyBefore = camera.gameObject.scene.isDirty;

			try
			{
				ToolResult result = Inspection.CaptureEvidence(
					"capture-evidence-capture",
					ObjectId(camera),
					Session.Revision,
					64,
					64,
					new[] { "COLOR", "LINEAR_DEPTH", "OBJECT_ID" },
					"capture-evidence-test",
					32);

				Assert.That(camera.targetTexture, Is.SameAs(originalTarget));
				Assert.That(RenderTexture.active, Is.SameAs(originalActive));
				Assert.That(camera.gameObject.scene.isDirty, Is.EqualTo(dirtyBefore));

				if (result.status == E_MCP_TOOL_STATUS.UNVERIFIED.ToString())
				{
					Assert.That(SystemInfo.graphicsDeviceType, Is.EqualTo(GraphicsDeviceType.Null));
					return;
				}

				Assert.That(result.IsSuccessful, Is.True, result.summary + "\n" + JsonConvert.SerializeObject(result.data));
				Dictionary<string, object> data = ResultData(result);
				string bundlePath = data["bundlePath"] as string;
				string absoluteBundle = ProjectAbsolutePath(bundlePath);
				_captureDirectories.Add(absoluteBundle);

				Assert.That(Directory.Exists(absoluteBundle), Is.True);
				Assert.That(File.Exists(Path.Combine(absoluteBundle, "color.png")), Is.True);
				Assert.That(File.Exists(Path.Combine(absoluteBundle, "capture-manifest.json")), Is.True);
				Assert.That(File.Exists(Path.Combine(absoluteBundle, "linear-depth.exr")), Is.True);
				Assert.That(File.Exists(Path.Combine(absoluteBundle, "object-id.png")), Is.True);
				Assert.That(File.Exists(Path.Combine(absoluteBundle, "object-id-map.json")), Is.True);
				Assert.That(data["evidenceDigest"] as string, Has.Length.EqualTo(64));
				Assert.That(data["temporaryStateRestored"], Is.EqualTo(true));
				Assert.That(data["visualAccepted"], Is.EqualTo(false));
			}
			finally
			{
				RenderTexture.active = null;
				originalActive.Release();
				UnityEngine.Object.DestroyImmediate(originalActive);
			}
		}

		[Test]
		public void SubmitVisualReview_RequiresExactEvidenceDigest()
		{
			string captureId = StoreFakeCapture("digest-a");
			ToolResult result = Inspection.SubmitVisualReview(
				"capture-evidence-review-digest",
				captureId,
				Session.Revision,
				"digest-b",
				"REJECTED",
				"Reviewer",
				new[] { "意図したLookと一致しない。" },
				null,
				null);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
		}

		[Test]
		public void SubmitVisualReview_AcceptedRequiresConfirmation()
		{
			string captureId = StoreFakeCapture("digest");
			ToolResult result = Inspection.SubmitVisualReview(
				"capture-evidence-review-confirm",
				captureId,
				Session.Revision,
				"digest",
				"ACCEPTED",
				"Reviewer",
				new[] { "構図とLightingを確認した。" },
				null,
				null);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void SubmitVisualReview_AcceptedRejectsAdjustment()
		{
			string captureId = StoreFakeCapture("digest");
			ToolResult result = Inspection.SubmitVisualReview(
				"capture-evidence-review-accepted-adjustment",
				captureId,
				Session.Revision,
				"digest",
				"ACCEPTED",
				"Reviewer",
				new[] { "全体を確認した。" },
				new[] { "露出を下げる。" },
				"VISUAL_ACCEPTED");
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void SubmitVisualReview_AcceptedIsFinalAndImmutable()
		{
			string captureId = StoreFakeCapture("digest");
			ToolResult accepted = Inspection.SubmitVisualReview(
				"capture-evidence-review-accepted",
				captureId,
				Session.Revision,
				"digest",
				"ACCEPTED",
				"Reviewer",
				new[] { "Color、Depth、Object IDを照合した。" },
				null,
				"VISUAL_ACCEPTED");
			Assert.That(accepted.IsSuccessful, Is.True, accepted.summary);
			Dictionary<string, object> acceptedData = ResultData(accepted);
			Assert.That(acceptedData["visualAccepted"], Is.EqualTo(true));
			Assert.That(acceptedData["reviewId"] as string, Is.Not.Empty);

			ToolResult second = Inspection.SubmitVisualReview(
				"capture-evidence-review-second",
				captureId,
				Session.Revision,
				"digest",
				"REJECTED",
				"Reviewer",
				new[] { "後から判定を変更する。" },
				null,
				null);
			Assert.That(second.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void SubmitVisualReview_NeedsAdjustmentIsRecorded()
		{
			string captureId = StoreFakeCapture("digest");
			ToolResult result = SubmitNeedsAdjustment(captureId, "digest");
			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["decision"], Is.EqualTo("NEEDS_ADJUSTMENT"));
			Assert.That(data["requiresRefinement"], Is.EqualTo(true));
			Assert.That(data["visualAccepted"], Is.EqualTo(false));
		}

		[Test]
		public void RefineFromVisualReview_CreatesPlanForNeedsAdjustment()
		{
			Dictionary<string, object> direction = CompileDirection();
			string captureId = StoreFakeCapture("digest");
			Dictionary<string, object> review = ResultData(SubmitNeedsAdjustment(captureId, "digest"));

			ToolResult result = Inspection.RefineFromVisualReview(
				"capture-evidence-refine",
				direction["planId"] as string,
				review["reviewId"] as string,
				Convert.ToInt64(direction["expectedRevision"]));

			Assert.That(result.IsSuccessful, Is.True, result.summary);
			Dictionary<string, object> data = ResultData(result);
			Assert.That(data["planId"] as string, Is.Not.Empty);
			Assert.That(data["planId"] as string, Is.Not.EqualTo(direction["planId"] as string));
			Assert.That(data["decision"], Is.EqualTo("NEEDS_ADJUSTMENT"));
			Assert.That(data["visualAccepted"], Is.EqualTo(false));
			Assert.That(data["mutationApplied"], Is.EqualTo(false));
		}

		[Test]
		public void RefineFromVisualReview_RejectsAcceptedReview()
		{
			Dictionary<string, object> direction = CompileDirection();
			string captureId = StoreFakeCapture("digest");
			Dictionary<string, object> review = ResultData(
				Inspection.SubmitVisualReview(
					"capture-evidence-review-final",
					captureId,
					Session.Revision,
					"digest",
					"ACCEPTED",
					"Reviewer",
					new[] { "最終Lookを確認した。" },
					null,
					"VISUAL_ACCEPTED"));
			ToolResult result = Inspection.RefineFromVisualReview(
				"capture-evidence-refine-accepted",
				direction["planId"] as string,
				review["reviewId"] as string,
				Convert.ToInt64(direction["expectedRevision"]));
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.INVALID_REQUEST.ToString()));
		}

		[Test]
		public void VisualReview_RejectsAfterRevisionChange()
		{
			string captureId = StoreFakeCapture("digest");
			Session.NotifyMutationApplied();
			ToolResult result = Inspection.SubmitVisualReview(
				"capture-evidence-review-revision",
				captureId,
				Session.Revision,
				"digest",
				"REJECTED",
				"Reviewer",
				new[] { "Revision変更後の古いCapture。" },
				null,
				null);
			Assert.That(result.status, Is.EqualTo(E_MCP_TOOL_STATUS.STALE_SNAPSHOT.ToString()));
		}

		private static Camera CreateSavedCamera()
		{
			GameObject cameraObject = new GameObject("CaptureEvidence Capture Camera");
			Camera camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = Color.gray;
			camera.transform.position = new Vector3(0.0f, 0.0f, -5.0f);

			GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
			target.name = "CaptureEvidence Capture Target";

			Scene scene = SceneManager.GetActiveScene();
			Assert.That(EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false), Is.True);
			Assert.That(scene.isDirty, Is.False);

			Shader.Find("Hidden/MyUnityMCP/CaptureEvidence");
			foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
			{
				if (renderer != null && renderer.gameObject.scene == scene)
				{
					Material[] materials = renderer.sharedMaterials;
					foreach (Material material in materials)
					{
						if (material != null)
						{
							Shader shader = material.shader;
						}
					}
				}
			}
			return camera;
		}

		private static CaptureEvidenceRecord CreateDigestRecord()
		{
			return new CaptureEvidenceRecord
			{
				CaptureId = "capture",
				Revision = 10,
				CameraObjectId = "camera",
				CameraSceneHandle = 1,
				CameraScenePath = "Assets/Test.unity",
				CameraBaselineDigest = new string('b', 64),
				Width = 64,
				Height = 64,
				EncodedRendererCount = 2,
				SkippedRendererCount = 1,
				UnsupportedTerrainCount = 0,
				Artifacts = new List<CaptureArtifactRecord>
				{
					new CaptureArtifactRecord
					{
						Channel = "COLOR",
						OutputPath = "bundle/color.png",
						Sha256 = new string('a', 64),
						ByteLength = 10,
						Format = "PNG_RGBA8"
					},
					new CaptureArtifactRecord
					{
						Channel = "OBJECT_ID",
						OutputPath = "bundle/object-id.png",
						Sha256 = new string('c', 64),
						ByteLength = 20,
						Format = "PNG_RGB24_ID"
					},
					new CaptureArtifactRecord
					{
						Channel = "MANIFEST",
						OutputPath = "bundle/capture-manifest.json",
						Sha256 = new string('d', 64),
						ByteLength = 30,
						Format = "JSON_UTF8"
					}
				}
			};
		}

		private static string StoreFakeCapture(string digest)
		{
			CaptureEvidenceRecord capture = new CaptureEvidenceRecord
			{
				Revision = Session.Revision,
				CameraObjectId = "test-camera",
				CameraSceneHandle = IdentityCompatibility.GetSceneToken(SceneManager.GetActiveScene()),
				CameraScenePath = SceneManager.GetActiveScene().path,
				CameraBaselineDigest = new string('b', 64),
				EvidenceDigest = digest,
				BundlePath = "Library/MyUnityMCP/Captures/test",
				Width = 64,
				Height = 64
			};
			return CaptureEvidenceSession.StoreCaptureForTests(capture);
		}

		private static ToolResult SubmitNeedsAdjustment(string captureId, string digest)
		{
			return Inspection.SubmitVisualReview(
				"capture-evidence-review-adjustment",
				captureId,
				Session.Revision,
				digest,
				"NEEDS_ADJUSTMENT",
				"Reviewer",
				new[] { "背景がHeroより明るい。" },
				new[] { "背景露出を下げる。" },
				null);
		}

		private static Dictionary<string, object> CompileDirection()
		{
			ToolResult result = Inspection.CompileDirection(
				"capture-evidence-direction",
				"Capture EvidenceをHuman Reviewして画作りを調整する。",
				new[] { "Heroが中央に配置されている。" },
				new[] { "ドラマチック" },
				new[] { "Hero / Background" },
				new[] { "Eye level" },
				new[] { "Key / Rim" },
				new[] { "暖色Keyと寒色背景" },
				new[] { "HeroのSpecularを維持" },
				new[] { "背景をFogで分離" },
				new[] { "静的" },
				new[] { "Editor Evaluation" },
				null,
				new[] { "Human Review required" },
				Session.Revision);
			Assert.That(result.IsSuccessful, Is.True, result.summary);
			return ResultData(result);
		}

		private static string ObjectId(UnityEngine.Object target)
		{
			return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
		}

		private static Dictionary<string, object> ResultData(ToolResult result)
		{
			Dictionary<string, object> data = result.data as Dictionary<string, object>;
			Assert.That(data, Is.Not.Null);
			return data;
		}

		private static McpForUnityToolAttribute GetToolAttribute(Type type)
		{
			McpForUnityToolAttribute attribute = Attribute.GetCustomAttribute(
				type,
				typeof(McpForUnityToolAttribute)) as McpForUnityToolAttribute;
			Assert.That(attribute, Is.Not.Null);
			return attribute;
		}

		private static string ProjectAbsolutePath(string relativePath)
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			return Path.GetFullPath(Path.Combine(
				projectRoot,
				(relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
		}
	}
}

#endif