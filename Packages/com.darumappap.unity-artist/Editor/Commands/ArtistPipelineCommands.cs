#if UNITY_EDITOR && UNITY_ARTIST_PIPELINE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.Pipeline.Commands;

namespace DarumaPPAP.UnityArtist
{
	[Serializable]
	public sealed class ArtistIntent
	{
		public string workflow;
		public string targetName;
		public bool setFog;
		public float fogDensity;
		public bool setLightIntensity;
		public float lightIntensity;
		public bool setCameraFieldOfView;
		public float cameraFieldOfView;
		public string captureCameraName;
		public string timelineAssetName;
		public string[] requestedChannels;
	}

	[Serializable]
	public sealed class CinematicRequest
	{
		public string directorName;
		public string trackKind;
		public string trackName;
		public string bindingTargetName;
		public float markerTime;
	}

	[Serializable]
	public sealed class ArtistError
	{
		public string code;
		public string message;
	}

	[Serializable]
	public sealed class ArtistChange
	{
		public string target;
		public string property;
		public string before;
		public string after;
	}

	[Serializable]
	public sealed class ArtistTarget
	{
		public string kind;
		public string name;
		public string scenePath;
		public string hierarchyPath;
		public bool enabled;
	}

	[Serializable]
	public sealed class ArtistSupport
	{
		public bool supported;
		public string supportTier;
		public string unityVersion;
		public string renderPipeline;
		public string compatibilityBackend;
		public string transport;
		public string errorCode;
		public string reason;
	}

	[Serializable]
	public sealed class ArtistResult
	{
		public string schemaVersion = "2.0";
		public string product = "UnityArtistCLI";
		public string command;
		public string status;
		public bool verified;
		public string planId;
		public string captureId;
		public string evaluationId;
		public string revision;
		public string baseRevision;
		public bool approvalRequired;
		public bool savePerformed;
		public bool undoAvailable;
		public bool humanReview;
		public ArtistSupport support;
		public List<ArtistChange> exactDiff = new List<ArtistChange>();
		public List<ArtistTarget> targets = new List<ArtistTarget>();
		public List<string> evidence = new List<string>();
		public List<ArtistError> errors = new List<ArtistError>();
	}

	internal sealed class StoredPlan
	{
		public string planId;
		public string baseRevision;
		public ArtistIntent intent;
		public CinematicRequest cinematic;
		public ArtistResult preview;
	}

	internal static class ArtistSession
	{
		private static readonly Dictionary<string, StoredPlan> plans = new Dictionary<string, StoredPlan>();
		private static readonly Dictionary<string, ArtistResult> captures = new Dictionary<string, ArtistResult>();
		private static readonly List<string> history = new List<string>();

		public static ArtistResult Inspect()
		{
			ArtistSupport support = Support();
			ArtistResult result = Base("artist.inspect", support);
			if (!support.supported) return Error(result, support.errorCode, support.reason);
			result.verified = support.supported;
			foreach (Camera camera in SceneObjects<Camera>())
			{
				result.targets.Add(Target("camera", camera, camera.enabled));
			}
			foreach (Light light in SceneObjects<Light>())
			{
				result.targets.Add(Target("light", light, light.enabled));
			}
			foreach (ReflectionProbe probe in SceneObjects<ReflectionProbe>())
			{
				result.targets.Add(Target("reflection_probe", probe, probe.enabled));
			}
			foreach (PlayableDirector director in SceneObjects<PlayableDirector>())
			{
				result.targets.Add(Target("playable_director", director, director.enabled));
			}
			result.evidence.Add("visual_art_inspection");
			result.evidence.Add("pipeline_support_fact");
			return result;
		}

		public static ArtistResult Plan(string requestJson, string expectedRevision)
		{
			ArtistSupport support = Support();
			ArtistResult result = Base("artist.plan", support);
			if (!support.supported)
			{
				return Error(result, support.errorCode, support.reason);
			}
			ArtistIntent intent;
			try
			{
				intent = JsonUtility.FromJson<ArtistIntent>(string.IsNullOrWhiteSpace(requestJson) ? "{}" : requestJson);
			}
			catch (Exception exception)
			{
				return Error(result, "INVALID_INTENT", exception.Message);
			}
			if (intent == null || !HasChange(intent))
			{
				return Error(result, "INVALID_INTENT", "At least one explicit visual intent change is required.");
			}
			string currentRevision = CurrentRevision();
			if (!string.IsNullOrWhiteSpace(expectedRevision) && !string.Equals(expectedRevision, currentRevision, StringComparison.Ordinal))
			{
				return Error(result, "STALE_REVISION", "The requested revision does not match the current Editor state.");
			}

			string planId = "artist-plan-" + Guid.NewGuid().ToString("N");
			result.planId = planId;
			result.revision = currentRevision;
			result.baseRevision = currentRevision;
			result.approvalRequired = true;
			result.savePerformed = false;
			result.undoAvailable = true;
			BuildDiff(result, intent);
			result.evidence.Add("visual_direction_plan");
			result.evidence.Add("exact_diff");
			result.evidence.Add("expected_revision");
			plans[planId] = new StoredPlan { planId = planId, baseRevision = currentRevision, intent = intent, preview = result };
			history.Add(planId);
			return result;
		}

		public static ArtistResult Preview(string planId, string expectedRevision)
		{
			if (!plans.TryGetValue(planId ?? string.Empty, out StoredPlan plan))
			{
				return Error(Base("artist.preview", Support()), "PLAN_NOT_FOUND", "The plan is not present in this Editor session.");
			}
			if (!string.IsNullOrWhiteSpace(expectedRevision) && !string.Equals(expectedRevision, plan.baseRevision, StringComparison.Ordinal))
			{
				return Error(Base("artist.preview", Support()), "STALE_REVISION", "The plan revision is stale.");
			}
			plan.preview.command = "artist.preview";
			return plan.preview;
		}

		public static ArtistResult Apply(string planId, string expectedRevision, string approvalToken)
		{
			ArtistResult result = Base("artist.apply", Support());
			if (string.IsNullOrWhiteSpace(approvalToken)) return Error(result, "APPROVAL_REQUIRED", "Apply requires an opaque approval token from UnityAgent.");
			if (!plans.TryGetValue(planId ?? string.Empty, out StoredPlan plan)) return Error(result, "PLAN_NOT_FOUND", "The plan is not present in this Editor session.");
			string currentRevision = CurrentRevision();
			if (!string.Equals(plan.baseRevision, currentRevision, StringComparison.Ordinal) || !string.Equals(expectedRevision, currentRevision, StringComparison.Ordinal))
			{
				return Error(result, "STALE_REVISION", "The Editor state changed after the plan was prepared.");
			}
			ArtistSupport support = Support();
			if (!support.supported) return Error(result, support.errorCode, support.reason);
			if (!ApplyIntent(plan.intent, result)) return result;
			result.status = "passed";
			result.verified = true;
			result.planId = planId;
			result.baseRevision = currentRevision;
			result.revision = CurrentRevision();
			result.approvalRequired = true;
			result.savePerformed = false;
			result.undoAvailable = true;
			result.evidence.Add("mutation_evidence");
			result.evidence.Add("undo_registration");
			result.evidence.Add("save_not_performed");
			plans.Remove(planId);
			history.Add("applied:" + planId);
			return result;
		}

		public static ArtistResult Capture(string requestJson)
		{
			ArtistResult result = Base("artist.capture", Support());
			if (!result.support.supported) return Error(result, result.support.errorCode, result.support.reason);
			ArtistIntent intent;
			try
			{
				intent = string.IsNullOrWhiteSpace(requestJson) ? new ArtistIntent() : JsonUtility.FromJson<ArtistIntent>(requestJson);
			}
			catch (Exception exception)
			{
				return Error(result, "INVALID_CAPTURE_REQUEST", exception.Message);
			}
			Camera camera = FindCamera(intent == null ? null : intent.captureCameraName);
			if (camera == null) return Error(result, "CAMERA_NOT_FOUND", "Capture requires an exact camera target or a Main Camera.");
			string captureId = "artist-capture-" + Guid.NewGuid().ToString("N");
			string directory = Path.Combine("Library", "UnityArtist", "Captures", captureId);
			Directory.CreateDirectory(directory);
			string colorPath = Path.Combine(directory, "color.png").Replace('\\', '/');
			ScreenCapture.CaptureScreenshot(colorPath);
			result.captureId = captureId;
			result.verified = true;
			result.status = "passed";
			result.savePerformed = false;
			result.evidence.Add("visual_capture");
			result.evidence.Add("camera_binding:" + camera.name);
			result.evidence.Add("capture_manifest");
			result.evidence.Add("color_path:" + colorPath);
			result.evidence.Add("depth_channel:not_configured");
			result.evidence.Add("object_id_channel:not_configured");
			captures[captureId] = result;
			history.Add(captureId);
			return result;
		}

		public static ArtistResult Evaluate(string captureId, string decision, string notes)
		{
			if (!captures.ContainsKey(captureId ?? string.Empty)) return Error(Base("artist.evaluate", Support()), "CAPTURE_NOT_FOUND", "Capture Evidence was not found in this Editor session.");
			string normalized = (decision ?? string.Empty).Trim().ToLowerInvariant();
			if (normalized != "accepted" && normalized != "rejected" && normalized != "needs_refine") return Error(Base("artist.evaluate", Support()), "INVALID_REVIEW_DECISION", "decision must be accepted, rejected, or needs_refine.");
			ArtistResult result = Base("artist.evaluate", Support());
			result.captureId = captureId;
			result.evaluationId = "artist-evaluation-" + Guid.NewGuid().ToString("N");
			result.humanReview = true;
			result.verified = normalized == "accepted";
			result.status = "passed";
			result.evidence.Add("artist_validation");
			result.evidence.Add("human_review_decision");
			result.evidence.Add("review_decision:" + normalized);
			if (!string.IsNullOrWhiteSpace(notes)) result.evidence.Add("review_notes_present");
			history.Add(result.evaluationId);
			return result;
		}

		public static ArtistResult Refine(string evaluationId, string requestJson)
		{
			if (string.IsNullOrWhiteSpace(evaluationId)) return Error(Base("artist.refine", Support()), "EVALUATION_REQUIRED", "refine requires an evaluation id.");
			ArtistResult result = Plan(requestJson, string.Empty);
			result.command = "artist.refine";
			result.evaluationId = evaluationId;
			result.evidence.Add("refinement_plan");
			result.evidence.Add("evaluation_reference");
			return result;
		}

		public static ArtistResult Cinematic(string operation, string requestJson, string planId, string expectedRevision, string approvalToken)
		{
			ArtistResult result = Base("artist.cinematic", Support());
			if (!result.support.supported) return Error(result, result.support.errorCode, result.support.reason);
			string normalized = (operation ?? "inspect").Trim().ToLowerInvariant();
			if (normalized != "inspect" && normalized != "plan" && normalized != "preview" && normalized != "apply")
				return Error(result, "INVALID_CINEMATIC_OPERATION", "operation must be inspect, plan, preview, or apply.");

			CinematicRequest request;
			try
			{
				request = JsonUtility.FromJson<CinematicRequest>(string.IsNullOrWhiteSpace(requestJson) ? "{}" : requestJson);
			}
			catch (Exception exception)
			{
				return Error(result, "INVALID_CINEMATIC_REQUEST", exception.Message);
			}
			if (request == null) return Error(result, "INVALID_CINEMATIC_REQUEST", "The cinematic request must be a JSON object.");

			if (normalized == "inspect")
			{
				foreach (PlayableDirector director in SceneObjects<PlayableDirector>())
				{
					if (string.IsNullOrWhiteSpace(request.directorName) || string.Equals(director.name, request.directorName, StringComparison.Ordinal))
						InspectCinematicDirector(director, result);
				}
				result.evidence.Add("cinematic_operation:inspect");
				result.evidence.Add("timeline_evidence");
				return result;
			}

			if (normalized == "plan")
			{
				PlayableDirector director = FindDirector(request.directorName);
				if (director == null) return Error(result, "TIMELINE_DIRECTOR_NOT_FOUND", "An exact PlayableDirector target is required.");
				if (!ValidateCinematicRequest(request, result)) return result;
				string currentRevision = CurrentRevision();
				string newPlanId = "artist-cinematic-plan-" + Guid.NewGuid().ToString("N");
				result.planId = newPlanId;
				result.revision = currentRevision;
				result.baseRevision = currentRevision;
				result.approvalRequired = true;
				result.savePerformed = false;
				result.undoAvailable = true;
				result.exactDiff.Add(new ArtistChange { target = director.name, property = CinematicProperty(request), before = "observed", after = CinematicValue(request) });
				result.evidence.Add("cinematic_plan");
				result.evidence.Add("timeline_evidence");
				result.evidence.Add("exact_diff");
				result.evidence.Add("expected_revision");
				plans[newPlanId] = new StoredPlan { planId = newPlanId, baseRevision = currentRevision, cinematic = request, preview = result };
				history.Add(newPlanId);
				return result;
			}

			if (!plans.TryGetValue(planId ?? string.Empty, out StoredPlan stored) || stored.cinematic == null)
				return Error(result, "PLAN_NOT_FOUND", "The cinematic plan is not present in this Editor session.");
			if (!string.IsNullOrWhiteSpace(expectedRevision) && !string.Equals(expectedRevision, stored.baseRevision, StringComparison.Ordinal))
				return Error(result, "STALE_REVISION", "The cinematic plan revision is stale.");
			if (normalized == "preview")
			{
				stored.preview.command = "artist.cinematic";
				return stored.preview;
			}
			if (string.IsNullOrWhiteSpace(approvalToken)) return Error(result, "APPROVAL_REQUIRED", "Cinematic apply requires an opaque approval token from UnityAgent.");
			if (!string.Equals(expectedRevision, CurrentRevision(), StringComparison.Ordinal)) return Error(result, "STALE_REVISION", "The Editor state changed after the cinematic plan was prepared.");
			if (!ApplyCinematic(stored.cinematic, result)) return result;
			result.planId = planId;
			result.revision = CurrentRevision();
			result.baseRevision = stored.baseRevision;
			result.approvalRequired = true;
			result.savePerformed = false;
			result.undoAvailable = true;
			result.evidence.Add("mutation_evidence");
			result.evidence.Add("undo_registration");
			result.evidence.Add("save_not_performed");
			plans.Remove(planId);
			history.Add("applied:" + planId);
			return result;
		}

		public static ArtistResult History()
		{
			ArtistResult result = Base("artist.history", Support());
			if (!result.support.supported) return Error(result, result.support.errorCode, result.support.reason);
			result.evidence.AddRange(history);
			result.verified = true;
			return result;
		}

		private static bool ApplyIntent(ArtistIntent intent, ArtistResult result)
		{
			if (intent.setFog)
			{
				Undo.IncrementCurrentGroup();
				RenderSettings.fog = true;
				RenderSettings.fogDensity = Mathf.Clamp(intent.fogDensity, 0.0f, 1.0f);
				MarkScenesDirty();
				result.exactDiff.Add(new ArtistChange { target = "RenderSettings", property = "fogDensity", before = "observed", after = RenderSettings.fogDensity.ToString("0.####") });
			}
			if (intent.setLightIntensity)
			{
				Light light = FindLight(intent.targetName);
				if (light == null)
				{
					Error(result, "TARGET_NOT_FOUND", "An exact light target is required for lightIntensity.");
					return false;
				}
				Undo.RecordObject(light, "UnityArtistCLI Light LookDev");
				float before = light.intensity;
				light.intensity = Mathf.Max(0.0f, intent.lightIntensity);
				result.exactDiff.Add(new ArtistChange { target = light.name, property = "intensity", before = before.ToString("0.####"), after = light.intensity.ToString("0.####") });
			}
			if (intent.setCameraFieldOfView)
			{
				Camera camera = FindCamera(intent.targetName);
				if (camera == null)
				{
					Error(result, "TARGET_NOT_FOUND", "An exact camera target is required for cameraFieldOfView.");
					return false;
				}
				Undo.RecordObject(camera, "UnityArtistCLI Camera Composition");
				float before = camera.fieldOfView;
				camera.fieldOfView = Mathf.Clamp(intent.cameraFieldOfView, 1.0f, 179.0f);
				result.exactDiff.Add(new ArtistChange { target = camera.name, property = "fieldOfView", before = before.ToString("0.####"), after = camera.fieldOfView.ToString("0.####") });
			}
			return result.errors.Count == 0;
		}

		private static bool ValidateCinematicRequest(CinematicRequest request, ArtistResult result)
		{
			string kind = (request.trackKind ?? string.Empty).Trim().ToLowerInvariant();
			string[] allowed = { "binding", "activation", "animation", "control", "signal", "marker", "cinemachine_shot", "shot" };
			if (!allowed.Contains(kind, StringComparer.Ordinal))
			{
				Error(result, "INVALID_CINEMATIC_REQUEST", "trackKind must be one of binding, activation, animation, control, signal, marker, cinemachine_shot, or shot.");
				return false;
			}
			if (kind != "marker" && string.IsNullOrWhiteSpace(request.trackName))
			{
				Error(result, "INVALID_CINEMATIC_REQUEST", "An exact trackName is required for cinematic mutation.");
				return false;
			}
			return true;
		}

		private static bool ApplyCinematic(CinematicRequest request, ArtistResult result)
		{
			PlayableDirector director = FindDirector(request.directorName);
			if (director == null)
			{
				Error(result, "TIMELINE_DIRECTOR_NOT_FOUND", "An exact PlayableDirector target is required.");
				return false;
			}
			Undo.RecordObject(director, "UnityArtistCLI Cinematic Binding");
			string kind = (request.trackKind ?? string.Empty).Trim().ToLowerInvariant();
			if (kind == "binding")
			{
				if (string.IsNullOrWhiteSpace(request.bindingTargetName))
				{
					Error(result, "BINDING_TARGET_REQUIRED", "Binding mutation requires an exact bindingTargetName.");
					return false;
				}
				UnityEngine.Object bindingKey = FindOutputKey(director, request.trackName);
				GameObject target = FindGameObject(request.bindingTargetName);
				if (bindingKey == null || target == null)
				{
					Error(result, "CINEMATIC_TARGET_NOT_FOUND", "The exact Timeline binding or target GameObject was not found.");
					return false;
				}
				director.SetGenericBinding(bindingKey, target);
				result.exactDiff.Add(new ArtistChange { target = request.trackName, property = "genericBinding", before = "observed", after = target.name });
				result.evidence.Add("camera_binding");
				return true;
			}

			if (!TryCreateTimelineArtifact(director, request, result)) return false;
			return true;
		}

		private static bool TryCreateTimelineArtifact(PlayableDirector director, CinematicRequest request, ArtistResult result)
		{
			if (director.playableAsset == null)
			{
				Error(result, "TIMELINE_ASSET_NOT_FOUND", "The exact PlayableDirector has no Timeline asset.");
				return false;
			}
			string kind = (request.trackKind ?? string.Empty).Trim().ToLowerInvariant();
			string typeName = kind switch
			{
				"activation" => "UnityEngine.Timeline.ActivationTrack, Unity.Timeline",
				"animation" => "UnityEngine.Timeline.AnimationTrack, Unity.Timeline",
				"control" => "UnityEngine.Timeline.ControlTrack, Unity.Timeline",
				"signal" => "UnityEngine.Timeline.SignalTrack, Unity.Timeline",
				"marker" => "UnityEngine.Timeline.SignalEmitter, Unity.Timeline",
				"cinemachine_shot" or "shot" => "Unity.Cinemachine.CinemachineTrack, Unity.Cinemachine.Runtime",
				_ => string.Empty
			};
			Type artifactType = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName, false);
			if (artifactType == null)
			{
				Error(result, "CAPABILITY_UNAVAILABLE", "The requested allowlisted Timeline or Cinemachine type is not installed in this Project.");
				return false;
			}
			int expectedParameterCount = kind == "marker" ? 2 : 3;
			MethodInfo create = director.playableAsset.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.FirstOrDefault(method => method.Name == (kind == "marker" ? "CreateMarker" : "CreateTrack")
					&& method.GetParameters().Length == expectedParameterCount
					&& method.GetParameters()[0].ParameterType == typeof(Type));
			if (create == null)
			{
				Error(result, "CAPABILITY_UNAVAILABLE", "The installed Timeline API does not expose the required bounded creation method.");
				return false;
			}
			try
			{
				object created = kind == "marker"
					? create.Invoke(director.playableAsset, new object[] { artifactType, request.markerTime })
					: create.Invoke(director.playableAsset, new object[] { artifactType, null, request.trackName });
				if (created == null)
				{
					Error(result, "CINEMATIC_CREATE_FAILED", "The Timeline API did not create the requested bounded artifact.");
					return false;
				}
				result.exactDiff.Add(new ArtistChange { target = director.name, property = kind, before = "absent", after = request.trackName ?? request.markerTime.ToString("0.###") });
				result.evidence.Add("timeline_evidence");
				return true;
			}
			catch (Exception exception)
			{
				Error(result, "CINEMATIC_CREATE_FAILED", exception.InnerException == null ? exception.Message : exception.InnerException.Message);
				return false;
			}
		}

		private static void InspectCinematicDirector(PlayableDirector director, ArtistResult result)
		{
			result.targets.Add(Target("timeline_director", director, director.enabled));
			if (director.playableAsset == null) return;
			result.evidence.Add("playable_asset:" + director.playableAsset.name);
			foreach (PlayableBinding output in director.playableAsset.outputs)
			{
				UnityEngine.Object key = output.sourceObject;
				if (key == null) continue;
				string typeName = key.GetType().Name;
				result.evidence.Add("timeline_track:" + typeName + ":" + key.name);
				UnityEngine.Object binding = director.GetGenericBinding(key);
				if (binding != null) result.evidence.Add("binding:" + key.name + "->" + binding.name);
				string lower = typeName.ToLowerInvariant();
				if (lower.Contains("cinemachine")) result.evidence.Add("cinemachine_shot");
				if (lower.Contains("activation")) result.evidence.Add("activation_track");
				if (lower.Contains("signal")) result.evidence.Add("signal_marker");
				if (lower.Contains("control")) result.evidence.Add("control_track");
				if (lower.Contains("animation")) result.evidence.Add("animation_track");
			}
		}

		private static string CinematicProperty(CinematicRequest request) => (request.trackKind ?? "timeline").Trim().ToLowerInvariant();

		private static string CinematicValue(CinematicRequest request) => string.IsNullOrWhiteSpace(request.trackName) ? request.markerTime.ToString("0.###") : request.trackName;

		private static void BuildDiff(ArtistResult result, ArtistIntent intent)
		{
			if (intent.setFog) result.exactDiff.Add(new ArtistChange { target = "RenderSettings", property = "fogDensity", before = RenderSettings.fogDensity.ToString("0.####"), after = Mathf.Clamp(intent.fogDensity, 0.0f, 1.0f).ToString("0.####") });
			if (intent.setLightIntensity) result.exactDiff.Add(new ArtistChange { target = intent.targetName ?? string.Empty, property = "intensity", before = "observed", after = Mathf.Max(0.0f, intent.lightIntensity).ToString("0.####") });
			if (intent.setCameraFieldOfView) result.exactDiff.Add(new ArtistChange { target = intent.targetName ?? string.Empty, property = "fieldOfView", before = "observed", after = Mathf.Clamp(intent.cameraFieldOfView, 1.0f, 179.0f).ToString("0.####") });
		}

		private static bool HasChange(ArtistIntent intent)
		{
			return intent.setFog || intent.setLightIntensity || intent.setCameraFieldOfView;
		}

		private static ArtistResult Base(string command, ArtistSupport support)
		{
			return new ArtistResult { command = command, status = "passed", support = support, revision = CurrentRevision(), baseRevision = CurrentRevision(), savePerformed = false, undoAvailable = true };
		}

		private static ArtistResult Error(ArtistResult result, string code, string message)
		{
			result.status = "blocked";
			result.verified = false;
			result.errors.Add(new ArtistError { code = code, message = message });
			return result;
		}

		private static ArtistSupport Support()
		{
			string version = Application.unityVersion ?? string.Empty;
			string pipeline = DetectPipeline();
			bool supported = ArtistCompatibility.IsSupported(version, pipeline);
			return new ArtistSupport
			{
				supported = supported,
				supportTier = supported ? "primary" : "unsupported",
				unityVersion = version,
				renderPipeline = pipeline,
				compatibilityBackend = supported ? ArtistCompatibility.Backend(pipeline) : "none",
				transport = "official_unity_cli_pipeline",
				errorCode = supported ? string.Empty : (version.StartsWith("2022.3.", StringComparison.Ordinal) ? "UNSUPPORTED_RENDER_PIPELINE_VERSION" : "UNSUPPORTED_UNITY_VERSION"),
				reason = supported ? string.Empty : "This Unity version and render pipeline are outside the formal UnityArtistCLI release matrix."
			};
		}

		private static string DetectPipeline()
		{
			RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
			if (asset == null) return "builtin";
			string typeName = asset.GetType().FullName ?? asset.GetType().Name;
			if (typeName.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0 || typeName.IndexOf("HDRP", StringComparison.OrdinalIgnoreCase) >= 0) return "hdrp";
			if (typeName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0 || typeName.IndexOf("URP", StringComparison.OrdinalIgnoreCase) >= 0) return "urp";
			return "unknown";
		}

		private static IEnumerable<T> SceneObjects<T>() where T : Component
		{
			return Resources.FindObjectsOfTypeAll<T>().Where(value => value != null && value.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(value));
		}

		private static ArtistTarget Target(string kind, Component component, bool enabled)
		{
			return new ArtistTarget { kind = kind, name = component.name, scenePath = component.gameObject.scene.path, hierarchyPath = HierarchyPath(component.transform), enabled = enabled };
		}

		private static string HierarchyPath(Transform transform)
		{
			return transform.parent == null ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;
		}

		private static Light FindLight(string name)
		{
			return SceneObjects<Light>().FirstOrDefault(value => string.Equals(value.name, name, StringComparison.Ordinal));
		}

		private static Camera FindCamera(string name)
		{
			IEnumerable<Camera> cameras = SceneObjects<Camera>();
			if (!string.IsNullOrWhiteSpace(name)) return cameras.FirstOrDefault(value => string.Equals(value.name, name, StringComparison.Ordinal));
			return cameras.FirstOrDefault(value => value.CompareTag("MainCamera")) ?? cameras.FirstOrDefault();
		}

		private static PlayableDirector FindDirector(string name)
		{
			IEnumerable<PlayableDirector> directors = SceneObjects<PlayableDirector>();
			if (!string.IsNullOrWhiteSpace(name)) return directors.FirstOrDefault(value => string.Equals(value.name, name, StringComparison.Ordinal));
			return directors.FirstOrDefault();
		}

		private static GameObject FindGameObject(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return null;
			return SceneObjects<Transform>().FirstOrDefault(value => string.Equals(value.name, name, StringComparison.Ordinal))?.gameObject;
		}

		private static UnityEngine.Object FindOutputKey(PlayableDirector director, string name)
		{
			if (director == null || director.playableAsset == null || string.IsNullOrWhiteSpace(name)) return null;
			foreach (PlayableBinding output in director.playableAsset.outputs)
			{
				if (output.sourceObject != null && string.Equals(output.sourceObject.name, name, StringComparison.Ordinal)) return output.sourceObject;
			}
			return null;
		}

		private static string CurrentRevision()
		{
			StringBuilder material = new StringBuilder(Application.unityVersion);
			for (int index = 0; index < SceneManager.sceneCount; index++) material.Append('|').Append(SceneManager.GetSceneAt(index).path);
			foreach (Light light in SceneObjects<Light>().OrderBy(value => value.name)) material.Append('|').Append(light.name).Append(':').Append(light.intensity.ToString("R"));
			foreach (Camera camera in SceneObjects<Camera>().OrderBy(value => value.name)) material.Append('|').Append(camera.name).Append(':').Append(camera.fieldOfView.ToString("R"));
			using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(material.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
		}

		private static void MarkScenesDirty()
		{
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (scene.IsValid() && scene.isLoaded) EditorSceneManager.MarkSceneDirty(scene);
			}
		}

		private static string Serialize(ArtistResult result) => JsonUtility.ToJson(result, true);
	}

	public static class ArtistPipelineCommands
	{
		[CliCommand("artist.inspect", "Inspect visual targets, render pipeline support and cinematic objects.")]
		public static string Inspect() => JsonUtility.ToJson(ArtistSession.Inspect());

		[CliCommand("artist.plan", "Create a read-only visual intent plan with an exact diff.")]
		public static string Plan([CliArg("request-json", "Structured visual intent JSON.", Required = false)] string requestJson = "{}", [CliArg("expected-revision", "Expected Editor revision.", Required = false)] string expectedRevision = "") => JsonUtility.ToJson(ArtistSession.Plan(requestJson, expectedRevision));

		[CliCommand("artist.preview", "Return an exact visual plan without mutating the Editor.")]
		public static string Preview([CliArg("plan-id", "Plan id.", Required = true)] string planId, [CliArg("expected-revision", "Expected Editor revision.", Required = false)] string expectedRevision = "") => JsonUtility.ToJson(ArtistSession.Preview(planId, expectedRevision));

		[CliCommand("artist.apply", "Apply an approved visual plan with revision and Undo guards.")]
		public static string Apply([CliArg("plan-id", "Plan id.", Required = true)] string planId, [CliArg("expected-revision", "Expected Editor revision.", Required = true)] string expectedRevision, [CliArg("approval-token", "Opaque UnityAgent approval token.", Required = true)] string approvalToken) => JsonUtility.ToJson(ArtistSession.Apply(planId, expectedRevision, approvalToken));

		[CliCommand("artist.capture", "Capture visual evidence from an exact camera binding.")]
		public static string Capture([CliArg("request-json", "Structured capture request JSON.", Required = false)] string requestJson = "{}") => JsonUtility.ToJson(ArtistSession.Capture(requestJson));

		[CliCommand("artist.evaluate", "Record a human visual review decision for a capture.")]
		public static string Evaluate([CliArg("capture-id", "Capture id.", Required = true)] string captureId, [CliArg("decision", "accepted, rejected, or needs_refine.", Required = true)] string decision, [CliArg("notes", "Human review notes.", Required = false)] string notes = "") => JsonUtility.ToJson(ArtistSession.Evaluate(captureId, decision, notes));

		[CliCommand("artist.refine", "Create a linked refinement plan from a human visual review.")]
		public static string Refine([CliArg("evaluation-id", "Evaluation id.", Required = true)] string evaluationId, [CliArg("request-json", "Structured refinement intent JSON.", Required = true)] string requestJson) => JsonUtility.ToJson(ArtistSession.Refine(evaluationId, requestJson));

		[CliCommand("artist.history", "Read the current session's redacted visual evidence history.")]
		public static string History() => JsonUtility.ToJson(ArtistSession.History());

		[CliCommand("artist.cinematic", "Inspect, plan, preview or apply bounded Timeline, camera-shot and binding workflows.")]
		public static string Cinematic([CliArg("operation", "inspect, plan, preview, or apply.", Required = false)] string operation = "inspect", [CliArg("request-json", "Structured cinematic request JSON.", Required = false)] string requestJson = "{}", [CliArg("plan-id", "Plan id for preview/apply.", Required = false)] string planId = "", [CliArg("expected-revision", "Expected Editor revision.", Required = false)] string expectedRevision = "", [CliArg("approval-token", "Opaque UnityAgent approval token.", Required = false)] string approvalToken = "") => JsonUtility.ToJson(ArtistSession.Cinematic(operation, requestJson, planId, expectedRevision, approvalToken));
	}
}

#endif
