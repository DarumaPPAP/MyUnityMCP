#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	public enum E_GRAPHICS_APV_JOB_STATUS
	{
		PREPARED,
		RUNNING,
		SUCCEEDED,
		PARTIAL,
		FAILED,
		CANCEL_REQUESTED,
		CANCELLED
	}

	public sealed class UnityGraphicsMcpApvBakePlanInput
	{
		public string bakingSetAssetPath { get; set; }
		public string lightingScenario { get; set; }
		public string[] scenePaths { get; set; }
		public string[] outputAssetRoots { get; set; }
		public int? timeoutSeconds { get; set; }
	}

	internal sealed class UnityGraphicsMcpApvEnvironment
	{
		public string PipelineKind { get; set; }
		public string PipelineAssetType { get; set; }
		public string BakingSetAssetPath { get; set; }
		public string BakingSetType { get; set; }
		public string BakingSetDigest { get; set; }
		public List<string> ScenePaths { get; set; } = new List<string>();
		public List<string> LightingScenarios { get; set; } = new List<string>();
		public string BackendType { get; set; }
		public string BakeMethod { get; set; }
		public string RunningProperty { get; set; }
		public string CancelMethod { get; set; }
		public bool NativeCancellationSupported { get; set; }
		public Object BakingSetAsset { get; set; }
	}

	internal sealed class UnityGraphicsMcpApvBakePlan
	{
		public string PlanId { get; set; }
		public long Revision { get; set; }
		public DateTime CreatedUtc { get; set; }
		public DateTime ExpiresUtc { get; set; }
		public string ApprovalTokenHash { get; set; }
		public string DiffDigest { get; set; }
		public bool Consumed { get; set; }
		public string BakingSetAssetPath { get; set; }
		public string BakingSetDigest { get; set; }
		public string LightingScenario { get; set; }
		public List<string> ScenePaths { get; set; } = new List<string>();
		public List<string> OutputAssetRoots { get; set; } = new List<string>();
		public int TimeoutSeconds { get; set; }
		public string PipelineKind { get; set; }
		public string PipelineAssetType { get; set; }
		public string BackendType { get; set; }
		public string BakeMethod { get; set; }
		public string RunningProperty { get; set; }
		public string CancelMethod { get; set; }
		public bool NativeCancellationSupported { get; set; }
	}

	internal sealed class UnityGraphicsMcpApvBackendState
	{
		public object ProbeReferenceVolumeInstance { get; set; }
		public object PreviousBakingSet { get; set; }
		public string PreviousLightingScenario { get; set; }
		public bool Started { get; set; }
	}

	internal sealed class UnityGraphicsMcpApvBakeJob
	{
		public string JobId { get; set; }
		public string PlanId { get; set; }
		public long StartRevision { get; set; }
		public DateTime StartedUtc { get; set; }
		public DateTime? CompletedUtc { get; set; }
		public string Status { get; set; }
		public string FailureCode { get; set; }
		public string FailureMessage { get; set; }
		public bool CancellationRequested { get; set; }
		public bool CancellationInvoked { get; set; }
		public bool MutationRevisionNotified { get; set; }
		public bool BackendStarted { get; set; }
		public bool Finalizing { get; set; }
		public UnityGraphicsMcpApvBakePlan Plan { get; set; }
		public UnityGraphicsMcpApvBackendState BackendState { get; set; }
		public Dictionary<string, string> OutputBefore { get; set; } =
			new Dictionary<string, string>(StringComparer.Ordinal);
		public Dictionary<string, string> OutputAfter { get; set; } =
			new Dictionary<string, string>(StringComparer.Ordinal);
		public List<Dictionary<string, object>> OutputDiff { get; set; } =
			new List<Dictionary<string, object>>();
		public List<string> CompletedStages { get; set; } = new List<string>();
	}

	[InitializeOnLoad]
	internal static class UnityGraphicsMcpPhase4DApvSession
	{
		private const int MAX_PLAN_COUNT = 8;
		private const int MAX_JOB_COUNT = 8;
		private static readonly TimeSpan PLAN_LIFETIME = TimeSpan.FromMinutes(10.0);
		private static readonly Dictionary<string, UnityGraphicsMcpApvBakePlan> _plans =
			new Dictionary<string, UnityGraphicsMcpApvBakePlan>(StringComparer.Ordinal);
		private static readonly Dictionary<string, UnityGraphicsMcpApvBakeJob> _jobs =
			new Dictionary<string, UnityGraphicsMcpApvBakeJob>(StringComparer.Ordinal);

		internal static Func<UnityGraphicsMcpApvBakePlanInput, UnityGraphicsMcpApvEnvironment>
			EnvironmentOverrideForTests { get; set; }
		internal static Func<UnityGraphicsMcpApvBakePlan, UnityGraphicsMcpApvBackendState>
			StartOverrideForTests { get; set; }
		internal static Func<bool> IsRunningOverrideForTests { get; set; }
		internal static Func<bool> CancelOverrideForTests { get; set; }
		internal static Func<UnityGraphicsMcpApvBakePlan, Dictionary<string, string>>
			OutputSnapshotOverrideForTests { get; set; }

		static UnityGraphicsMcpPhase4DApvSession()
		{
			EditorApplication.update += Tick;
			EditorApplication.playModeStateChanged += state => Clear();
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			CompilationPipeline.compilationStarted += context => Clear();
			EditorApplication.quitting += Clear;
		}

		public static string StorePlan(UnityGraphicsMcpApvBakePlan plan)
		{
			RemoveExpiredPlans();
			while (_plans.Count >= MAX_PLAN_COUNT)
			{
				string oldest = _plans.OrderBy(item => item.Value.CreatedUtc).First().Key;
				_plans.Remove(oldest);
			}

			plan.PlanId = UnityGraphicsMcpSession.SessionId +
				":apv-bake-plan:" + Guid.NewGuid().ToString("N");
			plan.CreatedUtc = DateTime.UtcNow;
			plan.ExpiresUtc = plan.CreatedUtc + PLAN_LIFETIME;
			_plans[plan.PlanId] = plan;
			return plan.PlanId;
		}

		public static bool TryGetPlan(
			string planId,
			long expectedRevision,
			string approvalToken,
			out UnityGraphicsMcpApvBakePlan plan,
			out E_MCP_TOOL_STATUS failureStatus,
			out string failureMessage)
		{
			plan = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;
			failureMessage = null;
			RemoveExpiredPlans();

			if (string.IsNullOrWhiteSpace(planId) ||
				!planId.StartsWith(
					UnityGraphicsMcpSession.SessionId + ":apv-bake-plan:",
					StringComparison.Ordinal) ||
				!_plans.TryGetValue(planId, out plan))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				failureMessage = "APV Bake Planが現在のEditor Sessionに存在しないか有効期限切れです。";
				return false;
			}

			if (plan.Consumed)
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "APV Bake Planは既に使用済みです。";
				return false;
			}

			if (expectedRevision != UnityGraphicsMcpSession.Revision ||
				plan.Revision != UnityGraphicsMcpSession.Revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				failureMessage = "APV Bake Plan作成後にEditor Revisionが変更されました。";
				return false;
			}

			if (string.IsNullOrWhiteSpace(approvalToken) ||
				!string.Equals(
					plan.ApprovalTokenHash,
					UnityGraphicsMcpPhase4Session.HashText(approvalToken),
					StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				failureMessage = "APV Bake承認Tokenが不足しているか一致しません。";
				return false;
			}

			return true;
		}

		public static UnityGraphicsMcpApvBakeJob StartJob(
			UnityGraphicsMcpApvBakePlan plan,
			out string failureMessage)
		{
			failureMessage = null;
			while (_jobs.Count >= MAX_JOB_COUNT)
			{
				string oldest = _jobs.OrderBy(item => item.Value.StartedUtc).First().Key;
				_jobs.Remove(oldest);
			}

			UnityGraphicsMcpApvBakeJob job = new UnityGraphicsMcpApvBakeJob
			{
				JobId = UnityGraphicsMcpSession.SessionId +
					":apv-bake-job:" + Guid.NewGuid().ToString("N"),
				PlanId = plan.PlanId,
				StartRevision = UnityGraphicsMcpSession.Revision,
				StartedUtc = DateTime.UtcNow,
				Status = E_GRAPHICS_APV_JOB_STATUS.RUNNING.ToString(),
				Plan = plan,
				OutputBefore = CaptureOutputSnapshot(plan)
			};
			job.CompletedStages.Add("PLAN_VALIDATED");
			job.CompletedStages.Add("OUTPUT_BASELINE_CAPTURED");

			try
			{
				UnityGraphicsMcpApvBackendState state =
					StartOverrideForTests != null
						? StartOverrideForTests(plan)
						: UnityGraphicsMcpApvReflectionBackend.Start(plan);
				if (state == null || !state.Started)
				{
					failureMessage = "AdaptiveProbeVolumes.BakeAsync()が開始されませんでした。";
					job.Status = E_GRAPHICS_APV_JOB_STATUS.FAILED.ToString();
					job.FailureCode = "APV_BAKE_START_REJECTED";
					job.FailureMessage = failureMessage;
					job.CompletedUtc = DateTime.UtcNow;
				}
				else
				{
					job.BackendState = state;
					job.BackendStarted = true;
					job.CompletedStages.Add("APV_BAKE_STARTED");
				}
			}
			catch (Exception exception)
			{
				failureMessage = exception.Message;
				job.Status = E_GRAPHICS_APV_JOB_STATUS.FAILED.ToString();
				job.FailureCode = "APV_BAKE_START_EXCEPTION";
				job.FailureMessage = exception.Message;
				job.CompletedUtc = DateTime.UtcNow;
			}

			plan.Consumed = true;
			_jobs[job.JobId] = job;
			if (job.BackendStarted)
			{
				TickJob(job);
			}
			return job;
		}

		public static bool TryGetJob(string jobId, out UnityGraphicsMcpApvBakeJob job)
		{
			job = null;
			if (string.IsNullOrWhiteSpace(jobId) ||
				!jobId.StartsWith(
					UnityGraphicsMcpSession.SessionId + ":apv-bake-job:",
					StringComparison.Ordinal) ||
				!_jobs.TryGetValue(jobId, out job))
			{
				return false;
			}

			TickJob(job);
			return true;
		}

		public static bool RequestCancellation(
			UnityGraphicsMcpApvBakeJob job,
			out string failureMessage)
		{
			failureMessage = null;
			if (job == null)
			{
				failureMessage = "APV Bake Jobがありません。";
				return false;
			}
			if (IsTerminal(job.Status))
			{
				failureMessage = "APV Bake Jobは既に完了しています。";
				return false;
			}

			job.CancellationRequested = true;
			job.Status = E_GRAPHICS_APV_JOB_STATUS.CANCEL_REQUESTED.ToString();
			TickJob(job);
			return true;
		}

		public static void TickForTests()
		{
			Tick();
		}

		public static void ClearForTests()
		{
			Clear();
		}

		private static void Tick()
		{
			foreach (UnityGraphicsMcpApvBakeJob job in _jobs.Values.ToArray())
			{
				TickJob(job);
			}
		}

		private static void TickJob(UnityGraphicsMcpApvBakeJob job)
		{
			if (job == null || job.Finalizing || IsTerminal(job.Status))
			{
				return;
			}

			if (UnityGraphicsMcpSession.Revision != job.StartRevision)
			{
				job.CancellationRequested = true;
				job.FailureCode = "APV_BAKE_STALE_DURING_EXECUTION";
				job.FailureMessage = "APV Bake実行中にEditor Revisionが変更されました。";
			}
			if ((DateTime.UtcNow - job.StartedUtc).TotalSeconds > job.Plan.TimeoutSeconds)
			{
				job.CancellationRequested = true;
				job.FailureCode = "APV_BAKE_TIMEOUT";
				job.FailureMessage = "APV Bakeが明示Timeoutを超過しました。";
			}

			if (job.CancellationRequested && !job.CancellationInvoked)
			{
				try
				{
					job.CancellationInvoked = CancelOverrideForTests != null
						? CancelOverrideForTests()
						: UnityGraphicsMcpApvReflectionBackend.Cancel(job.Plan);
				}
				catch (Exception exception)
				{
					job.FailureCode = "APV_CANCEL_EXCEPTION";
					job.FailureMessage = exception.Message;
				}
			}

			bool running;
			try
			{
				running = IsRunningOverrideForTests != null
					? IsRunningOverrideForTests()
					: UnityGraphicsMcpApvReflectionBackend.IsRunning(job.Plan);
			}
			catch (Exception exception)
			{
				job.FailureCode = "APV_STATUS_EXCEPTION";
				job.FailureMessage = exception.Message;
				running = false;
			}

			if (running)
			{
				job.Status = job.CancellationRequested
					? E_GRAPHICS_APV_JOB_STATUS.CANCEL_REQUESTED.ToString()
					: E_GRAPHICS_APV_JOB_STATUS.RUNNING.ToString();
				return;
			}

			FinalizeJob(job);
		}

		private static void FinalizeJob(UnityGraphicsMcpApvBakeJob job)
		{
			if (job.Finalizing || IsTerminal(job.Status))
			{
				return;
			}

			job.Finalizing = true;
			try
			{
				try
				{
					if (StartOverrideForTests == null)
					{
						UnityGraphicsMcpApvReflectionBackend.Restore(job.BackendState);
					}
				}
				catch (Exception exception)
				{
					if (string.IsNullOrWhiteSpace(job.FailureCode))
					{
						job.FailureCode = "APV_EDITOR_STATE_RESTORE_FAILED";
						job.FailureMessage = exception.Message;
					}
				}

				job.OutputAfter = CaptureOutputSnapshot(job.Plan);
				job.OutputDiff = BuildOutputDiff(job.OutputBefore, job.OutputAfter);
				job.CompletedStages.Add("OUTPUT_DIFF_CAPTURED");
				job.CompletedUtc = DateTime.UtcNow;

				bool hasOutput = job.OutputDiff.Count > 0;
				if (job.CancellationRequested)
				{
					job.Status = hasOutput
						? E_GRAPHICS_APV_JOB_STATUS.PARTIAL.ToString()
						: E_GRAPHICS_APV_JOB_STATUS.CANCELLED.ToString();
					if (string.IsNullOrWhiteSpace(job.FailureCode))
					{
						job.FailureCode = "APV_BAKE_CANCELLED";
						job.FailureMessage = "APV BakeはCancellation契約により停止されました。";
					}
				}
				else if (!string.IsNullOrWhiteSpace(job.FailureCode))
				{
					job.Status = hasOutput
						? E_GRAPHICS_APV_JOB_STATUS.PARTIAL.ToString()
						: E_GRAPHICS_APV_JOB_STATUS.FAILED.ToString();
				}
				else if (!hasOutput)
				{
					job.Status = E_GRAPHICS_APV_JOB_STATUS.FAILED.ToString();
					job.FailureCode = "APV_BAKE_NO_OUTPUT_DIFF";
					job.FailureMessage =
						"APV Bake終了後に明示Output Root内の追加または変更Assetを確認できませんでした。";
				}
				else
				{
					job.Status = E_GRAPHICS_APV_JOB_STATUS.SUCCEEDED.ToString();
					job.CompletedStages.Add("APV_BAKE_COMPLETED");
				}

				if (hasOutput && !job.MutationRevisionNotified)
				{
					job.MutationRevisionNotified = true;
					UnityGraphicsMcpSession.NotifyMutationApplied();
				}
			}
			finally
			{
				job.Finalizing = false;
			}
		}

		private static Dictionary<string, string> CaptureOutputSnapshot(
			UnityGraphicsMcpApvBakePlan plan)
		{
			if (OutputSnapshotOverrideForTests != null)
			{
				return OutputSnapshotOverrideForTests(plan) ??
					new Dictionary<string, string>(StringComparer.Ordinal);
			}

			Dictionary<string, string> snapshot =
				new Dictionary<string, string>(StringComparer.Ordinal);
			string[] roots = plan.OutputAssetRoots
				.Where(path => !string.IsNullOrWhiteSpace(path))
				.Distinct(StringComparer.Ordinal)
				.ToArray();
			foreach (string guid in AssetDatabase.FindAssets(string.Empty, roots))
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrWhiteSpace(assetPath) ||
					AssetDatabase.IsValidFolder(assetPath))
				{
					continue;
				}

				string absolutePath = ToAbsoluteProjectPath(assetPath);
				if (File.Exists(absolutePath))
				{
					snapshot[assetPath] = HashFile(absolutePath);
				}
			}
			return snapshot;
		}

		private static List<Dictionary<string, object>> BuildOutputDiff(
			Dictionary<string, string> before,
			Dictionary<string, string> after)
		{
			List<Dictionary<string, object>> diff =
				new List<Dictionary<string, object>>();
			HashSet<string> paths = new HashSet<string>(
				before.Keys.Concat(after.Keys),
				StringComparer.Ordinal);
			foreach (string path in paths.OrderBy(value => value, StringComparer.Ordinal))
			{
				string beforeHash;
				string afterHash;
				bool existedBefore = before.TryGetValue(path, out beforeHash);
				bool existsAfter = after.TryGetValue(path, out afterHash);
				if (existedBefore && existsAfter &&
					string.Equals(beforeHash, afterHash, StringComparison.Ordinal))
				{
					continue;
				}

				diff.Add(new Dictionary<string, object>
				{
					{ "assetPath", path },
					{ "change", !existedBefore ? "ADDED" : !existsAfter ? "REMOVED" : "MODIFIED" },
					{ "beforeSha256", existedBefore ? beforeHash : null },
					{ "afterSha256", existsAfter ? afterHash : null }
				});
			}
			return diff;
		}

		private static string HashFile(string path)
		{
			using (SHA256 sha256 = SHA256.Create())
			using (FileStream stream = File.OpenRead(path))
			{
				byte[] hash = sha256.ComputeHash(stream);
				StringBuilder builder = new StringBuilder(hash.Length * 2);
				foreach (byte value in hash)
				{
					builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
				}
				return builder.ToString();
			}
		}

		private static string ToAbsoluteProjectPath(string assetPath)
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
		}

		private static bool IsTerminal(string status)
		{
			return status == E_GRAPHICS_APV_JOB_STATUS.SUCCEEDED.ToString() ||
				status == E_GRAPHICS_APV_JOB_STATUS.PARTIAL.ToString() ||
				status == E_GRAPHICS_APV_JOB_STATUS.FAILED.ToString() ||
				status == E_GRAPHICS_APV_JOB_STATUS.CANCELLED.ToString();
		}

		private static void RemoveExpiredPlans()
		{
			DateTime now = DateTime.UtcNow;
			foreach (string id in _plans
				.Where(item => item.Value.ExpiresUtc <= now)
				.Select(item => item.Key)
				.ToArray())
			{
				_plans.Remove(id);
			}
		}

		private static void Clear()
		{
			_plans.Clear();
			_jobs.Clear();
			EnvironmentOverrideForTests = null;
			StartOverrideForTests = null;
			IsRunningOverrideForTests = null;
			CancelOverrideForTests = null;
			OutputSnapshotOverrideForTests = null;
		}
	}

	internal static class UnityGraphicsMcpApvReflectionBackend
	{
		private const string PROBE_REFERENCE_VOLUME_TYPE =
			"UnityEngine.Rendering.ProbeReferenceVolume";
		private const string PROBE_VOLUME_BAKING_SET_TYPE =
			"UnityEngine.Rendering.ProbeVolumeBakingSet";
		private const string ADAPTIVE_PROBE_VOLUMES_TYPE =
			"UnityEditor.Rendering.AdaptiveProbeVolumes";

		public static bool TryInspectEnvironment(
			UnityGraphicsMcpApvBakePlanInput input,
			out UnityGraphicsMcpApvEnvironment environment,
			out string failureCode,
			out string failureMessage)
		{
			environment = null;
			failureCode = null;
			failureMessage = null;

			RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
			if (pipelineAsset == null)
			{
				failureCode = "APV_REQUIRES_SRP";
				failureMessage = "APVはURPまたはHDRPのRender Pipeline Assetを必要とします。";
				return false;
			}

			string pipelineType = pipelineAsset.GetType().FullName ?? string.Empty;
			string pipelineKind;
			if (pipelineType.IndexOf("UniversalRenderPipelineAsset", StringComparison.Ordinal) >= 0)
			{
				pipelineKind = "URP";
			}
			else if (pipelineType.IndexOf("HDRenderPipelineAsset", StringComparison.Ordinal) >= 0)
			{
				pipelineKind = "HDRP";
			}
			else
			{
				failureCode = "APV_PIPELINE_UNSUPPORTED";
				failureMessage = "現在のSRPはMyUnityMCP APV Capability対象外です。";
				return false;
			}

			string bakingSetPath = NormalizeAssetPath(input.bakingSetAssetPath);
			Object bakingSet = AssetDatabase.LoadMainAssetAtPath(bakingSetPath);
			if (bakingSet == null ||
				!string.Equals(
					bakingSet.GetType().FullName,
					PROBE_VOLUME_BAKING_SET_TYPE,
					StringComparison.Ordinal))
			{
				failureCode = "APV_BAKING_SET_NOT_FOUND";
				failureMessage =
					"bakingSetAssetPathからProbeVolumeBakingSetを解決できません。";
				return false;
			}

			Type referenceType = FindType(PROBE_REFERENCE_VOLUME_TYPE);
			Type backendType = FindType(ADAPTIVE_PROBE_VOLUMES_TYPE);
			MethodInfo bakeMethod = backendType == null
				? null
				: backendType.GetMethod(
					"BakeAsync",
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
					null,
					Type.EmptyTypes,
					null);
			PropertyInfo runningProperty = backendType == null
				? null
				: backendType.GetProperty(
					"isRunning",
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			if (referenceType == null || backendType == null ||
				bakeMethod == null || runningProperty == null)
			{
				failureCode = "APV_BACKEND_NOT_AVAILABLE";
				failureMessage =
					"ProbeReferenceVolumeまたはAdaptiveProbeVolumes APIを解決できません。";
				return false;
			}

			MethodInfo cancelMethod = ResolveCancelMethod(backendType);
			environment = new UnityGraphicsMcpApvEnvironment
			{
				PipelineKind = pipelineKind,
				PipelineAssetType = pipelineType,
				BakingSetAssetPath = bakingSetPath,
				BakingSetType = bakingSet.GetType().FullName,
				BakingSetDigest = BuildAssetDigest(bakingSetPath, bakingSet),
				ScenePaths = ExtractScenePaths(bakingSet),
				LightingScenarios = ExtractLightingScenarios(bakingSet),
				BackendType = backendType.FullName,
				BakeMethod = bakeMethod.Name,
				RunningProperty = runningProperty.Name,
				CancelMethod = cancelMethod == null ? null : cancelMethod.Name,
				NativeCancellationSupported = cancelMethod != null,
				BakingSetAsset = bakingSet
			};
			return true;
		}

		public static UnityGraphicsMcpApvBackendState Start(
			UnityGraphicsMcpApvBakePlan plan)
		{
			Object bakingSet = AssetDatabase.LoadMainAssetAtPath(plan.BakingSetAssetPath);
			if (bakingSet == null)
			{
				throw new InvalidOperationException("APV Baking Set Assetを再解決できません。");
			}

			Type referenceType = FindType(PROBE_REFERENCE_VOLUME_TYPE);
			Type backendType = FindType(plan.BackendType);
			PropertyInfo instanceProperty = referenceType == null
				? null
				: referenceType.GetProperty(
					"instance",
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			object instance = instanceProperty == null
				? null
				: instanceProperty.GetValue(null, null);
			if (instance == null || backendType == null)
			{
				throw new InvalidOperationException(
					"ProbeReferenceVolume.instanceまたはAPV Backendを解決できません。");
			}

			MethodInfo setActive = referenceType.GetMethod(
				"SetActiveBakingSet",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			PropertyInfo currentSet = referenceType.GetProperty(
				"currentBakingSet",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			PropertyInfo scenario = referenceType.GetProperty(
				"lightingScenario",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (setActive == null || scenario == null || !scenario.CanWrite)
			{
				throw new InvalidOperationException(
					"APV Baking SetまたはLighting Scenarioを明示設定できません。");
			}

			UnityGraphicsMcpApvBackendState state = new UnityGraphicsMcpApvBackendState
			{
				ProbeReferenceVolumeInstance = instance,
				PreviousBakingSet = currentSet == null ? null : currentSet.GetValue(instance, null),
				PreviousLightingScenario = scenario.GetValue(instance, null) as string
			};
			setActive.Invoke(instance, new object[] { bakingSet });
			scenario.SetValue(instance, plan.LightingScenario, null);

			MethodInfo bakeMethod = backendType.GetMethod(
				plan.BakeMethod,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
				null,
				Type.EmptyTypes,
				null);
			if (bakeMethod == null)
			{
				throw new MissingMethodException(backendType.FullName, plan.BakeMethod);
			}

			object started = bakeMethod.Invoke(null, null);
			state.Started = !(started is bool) || (bool)started;
			return state;
		}

		public static bool IsRunning(UnityGraphicsMcpApvBakePlan plan)
		{
			Type backendType = FindType(plan.BackendType);
			PropertyInfo property = backendType == null
				? null
				: backendType.GetProperty(
					plan.RunningProperty,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			if (property == null || property.PropertyType != typeof(bool))
			{
				throw new MissingMemberException(plan.BackendType, plan.RunningProperty);
			}
			return (bool)property.GetValue(null, null);
		}

		public static bool Cancel(UnityGraphicsMcpApvBakePlan plan)
		{
			Type backendType = FindType(plan.BackendType);
			MethodInfo cancelMethod = ResolveCancelMethod(backendType);
			bool invoked = false;
			if (cancelMethod != null)
			{
				object result = cancelMethod.Invoke(null, null);
				invoked = !(result is bool) || (bool)result;
			}
			if (Lightmapping.isRunning)
			{
				Lightmapping.Cancel();
				invoked = true;
			}
			return invoked;
		}

		public static void Restore(UnityGraphicsMcpApvBackendState state)
		{
			if (state == null || state.ProbeReferenceVolumeInstance == null)
			{
				return;
			}

			Type type = state.ProbeReferenceVolumeInstance.GetType();
			MethodInfo setActive = type.GetMethod(
				"SetActiveBakingSet",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			PropertyInfo scenario = type.GetProperty(
				"lightingScenario",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (setActive != null && state.PreviousBakingSet != null)
			{
				setActive.Invoke(
					state.ProbeReferenceVolumeInstance,
					new[] { state.PreviousBakingSet });
			}
			if (scenario != null && scenario.CanWrite &&
				state.PreviousLightingScenario != null)
			{
				scenario.SetValue(
					state.ProbeReferenceVolumeInstance,
					state.PreviousLightingScenario,
					null);
			}
		}

		public static string BuildAssetDigest(string assetPath, Object asset)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(assetPath).Append('|');
			builder.Append(asset == null ? string.Empty : asset.GetType().FullName).Append('|');
			builder.Append(asset == null ? string.Empty : EditorJsonUtility.ToJson(asset, false));
			return UnityGraphicsMcpPhase4Session.HashText(builder.ToString());
		}

		private static Type FindType(string fullName)
		{
			if (string.IsNullOrWhiteSpace(fullName))
			{
				return null;
			}
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type type = assembly.GetType(fullName, false);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}

		private static MethodInfo ResolveCancelMethod(Type backendType)
		{
			if (backendType == null)
			{
				return null;
			}
			foreach (string name in new[] { "Cancel", "CancelBake", "CancelBaking" })
			{
				MethodInfo method = backendType.GetMethod(
					name,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
					null,
					Type.EmptyTypes,
					null);
				if (method != null)
				{
					return method;
				}
			}
			return null;
		}

		private static List<string> ExtractScenePaths(Object bakingSet)
		{
			HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
			ExtractEnumerableMemberValues(
				bakingSet,
				new[] { "sceneGUIDs", "sceneGuids", "scenes" },
				values,
				true);
			ExtractSerializedStrings(
				bakingSet,
				path => path.IndexOf("scene", StringComparison.OrdinalIgnoreCase) >= 0,
				values,
				true);
			return values
				.Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
				.OrderBy(path => path, StringComparer.Ordinal)
				.ToList();
		}

		private static List<string> ExtractLightingScenarios(Object bakingSet)
		{
			HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
			ExtractEnumerableMemberValues(
				bakingSet,
				new[] { "lightingScenarios", "scenarios" },
				values,
				false);
			ExtractSerializedStrings(
				bakingSet,
				path => path.IndexOf("scenario", StringComparison.OrdinalIgnoreCase) >= 0,
				values,
				false);
			return values
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.OrderBy(value => value, StringComparer.Ordinal)
				.ToList();
		}

		private static void ExtractEnumerableMemberValues(
			Object target,
			IEnumerable<string> names,
			HashSet<string> values,
			bool convertSceneGuid)
		{
			Type type = target.GetType();
			foreach (string name in names)
			{
				PropertyInfo property = type.GetProperty(
					name,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				object raw = property == null ? null : property.GetValue(target, null);
				if (raw == null)
				{
					FieldInfo field = type.GetField(
						name,
						BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					raw = field == null ? null : field.GetValue(target);
				}

				IEnumerable enumerable = raw as IEnumerable;
				if (enumerable == null || raw is string)
				{
					continue;
				}
				foreach (object item in enumerable)
				{
					string value = item as string;
					if (!string.IsNullOrWhiteSpace(value))
					{
						values.Add(convertSceneGuid
							? NormalizeSceneValue(value)
							: value.Trim());
					}
				}
			}
		}

		private static void ExtractSerializedStrings(
			Object target,
			Func<string, bool> pathFilter,
			HashSet<string> values,
			bool convertSceneGuid)
		{
			SerializedObject serialized = new SerializedObject(target);
			SerializedProperty property = serialized.GetIterator();
			while (property.Next(true))
			{
				if (property.propertyType != SerializedPropertyType.String ||
					!pathFilter(property.propertyPath))
				{
					continue;
				}
				string value = property.stringValue;
				if (!string.IsNullOrWhiteSpace(value))
				{
					values.Add(convertSceneGuid
						? NormalizeSceneValue(value)
						: value.Trim());
				}
			}
		}

		private static string NormalizeSceneValue(string value)
		{
			string trimmed = value.Trim().Replace('\\', '/');
			string path = AssetDatabase.GUIDToAssetPath(trimmed);
			return string.IsNullOrWhiteSpace(path) ? trimmed : path;
		}

		private static string NormalizeAssetPath(string value)
		{
			return string.IsNullOrWhiteSpace(value)
				? string.Empty
				: value.Trim().Replace('\\', '/');
		}
	}

	public static partial class UnityGraphicsMcpInspection
	{
		private const string PHASE4D_APV_BAKE_MODE = "EXPLICIT_APV_BAKING_SET";
		private const int PHASE4D_MIN_TIMEOUT_SECONDS = 30;
		private const int PHASE4D_MAX_TIMEOUT_SECONDS = 86400;

		public static UnityGraphicsMcpToolResult PrepareApvBakePlan(
			string requestId,
			long? expectedRevision,
			UnityGraphicsMcpApvBakePlanInput input)
		{
			return ExecuteReadOnly(
				"graphics.prepare_apv_bake_plan",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.prepare_apv_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}
					if (expectedRevision.Value != UnityGraphicsMcpSession.Revision)
					{
						return CreateResult(
							"graphics.prepare_apv_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							"expectedRevisionが現在のEditor Revisionと一致しません。",
							null);
					}
					if (input == null ||
						string.IsNullOrWhiteSpace(input.bakingSetAssetPath) ||
						string.IsNullOrWhiteSpace(input.lightingScenario) ||
						input.scenePaths == null || input.scenePaths.Length == 0)
					{
						return CreateResult(
							"graphics.prepare_apv_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Baking Set、Lighting Scenario、Scene集合を明示してください。",
							null);
					}

					UnityGraphicsMcpApvEnvironment environment;
					string failureCode;
					string failureMessage;
					bool environmentResolved;
					if (UnityGraphicsMcpPhase4DApvSession.EnvironmentOverrideForTests != null)
					{
						environment = UnityGraphicsMcpPhase4DApvSession
							.EnvironmentOverrideForTests(input);
						environmentResolved = environment != null;
						failureCode = environmentResolved
							? null
							: "APV_TEST_ENVIRONMENT_UNAVAILABLE";
						failureMessage = environmentResolved
							? null
							: "APV Environmentを解決できません。";
					}
					else
					{
						environmentResolved =
							UnityGraphicsMcpApvReflectionBackend.TryInspectEnvironment(
								input,
								out environment,
								out failureCode,
								out failureMessage);
					}

					if (!environmentResolved)
					{
						UnityGraphicsMcpToolResult unsupported = CreateResult(
							"graphics.prepare_apv_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.BACKEND_NOT_IMPLEMENTED,
							failureMessage,
							new Dictionary<string, object>
							{
								{ "failureCode", failureCode },
								{ "bakingSetAssetPath", input.bakingSetAssetPath },
								{ "lightingScenario", input.lightingScenario }
							});
						unsupported.issues.Add(new UnityGraphicsMcpIssue
						{
							code = failureCode,
							message = failureMessage,
							evidence = unsupported.data
						});
						return unsupported;
					}

					List<string> explicitScenes = input.scenePaths
						.Where(value => !string.IsNullOrWhiteSpace(value))
						.Select(NormalizePhase4SceneAssetPath)
						.Distinct(StringComparer.Ordinal)
						.OrderBy(value => value, StringComparer.Ordinal)
						.ToList();
					foreach (string scenePath in explicitScenes)
					{
						Scene scene;
						if (!IsSupportedPhase4SceneAssetPath(scenePath) ||
							!TryResolvePhase4LoadedScene(scenePath, out scene))
						{
							return CreateResult(
								"graphics.prepare_apv_bake_plan",
								requestId,
								E_MCP_TOOL_STATUS.INVALID_REQUEST,
								"APV対象SceneはAssets配下のLoaded Sceneとして明示してください。",
								new Dictionary<string, object>
								{
									{ "scenePath", scenePath }
								});
						}
					}

					List<string> bakingSetScenes = environment.ScenePaths
						.Select(NormalizePhase4SceneAssetPath)
						.Distinct(StringComparer.Ordinal)
						.OrderBy(value => value, StringComparer.Ordinal)
						.ToList();
					if (!explicitScenes.SequenceEqual(bakingSetScenes, StringComparer.Ordinal))
					{
						return CreateResult(
							"graphics.prepare_apv_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"明示Scene集合がBaking SetのScene集合と完全一致しません。",
							new Dictionary<string, object>
							{
								{ "explicitScenePaths", explicitScenes },
								{ "bakingSetScenePaths", bakingSetScenes }
							});
					}

					string scenario = input.lightingScenario.Trim();
					if (!environment.LightingScenarios.Contains(scenario, StringComparer.Ordinal))
					{
						return CreateResult(
							"graphics.prepare_apv_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"Lighting ScenarioがBaking Setに存在しません。",
							new Dictionary<string, object>
							{
								{ "lightingScenario", scenario },
								{ "availableScenarios", environment.LightingScenarios }
							});
					}

					int timeoutSeconds = input.timeoutSeconds ?? 3600;
					if (timeoutSeconds < PHASE4D_MIN_TIMEOUT_SECONDS ||
						timeoutSeconds > PHASE4D_MAX_TIMEOUT_SECONDS)
					{
						return CreateResult(
							"graphics.prepare_apv_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"timeoutSecondsは30～86400で指定してください。",
							null);
					}

					List<string> outputRoots = NormalizePhase4DOutputRoots(
						input.outputAssetRoots,
						environment.BakingSetAssetPath);
					if (outputRoots.Count == 0)
					{
						return CreateResult(
							"graphics.prepare_apv_bake_plan",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"APV Output Asset RootをAssets配下で一つ以上指定してください。",
							null);
					}

					string approvalToken = Guid.NewGuid().ToString("N") +
						Guid.NewGuid().ToString("N");
					UnityGraphicsMcpApvBakePlan plan = new UnityGraphicsMcpApvBakePlan
					{
						Revision = expectedRevision.Value,
						ApprovalTokenHash = UnityGraphicsMcpPhase4Session.HashText(approvalToken),
						BakingSetAssetPath = environment.BakingSetAssetPath,
						BakingSetDigest = environment.BakingSetDigest,
						LightingScenario = scenario,
						ScenePaths = explicitScenes,
						OutputAssetRoots = outputRoots,
						TimeoutSeconds = timeoutSeconds,
						PipelineKind = environment.PipelineKind,
						PipelineAssetType = environment.PipelineAssetType,
						BackendType = environment.BackendType,
						BakeMethod = environment.BakeMethod,
						RunningProperty = environment.RunningProperty,
						CancelMethod = environment.CancelMethod,
						NativeCancellationSupported = environment.NativeCancellationSupported
					};
					plan.DiffDigest = BuildPhase4DApvPlanDigest(plan);
					UnityGraphicsMcpPhase4DApvSession.StorePlan(plan);

					return CreateResult(
						"graphics.prepare_apv_bake_plan",
						requestId,
						E_MCP_TOOL_STATUS.SUCCESS,
						"APV Baking Set、Lighting Scenario、Scene集合、Backend Capabilityを固定しました。",
						new Dictionary<string, object>
						{
							{ "planId", plan.PlanId },
							{ "approvalToken", approvalToken },
							{ "approvalTokenExpiresUtc", plan.ExpiresUtc.ToString("O", CultureInfo.InvariantCulture) },
							{ "expectedRevision", plan.Revision },
							{ "diffDigest", plan.DiffDigest },
							{ "bakeMode", PHASE4D_APV_BAKE_MODE },
							{ "pipelineKind", plan.PipelineKind },
							{ "pipelineAssetType", plan.PipelineAssetType },
							{ "bakingSetAssetPath", plan.BakingSetAssetPath },
							{ "lightingScenario", plan.LightingScenario },
							{ "scenePaths", plan.ScenePaths },
							{ "outputAssetRoots", plan.OutputAssetRoots },
							{ "backendType", plan.BackendType },
							{ "bakeMethod", plan.BakeMethod },
							{ "cancellationContract", new Dictionary<string, object>
								{
									{ "mode", plan.NativeCancellationSupported ? "NATIVE_PLUS_COOPERATIVE" : "COOPERATIVE_POLLING" },
									{ "nativeCancellationSupported", plan.NativeCancellationSupported },
									{ "pollTool", "graphics.get_apv_bake_status" },
									{ "cancelTool", "graphics.cancel_apv_bake" }
								}
							},
							{ "bakePerformed", false },
							{ "savePerformed", false }
						});
				});
		}

		public static UnityGraphicsMcpToolResult StartApvBake(
			string requestId,
			string planId,
			long? expectedRevision,
			string approvalToken,
			string bakeMode)
		{
			return ExecutePhase4PersistentOperation(
				"graphics.start_apv_bake",
				requestId,
				delegate
				{
					if (!expectedRevision.HasValue)
					{
						return CreateResult(
							"graphics.start_apv_bake",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							"expectedRevisionを指定してください。",
							null);
					}
					if (!string.Equals(
						bakeMode == null ? string.Empty : bakeMode.Trim(),
						PHASE4D_APV_BAKE_MODE,
						StringComparison.OrdinalIgnoreCase))
					{
						return CreateResult(
							"graphics.start_apv_bake",
							requestId,
							E_MCP_TOOL_STATUS.UNSUPPORTED,
							"bakeModeはEXPLICIT_APV_BAKING_SETだけです。",
							null);
					}

					UnityGraphicsMcpApvBakePlan plan;
					E_MCP_TOOL_STATUS failureStatus;
					string failureMessage;
					if (!UnityGraphicsMcpPhase4DApvSession.TryGetPlan(
						planId,
						expectedRevision.Value,
						approvalToken,
						out plan,
						out failureStatus,
						out failureMessage))
					{
						return CreateResult(
							"graphics.start_apv_bake",
							requestId,
							failureStatus,
							failureMessage,
							null);
					}

					if (!ValidatePhase4DApvPlan(plan, out failureMessage))
					{
						return CreateResult(
							"graphics.start_apv_bake",
							requestId,
							E_MCP_TOOL_STATUS.STALE_SNAPSHOT,
							failureMessage,
							null);
					}

					UnityGraphicsMcpApvBakeJob job =
						UnityGraphicsMcpPhase4DApvSession.StartJob(
							plan,
							out failureMessage);
					return BuildPhase4DApvJobResult(
						"graphics.start_apv_bake",
						requestId,
						job,
						failureMessage);
				});
		}

		public static UnityGraphicsMcpToolResult GetApvBakeStatus(
			string requestId,
			string jobId)
		{
			return ExecuteReadOnly(
				"graphics.get_apv_bake_status",
				requestId,
				delegate
				{
					UnityGraphicsMcpApvBakeJob job;
					if (!UnityGraphicsMcpPhase4DApvSession.TryGetJob(jobId, out job))
					{
						return CreateResult(
							"graphics.get_apv_bake_status",
							requestId,
							E_MCP_TOOL_STATUS.SESSION_EXPIRED,
							"APV Bake Jobが現在のEditor Sessionに存在しません。",
							null);
					}
					return BuildPhase4DApvJobResult(
						"graphics.get_apv_bake_status",
						requestId,
						job,
						job.FailureMessage);
				});
		}

		public static UnityGraphicsMcpToolResult CancelApvBake(
			string requestId,
			string jobId)
		{
			return ExecuteReadOnly(
				"graphics.cancel_apv_bake",
				requestId,
				delegate
				{
					UnityGraphicsMcpApvBakeJob job;
					if (!UnityGraphicsMcpPhase4DApvSession.TryGetJob(jobId, out job))
					{
						return CreateResult(
							"graphics.cancel_apv_bake",
							requestId,
							E_MCP_TOOL_STATUS.SESSION_EXPIRED,
							"APV Bake Jobが現在のEditor Sessionに存在しません。",
							null);
					}

					string failureMessage;
					if (!UnityGraphicsMcpPhase4DApvSession.RequestCancellation(
						job,
						out failureMessage))
					{
						return CreateResult(
							"graphics.cancel_apv_bake",
							requestId,
							E_MCP_TOOL_STATUS.INVALID_REQUEST,
							failureMessage,
							BuildPhase4DApvJobData(job));
					}

					return BuildPhase4DApvJobResult(
						"graphics.cancel_apv_bake",
						requestId,
						job,
						"APV BakeへCancellationを要求しました。");
				});
		}

		private static UnityGraphicsMcpToolResult BuildPhase4DApvJobResult(
			string toolName,
			string requestId,
			UnityGraphicsMcpApvBakeJob job,
			string message)
		{
			E_MCP_TOOL_STATUS status;
			if (job.Status == E_GRAPHICS_APV_JOB_STATUS.SUCCEEDED.ToString())
			{
				status = E_MCP_TOOL_STATUS.SUCCESS;
			}
			else if (job.Status == E_GRAPHICS_APV_JOB_STATUS.FAILED.ToString())
			{
				status = E_MCP_TOOL_STATUS.FAILED;
			}
			else
			{
				status = E_MCP_TOOL_STATUS.PARTIAL;
			}

			UnityGraphicsMcpToolResult result = CreateResult(
				toolName,
				requestId,
				status,
				string.IsNullOrWhiteSpace(message)
					? "APV Bake Job状態を取得しました。"
					: message,
				BuildPhase4DApvJobData(job));
			if (!string.IsNullOrWhiteSpace(job.FailureCode))
			{
				result.issues.Add(new UnityGraphicsMcpIssue
				{
					code = job.FailureCode,
					message = job.FailureMessage,
					evidence = new Dictionary<string, object>
					{
						{ "jobId", job.JobId },
						{ "jobStatus", job.Status },
						{ "outputDiffCount", job.OutputDiff.Count }
					}
				});
			}
			return result;
		}

		private static Dictionary<string, object> BuildPhase4DApvJobData(
			UnityGraphicsMcpApvBakeJob job)
		{
			return new Dictionary<string, object>
			{
				{ "jobId", job.JobId },
				{ "planId", job.PlanId },
				{ "jobStatus", job.Status },
				{ "startedUtc", job.StartedUtc.ToString("O", CultureInfo.InvariantCulture) },
				{ "completedUtc", job.CompletedUtc.HasValue ? job.CompletedUtc.Value.ToString("O", CultureInfo.InvariantCulture) : null },
				{ "startRevision", job.StartRevision },
				{ "currentRevision", UnityGraphicsMcpSession.Revision },
				{ "bakingSetAssetPath", job.Plan.BakingSetAssetPath },
				{ "lightingScenario", job.Plan.LightingScenario },
				{ "scenePaths", job.Plan.ScenePaths },
				{ "completedStages", job.CompletedStages },
				{ "outputDiff", job.OutputDiff },
				{ "failureCode", job.FailureCode },
				{ "failureMessage", job.FailureMessage },
				{ "cancellationRequested", job.CancellationRequested },
				{ "cancellationInvoked", job.CancellationInvoked },
				{ "nativeCancellationSupported", job.Plan.NativeCancellationSupported },
				{ "bakePerformed", job.BackendStarted },
				{ "partialResult", job.Status == E_GRAPHICS_APV_JOB_STATUS.PARTIAL.ToString() || job.Status == E_GRAPHICS_APV_JOB_STATUS.CANCELLED.ToString() },
				{ "savePerformed", false },
				{ "automaticRollback", false }
			};
		}

		private static bool ValidatePhase4DApvPlan(
			UnityGraphicsMcpApvBakePlan plan,
			out string failureMessage)
		{
			failureMessage = null;
			if (UnityGraphicsMcpPhase4DApvSession.EnvironmentOverrideForTests == null)
			{
				Object asset = AssetDatabase.LoadMainAssetAtPath(plan.BakingSetAssetPath);
				if (asset == null)
				{
					failureMessage = "APV Baking Set Assetを再解決できません。";
					return false;
				}
				string digest = UnityGraphicsMcpApvReflectionBackend.BuildAssetDigest(
					plan.BakingSetAssetPath,
					asset);
				if (!string.Equals(plan.BakingSetDigest, digest, StringComparison.Ordinal))
				{
					failureMessage = "APV Baking SetがPrepare後に変更されました。";
					return false;
				}
			}

			foreach (string scenePath in plan.ScenePaths)
			{
				Scene scene;
				if (!TryResolvePhase4LoadedScene(scenePath, out scene))
				{
					failureMessage = "APV対象Scene集合がPrepare後に変更されました。";
					return false;
				}
			}
			return true;
		}

		private static List<string> NormalizePhase4DOutputRoots(
			string[] roots,
			string bakingSetAssetPath)
		{
			List<string> normalized = roots == null
				? new List<string>()
				: roots
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Select(value => value.Trim().Replace('\\', '/').TrimEnd('/'))
					.Where(value => value == "Assets" || value.StartsWith("Assets/", StringComparison.Ordinal))
					.Distinct(StringComparer.Ordinal)
					.ToList();
			if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(bakingSetAssetPath))
			{
				string folder = Path.GetDirectoryName(bakingSetAssetPath);
				if (!string.IsNullOrWhiteSpace(folder))
				{
					normalized.Add(folder.Replace('\\', '/'));
				}
			}
			return normalized.OrderBy(value => value, StringComparer.Ordinal).ToList();
		}

		private static string BuildPhase4DApvPlanDigest(
			UnityGraphicsMcpApvBakePlan plan)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(plan.Revision).Append('|');
			builder.Append(plan.BakingSetAssetPath).Append('|');
			builder.Append(plan.BakingSetDigest).Append('|');
			builder.Append(plan.LightingScenario).Append('|');
			builder.Append(plan.PipelineKind).Append('|');
			builder.Append(plan.PipelineAssetType).Append('|');
			builder.Append(plan.BackendType).Append('|');
			builder.Append(plan.BakeMethod).Append('|');
			builder.Append(plan.RunningProperty).Append('|');
			builder.Append(plan.CancelMethod).Append('|');
			builder.Append(plan.TimeoutSeconds).Append('|');
			foreach (string scenePath in plan.ScenePaths)
			{
				builder.Append("S:").Append(scenePath).Append('|');
			}
			foreach (string root in plan.OutputAssetRoots)
			{
				builder.Append("O:").Append(root).Append('|');
			}
			return UnityGraphicsMcpPhase4Session.HashText(builder.ToString());
		}
	}
}

#endif
