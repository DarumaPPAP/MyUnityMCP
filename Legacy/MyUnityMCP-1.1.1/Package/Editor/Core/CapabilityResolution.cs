#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityGraphicsMcp
{
	public enum E_MCP_CAPABILITY_STATUS
	{
		AVAILABLE,
		UNAVAILABLE,
		UNSUPPORTED,
		UNVERIFIED,
		PACKAGE_NOT_INSTALLED,
		VERSION_NOT_SUPPORTED,
		PROJECT_CONFIGURATION_REQUIRED,
		BACKEND_NOT_IMPLEMENTED
	}

	/// <summary>
	/// 検出済みProject事実、今回要求されたTarget、Capability状態を分離します。
	/// </summary>
	public static partial class Inspection
	{
		public static ToolResult InspectProject(
			string requestId,
			string[] requestedPlatforms,
			string[] requestedConstraints)
		{
			ToolResult result = InspectProject(requestId);
			if (!result.IsSuccessful)
			{
				return result;
			}

			Dictionary<string, object> detectedProject =
				result.data as Dictionary<string, object> ?? new Dictionary<string, object>();

			Dictionary<string, object> pipeline =
				detectedProject.ContainsKey("renderPipeline")
					? detectedProject["renderPipeline"] as Dictionary<string, object>
					: null;

			pipeline = pipeline ?? new Dictionary<string, object>();
			string pipelineKind = pipeline.ContainsKey("kind")
				? pipeline["kind"] as string ?? "UNKNOWN"
				: "UNKNOWN";

			Dictionary<string, object> activeRenderer = ResolveActiveRendererSummary();
			pipeline["activeRenderer"] = activeRenderer;
			pipeline["packageVersion"] = ResolvePipelinePackageVersion();

			Object activeRendererAsset = ResolveActiveRendererAsset();
			if (activeRendererAsset != null)
			{
				pipeline["renderingPath"] = ResolveRenderingPathFromRenderer(activeRendererAsset);
			}

			detectedProject["renderPipeline"] = pipeline;
			string unityVersion = detectedProject.ContainsKey("unityVersion")
				? detectedProject["unityVersion"] as string ?? Application.unityVersion
				: Application.unityVersion;
			detectedProject["apiCompatibility"] =
				ApiCompatibility.BuildProjectSummary(unityVersion);
			detectedProject["apiCompatibilityPackages"] =
				PackageInspection.Inspect();

			Dictionary<string, object> requestedTarget = new Dictionary<string, object>
			{
				{ "platforms", NormalizeRequestedValues(requestedPlatforms) },
				{ "constraints", NormalizeRequestedValues(requestedConstraints) },
				{ "source", "EXPLICIT_REQUEST_ONLY" }
			};

			Dictionary<string, object> backendSelection = new Dictionary<string, object>
			{
				{ "detectedPipelineKind", pipelineKind },
				{ "inspectionBackend", "GENERIC_READ_ONLY" },
				{ "inspectionBackendStatus", E_MCP_CAPABILITY_STATUS.AVAILABLE.ToString() },
				{ "nativeMutationBackend", null },
				{ "nativeMutationBackendStatus", E_MCP_CAPABILITY_STATUS.BACKEND_NOT_IMPLEMENTED.ToString() },
				{ "silentFallbackUsed", false }
			};

			Dictionary<string, object> capabilities = new Dictionary<string, object>
			{
				{ "projectEnvironmentInspection", E_MCP_CAPABILITY_STATUS.AVAILABLE.ToString() },
				{ "unityApiCompatibility", E_MCP_CAPABILITY_STATUS.AVAILABLE.ToString() },
				{ "sceneCoreInspection", E_MCP_CAPABILITY_STATUS.AVAILABLE.ToString() },
				{ "graphicsValidation", E_MCP_CAPABILITY_STATUS.AVAILABLE.ToString() },
				{ "rendererFeatureInspection", ResolveRendererFeatureCapability(pipelineKind) },
				{ "pipelineNativeMutation", E_MCP_CAPABILITY_STATUS.BACKEND_NOT_IMPLEMENTED.ToString() },
				{ "frameInspection", E_MCP_CAPABILITY_STATUS.BACKEND_NOT_IMPLEMENTED.ToString() },
				{ "bake", E_MCP_CAPABILITY_STATUS.BACKEND_NOT_IMPLEMENTED.ToString() },
				{ "capture", E_MCP_CAPABILITY_STATUS.BACKEND_NOT_IMPLEMENTED.ToString() }
			};

			result.data = new Dictionary<string, object>
			{
				{ "detectedProject", detectedProject },
				{ "requestedTarget", requestedTarget },
				{ "backendSelection", backendSelection },
				{ "capabilities", capabilities },
				{ "verificationState", E_MCP_CAPABILITY_STATUS.UNVERIFIED.ToString() }
			};

			return result;
		}

		private static List<string> NormalizeRequestedValues(string[] values)
		{
			return values == null
				? new List<string>()
				: values
					.Where(item => !string.IsNullOrWhiteSpace(item))
					.Select(item => item.Trim())
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
		}

		private static string ResolveRendererFeatureCapability(string pipelineKind)
		{
			if (pipelineKind == "BUILT_IN")
			{
				return E_MCP_CAPABILITY_STATUS.UNAVAILABLE.ToString();
			}

			return ResolveRendererDataAssets().Count > 0
				? E_MCP_CAPABILITY_STATUS.AVAILABLE.ToString()
				: E_MCP_CAPABILITY_STATUS.UNVERIFIED.ToString();
		}

		private static string ResolvePipelinePackageVersion()
		{
			RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
			if (pipelineAsset == null)
			{
				return null;
			}

			PackageInfo packageInfo = PackageInfo.FindForAssembly(pipelineAsset.GetType().Assembly);
			return packageInfo == null ? null : packageInfo.version;
		}

		private static Dictionary<string, object> ResolveActiveRendererSummary()
		{
			Object rendererData = ResolveActiveRendererAsset();
			if (rendererData == null)
			{
				return new Dictionary<string, object>
				{
					{ "status", E_MCP_CAPABILITY_STATUS.UNVERIFIED.ToString() },
					{ "reason", "ACTIVE_RENDERER_COULD_NOT_BE_RESOLVED" }
				};
			}

			string stability;
			return new Dictionary<string, object>
			{
				{ "status", E_MCP_CAPABILITY_STATUS.AVAILABLE.ToString() },
				{ "name", rendererData.name },
				{ "type", rendererData.GetType().FullName },
				{ "objectId", ResolveObjectId(rendererData, out stability) },
				{ "idStability", stability }
			};
		}

		private static Object ResolveActiveRendererAsset()
		{
			List<Object> rendererDataAssets = ResolveRendererDataAssets();
			if (rendererDataAssets.Count == 0)
			{
				return null;
			}

			RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
			if (pipelineAsset == null)
			{
				return rendererDataAssets[0];
			}

			SerializedObject serializedObject = new SerializedObject(pipelineAsset);
			SerializedProperty defaultRendererIndex =
				serializedObject.FindProperty("m_DefaultRendererIndex");

			if (defaultRendererIndex == null ||
				defaultRendererIndex.propertyType != SerializedPropertyType.Integer)
			{
				return rendererDataAssets[0];
			}

			int index = defaultRendererIndex.intValue;
			return index >= 0 && index < rendererDataAssets.Count
				? rendererDataAssets[index]
				: rendererDataAssets[0];
		}

		private static string ResolveRenderingPathFromRenderer(Object rendererData)
		{
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
						return property.enumDisplayNames[enumIndex]
							.ToUpperInvariant()
							.Replace(" ", "_");
					}
				}

				if (property.propertyType == SerializedPropertyType.Integer)
				{
					return "SERIALIZED_VALUE_" + property.intValue;
				}
			}

			return "UNKNOWN";
		}
	}
}

#endif