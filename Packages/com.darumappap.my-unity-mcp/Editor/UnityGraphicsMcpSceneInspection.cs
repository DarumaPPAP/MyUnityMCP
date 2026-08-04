#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public static partial class UnityGraphicsMcpInspection
	{
		private static UnityGraphicsMcpSceneSnapshot BuildSceneSnapshot(
			bool includeInactive,
			HashSet<string> sections,
			long revision)
		{
			UnityGraphicsMcpSceneSnapshot snapshot = new UnityGraphicsMcpSceneSnapshot
			{
				Revision = revision,
				CreatedUtc = DateTime.UtcNow
			};

			Dictionary<string, int> counts =
				new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			HashSet<int> materialIds = new HashSet<int>();
			HashSet<int> shaderIds = new HashSet<int>();

			for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
			{
				Scene scene = SceneManager.GetSceneAt(sceneIndex);
				if (!scene.IsValid() || !scene.isLoaded)
				{
					continue;
				}

				foreach (GameObject rootObject in scene.GetRootGameObjects())
				{
					TraverseGameObject(
						rootObject,
						rootObject.name,
						scene.path,
						includeInactive,
						sections,
						snapshot.Items,
						counts,
						materialIds,
						shaderIds);
				}
			}

			if (ShouldInclude(sections, "LIGHTMAP"))
			{
				AddLightmapState(snapshot.Items, counts);
			}

			if (ShouldInclude(sections, "RENDERER_FEATURE"))
			{
				AddRendererFeatureState(snapshot.Items, counts);
			}

			snapshot.Summary["loadedSceneCount"] = SceneManager.sceneCount;
			snapshot.Summary["counts"] = counts;
			snapshot.Summary["uniqueMaterialCount"] = materialIds.Count;
			snapshot.Summary["uniqueShaderCount"] = shaderIds.Count;
			snapshot.Summary["lightmapCount"] = LightmapSettings.lightmaps == null
				? 0
				: LightmapSettings.lightmaps.Length;
			snapshot.Summary["lightProbeCount"] = LightmapSettings.lightProbes == null
				? 0
				: LightmapSettings.lightProbes.count;

			return snapshot;
		}

		private static void TraverseGameObject(
			GameObject gameObject,
			string hierarchyPath,
			string scenePath,
			bool includeInactive,
			HashSet<string> sections,
			List<UnityGraphicsMcpSceneItem> items,
			Dictionary<string, int> counts,
			HashSet<int> materialIds,
			HashSet<int> shaderIds)
		{
			if (!includeInactive && !gameObject.activeInHierarchy)
			{
				return;
			}

			AddKnownComponents(
				gameObject,
				hierarchyPath,
				scenePath,
				sections,
				items,
				counts,
				materialIds,
				shaderIds);

			AddPackageComponents(
				gameObject,
				hierarchyPath,
				scenePath,
				sections,
				items,
				counts);

			for (int childIndex = 0; childIndex < gameObject.transform.childCount; childIndex++)
			{
				Transform child = gameObject.transform.GetChild(childIndex);
				TraverseGameObject(
					child.gameObject,
					hierarchyPath + "/" + child.name,
					scenePath,
					includeInactive,
					sections,
					items,
					counts,
					materialIds,
					shaderIds);
			}
		}

		private static void AddKnownComponents(
			GameObject gameObject,
			string hierarchyPath,
			string scenePath,
			HashSet<string> sections,
			List<UnityGraphicsMcpSceneItem> items,
			Dictionary<string, int> counts,
			HashSet<int> materialIds,
			HashSet<int> shaderIds)
		{
			if (ShouldInclude(sections, "CAMERA"))
			{
				Camera camera = gameObject.GetComponent<Camera>();
				if (camera != null)
				{
					AddComponentItem(items, counts, "CAMERA", camera, hierarchyPath, scenePath,
						new Dictionary<string, object>
						{
							{ "enabled", camera.enabled },
							{ "orthographic", camera.orthographic },
							{ "fieldOfView", camera.fieldOfView },
							{ "depth", camera.depth },
							{ "clearFlags", camera.clearFlags.ToString() },
							{ "cullingMask", camera.cullingMask }
						});
				}
			}

			if (ShouldInclude(sections, "LIGHT"))
			{
				Light light = gameObject.GetComponent<Light>();
				if (light != null)
				{
					AddComponentItem(items, counts, "LIGHT", light, hierarchyPath, scenePath,
						new Dictionary<string, object>
						{
							{ "enabled", light.enabled },
							{ "type", light.type.ToString() },
							{ "mode", light.lightmapBakeType.ToString() },
							{ "intensity", light.intensity },
							{ "range", light.range },
							{ "shadows", light.shadows.ToString() },
							{ "cullingMask", light.cullingMask }
						});
				}
			}

			if (ShouldInclude(sections, "REFLECTION_PROBE"))
			{
				ReflectionProbe probe = gameObject.GetComponent<ReflectionProbe>();
				if (probe != null)
				{
					AddComponentItem(items, counts, "REFLECTION_PROBE", probe, hierarchyPath, scenePath,
						new Dictionary<string, object>
						{
							{ "enabled", probe.enabled },
							{ "mode", probe.mode.ToString() },
							{ "importance", probe.importance },
							{ "boxProjection", probe.boxProjection },
							{ "size", VectorToDictionary(probe.size) },
							{ "blendDistance", probe.blendDistance }
						});
				}
			}

			if (ShouldInclude(sections, "LIGHT_PROBE"))
			{
				LightProbeGroup probeGroup = gameObject.GetComponent<LightProbeGroup>();
				if (probeGroup != null)
				{
					AddComponentItem(items, counts, "LIGHT_PROBE_GROUP", probeGroup, hierarchyPath, scenePath,
						new Dictionary<string, object>
						{
							{ "probeCount", probeGroup.probePositions == null ? 0 : probeGroup.probePositions.Length }
						});
				}

				LightProbeProxyVolume proxyVolume = gameObject.GetComponent<LightProbeProxyVolume>();
				if (proxyVolume != null)
				{
					AddComponentItem(items, counts, "LIGHT_PROBE_PROXY_VOLUME", proxyVolume, hierarchyPath, scenePath,
						new Dictionary<string, object>
						{
							{ "enabled", proxyVolume.enabled },
							{ "resolutionMode", proxyVolume.resolutionMode.ToString() },
							{ "boundingBoxMode", proxyVolume.boundingBoxMode.ToString() }
						});
				}
			}

			if (ShouldInclude(sections, "PARTICLE"))
			{
				ParticleSystem particleSystem = gameObject.GetComponent<ParticleSystem>();
				if (particleSystem != null)
				{
					ParticleSystem.MainModule main = particleSystem.main;
					AddComponentItem(items, counts, "PARTICLE_SYSTEM", particleSystem, hierarchyPath, scenePath,
						new Dictionary<string, object>
						{
							{ "playOnAwake", main.playOnAwake },
							{ "loop", main.loop },
							{ "maxParticles", main.maxParticles },
							{ "simulationSpace", main.simulationSpace.ToString() }
						});
				}
			}

			if (ShouldInclude(sections, "RENDERER_MATERIAL"))
			{
				Renderer renderer = gameObject.GetComponent<Renderer>();
				if (renderer != null)
				{
					AddRendererItem(items, counts, renderer, hierarchyPath, scenePath, materialIds, shaderIds);
				}
			}

			if (ShouldInclude(sections, "CINEMATIC"))
			{
				PlayableDirector director = gameObject.GetComponent<PlayableDirector>();
				if (director != null)
				{
					AddComponentItem(items, counts, "PLAYABLE_DIRECTOR", director, hierarchyPath, scenePath,
						new Dictionary<string, object>
						{
							{ "playableAsset", director.playableAsset == null ? null : director.playableAsset.name },
							{ "duration", director.duration },
							{ "timeUpdateMode", director.timeUpdateMode.ToString() }
						});
				}
			}
		}

		private static void AddPackageComponents(
			GameObject gameObject,
			string hierarchyPath,
			string scenePath,
			HashSet<string> sections,
			List<UnityGraphicsMcpSceneItem> items,
			Dictionary<string, int> counts)
		{
			foreach (Component component in gameObject.GetComponents<Component>())
			{
				if (component == null)
				{
					continue;
				}

				string fullName = component.GetType().FullName ?? component.GetType().Name;
				if (ShouldInclude(sections, "VOLUME") && fullName == "UnityEngine.Rendering.Volume")
				{
					AddGenericComponentItem(items, counts, "VOLUME", component, hierarchyPath, scenePath,
						new[] { "isGlobal", "priority", "blendDistance", "weight", "sharedProfile" });
				}
				else if (ShouldInclude(sections, "DECAL") &&
					fullName.IndexOf("DecalProjector", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					AddGenericComponentItem(items, counts, "DECAL", component, hierarchyPath, scenePath,
						new[] { "material", "size", "fadeFactor", "drawDistance" });
				}
				else if (ShouldInclude(sections, "APV") &&
					fullName.IndexOf("ProbeVolume", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					AddGenericComponentItem(items, counts, "PROBE_VOLUME", component, hierarchyPath, scenePath,
						new[] { "size", "globalVolume", "mode" });
				}
				else if (ShouldInclude(sections, "CINEMATIC") &&
					fullName.IndexOf("Cinemachine", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					AddGenericComponentItem(items, counts, "CINEMACHINE", component, hierarchyPath, scenePath,
						new[] { "Priority", "StandbyUpdate", "Follow", "LookAt" });
				}
				else if (ShouldInclude(sections, "VFX") && fullName == "UnityEngine.VFX.VisualEffect")
				{
					AddGenericComponentItem(items, counts, "VISUAL_EFFECT", component, hierarchyPath, scenePath,
						new[] { "visualEffectAsset", "pause", "playRate" });
				}
			}
		}

		private static void AddRendererItem(
			List<UnityGraphicsMcpSceneItem> items,
			Dictionary<string, int> counts,
			Renderer renderer,
			string hierarchyPath,
			string scenePath,
			HashSet<int> materialIds,
			HashSet<int> shaderIds)
		{
			Material[] materials = renderer.sharedMaterials;
			List<Dictionary<string, object>> materialSummaries =
				new List<Dictionary<string, object>>();

			for (int index = 0; index < materials.Length; index++)
			{
				Material material = materials[index];
				if (material == null)
				{
					materialSummaries.Add(new Dictionary<string, object>
					{
						{ "index", index },
						{ "missing", true }
					});
					continue;
				}

				materialIds.Add(material.GetInstanceID());
				Shader shader = material.shader;
				if (shader != null)
				{
					shaderIds.Add(shader.GetInstanceID());
				}

				string stability;
				materialSummaries.Add(new Dictionary<string, object>
				{
					{ "index", index },
					{ "materialId", ResolveObjectId(material, out stability) },
					{ "idStability", stability },
					{ "materialName", material.name },
					{ "shaderName", shader == null ? null : shader.name },
					{ "renderQueue", material.renderQueue },
					{ "keywordCount", material.shaderKeywords == null ? 0 : material.shaderKeywords.Length }
				});
			}

			AddComponentItem(items, counts, "RENDERER", renderer, hierarchyPath, scenePath,
				new Dictionary<string, object>
				{
					{ "rendererType", renderer.GetType().FullName },
					{ "enabled", renderer.enabled },
					{ "lightmapIndex", renderer.lightmapIndex },
					{ "lightmapScaleOffset", VectorToDictionary(renderer.lightmapScaleOffset) },
					{ "materialCount", materials.Length },
					{ "materials", materialSummaries }
				});
		}

		private static void AddGenericComponentItem(
			List<UnityGraphicsMcpSceneItem> items,
			Dictionary<string, int> counts,
			string category,
			Component component,
			string hierarchyPath,
			string scenePath,
			string[] memberNames)
		{
			Dictionary<string, object> values = new Dictionary<string, object>
			{
				{ "componentType", component.GetType().FullName }
			};

			foreach (string memberName in memberNames)
			{
				object value;
				if (TryReadMember(component, memberName, out value))
				{
					values[memberName] = NormalizeValue(value);
				}
			}

			AddComponentItem(items, counts, category, component, hierarchyPath, scenePath, values);
		}

		private static void AddComponentItem(
			List<UnityGraphicsMcpSceneItem> items,
			Dictionary<string, int> counts,
			string category,
			Object target,
			string hierarchyPath,
			string scenePath,
			Dictionary<string, object> values)
		{
			string stability;
			items.Add(new UnityGraphicsMcpSceneItem
			{
				category = category,
				objectId = ResolveObjectId(target, out stability),
				idStability = stability,
				name = target.name,
				hierarchyPath = hierarchyPath,
				scenePath = scenePath,
				values = values
			});

			IncrementCount(counts, category);
		}

		private static void AddLightmapState(
			List<UnityGraphicsMcpSceneItem> items,
			Dictionary<string, int> counts)
		{
			Object lightingDataAsset = ResolveLightingDataAsset();
			string stability = null;
			string objectId = lightingDataAsset == null
				? null
				: ResolveObjectId(lightingDataAsset, out stability);

			items.Add(new UnityGraphicsMcpSceneItem
			{
				category = "LIGHTMAP_STATE",
				objectId = objectId,
				idStability = stability,
				name = "LightmapSettings",
				values = new Dictionary<string, object>
				{
					{ "lightmapCount", LightmapSettings.lightmaps == null ? 0 : LightmapSettings.lightmaps.Length },
					{ "lightProbeCount", LightmapSettings.lightProbes == null ? 0 : LightmapSettings.lightProbes.count },
					{ "lightingDataAsset", lightingDataAsset == null ? null : lightingDataAsset.name }
				}
			});

			IncrementCount(counts, "LIGHTMAP_STATE");
		}

		private static void AddRendererFeatureState(
			List<UnityGraphicsMcpSceneItem> items,
			Dictionary<string, int> counts)
		{
			foreach (Object rendererData in ResolveRendererDataAssets())
			{
				SerializedObject serializedObject = new SerializedObject(rendererData);
				SerializedProperty features = serializedObject.FindProperty("m_RendererFeatures");
				if (features == null || !features.isArray)
				{
					continue;
				}

				for (int index = 0; index < features.arraySize; index++)
				{
					Object feature = features.GetArrayElementAtIndex(index).objectReferenceValue;
					if (feature == null)
					{
						continue;
					}

					string stability;
					items.Add(new UnityGraphicsMcpSceneItem
					{
						category = "RENDERER_FEATURE",
						objectId = ResolveObjectId(feature, out stability),
						idStability = stability,
						name = feature.name,
						values = new Dictionary<string, object>
						{
							{ "featureType", feature.GetType().FullName },
							{ "rendererData", rendererData.name },
							{ "order", index },
							{ "active", ReadBooleanMember(feature, "isActive", true) }
						}
					});

					IncrementCount(counts, "RENDERER_FEATURE");
				}
			}
		}
	}
}

#endif
