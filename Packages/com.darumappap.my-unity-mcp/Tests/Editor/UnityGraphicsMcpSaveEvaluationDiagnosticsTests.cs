#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpApvVisualAcceptanceiagnosticsTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpApvVisualAcceptanceiagnosticsScene.unity";

		[Test]
		public void CaptureEvaluation_LogsReadOnlyViolationEvidence()
		{
			EditorSceneManager.NewScene(
				UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
				UnityEditor.SceneManagement.NewSceneMode.Single);
			GameObject cameraObject = new GameObject("SaveEvaluation Diagnostics Camera");
			Camera camera = cameraObject.AddComponent<Camera>();
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH), Is.True);

			RenderTexture originalActive = new RenderTexture(16, 16, 0);
			originalActive.Create();
			RenderTexture.active = originalActive;

			try
			{
				UnityGraphicsMcpToolResult result =
					UnityGraphicsMcpInspection.CaptureEvaluation(
						"save-evaluation-capture-diagnostics",
						GlobalObjectId.GetGlobalObjectIdSlow(camera).ToString(),
						UnityGraphicsMcpSession.Revision,
						64,
						64,
						"diagnostics");

				Debug.Log(
					"SAVE_EVALUATION_CAPTURE_DIAGNOSTICS status=" + result.status +
					" summary=" + result.summary +
					" data=" + FormatValue(result.data));
			}
			finally
			{
				RenderTexture.active = null;
				originalActive.Release();
				UnityEngine.Object.DestroyImmediate(originalActive);
				EditorSceneManager.NewScene(
					UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
					UnityEditor.SceneManagement.NewSceneMode.Single);
				AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
			}
		}

		private static string FormatValue(object value)
		{
			if (value == null)
			{
				return "null";
			}

			IDictionary dictionary = value as IDictionary;
			if (dictionary != null)
			{
				StringBuilder builder = new StringBuilder("{");
				bool first = true;
				foreach (DictionaryEntry entry in dictionary)
				{
					if (!first)
					{
						builder.Append(", ");
					}

					first = false;
					builder.Append(entry.Key).Append('=').Append(FormatValue(entry.Value));
				}

				return builder.Append('}').ToString();
			}

			IEnumerable enumerable = value as IEnumerable;
			if (!(value is string) && enumerable != null)
			{
				StringBuilder builder = new StringBuilder("[");
				bool first = true;
				foreach (object item in enumerable)
				{
					if (!first)
					{
						builder.Append(", ");
					}

					first = false;
					builder.Append(FormatValue(item));
				}

				return builder.Append(']').ToString();
			}

			return Convert.ToString(value);
		}
	}
}

#endif
