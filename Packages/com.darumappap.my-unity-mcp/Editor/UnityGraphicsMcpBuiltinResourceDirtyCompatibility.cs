#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Camera Render時にUnity組み込みMaterialへ付く一時Dirty FlagをProject Asset変更から分離します。
	/// </summary>
	[InitializeOnLoad]
	internal static class UnityGraphicsMcpBuiltinResourceDirtyCompatibility
	{
		private const string DEFAULT_MATERIAL_NAME = "Default-Material";
		private const string BUILTIN_RESOURCE_PATH = "Resources/unity_builtin_extra";

		static UnityGraphicsMcpBuiltinResourceDirtyCompatibility()
		{
			Camera.onPostRender += OnCameraRenderCompleted;
			RenderPipelineManager.endCameraRendering += OnCameraRenderCompleted;
		}

		private static void OnCameraRenderCompleted(Camera camera)
		{
			ClearTransientDefaultMaterialDirtyFlag();
		}

		private static void OnCameraRenderCompleted(
			ScriptableRenderContext context,
			Camera camera)
		{
			ClearTransientDefaultMaterialDirtyFlag();
		}

		private static void ClearTransientDefaultMaterialDirtyFlag()
		{
			foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
			{
				if (material == null ||
					!EditorUtility.IsDirty(material) ||
					material.name != DEFAULT_MATERIAL_NAME ||
					AssetDatabase.GetAssetPath(material) != BUILTIN_RESOURCE_PATH)
				{
					continue;
				}

				EditorUtility.ClearDirty(material);
			}
		}
	}
}

#endif
