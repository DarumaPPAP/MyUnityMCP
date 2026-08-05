#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public enum E_GRAPHICS_PLAN_CONFIDENCE
	{
		LOW,
		MEDIUM,
		HIGH
	}

	public enum E_GRAPHICS_PLAN_VERIFICATION
	{
		EDITOR_PLAN_ONLY,
		EDITOR_INSPECTION_REQUIRED,
		PLAYER_REQUIRED,
		TARGET_DEVICE_REQUIRED,
		HUMAN_REVIEW_REQUIRED
	}

	public sealed class UnityGraphicsMcpPlanRecommendation
	{
		public string recommendationId { get; set; }
		public string section { get; set; }
		public object recommendedValue { get; set; }
		public List<string> allowedRange { get; set; } = new List<string>();
		public string reason { get; set; }
		public List<string> dependencies { get; set; } = new List<string>();
		public string confidence { get; set; }
		public string pipelineImpact { get; set; }
		public List<string> platformImpact { get; set; } = new List<string>();
		public string verificationLevel { get; set; }
		public string nativeMutationBackendStatus { get; set; }
	}

	public sealed class UnityGraphicsMcpDirectionPlan
	{
		public string PlanId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public Dictionary<string, object> ProjectContext { get; set; } =
			new Dictionary<string, object>();
		public Dictionary<string, object> VisualIntent { get; set; } =
			new Dictionary<string, object>();
		public List<UnityGraphicsMcpPlanRecommendation> Recommendations { get; set; } =
			new List<UnityGraphicsMcpPlanRecommendation>();
		public List<UnityGraphicsMcpIssue> Issues { get; set; } =
			new List<UnityGraphicsMcpIssue>();
	}

	/// <summary>
	/// Owner: UnityGraphicsMCP。
	/// Lifetime: Unity Editor Session内のRevision単位。
	/// Consumers: plan Toolと将来のMutation Tool。
	/// Responsibility: 構造化Visual IntentをProject事実へ照合し、Read-only Direction Planと差分予告を生成します。
	/// Split Reason: Project / Scene列挙とは変更理由とTest境界が異なるため、Direction Planningを一ファイルへ分離します。
	/// </summary>
	public static partial class UnityGraphicsMcpInspection
	{
		private static readonly string[] PLAN_SECTIONS =
		{
			"LIGHTING",
			"GI",
			"REFLECTION",
			"ATMOSPHERE",
			"LOOK",
			"PLATFORM"
		};

		public static UnityGraphicsMcpToolResult CompileDirection(
			string requestId,
			string goal,
			string[] referenceObservations,
			string[] emotionalIntent,
			string[] compositionHierarchy,
			string[] cameraLanguage,
			string[] lightingHierarchy,
			string[] colorScript,
			string[] materialReflectionIntent,
			string[] atmosphericDepth,
			string[] motionEnergy,
			string[] performancePriorities,
			string[] requestedPlatforms,
			string[] requestedConstraints,
			long? expectedRevision)
		{
			return ExecuteReadOnly(
				"graphics.compile_direction",
				requestId,
				delegate
				{
					long startRevision = UnityGraphicsMcpSession.Revision;
					if (expectedRevision.HasValue && expectedRevision.Value != startRevision)
					{
						return CreateResult(
							"graphics.compile_direction",
							requestId,
							E_MCP_TOOL_STATUS.STALE_DURING_SCAN,
							"expectedRevisionが現在のEditor Revisionと一致しません。",
							new Dictionary<string, object>
							{
								{ "expectedRevision", expectedRevision.Value },
								{ "currentRevision", startRevision }
							});
					}

					Dictionary<string, object> visualIntent = BuildVisualIntent(
						goal,
						referenceObservations,
						emotionalIntent,
						compositionHierarchy,
						cameraLanguage,
						lightingHierarchy,
						colorScript,
						materialReflectionIntent,
						atmosphericDepth,
						motionEnergy,
						performancePriorities);

					bool hasAnyIntent = HasAnyIntent(visualIntent);
					if (!hasAnyIntent)
					{
						return CreateResult(
							"graphics.compile_direction",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"goalまたはVisual Intentの構造化項目を一つ以上指定してください。",
							null);
					}

					UnityGraphicsMcpToolResult projectResult =
						InspectProject(requestId, requestedPlatforms, requestedConstraints);

					if (!projectResult.IsSuccessful)
					{
						return CreateResult(
							"graphics.compile_direction",
							requestId,
							E_MCP_TOOL_STATUS.FAILED,
							"Direction Planの前提となるProject Inspectionに失敗しました。",
							new Dictionary<string, object>
							{
								{ "projectInspectionStatus", projectResult.status },
								{ "projectInspectionSummary", projectResult.summary }
							});
					}

					if (startRevision != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult(
							"graphics.compile_direction",
							requestId,
							E_MCP_TOOL_STATUS.STALE_DURING_SCAN,
							"Direction Plan作成中にProject状態が変更されたため結果を破棄しました。",
							null);
					}

					Dictionary<string, object> projectContext =
						projectResult.data as Dictionary<string, object> ??
						new Dictionary<string, object>();

					bool hasStructuredIntent =
						Convert.ToBoolean(visualIntent["hasStructuredIntent"]);

					UnityGraphicsMcpDirectionPlan plan = new UnityGraphicsMcpDirectionPlan
					{
						Revision = startRevision,
						CreatedUtc = DateTime.UtcNow,
						ProjectContext = projectContext,
						VisualIntent = visualIntent,
						Recommendations = BuildDirectionRecommendations(
							projectContext,
							visualIntent,
							NormalizePlanValues(requestedPlatforms))
					};

					if (!hasStructuredIntent)
					{
						plan.Issues.Add(new UnityGraphicsMcpIssue
						{
							code = "STRUCTURED_VISUAL_INTENT_REQUIRED",
							message =
								"Unity C# Toolは自然言語や画像を独自に意味解釈しません。UnityAgentまたはMCP ClientでVisual Intentを構造化するとPlan精度が上がります。",
							evidence = new Dictionary<string, object>
							{
								{ "goalProvided", !string.IsNullOrWhiteSpace(goal) },
								{ "referenceObservationCount", NormalizePlanValues(referenceObservations).Count },
								{ "imageAnalysisPerformedByUnity", false }
							}
						});
					}

					UnityGraphicsMcpSession.StorePlan(plan);

					E_MCP_TOOL_STATUS status = hasStructuredIntent
						? E_MCP_TOOL_STATUS.SUCCESS
						: E_MCP_TOOL_STATUS.PARTIAL;

					UnityGraphicsMcpToolResult result = CreateResult(
						"graphics.compile_direction",
						requestId,
						status,
						hasStructuredIntent
							? "構造化Visual IntentをDirection PlanへCompileしました。"
							: "自然言語Goalを保持し、不足している構造化Visual Intentを明示したDirection Planを作成しました。",
						new Dictionary<string, object>
						{
							{ "planId", plan.PlanId },
							{ "expectedRevision", plan.Revision },
							{ "createdUtc", plan.CreatedUtc.ToString("O") },
							{ "visualIntent", plan.VisualIntent },
							{ "projectContext", plan.ProjectContext },
							{ "recommendations", plan.Recommendations },
							{ "planSections", PLAN_SECTIONS },
							{ "mutationApplied", false },
							{ "savePerformed", false },
							{ "bakePerformed", false }
						});

					result.issues.AddRange(plan.Issues);
					return result;
				});
		}

		public static UnityGraphicsMcpToolResult PreviewPlan(
			string requestId,
			string planId,
			long? expectedRevision)
		{
			return ExecuteReadOnly(
				"graphics.preview_plan",
				requestId,
				delegate
				{
					if (string.IsNullOrWhiteSpace(planId))
					{
						return CreateResult(
							"graphics.preview_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"planIdを指定してください。",
							null);
					}

					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.preview_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							new Dictionary<string, object>
							{
								{ "currentRevision", UnityGraphicsMcpSession.Revision }
							});
					}

					UnityGraphicsMcpDirectionPlan plan;
					E_MCP_TOOL_STATUS failureStatus;
					if (!UnityGraphicsMcpSession.TryGetPlan(
						planId,
						expectedRevision.Value,
						out plan,
						out failureStatus))
					{
						return CreateResult(
							"graphics.preview_plan",
							requestId,
							failureStatus,
							"Planは現在のEditor SessionまたはRevisionでは利用できません。",
							new Dictionary<string, object>
							{
								{ "planId", planId },
								{ "expectedRevision", expectedRevision.Value },
								{ "currentRevision", UnityGraphicsMcpSession.Revision }
							});
					}

					long startRevision = UnityGraphicsMcpSession.Revision;
					Dictionary<string, object> preview = BuildPlanPreview(plan);

					if (startRevision != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult(
							"graphics.preview_plan",
							requestId,
							E_MCP_TOOL_STATUS.STALE_DURING_SCAN,
							"Plan Preview作成中にProject状態が変更されたため結果を破棄しました。",
							null);
					}

					return CreateResult(
						"graphics.preview_plan",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"Unity状態を変更せず、Planが発生させる候補差分と検証要件を予告しました。",
						preview);
				});
		}

		private static Dictionary<string, object> BuildVisualIntent(
			string goal,
			string[] referenceObservations,
			string[] emotionalIntent,
			string[] compositionHierarchy,
			string[] cameraLanguage,
			string[] lightingHierarchy,
			string[] colorScript,
			string[] materialReflectionIntent,
			string[] atmosphericDepth,
			string[] motionEnergy,
			string[] performancePriorities)
		{
			Dictionary<string, object> dimensions = new Dictionary<string, object>
			{
				{ "emotionalIntent", NormalizePlanValues(emotionalIntent) },
				{ "compositionHierarchy", NormalizePlanValues(compositionHierarchy) },
				{ "cameraLanguage", NormalizePlanValues(cameraLanguage) },
				{ "lightingHierarchy", NormalizePlanValues(lightingHierarchy) },
				{ "colorScript", NormalizePlanValues(colorScript) },
				{ "materialReflectionIntent", NormalizePlanValues(materialReflectionIntent) },
				{ "atmosphericDepth", NormalizePlanValues(atmosphericDepth) },
				{ "motionEnergy", NormalizePlanValues(motionEnergy) },
				{ "performancePriorities", NormalizePlanValues(performancePriorities) }
			};

			bool hasStructuredIntent = dimensions.Values
				.OfType<List<string>>()
				.Any(values => values.Count > 0);

			return new Dictionary<string, object>
			{
				{ "goal", string.IsNullOrWhiteSpace(goal) ? null : goal.Trim() },
				{ "referenceObservations", NormalizePlanValues(referenceObservations) },
				{ "dimensions", dimensions },
				{ "hasStructuredIntent", hasStructuredIntent },
				{
					"semanticInterpretationSource",
					hasStructuredIntent ? "CALLER_STRUCTURED" : "NATURAL_LANGUAGE_UNPARSED"
				},
				{ "imageAnalysisPerformedByUnity", false }
			};
		}

		private static bool HasAnyIntent(Dictionary<string, object> visualIntent)
		{
			if (visualIntent == null)
			{
				return false;
			}

			string goal = visualIntent.ContainsKey("goal")
				? visualIntent["goal"] as string
				: null;

			List<string> observations = visualIntent.ContainsKey("referenceObservations")
				? visualIntent["referenceObservations"] as List<string>
				: null;

			bool hasStructuredIntent = visualIntent.ContainsKey("hasStructuredIntent") &&
				Convert.ToBoolean(visualIntent["hasStructuredIntent"]);

			return !string.IsNullOrWhiteSpace(goal) ||
				(observations != null && observations.Count > 0) ||
				hasStructuredIntent;
		}

		private static List<UnityGraphicsMcpPlanRecommendation> BuildDirectionRecommendations(
			Dictionary<string, object> projectContext,
			Dictionary<string, object> visualIntent,
			List<string> requestedPlatforms)
		{
			Dictionary<string, object> dimensions =
				visualIntent["dimensions"] as Dictionary<string, object> ??
				new Dictionary<string, object>();

			string pipelineKind = ExtractPipelineKind(projectContext);
			string nativeBackendStatus = ExtractNativeMutationBackendStatus(projectContext);
			bool hasStructuredIntent = Convert.ToBoolean(visualIntent["hasStructuredIntent"]);

			List<UnityGraphicsMcpPlanRecommendation> recommendations =
				new List<UnityGraphicsMcpPlanRecommendation>
				{
					CreateRecommendation(
						"GFX-DIR-LIGHTING",
						"LIGHTING",
						MergeIntentValues(dimensions, "lightingHierarchy", "compositionHierarchy"),
						new[]
						{
							"PRESERVE_CURRENT_AND_REVIEW",
							"REBALANCE_EXISTING_LIGHTS",
							"ADD_MISSING_LIGHT_ROLE"
						},
						"Key / Fill / Rim / Practicalの役割とShadow Budgetを、構造化されたLighting Hierarchyへ合わせます。",
						new[] { "Loaded Scene Light inventory", "Shadow capability", "Camera composition" },
						hasStructuredIntent,
						pipelineKind,
						requestedPlatforms,
						E_GRAPHICS_PLAN_VERIFICATION.EDITOR_INSPECTION_REQUIRED,
						nativeBackendStatus),
					CreateRecommendation(
						"GFX-DIR-GI",
						"GI",
						MergeIntentValues(dimensions, "atmosphericDepth", "performancePriorities"),
						new[]
						{
							"PRESERVE_CURRENT_GI",
							"REVIEW_BAKED_GI_COVERAGE",
							"REVIEW_PROBE_OR_PIPELINE_EQUIVALENT"
						},
						"Indirect LightingはAtmospheric DepthとPerformance Priorityの両方へ影響するため、Bake方式とProbe構成を分離して検討します。",
						new[] { "Lighting Settings", "Lightmap state", "Probe capability", "Target budget" },
						hasStructuredIntent,
						pipelineKind,
						requestedPlatforms,
						E_GRAPHICS_PLAN_VERIFICATION.EDITOR_INSPECTION_REQUIRED,
						nativeBackendStatus),
					CreateRecommendation(
						"GFX-DIR-REFLECTION",
						"REFLECTION",
						MergeIntentValues(dimensions, "materialReflectionIntent", "compositionHierarchy"),
						new[]
						{
							"PRESERVE_CURRENT_REFLECTIONS",
							"REVIEW_PROBE_COVERAGE",
							"ADD_MISSING_REFLECTION_REGION"
						},
						"MaterialとReflectionの意図を、Reflection ProbeまたはPipeline同等機能の配置・更新方針へ変換します。",
						new[] { "Material summary", "Reflection Probe inventory", "Pipeline reflection capability" },
						hasStructuredIntent,
						pipelineKind,
						requestedPlatforms,
						E_GRAPHICS_PLAN_VERIFICATION.EDITOR_INSPECTION_REQUIRED,
						nativeBackendStatus),
					CreateRecommendation(
						"GFX-DIR-ATMOSPHERE",
						"ATMOSPHERE",
						MergeIntentValues(dimensions, "atmosphericDepth", "colorScript"),
						new[]
						{
							"PRESERVE_CURRENT_ENVIRONMENT",
							"REBALANCE_SKY_AMBIENT_FOG",
							"ADD_PIPELINE_ATMOSPHERE_CONTROL"
						},
						"Sky、Ambient、Fog、Volume等を一つの数値へ潰さず、Atmospheric DepthとColor Scriptを満たす役割へ分解します。",
						new[] { "RenderSettings", "Volume or pipeline equivalent", "Camera exposure context" },
						hasStructuredIntent,
						pipelineKind,
						requestedPlatforms,
						E_GRAPHICS_PLAN_VERIFICATION.EDITOR_INSPECTION_REQUIRED,
						nativeBackendStatus),
					CreateRecommendation(
						"GFX-DIR-LOOK",
						"LOOK",
						MergeIntentValues(
							dimensions,
							"emotionalIntent",
							"colorScript",
							"cameraLanguage",
							"motionEnergy"),
						new[]
						{
							"PRESERVE_CURRENT_LOOK",
							"REBALANCE_VOLUME_OR_IMAGE_EFFECT",
							"REVIEW_CAMERA_AND_MOTION_LANGUAGE"
						},
						"Emotional Intentを単一Post Process値へ変換せず、Color、Camera、Motion、Materialの役割へ分解します。",
						new[] { "Camera inventory", "Volume capability", "Material summary", "Human visual review" },
						hasStructuredIntent,
						pipelineKind,
						requestedPlatforms,
						E_GRAPHICS_PLAN_VERIFICATION.HUMAN_REVIEW_REQUIRED,
						nativeBackendStatus),
					CreateRecommendation(
						"GFX-DIR-PLATFORM",
						"PLATFORM",
						MergeIntentValues(dimensions, "performancePriorities"),
						new[]
						{
							"PRESERVE_CURRENT_TARGET",
							"REVIEW_PROJECT_CONFIGURATION",
							"DEFINE_TARGET_DEVICE_BUDGET"
						},
						"PipelineとPlatformを別軸で扱い、Editor上の推測をTarget Device性能の保証として扱いません。",
						new[] { "Requested Target", "Installed Build Target", "Player build", "Target device measurement" },
						hasStructuredIntent,
						pipelineKind,
						requestedPlatforms,
						E_GRAPHICS_PLAN_VERIFICATION.TARGET_DEVICE_REQUIRED,
						nativeBackendStatus)
				};

			return recommendations;
		}

		private static UnityGraphicsMcpPlanRecommendation CreateRecommendation(
			string recommendationId,
			string section,
			List<string> intentValues,
			IEnumerable<string> allowedRange,
			string reason,
			IEnumerable<string> dependencies,
			bool hasStructuredIntent,
			string pipelineKind,
			List<string> requestedPlatforms,
			E_GRAPHICS_PLAN_VERIFICATION verificationLevel,
			string nativeBackendStatus)
		{
			return new UnityGraphicsMcpPlanRecommendation
			{
				recommendationId = recommendationId,
				section = section,
				recommendedValue = new Dictionary<string, object>
				{
					{
						"strategy",
						intentValues.Count > 0
							? "ALIGN_WITH_STRUCTURED_INTENT"
							: "PRESERVE_CURRENT_AND_REQUEST_MORE_DIRECTION"
					},
					{ "intentValues", intentValues }
				},
				allowedRange = allowedRange.ToList(),
				reason = reason,
				dependencies = dependencies.ToList(),
				confidence = (
					intentValues.Count > 0
						? E_GRAPHICS_PLAN_CONFIDENCE.HIGH
						: hasStructuredIntent
							? E_GRAPHICS_PLAN_CONFIDENCE.MEDIUM
							: E_GRAPHICS_PLAN_CONFIDENCE.LOW).ToString(),
				pipelineImpact = pipelineKind,
				platformImpact = requestedPlatforms,
				verificationLevel = verificationLevel.ToString(),
				nativeMutationBackendStatus = nativeBackendStatus
			};
		}

		private static Dictionary<string, object> BuildPlanPreview(
			UnityGraphicsMcpDirectionPlan plan)
		{
			List<Dictionary<string, object>> created = new List<Dictionary<string, object>>();
			List<Dictionary<string, object>> modified = new List<Dictionary<string, object>>();
			List<Dictionary<string, object>> dirty = new List<Dictionary<string, object>>();
			List<Dictionary<string, object>> bakeRequired = new List<Dictionary<string, object>>();
			List<Dictionary<string, object>> unsupported = new List<Dictionary<string, object>>();
			List<Dictionary<string, object>> unverified = new List<Dictionary<string, object>>();

			List<Light> lights = FindLoadedSceneComponents<Light>();
			List<Camera> cameras = FindLoadedSceneComponents<Camera>();
			List<ReflectionProbe> reflectionProbes =
				FindLoadedSceneComponents<ReflectionProbe>();
			List<Object> volumes = FindLoadedSceneObjectsByType(
				"UnityEngine.Rendering.Volume",
				"Unity.RenderPipelines.Core.Runtime");

			foreach (UnityGraphicsMcpPlanRecommendation recommendation in plan.Recommendations)
			{
				switch (recommendation.section)
				{
					case "LIGHTING":
						AddComponentForecast(
							created,
							modified,
							dirty,
							"LIGHTING",
							"UnityEngine.Light",
							lights.Cast<Component>().ToList(),
							"Key / Fill / Rim / Practicalの役割調整");
						AddBakeForecast(
							bakeRequired,
							"LIGHTMAP",
							"STATIC_OR_MIXED_LIGHTING_CHANGED");
						AddBakeForecast(
							bakeRequired,
							"LIGHT_PROBE",
							"LIGHTING_OR_PROBE_COVERAGE_CHANGED");
						break;

					case "GI":
						modified.Add(CreateForecastEntry(
							"GI",
							"LightingSettingsOrPipelineEquivalent",
							new List<string>(),
							"Indirect Lighting方式、Lightmap、Probe構成の候補変更",
							false));
						dirty.Add(CreateDirtyForecast(
							"GI",
							"LightingSettingsOrPipelineEquivalent"));
						AddBakeForecast(
							bakeRequired,
							"LIGHTMAP",
							"GI_SETTINGS_OR_STATIC_GEOMETRY_CHANGED");
						AddBakeForecast(
							bakeRequired,
							"LIGHT_PROBE_OR_PIPELINE_EQUIVALENT",
							"PROBE_SETTINGS_OR_COVERAGE_CHANGED");
						break;

					case "REFLECTION":
						AddComponentForecast(
							created,
							modified,
							dirty,
							"REFLECTION",
							"UnityEngine.ReflectionProbe",
							reflectionProbes.Cast<Component>().ToList(),
							"Reflection coverageとImportanceの候補調整");
						AddBakeForecast(
							bakeRequired,
							"REFLECTION_PROBE",
							"BAKED_REFLECTION_CONFIGURATION_CHANGED");
						break;

					case "ATMOSPHERE":
						modified.Add(CreateForecastEntry(
							"ATMOSPHERE",
							"RenderSettings",
							new List<string>(),
							"Sky / Ambient / Fogの候補変更",
							false));
						dirty.Add(CreateDirtyForecast("ATMOSPHERE", "Active Scene"));

						if (volumes.Count > 0)
						{
							modified.Add(CreateForecastEntry(
								"ATMOSPHERE",
								"UnityEngine.Rendering.Volume",
								ResolveObjectIds(volumes),
								"既存VolumeまたはPipeline同等機能の候補変更",
								false));
						}
						else
						{
							created.Add(CreateForecastEntry(
								"ATMOSPHERE",
								"VolumeOrPipelineEquivalent",
								new List<string>(),
								"Atmosphere制御面が必要な場合の作成候補",
								false));
						}
						break;

					case "LOOK":
						if (cameras.Count > 0)
						{
							modified.Add(CreateForecastEntry(
								"LOOK",
								"UnityEngine.Camera",
								ResolveObjectIds(cameras.Cast<Object>().ToList()),
								"Camera languageの候補変更",
								false));
						}

						if (volumes.Count > 0)
						{
							modified.Add(CreateForecastEntry(
								"LOOK",
								"UnityEngine.Rendering.Volume",
								ResolveObjectIds(volumes),
								"Color / Exposure / Post Processの候補変更",
								false));
						}
						else
						{
							created.Add(CreateForecastEntry(
								"LOOK",
								"VolumeOrProjectImageEffect",
								new List<string>(),
								"Look制御面が必要な場合の作成候補",
								false));
						}
						break;

					case "PLATFORM":
						modified.Add(CreateForecastEntry(
							"PLATFORM",
							"PlayerSettingsOrQualitySettings",
							new List<string>(),
							"Target Platform Budgetに合わせる候補変更",
							true));
						dirty.Add(CreateDirtyForecast(
							"PLATFORM",
							"Project Settings"));
						break;
				}

				if (recommendation.nativeMutationBackendStatus ==
					E_MCP_CAPABILITY_STATUS.BACKEND_NOT_IMPLEMENTED.ToString())
				{
					AddUniqueIssueForecast(
						unsupported,
						"NATIVE_MUTATION_BACKEND_NOT_IMPLEMENTED",
						recommendation.section,
						"Direction PlanningはPlan Previewのみで、Pipeline Native Mutationは実行しません。");
				}
			}

			Dictionary<string, object> requestedTarget =
				plan.ProjectContext.ContainsKey("requestedTarget")
					? plan.ProjectContext["requestedTarget"] as Dictionary<string, object>
					: null;

			List<string> requestedPlatforms = requestedTarget != null &&
				requestedTarget.ContainsKey("platforms")
					? requestedTarget["platforms"] as List<string> ?? new List<string>()
					: new List<string>();

			if (requestedPlatforms.Count > 0)
			{
				unverified.Add(new Dictionary<string, object>
				{
					{ "code", "TARGET_DEVICE_VERIFICATION_REQUIRED" },
					{ "requestedPlatforms", requestedPlatforms },
					{ "reason", "Editor PlanだけではPlayerまたは実機性能を保証できません。" }
				});
			}

			unverified.Add(new Dictionary<string, object>
			{
				{ "code", "HUMAN_VISUAL_REVIEW_REQUIRED" },
				{ "reason", "CaptureとHuman ReviewなしにVisual Acceptanceを確定しません。" }
			});

			return new Dictionary<string, object>
			{
				{ "planId", plan.PlanId },
				{ "expectedRevision", plan.Revision },
				{ "actualChangesApplied", false },
				{ "forecastOnly", true },
				{ "created", created },
				{ "modified", modified },
				{ "dirty", dirty },
				{ "bakeRequired", bakeRequired },
				{ "unsupported", unsupported },
				{ "unverified", unverified },
				{
					"approvalRequirements",
					new Dictionary<string, object>
					{
						{ "mutationApprovalRequired", true },
						{ "expectedRevisionRequired", true },
						{ "projectSettingsApprovalRequired", true },
						{ "pipelineAssetApprovalRequired", true },
						{ "bakeApprovalRequired", true },
						{ "automaticSave", false }
					}
				},
				{
					"executionReadiness",
					new Dictionary<string, object>
					{
						{ "planReview", "READY" },
						{ "mutation", "REQUIRES_SEPARATE_APPROVED_TOOL" },
						{ "bake", "REQUIRES_SEPARATE_APPROVED_TOOL" },
						{ "capture", "REQUIRES_SEPARATE_APPROVED_TOOL" }
					}
				},
				{ "recommendations", plan.Recommendations }
			};
		}

		private static void AddComponentForecast(
			List<Dictionary<string, object>> created,
			List<Dictionary<string, object>> modified,
			List<Dictionary<string, object>> dirty,
			string section,
			string targetType,
			List<Component> existingComponents,
			string reason)
		{
			if (existingComponents.Count > 0)
			{
				modified.Add(CreateForecastEntry(
					section,
					targetType,
					ResolveObjectIds(existingComponents.Cast<Object>().ToList()),
					reason,
					false));

				foreach (string scenePath in existingComponents
					.Select(component => component.gameObject.scene.path)
					.Distinct())
				{
					dirty.Add(CreateDirtyForecast(
						section,
						string.IsNullOrEmpty(scenePath) ? "Untitled Scene" : scenePath));
				}

				return;
			}

			created.Add(CreateForecastEntry(
				section,
				targetType,
				new List<string>(),
				reason,
				false));
			dirty.Add(CreateDirtyForecast(section, "Active Scene"));
		}

		private static Dictionary<string, object> CreateForecastEntry(
			string section,
			string targetType,
			List<string> objectIds,
			string reason,
			bool requiresSeparateApproval)
		{
			return new Dictionary<string, object>
			{
				{ "section", section },
				{ "targetType", targetType },
				{ "objectIds", objectIds },
				{ "reason", reason },
				{ "forecastOnly", true },
				{ "requiresSeparateApproval", requiresSeparateApproval }
			};
		}

		private static Dictionary<string, object> CreateDirtyForecast(
			string section,
			string target)
		{
			return new Dictionary<string, object>
			{
				{ "section", section },
				{ "target", target },
				{ "forecastOnly", true }
			};
		}

		private static void AddBakeForecast(
			List<Dictionary<string, object>> bakeRequired,
			string dependency,
			string condition)
		{
			if (bakeRequired.Any(item =>
				string.Equals(
					item["dependency"] as string,
					dependency,
					StringComparison.Ordinal)))
			{
				return;
			}

			bakeRequired.Add(new Dictionary<string, object>
			{
				{ "dependency", dependency },
				{ "condition", condition },
				{ "requiredNow", false },
				{ "forecastOnly", true },
				{ "separateApprovalRequired", true }
			});
		}

		private static void AddUniqueIssueForecast(
			List<Dictionary<string, object>> target,
			string code,
			string section,
			string message)
		{
			if (target.Any(item =>
				string.Equals(item["code"] as string, code, StringComparison.Ordinal) &&
				string.Equals(item["section"] as string, section, StringComparison.Ordinal)))
			{
				return;
			}

			target.Add(new Dictionary<string, object>
			{
				{ "code", code },
				{ "section", section },
				{ "message", message }
			});
		}

		private static List<T> FindLoadedSceneComponents<T>()
			where T : Component
		{
			return Resources.FindObjectsOfTypeAll<T>()
				.Where(component =>
					component != null &&
					component.gameObject.scene.IsValid() &&
					component.gameObject.scene.isLoaded)
				.ToList();
		}

		private static List<Object> FindLoadedSceneObjectsByType(
			string fullTypeName,
			string assemblyName)
		{
			Type type = Type.GetType(fullTypeName + ", " + assemblyName);
			if (type == null)
			{
				return new List<Object>();
			}

			return Resources.FindObjectsOfTypeAll(type)
				.Where(target =>
					target != null &&
					IsLoadedSceneObject(target))
				.ToList();
		}

		private static bool IsLoadedSceneObject(Object target)
		{
			Component component = target as Component;
			return component != null &&
				component.gameObject.scene.IsValid() &&
				component.gameObject.scene.isLoaded;
		}

		private static List<string> ResolveObjectIds(List<Object> targets)
		{
			List<string> ids = new List<string>();

			foreach (Object target in targets.Where(item => item != null).Take(32))
			{
				string stability;
				ids.Add(ResolveObjectId(target, out stability));
			}

			return ids;
		}

		private static List<string> MergeIntentValues(
			Dictionary<string, object> dimensions,
			params string[] keys)
		{
			List<string> merged = new List<string>();

			foreach (string key in keys)
			{
				List<string> values = dimensions.ContainsKey(key)
					? dimensions[key] as List<string>
					: null;

				if (values == null)
				{
					continue;
				}

				foreach (string value in values)
				{
					if (!merged.Contains(value, StringComparer.OrdinalIgnoreCase))
					{
						merged.Add(value);
					}
				}
			}

			return merged;
		}

		private static List<string> NormalizePlanValues(string[] values)
		{
			return values == null
				? new List<string>()
				: values
					.Where(item => !string.IsNullOrWhiteSpace(item))
					.Select(item => item.Trim())
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
		}

		private static string ExtractPipelineKind(
			Dictionary<string, object> projectContext)
		{
			Dictionary<string, object> detectedProject =
				projectContext.ContainsKey("detectedProject")
					? projectContext["detectedProject"] as Dictionary<string, object>
					: null;

			Dictionary<string, object> renderPipeline =
				detectedProject != null && detectedProject.ContainsKey("renderPipeline")
					? detectedProject["renderPipeline"] as Dictionary<string, object>
					: null;

			return renderPipeline != null && renderPipeline.ContainsKey("kind")
				? renderPipeline["kind"] as string ?? "UNKNOWN"
				: "UNKNOWN";
		}

		private static string ExtractNativeMutationBackendStatus(
			Dictionary<string, object> projectContext)
		{
			Dictionary<string, object> backendSelection =
				projectContext.ContainsKey("backendSelection")
					? projectContext["backendSelection"] as Dictionary<string, object>
					: null;

			return backendSelection != null &&
				backendSelection.ContainsKey("nativeMutationBackendStatus")
					? backendSelection["nativeMutationBackendStatus"] as string ??
						E_MCP_CAPABILITY_STATUS.UNVERIFIED.ToString()
					: E_MCP_CAPABILITY_STATUS.UNVERIFIED.ToString();
		}
	}
}

#endif
