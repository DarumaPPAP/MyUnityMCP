#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public static partial class UnityGraphicsMcpInspection
	{
		private static List<UnityGraphicsMcpFinding> BuildValidationFindings(bool includeInactive)
		{
			List<UnityGraphicsMcpFinding> findings = new List<UnityGraphicsMcpFinding>();
			int lightmapCount = LightmapSettings.lightmaps == null
				? 0
				: LightmapSettings.lightmaps.Length;

			foreach (GameObject gameObject in EnumerateLoadedGameObjects(includeInactive))
			{
				ValidateRenderers(gameObject, lightmapCount, findings);
				ValidatePackageComponents(gameObject, findings);
			}

			Object lightingDataAsset = ResolveLightingDataAsset();
			if (lightmapCount > 0 && lightingDataAsset == null)
			{
				findings.Add(new UnityGraphicsMcpFinding
				{
					ruleId = "GFX-LIGHTMAP-002",
					kind = E_GRAPHICS_RULE_KIND.HEURISTIC.ToString(),
					severity = E_FINDING_SEVERITY.WARNING.ToString(),
					confidence = E_FINDING_CONFIDENCE.LIKELY.ToString(),
					message = "Lightmapは存在しますがLightingDataAssetを確認できません。意図した自前管理か確認してください。",
					evidence = new Dictionary<string, object>
					{
						{ "lightmapCount", lightmapCount },
						{ "lightingDataAsset", null }
					}
				});
			}

			if (ResolvePipelineKind(GraphicsSettings.currentRenderPipeline) == "UNIVERSAL" &&
				ResolveRendererDataAssets().Count == 0)
			{
				findings.Add(new UnityGraphicsMcpFinding
				{
					ruleId = "GFX-PIPELINE-001",
					kind = E_GRAPHICS_RULE_KIND.INVARIANT.ToString(),
					severity = E_FINDING_SEVERITY.ERROR.ToString(),
					confidence = E_FINDING_CONFIDENCE.CONFIRMED.ToString(),
					message = "Universal Render Pipeline AssetからRenderer Dataを解決できません。",
					evidence = new Dictionary<string, object>
					{
						{ "pipeline", "UNIVERSAL" }
					}
				});
			}

			return findings;
		}

		private static void ValidateRenderers(
			GameObject gameObject,
			int lightmapCount,
			List<UnityGraphicsMcpFinding> findings)
		{
			foreach (Renderer renderer in gameObject.GetComponents<Renderer>())
			{
				Material[] materials = renderer.sharedMaterials;
				for (int index = 0; index < materials.Length; index++)
				{
					Material material = materials[index];
					if (material == null)
					{
						findings.Add(CreateFinding(
							"GFX-MATERIAL-001",
							E_GRAPHICS_RULE_KIND.INVARIANT,
							E_FINDING_SEVERITY.ERROR,
							E_FINDING_CONFIDENCE.CONFIRMED,
							"RendererのShared Material参照がMissingです。",
							renderer,
							new Dictionary<string, object> { { "materialIndex", index } }));
					}
					else if (material.shader == null)
					{
						findings.Add(CreateFinding(
							"GFX-MATERIAL-002",
							E_GRAPHICS_RULE_KIND.INVARIANT,
							E_FINDING_SEVERITY.ERROR,
							E_FINDING_CONFIDENCE.CONFIRMED,
							"MaterialのShader参照がMissingです。",
							material,
							new Dictionary<string, object> { { "renderer", renderer.name } }));
					}
				}

				int lightmapIndex = renderer.lightmapIndex;
				if (lightmapIndex >= 0 &&
					lightmapIndex < SPECIAL_LIGHTMAP_INDEX &&
					lightmapIndex >= lightmapCount)
				{
					findings.Add(CreateFinding(
						"GFX-LIGHTMAP-001",
						E_GRAPHICS_RULE_KIND.INVARIANT,
						E_FINDING_SEVERITY.ERROR,
						E_FINDING_CONFIDENCE.CONFIRMED,
						"RendererのLightmap Indexが現在のLightmap配列範囲外です。",
						renderer,
						new Dictionary<string, object>
						{
							{ "lightmapIndex", lightmapIndex },
							{ "lightmapCount", lightmapCount }
						}));
				}
			}
		}

		private static void ValidatePackageComponents(
			GameObject gameObject,
			List<UnityGraphicsMcpFinding> findings)
		{
			foreach (Component component in gameObject.GetComponents<Component>())
			{
				if (component == null)
				{
					continue;
				}

				string fullName = component.GetType().FullName ?? component.GetType().Name;
				if (fullName != "UnityEngine.Rendering.Volume" ||
					!ReadBooleanMember(component, "enabled", true))
				{
					continue;
				}

				object sharedProfile;
				if (TryReadMember(component, "sharedProfile", out sharedProfile) &&
					sharedProfile == null)
				{
					findings.Add(CreateFinding(
						"GFX-VOLUME-001",
						E_GRAPHICS_RULE_KIND.INVARIANT,
						E_FINDING_SEVERITY.ERROR,
						E_FINDING_CONFIDENCE.CONFIRMED,
						"有効なVolumeのShared Profileが設定されていません。",
						component,
						null));
				}
			}
		}

		private static UnityGraphicsMcpFinding CreateFinding(
			string ruleId,
			E_GRAPHICS_RULE_KIND kind,
			E_FINDING_SEVERITY severity,
			E_FINDING_CONFIDENCE confidence,
			string message,
			Object target,
			Dictionary<string, object> evidence)
		{
			UnityGraphicsMcpFinding finding = new UnityGraphicsMcpFinding
			{
				ruleId = ruleId,
				kind = kind.ToString(),
				severity = severity.ToString(),
				confidence = confidence.ToString(),
				message = message,
				evidence = evidence ?? new Dictionary<string, object>()
			};

			if (target != null)
			{
				string stability;
				finding.affectedObjectIds.Add(ResolveObjectId(target, out stability));
				finding.evidence["idStability"] = stability;
			}

			return finding;
		}

		private static IEnumerable<GameObject> EnumerateLoadedGameObjects(bool includeInactive)
		{
			for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
			{
				Scene scene = SceneManager.GetSceneAt(sceneIndex);
				if (!scene.IsValid() || !scene.isLoaded)
				{
					continue;
				}

				foreach (GameObject rootObject in scene.GetRootGameObjects())
				{
					foreach (GameObject gameObject in EnumerateHierarchy(rootObject, includeInactive))
					{
						yield return gameObject;
					}
				}
			}
		}

		private static IEnumerable<GameObject> EnumerateHierarchy(
			GameObject rootObject,
			bool includeInactive)
		{
			if (includeInactive || rootObject.activeInHierarchy)
			{
				yield return rootObject;
			}

			for (int childIndex = 0; childIndex < rootObject.transform.childCount; childIndex++)
			{
				foreach (GameObject child in EnumerateHierarchy(
					rootObject.transform.GetChild(childIndex).gameObject,
					includeInactive))
				{
					yield return child;
				}
			}
		}
	}
}

#endif
