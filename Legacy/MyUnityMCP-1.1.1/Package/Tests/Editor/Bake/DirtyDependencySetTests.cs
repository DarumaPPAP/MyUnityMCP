#if UNITY_EDITOR

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	public sealed class DependencyBakeDirtySetTests
	{
		private const string TEMP_SCENE_PATH =
			"Assets/MyUnityMcpDependencyBakeDirtySetTemporaryScene.unity";

		[SetUp]
		public void SetUp()
		{
			EditorSceneManager.NewScene(
				NewSceneSetup.EmptyScene,
				NewSceneMode.Single);
			DependencyBakeSession.ClearForTests();
			AssetDatabase.DeleteAsset(TEMP_SCENE_PATH);
		}

		[TearDown]
		public void TearDown()
		{
			DependencyBakeSession.ClearForTests();
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
			DependencyBakeSession.TrackDirtySceneForTests(scene);
			long firstSerial = DependencyBakeSession.DirtySerial;

			DependencyBakeSession.TrackDirtySceneForTests(scene);

			Assert.That(
				DependencyBakeSession.DirtySerial,
				Is.EqualTo(firstSerial));
		}
	}
}

#endif
