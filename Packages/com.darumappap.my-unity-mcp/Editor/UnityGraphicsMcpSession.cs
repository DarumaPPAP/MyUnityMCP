#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Unity Editor Session内のRevision、Snapshot、Plan、Read-only検証を所有します。
	/// </summary>
	public static class UnityGraphicsMcpSession
	{
		private const int MAX_SNAPSHOT_COUNT = 8;
		private const int MAX_PLAN_COUNT = 8;
		private static readonly TimeSpan SNAPSHOT_LIFETIME = TimeSpan.FromMinutes(10.0);
		private static readonly TimeSpan PLAN_LIFETIME = TimeSpan.FromMinutes(10.0);
		private static readonly Dictionary<string, UnityGraphicsMcpSceneSnapshot> _snapshots =
			new Dictionary<string, UnityGraphicsMcpSceneSnapshot>();
		private static readonly Dictionary<string, UnityGraphicsMcpDirectionPlan> _plans =
			new Dictionary<string, UnityGraphicsMcpDirectionPlan>();

		private static readonly int _mainThreadId = Thread.CurrentThread.ManagedThreadId;
		private static readonly string _sessionId = Guid.NewGuid().ToString("N");
		private static long _revision = 1;
		private static bool _isReloading;

		public static string SessionId => _sessionId;
		public static long Revision => _revision;
		public static bool IsReloading => _isReloading;
		public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

		static UnityGraphicsMcpSession()
		{
			EditorApplication.hierarchyChanged += IncrementRevision;
			EditorApplication.projectChanged += IncrementRevision;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			Undo.undoRedoPerformed += IncrementRevision;
			EditorSceneManager.sceneOpened += OnSceneOpened;
			EditorSceneManager.sceneClosed += OnSceneClosed;
			EditorSceneManager.sceneSaved += OnSceneSaved;
			EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
			CompilationPipeline.compilationStarted += OnCompilationStarted;
			CompilationPipeline.compilationFinished += OnCompilationFinished;
			EditorApplication.quitting += OnEditorQuitting;
		}

		public static UnityGraphicsMcpReadOnlyGuard BeginReadOnlyGuard()
		{
			return new UnityGraphicsMcpReadOnlyGuard(
				CaptureSceneDirtyState(),
				CaptureAssetDirtyState(),
				Undo.GetCurrentGroup());
		}

		public static string StoreSnapshot(UnityGraphicsMcpSceneSnapshot snapshot)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}

			RemoveExpiredSnapshots();
			RemoveOldestSnapshotsWhenFull();

			string snapshotId = _sessionId + ":scene:" + Guid.NewGuid().ToString("N");
			snapshot.SnapshotId = snapshotId;
			_snapshots[snapshotId] = snapshot;
			return snapshotId;
		}

		public static bool TryGetSnapshot(
			string snapshotId,
			out UnityGraphicsMcpSceneSnapshot snapshot,
			out E_MCP_TOOL_STATUS failureStatus)
		{
			snapshot = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;

			if (string.IsNullOrWhiteSpace(snapshotId))
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				return false;
			}

			if (!snapshotId.StartsWith(_sessionId + ":", StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				return false;
			}

			RemoveExpiredSnapshots();

			if (!_snapshots.TryGetValue(snapshotId, out snapshot))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				return false;
			}

			if (snapshot.Revision != _revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				return false;
			}

			return true;
		}

		public static string StorePlan(UnityGraphicsMcpDirectionPlan plan)
		{
			if (plan == null)
			{
				throw new ArgumentNullException(nameof(plan));
			}

			RemoveExpiredPlans();
			RemoveOldestPlansWhenFull();

			string planId = _sessionId + ":plan:" + Guid.NewGuid().ToString("N");
			plan.PlanId = planId;
			_plans[planId] = plan;
			return planId;
		}

		public static bool TryGetPlan(
			string planId,
			long expectedRevision,
			out UnityGraphicsMcpDirectionPlan plan,
			out E_MCP_TOOL_STATUS failureStatus)
		{
			plan = null;
			failureStatus = E_MCP_TOOL_STATUS.SUCCESS;

			if (string.IsNullOrWhiteSpace(planId))
			{
				failureStatus = E_MCP_TOOL_STATUS.INVALID_REQUEST;
				return false;
			}

			if (!planId.StartsWith(_sessionId + ":plan:", StringComparison.Ordinal))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				return false;
			}

			RemoveExpiredPlans();

			if (!_plans.TryGetValue(planId, out plan))
			{
				failureStatus = E_MCP_TOOL_STATUS.SESSION_EXPIRED;
				return false;
			}

			if (expectedRevision != _revision || plan.Revision != _revision)
			{
				failureStatus = E_MCP_TOOL_STATUS.STALE_SNAPSHOT;
				return false;
			}

			return true;
		}

		public static void ClearSnapshots()
		{
			_snapshots.Clear();
		}

		public static void ClearPlans()
		{
			_plans.Clear();
		}

		private static Dictionary<int, bool> CaptureSceneDirtyState()
		{
			Dictionary<int, bool> states = new Dictionary<int, bool>();

			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (scene.IsValid())
				{
					states[scene.handle] = scene.isDirty;
				}
			}

			return states;
		}

		private static Dictionary<int, bool> CaptureAssetDirtyState()
		{
			Dictionary<int, bool> states = new Dictionary<int, bool>();
			Object[] loadedObjects = Resources.FindObjectsOfTypeAll<Object>();

			foreach (Object target in loadedObjects)
			{
				if (target == null || !EditorUtility.IsPersistent(target))
				{
					continue;
				}

				states[target.GetInstanceID()] = EditorUtility.IsDirty(target);
			}

			return states;
		}

		private static void RemoveExpiredSnapshots()
		{
			DateTime threshold = DateTime.UtcNow - SNAPSHOT_LIFETIME;
			List<string> expiredIds = null;

			foreach (KeyValuePair<string, UnityGraphicsMcpSceneSnapshot> pair in _snapshots)
			{
				if (pair.Value.CreatedUtc >= threshold)
				{
					continue;
				}

				if (expiredIds == null)
				{
					expiredIds = new List<string>();
				}

				expiredIds.Add(pair.Key);
			}

			if (expiredIds == null)
			{
				return;
			}

			foreach (string expiredId in expiredIds)
			{
				_snapshots.Remove(expiredId);
			}
		}

		private static void RemoveExpiredPlans()
		{
			DateTime threshold = DateTime.UtcNow - PLAN_LIFETIME;
			List<string> expiredIds = null;

			foreach (KeyValuePair<string, UnityGraphicsMcpDirectionPlan> pair in _plans)
			{
				if (pair.Value.CreatedUtc >= threshold)
				{
					continue;
				}

				if (expiredIds == null)
				{
					expiredIds = new List<string>();
				}

				expiredIds.Add(pair.Key);
			}

			if (expiredIds == null)
			{
				return;
			}

			foreach (string expiredId in expiredIds)
			{
				_plans.Remove(expiredId);
			}
		}

		private static void RemoveOldestSnapshotsWhenFull()
		{
			while (_snapshots.Count >= MAX_SNAPSHOT_COUNT)
			{
				string oldestId = null;
				DateTime oldestTime = DateTime.MaxValue;

				foreach (KeyValuePair<string, UnityGraphicsMcpSceneSnapshot> pair in _snapshots)
				{
					if (pair.Value.CreatedUtc < oldestTime)
					{
						oldestTime = pair.Value.CreatedUtc;
						oldestId = pair.Key;
					}
				}

				if (string.IsNullOrEmpty(oldestId))
				{
					break;
				}

				_snapshots.Remove(oldestId);
			}
		}

		private static void RemoveOldestPlansWhenFull()
		{
			while (_plans.Count >= MAX_PLAN_COUNT)
			{
				string oldestId = null;
				DateTime oldestTime = DateTime.MaxValue;

				foreach (KeyValuePair<string, UnityGraphicsMcpDirectionPlan> pair in _plans)
				{
					if (pair.Value.CreatedUtc < oldestTime)
					{
						oldestTime = pair.Value.CreatedUtc;
						oldestId = pair.Key;
					}
				}

				if (string.IsNullOrEmpty(oldestId))
				{
					break;
				}

				_plans.Remove(oldestId);
			}
		}

		private static void ClearTransientState()
		{
			ClearSnapshots();
			ClearPlans();
		}

		private static void IncrementRevision()
		{
			_revision++;
			ClearTransientState();
		}

		private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
		{
			IncrementRevision();
		}

		private static void OnSceneClosed(Scene scene)
		{
			IncrementRevision();
		}

		private static void OnSceneSaved(Scene scene)
		{
			IncrementRevision();
		}

		private static void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
		{
			IncrementRevision();
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			IncrementRevision();
		}

		private static void OnCompilationStarted(object context)
		{
			_isReloading = true;
			IncrementRevision();
		}

		private static void OnCompilationFinished(object context)
		{
			_isReloading = false;
			IncrementRevision();
		}

		private static void OnBeforeAssemblyReload()
		{
			_isReloading = true;
			ClearTransientState();
		}

		private static void OnEditorQuitting()
		{
			_isReloading = true;
			ClearTransientState();
		}
	}

	public sealed class UnityGraphicsMcpReadOnlyGuard
	{
		private readonly Dictionary<int, bool> _sceneDirtyState;
		private readonly Dictionary<int, bool> _assetDirtyState;
		private readonly int _undoGroup;

		internal UnityGraphicsMcpReadOnlyGuard(
			Dictionary<int, bool> sceneDirtyState,
			Dictionary<int, bool> assetDirtyState,
			int undoGroup)
		{
			_sceneDirtyState = sceneDirtyState;
			_assetDirtyState = assetDirtyState;
			_undoGroup = undoGroup;
		}

		public bool HasViolation(out Dictionary<string, object> evidence)
		{
			evidence = new Dictionary<string, object>();
			List<Dictionary<string, object>> changedScenes =
				new List<Dictionary<string, object>>();
			List<Dictionary<string, object>> changedAssets =
				new List<Dictionary<string, object>>();

			if (_sceneDirtyState.Count != SceneManager.sceneCount)
			{
				evidence["sceneCountBefore"] = _sceneDirtyState.Count;
				evidence["sceneCountAfter"] = SceneManager.sceneCount;
			}

			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (!scene.IsValid())
				{
					continue;
				}

				bool beforeDirty;
				if (!_sceneDirtyState.TryGetValue(scene.handle, out beforeDirty) ||
					beforeDirty != scene.isDirty)
				{
					changedScenes.Add(new Dictionary<string, object>
					{
						{ "scene", scene.path },
						{ "beforeDirty", beforeDirty },
						{ "afterDirty", scene.isDirty }
					});
				}
			}

			Object[] loadedObjects = Resources.FindObjectsOfTypeAll<Object>();
			foreach (Object target in loadedObjects)
			{
				if (target == null || !EditorUtility.IsPersistent(target))
				{
					continue;
				}

				bool afterDirty = EditorUtility.IsDirty(target);
				bool beforeDirty;
				bool existedBefore =
					_assetDirtyState.TryGetValue(target.GetInstanceID(), out beforeDirty);

				if ((existedBefore && beforeDirty != afterDirty) ||
					(!existedBefore && afterDirty))
				{
					changedAssets.Add(new Dictionary<string, object>
					{
						{ "asset", target.name },
						{ "assetPath", AssetDatabase.GetAssetPath(target) },
						{ "beforeDirty", existedBefore ? (object)beforeDirty : null },
						{ "afterDirty", afterDirty }
					});
				}
			}

			int currentUndoGroup = Undo.GetCurrentGroup();

			if (changedScenes.Count > 0)
			{
				evidence["changedScenes"] = changedScenes;
			}

			if (changedAssets.Count > 0)
			{
				evidence["changedAssets"] = changedAssets;
			}

			if (currentUndoGroup != _undoGroup)
			{
				evidence["undoGroupBefore"] = _undoGroup;
				evidence["undoGroupAfter"] = currentUndoGroup;
			}

			return changedScenes.Count > 0 ||
				changedAssets.Count > 0 ||
				currentUndoGroup != _undoGroup ||
				_sceneDirtyState.Count != SceneManager.sceneCount;
		}
	}
}

#endif
