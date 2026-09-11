#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityGraphicsMcp
{
	public static partial class Inspection
	{
		private static Dictionary<string, object> InspectRenderPipeline()
		{
			RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
			string pipelineKind = ResolvePipelineKind(pipelineAsset);
			Dictionary<string, object> data = new Dictionary<string, object>
			{
				{ "kind", pipelineKind },
				{ "assetName", pipelineAsset == null ? null : pipelineAsset.name },
				{ "assetType", pipelineAsset == null ? null : pipelineAsset.GetType().FullName },
				{ "assetPath", pipelineAsset == null ? null : AssetDatabase.GetAssetPath(pipelineAsset) },
				{ "packageVersion", ResolvePackageVersion(pipelineAsset) },
				{ "rendererData", InspectRendererDataAssets() },
				{ "renderingPath", ResolveRenderingPath() },
				{ "renderGraphMode", ResolveRenderGraphMode() }
			};

			if (pipelineAsset != null)
			{
				string stability;
				data["assetId"] = ResolveObjectId(pipelineAsset, out stability);
				data["assetIdStability"] = stability;
			}

			return data;
		}

		private static string ResolvePipelineKind(RenderPipelineAsset pipelineAsset)
		{
			if (pipelineAsset == null)
			{
				return "BUILT_IN";
			}

			string fullName = pipelineAsset.GetType().FullName ?? pipelineAsset.GetType().Name;
			if (fullName.IndexOf("UniversalRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "UNIVERSAL";
			}

			if (fullName.IndexOf("HDRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "HIGH_DEFINITION";
			}

			return "CUSTOM_SRP";
		}

		private static string ResolvePackageVersion(Object asset)
		{
			if (asset == null)
			{
				return null;
			}

			string assetPath = AssetDatabase.GetAssetPath(asset);
			if (string.IsNullOrEmpty(assetPath))
			{
				return null;
			}

			PackageInfo packageInfo = PackageInfo.FindForAssetPath(assetPath);
			return packageInfo == null ? null : packageInfo.version;
		}

		private static List<Dictionary<string, object>> InspectRendererDataAssets()
		{
			List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
			List<Object> rendererDataAssets = ResolveRendererDataAssets();

			for (int index = 0; index < rendererDataAssets.Count; index++)
			{
				Object rendererData = rendererDataAssets[index];
				string stability;
				results.Add(new Dictionary<string, object>
				{
					{ "index", index },
					{ "name", rendererData.name },
					{ "type", rendererData.GetType().FullName },
					{ "objectId", ResolveObjectId(rendererData, out stability) },
					{ "idStability", stability },
					{ "featureCount", ResolveRendererFeatureCount(rendererData) }
				});
			}

			return results;
		}

		private static List<Object> ResolveRendererDataAssets()
		{
			List<Object> rendererDataAssets = new List<Object>();
			RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
			if (pipelineAsset == null)
			{
				return rendererDataAssets;
			}

			SerializedObject serializedObject = new SerializedObject(pipelineAsset);
			SerializedProperty rendererDataList = serializedObject.FindProperty("m_RendererDataList");
			if (rendererDataList != null && rendererDataList.isArray)
			{
				for (int index = 0; index < rendererDataList.arraySize; index++)
				{
					Object rendererData = rendererDataList.GetArrayElementAtIndex(index).objectReferenceValue;
					if (rendererData != null)
					{
						rendererDataAssets.Add(rendererData);
					}
				}
			}

			if (rendererDataAssets.Count == 0)
			{
				SerializedProperty rendererData = serializedObject.FindProperty("m_RendererData");
				if (rendererData != null && rendererData.objectReferenceValue != null)
				{
					rendererDataAssets.Add(rendererData.objectReferenceValue);
				}
			}

			return rendererDataAssets;
		}

		private static int ResolveRendererFeatureCount(Object rendererData)
		{
			SerializedObject serializedObject = new SerializedObject(rendererData);
			SerializedProperty features = serializedObject.FindProperty("m_RendererFeatures");
			return features != null && features.isArray ? features.arraySize : 0;
		}

		private static string ResolveRenderingPath()
		{
			List<Object> rendererDataAssets = ResolveRendererDataAssets();
			if (rendererDataAssets.Count == 0)
			{
				return "UNKNOWN";
			}

			Object rendererData = rendererDataAssets[0];
			string fullName = rendererData.GetType().FullName ?? rendererData.GetType().Name;
			if (fullName.IndexOf("Renderer2D", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "2D";
			}

			SerializedObject serializedObject = new SerializedObject(rendererData);
			string[] propertyNames =
			{
				"m_RenderingMode",
				"m_RenderingModeActual",
				"m_RenderingModeRequested"
			};

			foreach (string propertyName in propertyNames)
			{
				SerializedProperty property = serializedObject.FindProperty(propertyName);
				if (property == null)
				{
					continue;
				}

				if (property.propertyType == SerializedPropertyType.Enum)
				{
					int enumIndex = property.enumValueIndex;
					if (enumIndex >= 0 && enumIndex < property.enumDisplayNames.Length)
					{
						return property.enumDisplayNames[enumIndex].ToUpperInvariant().Replace(" ", "_");
					}
				}

				if (property.propertyType == SerializedPropertyType.Integer)
				{
					return "SERIALIZED_VALUE_" + property.intValue;
				}
			}

			return "UNKNOWN";
		}

		private static string ResolveRenderGraphMode()
		{
			Object[] candidates = ResolveRendererDataAssets().ToArray();
			RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
			if (pipelineAsset != null)
			{
				Array.Resize(ref candidates, candidates.Length + 1);
				candidates[candidates.Length - 1] = pipelineAsset;
			}

			string[] booleanProperties =
			{
				"m_UseRenderGraph",
				"m_EnableRenderGraph",
				"m_RenderGraphEnabled",
				"m_EnableRenderCompatibilityMode"
			};

			foreach (Object candidate in candidates)
			{
				if (candidate == null)
				{
					continue;
				}

				SerializedObject serializedObject = new SerializedObject(candidate);
				foreach (string propertyName in booleanProperties)
				{
					SerializedProperty property = serializedObject.FindProperty(propertyName);
					if (property == null || property.propertyType != SerializedPropertyType.Boolean)
					{
						continue;
					}

					if (propertyName.IndexOf("CompatibilityMode", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return property.boolValue ? "COMPATIBILITY_MODE" : "ENABLED";
					}

					return property.boolValue ? "ENABLED" : "DISABLED";
				}
			}

			return "UNKNOWN";
		}

		private static List<Dictionary<string, object>> InspectRelevantPackages()
		{
			List<Dictionary<string, object>> packages = new List<Dictionary<string, object>>();
			PackageInfo[] registeredPackages = PackageInfo.GetAllRegisteredPackages();
			if (registeredPackages == null)
			{
				return packages;
			}

			foreach (PackageInfo packageInfo in registeredPackages)
			{
				if (packageInfo == null || !RELEVANT_PACKAGE_NAMES.Contains(packageInfo.name))
				{
					continue;
				}

				packages.Add(new Dictionary<string, object>
				{
					{ "name", packageInfo.name },
					{ "version", packageInfo.version },
					{ "source", packageInfo.source.ToString() }
				});
			}

			return packages.OrderBy(item => item["name"]).ToList();
		}

		private static List<Dictionary<string, object>> InspectLoadedScenes()
		{
			List<Dictionary<string, object>> scenes = new List<Dictionary<string, object>>();

			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				scenes.Add(new Dictionary<string, object>
				{
					{ "name", scene.name },
					{ "path", scene.path },
					{ "isLoaded", scene.isLoaded },
					{ "isDirty", scene.isDirty },
					{ "isActive", scene == SceneManager.GetActiveScene() },
					{ "rootCount", scene.isLoaded ? scene.rootCount : 0 }
				});
			}

			return scenes;
		}

		private static List<string> ResolveInstalledBuildTargets()
		{
			HashSet<string> targets = new HashSet<string>();
			Array values = Enum.GetValues(typeof(BuildTarget));

			foreach (object value in values)
			{
				BuildTarget target = (BuildTarget)value;
				if (target == BuildTarget.NoTarget)
				{
					continue;
				}

				try
				{
					BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
					if (group != BuildTargetGroup.Unknown &&
						BuildPipeline.IsBuildTargetSupported(group, target))
					{
						targets.Add(target.ToString());
					}
				}
				catch
				{
					// Unity Version固有の廃止Targetは無視します。
				}
			}

			return targets.OrderBy(item => item).ToList();
		}

		private static List<string> ResolveGraphicsApis()
		{
			try
			{
				return PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget)
					.Select(item => item.ToString())
					.ToList();
			}
			catch (Exception exception)
			{
				return new List<string> { "UNRESOLVED:" + exception.GetType().Name };
			}
		}

		private static string ResolveScriptingBackend()
		{
			try
			{
				BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(
					EditorUserBuildSettings.activeBuildTarget);

				MethodInfo[] methods = typeof(PlayerSettings).GetMethods(
					BindingFlags.Public | BindingFlags.Static);

				foreach (MethodInfo method in methods)
				{
					if (method.Name != "GetScriptingBackend")
					{
						continue;
					}

					ParameterInfo[] parameters = method.GetParameters();
					if (parameters.Length != 1)
					{
						continue;
					}

					Type parameterType = parameters[0].ParameterType;
					if (parameterType == typeof(BuildTargetGroup))
					{
						object result = method.Invoke(null, new object[] { group });
						return result == null ? "UNKNOWN" : result.ToString();
					}

					if (parameterType.FullName == "UnityEditor.Build.NamedBuildTarget")
					{
						MethodInfo fromGroup = parameterType.GetMethod(
							"FromBuildTargetGroup",
							BindingFlags.Public | BindingFlags.Static);

						if (fromGroup == null)
						{
							continue;
						}

						object namedBuildTarget = fromGroup.Invoke(null, new object[] { group });
						object result = method.Invoke(null, new[] { namedBuildTarget });
						return result == null ? "UNKNOWN" : result.ToString();
					}
				}
			}
			catch (Exception exception)
			{
				return "UNRESOLVED:" + exception.GetType().Name;
			}

			return "UNKNOWN";
		}

		private static Object ResolveLightingDataAsset()
		{
			PropertyInfo property = typeof(Lightmapping).GetProperty(
				"lightingDataAsset",
				BindingFlags.Public | BindingFlags.Static);

			return property == null ? null : property.GetValue(null, null) as Object;
		}

		private static HashSet<string> NormalizeSections(string[] sections)
		{
			if (sections == null || sections.Length == 0)
			{
				return null;
			}

			return new HashSet<string>(
				sections.Where(item => !string.IsNullOrWhiteSpace(item))
					.Select(item => item.Trim().ToUpperInvariant()),
				StringComparer.OrdinalIgnoreCase);
		}

		private static bool ShouldInclude(HashSet<string> sections, string section)
		{
			return sections == null || sections.Contains(section);
		}

		private static bool TryParseCursor(string cursor, out int value)
		{
			if (string.IsNullOrWhiteSpace(cursor))
			{
				value = 0;
				return true;
			}

			return int.TryParse(cursor, out value) && value >= 0;
		}

		private static string ResolveObjectId(Object target, out string stability)
		{
			if (target == null)
			{
				stability = "NONE";
				return null;
			}

			try
			{
				GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target);
				string value = globalObjectId.ToString();
				if (!string.IsNullOrEmpty(value) &&
					!value.StartsWith("GlobalObjectId_V1-0-", StringComparison.Ordinal))
				{
					stability = "GLOBAL";
					return value;
				}
			}
			catch
			{
				// 未保存Object等はSession限定IDへFallbackします。
			}

			stability = "SESSION_ONLY";
			return "instance:" + IdentityCompatibility.GetObjectToken(target);
		}

		private static bool TryReadMember(object target, string memberName, out object value)
		{
			value = null;
			if (target == null)
			{
				return false;
			}

			Type type = target.GetType();
			PropertyInfo property = type.GetProperty(
				memberName,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);

			if (property != null && property.GetIndexParameters().Length == 0)
			{
				try
				{
					value = property.GetValue(target, null);
					return true;
				}
				catch
				{
					return false;
				}
			}

			FieldInfo field = type.GetField(
				memberName,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);

			if (field == null)
			{
				return false;
			}

			try
			{
				value = field.GetValue(target);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool ReadBooleanMember(object target, string memberName, bool fallback)
		{
			object value;
			return TryReadMember(target, memberName, out value) && value is bool
				? (bool)value
				: fallback;
		}

		private static object NormalizeValue(object value)
		{
			if (value == null)
			{
				return null;
			}

			Object unityObject = value as Object;
			if (unityObject != null)
			{
				string stability;
				return new Dictionary<string, object>
				{
					{ "name", unityObject.name },
					{ "type", unityObject.GetType().FullName },
					{ "objectId", ResolveObjectId(unityObject, out stability) },
					{ "idStability", stability }
				};
			}

			if (value is Vector2)
			{
				Vector2 vector = (Vector2)value;
				return new Dictionary<string, object>
				{
					{ "x", vector.x },
					{ "y", vector.y }
				};
			}

			if (value is Vector3)
			{
				return VectorToDictionary((Vector3)value);
			}

			if (value is Vector4)
			{
				return VectorToDictionary((Vector4)value);
			}

			if (value.GetType().IsEnum)
			{
				return value.ToString();
			}

			if (value is IEnumerable && !(value is string))
			{
				List<object> normalized = new List<object>();
				foreach (object item in (IEnumerable)value)
				{
					normalized.Add(NormalizeValue(item));
				}

				return normalized;
			}

			return value;
		}

		private static Dictionary<string, object> VectorToDictionary(Vector3 vector)
		{
			return new Dictionary<string, object>
			{
				{ "x", vector.x },
				{ "y", vector.y },
				{ "z", vector.z }
			};
		}

		private static Dictionary<string, object> VectorToDictionary(Vector4 vector)
		{
			return new Dictionary<string, object>
			{
				{ "x", vector.x },
				{ "y", vector.y },
				{ "z", vector.z },
				{ "w", vector.w }
			};
		}

		private static void IncrementCount(Dictionary<string, int> counts, string category)
		{
			int value;
			counts.TryGetValue(category, out value);
			counts[category] = value + 1;
		}
	}
}

#endif
