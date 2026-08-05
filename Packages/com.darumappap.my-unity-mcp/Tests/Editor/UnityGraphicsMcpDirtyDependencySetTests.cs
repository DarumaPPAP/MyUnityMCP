#if UNITY_EDITOR

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class UnityGraphicsMcpDependencyBakeDirtySetTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpDependencyBakeDirtySetTemporaryScene.unity";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			UnityGraphicsMcpDependencyBakeSession.ClearForTests();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[TearDown]
		public void TearDown()
		{
			UnityGraphicsMcpDependencyBakeSession.ClearForTests();
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
			UnityGraphicsMcpDependencyBakeSession.TrackDirtySceneForTests(scene);
			long firstSerial = UnityGraphicsMcpDependencyBakeSession.DirtySerial;

			UnityGraphicsMcpDependencyBakeSession.TrackDirtySceneForTests(scene);

			Assert.That(
				UnityGraphicsMcpDependencyBakeSession.DirtySerial,
				Is.EqualTo(firstSerial));
		}
	}
}

#endif
