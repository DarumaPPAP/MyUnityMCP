#if UNITY_EDITOR

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpPhase4BakeDirtySetTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpPhase4BakeDirtySetTemporaryScene.unity";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			UnityGraphicsMcpPhase4BakeSession.ClearForTests();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[TearDown]
		public void TearDown()
		{
			UnityGraphicsMcpPhase4BakeSession.ClearForTests();
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[Test]
		public void TrackDirtyScene_DuplicateDependencySet_DoesNotAdvanceSerial()
		{
			new GameObject("Dirty Dependency Target");
			Scene scene = SceneManager.GetActiveScene();
			Assert.That(
				EditorSceneManager.SaveScene(scene, TEMP_SCENE_PATH, false),
				Is.True);

			EditorSceneManager.MarkSceneDirty(scene);
			UnityGraphicsMcpPhase4BakeSession.TrackDirtySceneForTests(scene);
			long firstSerial = UnityGraphicsMcpPhase4BakeSession.DirtySerial;

			UnityGraphicsMcpPhase4BakeSession.TrackDirtySceneForTests(scene);

			Assert.That(
				UnityGraphicsMcpPhase4BakeSession.DirtySerial,
				Is.EqualTo(firstSerial));
		}
	}
}

#endif
