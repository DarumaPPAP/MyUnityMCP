#if UNITY_EDITOR

using System;
using System.Collections.Generic;
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
		private static readonly HashSet<ulong> _knownDirtySceneHandles =
			new HashSet<ulong>();
		private static readonly HashSet<ulong> _loadedSceneHandles =
			new HashSet<ulong>();

		static EditorSceneManager()
		{
			EditorApplication.update += PollSceneDirtiness;
			UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += OnSceneClosed;
		}

		/// <summary>
		/// Unity 6000.0に存在しないsceneDirtiedを、Scene.isDirtyの遷移監視で補います。
		/// </summary>
		public static event Action<Scene> sceneDirtied;

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
			bool marked = UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
			if (marked && scene.IsValid())
			{
				_knownDirtySceneHandles.Add(
					UnityGraphicsMcpIdentityCompatibility.GetSceneHandle(scene));
				sceneDirtied?.Invoke(scene);
			}

			return marked;
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
				_knownDirtySceneHandles.Remove(
					UnityGraphicsMcpIdentityCompatibility.GetSceneHandle(scene));
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

			_knownDirtySceneHandles.Remove(
				UnityGraphicsMcpIdentityCompatibility.GetSceneHandle(scene));
		}

		private static void PollSceneDirtiness()
		{
			_loadedSceneHandles.Clear();

			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (!scene.IsValid() || !scene.isLoaded)
				{
					continue;
				}

				ulong sceneHandle =
					UnityGraphicsMcpIdentityCompatibility.GetSceneHandle(scene);
				_loadedSceneHandles.Add(sceneHandle);
				if (scene.isDirty)
				{
					if (_knownDirtySceneHandles.Add(sceneHandle))
					{
						sceneDirtied?.Invoke(scene);
					}
				}
				else
				{
					_knownDirtySceneHandles.Remove(sceneHandle);
				}
			}

			_knownDirtySceneHandles.RemoveWhere(
				handle => !_loadedSceneHandles.Contains(handle));
		}

		private static void OnSceneClosed(Scene scene)
		{
			_knownDirtySceneHandles.Remove(
				UnityGraphicsMcpIdentityCompatibility.GetSceneHandle(scene));
		}
	}
}

#endif
