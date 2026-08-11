#if UNITY_EDITOR

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDomainMcp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityAnimationMcp
{
	public static class UnityAnimationMcpRuntime
	{
		private const string DOMAIN_ID = "unity_animation_mcp";

		public static UnityDomainMcpResult Inspect(bool includeInactive)
		{
			Animator[] animators = Resources.FindObjectsOfTypeAll<Animator>()
				.Where(value => IsSceneObject(value) && (includeInactive || value.gameObject.activeInHierarchy))
				.OrderBy(value => value.gameObject.scene.path)
				.ThenBy(value => HierarchyPath(value.transform))
				.ToArray();

			return UnityDomainMcpCommon.Result(
				"animation.inspect",
				E_DOMAIN_TOOL_STATUS.SUCCESS,
				"Animator構成を取得しました。",
				new JObject
				{
					["animators"] = new JArray(animators.Select(AnimatorData))
				});
		}

		public static UnityDomainMcpResult Validate()
		{
			JArray findings = new JArray();
			foreach (Animator animator in Resources.FindObjectsOfTypeAll<Animator>().Where(IsSceneObject))
			{
				if (animator.runtimeAnimatorController == null)
				{
					findings.Add(Finding("ANIMATION-CONTROLLER-MISSING", "WARNING", animator, "AnimatorにRuntimeAnimatorControllerがありません。"));
					continue;
				}

				AnimatorController controller = ResolveController(animator.runtimeAnimatorController);
				if (controller == null)
				{
					findings.Add(Finding("ANIMATION-CONTROLLER-UNSUPPORTED", "INFO", animator, "AnimatorController以外のRuntime Controllerです。"));
					continue;
				}

				foreach (IGrouping<string, AnimatorControllerParameter> duplicate in
					controller.parameters.GroupBy(value => value.name).Where(value => value.Count() > 1))
				{
					findings.Add(Finding("ANIMATION-PARAMETER-DUPLICATE", "ERROR", animator, $"Parameter名が重複しています: {duplicate.Key}"));
				}

				foreach (AnimationClip clip in controller.animationClips.Where(value => value != null))
				{
					foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(clip))
					{
						if (string.IsNullOrWhiteSpace(animationEvent.functionName))
						{
							findings.Add(new JObject
							{
								["code"] = "ANIMATION-EVENT-FUNCTION-MISSING",
								["severity"] = "WARNING",
								["message"] = $"Animation EventのFunction名が空です: {AssetDatabase.GetAssetPath(clip)}",
								["clip"] = clip.name,
								["time"] = animationEvent.time
							});
						}
					}
				}
			}

			return UnityDomainMcpCommon.Result(
				"animation.validate",
				findings.Any(value => value.Value<string>("severity") == "ERROR")
					? E_DOMAIN_TOOL_STATUS.PARTIAL
					: E_DOMAIN_TOOL_STATUS.SUCCESS,
				"Animation Validationを完了しました。",
				new JObject
				{
					["findings"] = findings,
					["findingCount"] = findings.Count
				});
		}

		public static UnityDomainMcpResult PrepareParameter(
			string controllerAssetPath,
			string parameterName,
			string parameterType,
			float? defaultFloat,
			int? defaultInt,
			bool? defaultBool,
			long? expectedRevision)
		{
			AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerAssetPath);
			if (controller == null)
			{
				return UnityDomainMcpCommon.Error("animation.prepare_parameter", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "AnimatorController Assetが見つかりません。");
			}
			if (string.IsNullOrWhiteSpace(parameterName) || parameterName.Length > 128)
			{
				return UnityDomainMcpCommon.Error("animation.prepare_parameter", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Parameter名が不正です。");
			}
			if (controller.parameters.Any(value => string.Equals(value.name, parameterName, StringComparison.Ordinal)))
			{
				return UnityDomainMcpCommon.Error("animation.prepare_parameter", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "同名Parameterが既に存在します。");
			}
			if (!Enum.TryParse(parameterType, true, out AnimatorControllerParameterType parsedType))
			{
				return UnityDomainMcpCommon.Error("animation.prepare_parameter", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "Parameter Typeが不正です。");
			}

			return UnityDomainMcpCommon.Prepare(
				"animation.prepare_parameter",
				DOMAIN_ID,
				"add_parameter",
				expectedRevision,
				true,
				new JObject
				{
					["controllerAssetPath"] = controllerAssetPath,
					["parameterName"] = parameterName,
					["parameterType"] = parsedType.ToString(),
					["defaultFloat"] = defaultFloat ?? 0f,
					["defaultInt"] = defaultInt ?? 0,
					["defaultBool"] = defaultBool ?? false,
					["baselineParameterCount"] = controller.parameters.Length,
					["stateMachineMutation"] = false,
					["clipMutation"] = false,
					["savePerformed"] = false
				});
		}

		public static UnityDomainMcpResult ApplyParameter(
			string planId,
			long? currentRevision,
			string approvalToken)
		{
			if (!currentRevision.HasValue)
			{
				return UnityDomainMcpCommon.Error("animation.apply_parameter", E_DOMAIN_TOOL_STATUS.INVALID_REQUEST, "currentRevisionが必要です。");
			}
			if (!UnityDomainMcpPlanStore.TryConsume(
				"animation.apply_parameter",
				DOMAIN_ID,
				planId,
				currentRevision.Value,
				approvalToken,
				out UnityDomainMcpPlan plan,
				out UnityDomainMcpResult failure))
			{
				return failure;
			}

			AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
				plan.Payload.Value<string>("controllerAssetPath"));
			if (controller == null)
			{
				return UnityDomainMcpCommon.Error("animation.apply_parameter", E_DOMAIN_TOOL_STATUS.NOT_FOUND, "AnimatorController Assetが見つかりません。");
			}

			string parameterName = plan.Payload.Value<string>("parameterName");
			if (controller.parameters.Any(value => string.Equals(value.name, parameterName, StringComparison.Ordinal)))
			{
				return UnityDomainMcpCommon.Error("animation.apply_parameter", E_DOMAIN_TOOL_STATUS.STALE_REVISION, "Preview後に同名Parameterが追加されました。");
			}

			AnimatorControllerParameter parameter = new AnimatorControllerParameter
			{
				name = parameterName,
				type = (AnimatorControllerParameterType)Enum.Parse(
					typeof(AnimatorControllerParameterType),
					plan.Payload.Value<string>("parameterType")),
				defaultFloat = plan.Payload.Value<float>("defaultFloat"),
				defaultInt = plan.Payload.Value<int>("defaultInt"),
				defaultBool = plan.Payload.Value<bool>("defaultBool")
			};

			Undo.RecordObject(controller, "MyUnityMCP Animator Parameter");
			controller.AddParameter(parameter);
			UnityDomainMcpCommon.CompleteMutation(controller);

			return UnityDomainMcpCommon.Result(
				"animation.apply_parameter",
				E_DOMAIN_TOOL_STATUS.SUCCESS,
				"Animator Parameterを追加しました。Asset Saveは行っていません。",
				new JObject
				{
					["controllerAssetPath"] = AssetDatabase.GetAssetPath(controller),
					["parameter"] = ParameterData(parameter),
					["parameterCount"] = controller.parameters.Length,
					["stateMachineMutation"] = false,
					["clipMutation"] = false,
					["savePerformed"] = false
				});
		}

		public static UnityDomainMcpResult GetSupportMatrix()
		{
			return UnityDomainMcpCommon.Result(
				"animation.get_support_matrix",
				E_DOMAIN_TOOL_STATUS.UNVERIFIED,
				"Animation MCPの対応状況です。",
				new JObject
				{
					["implemented"] = new JArray(
						"Animator inspection",
						"AnimatorController parameter and clip inspection",
						"Animation Event validation",
						"approval-gated parameter addition"),
					["excludedInitialMutation"] = new JArray(
						"state machine rewrite",
						"transition rewrite",
						"animation curve rewrite",
						"clip event mutation"),
					["unverified"] = new JArray(
						"AnimatorOverrideController mutation",
						"humanoid retargeting",
						"runtime State monitoring")
				});
		}

		private static JObject AnimatorData(Animator value)
		{
			AnimatorController controller = ResolveController(value.runtimeAnimatorController);
			return new JObject
			{
				["objectId"] = UnityDomainMcpCommon.ObjectId(value),
				["name"] = value.name,
				["scenePath"] = value.gameObject.scene.path,
				["hierarchyPath"] = HierarchyPath(value.transform),
				["enabled"] = value.enabled,
				["applyRootMotion"] = value.applyRootMotion,
				["updateMode"] = value.updateMode.ToString(),
				["cullingMode"] = value.cullingMode.ToString(),
				["controllerAssetPath"] = controller == null ? null : AssetDatabase.GetAssetPath(controller),
				["parameters"] = controller == null ? new JArray() : new JArray(controller.parameters.Select(ParameterData)),
				["layers"] = controller == null ? new JArray() : new JArray(controller.layers.Select(layer => layer.name)),
				["clips"] = controller == null ? new JArray() : new JArray(controller.animationClips.Select(ClipData))
			};
		}

		private static AnimatorController ResolveController(RuntimeAnimatorController value)
		{
			if (value is AnimatorController controller)
			{
				return controller;
			}
			if (value is AnimatorOverrideController overrideController)
			{
				return overrideController.runtimeAnimatorController as AnimatorController;
			}
			return null;
		}

		private static JObject ParameterData(AnimatorControllerParameter value)
		{
			return new JObject
			{
				["name"] = value.name,
				["type"] = value.type.ToString(),
				["defaultFloat"] = value.defaultFloat,
				["defaultInt"] = value.defaultInt,
				["defaultBool"] = value.defaultBool
			};
		}

		private static JObject ClipData(AnimationClip value)
		{
			return new JObject
			{
				["name"] = value.name,
				["assetPath"] = AssetDatabase.GetAssetPath(value),
				["length"] = value.length,
				["frameRate"] = value.frameRate,
				["legacy"] = value.legacy,
				["eventCount"] = AnimationUtility.GetAnimationEvents(value).Length
			};
		}

		private static JObject Finding(string code, string severity, Component target, string message)
		{
			return new JObject
			{
				["code"] = code,
				["severity"] = severity,
				["message"] = message,
				["objectId"] = UnityDomainMcpCommon.ObjectId(target),
				["scenePath"] = target.gameObject.scene.path,
				["hierarchyPath"] = HierarchyPath(target.transform)
			};
		}

		private static bool IsSceneObject(Component value)
		{
			return value != null && value.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(value);
		}

		private static string HierarchyPath(Transform transform)
		{
			return transform.parent == null
				? transform.name
				: HierarchyPath(transform.parent) + "/" + transform.name;
		}
	}
}

#endif
