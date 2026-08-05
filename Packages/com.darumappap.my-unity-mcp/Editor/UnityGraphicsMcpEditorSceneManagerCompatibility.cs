#if UNITY_EDITOR

using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Unity 6000.0と新しいUnity 6系のEditorSceneManager API差異を吸収します。
	/// </summary>
	internal static class EditorSceneManager
	{
		private static readonly MethodInfo _clearSceneDirtinessMethod =
			typeof(UnityEditor.SceneManagement.EditorSceneManager).GetMethod(
				"ClearSceneDirtiness",
				BindingFlags.Static |
				BindingFlags.Public |
				BindingFlags.NonPublic,
				null,
				new[] { typeof(Scene) },
				null);

		public static event UnityEditor.SceneManagement.EditorSceneManager.SceneOpenedCallback sceneOpened
		{
			add => UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += value;
			remove => UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= value;
		}

		public static event UnityEditor.SceneManagement.EditorSceneManager.SceneClosedCallback sceneClosed
		{
			add => UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += value;
			remove => UnityEditor.SceneManagement.EditorSceneManager.sceneClosed -= value;
		}

		public static event UnityEditor.SceneManagement.EditorSceneManager.SceneSavedCallback sceneSaved
		{
			add => UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += value;
			remove => UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= value;
		}

		public static event UnityAction<Scene, Scene> activeSceneChangedInEditMode
		{
			add => UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += value;
			remove => UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= value;
		}

		public static Scene NewScene(
			NewSceneSetup setup,
			NewSceneMode mode)
		{
			return UnityEditor.SceneManagement.EditorSceneManager.NewScene(
				setup,
				mode);
		}

		public static bool MarkSceneDirty(Scene scene)
		{
			return UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
		}

		public static bool SaveScene(
			Scene scene,
			string destinationScenePath,
			bool saveAsCopy = false)
		{
			return UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
				scene,
				destinationScenePath,
				saveAsCopy);
		}

		public static void ClearSceneDirtiness(Scene scene)
		{
			if (!scene.IsValid() || !scene.isDirty)
			{
				return;
			}

			if (_clearSceneDirtinessMethod != null)
			{
				_clearSceneDirtinessMethod.Invoke(null, new object[] { scene });
				return;
			}

			foreach (GameObject root in scene.GetRootGameObjects())
			{
				foreach (Component component in root.GetComponentsInChildren<Component>(true))
				{
					if (component != null)
					{
						EditorUtility.ClearDirty(component);
					}
				}

				EditorUtility.ClearDirty(root);
			}
		}
	}
}

#endif
