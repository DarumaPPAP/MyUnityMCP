#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Unity 6世代で変化するScene/Objectの内部ID表現をMyUnityMCPから隔離します。
	/// Scene raw Handleは比較専用、Session Tokenは既存のintベース内部Transaction互換専用です。
	/// 永続識別にはGlobalObjectIdを使用し、Session Tokenを保存Assetや別Sessionへ持ち越しません。
	/// </summary>
	internal static class UnityGraphicsMcpIdentityCompatibility
	{
		private static readonly Dictionary<ulong, int> _sceneTokensByRawHandle =
			new Dictionary<ulong, int>();
		private static readonly Dictionary<int, ulong> _sceneRawHandlesByToken =
			new Dictionary<int, ulong>();
		private static readonly Dictionary<Object, int> _objectTokens =
			new Dictionary<Object, int>();
		private static readonly Dictionary<int, Object> _objectsByToken =
			new Dictionary<int, Object>();

		private static int _nextSceneToken = 1;
		private static int _nextObjectToken = 1;

		public static ulong GetSceneHandle(Scene scene)
		{
#if UNITY_6000_4_OR_NEWER
			return scene.handle.GetRawData();
#else
			return unchecked((ulong)(uint)scene.handle);
#endif
		}

		public static int GetSceneToken(Scene scene)
		{
			ulong rawHandle = GetSceneHandle(scene);
			int token;
			if (_sceneTokensByRawHandle.TryGetValue(rawHandle, out token))
			{
				return token;
			}

			token = _nextSceneToken++;
			_sceneTokensByRawHandle[rawHandle] = token;
			_sceneRawHandlesByToken[token] = rawHandle;
			return token;
		}

		public static bool MatchesSceneToken(Scene scene, int token)
		{
			ulong expectedRawHandle;
			return _sceneRawHandlesByToken.TryGetValue(token, out expectedRawHandle) &&
				expectedRawHandle == GetSceneHandle(scene);
		}

		public static bool TryResolveSceneToken(int token, out Scene scene)
		{
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene candidate = SceneManager.GetSceneAt(index);
				if (candidate.IsValid() &&
					candidate.isLoaded &&
					MatchesSceneToken(candidate, token))
				{
					scene = candidate;
					return true;
				}
			}

			scene = default(Scene);
			return false;
		}

		public static int GetObjectToken(Object target)
		{
			if (target == null)
			{
				return 0;
			}

			int token;
			if (_objectTokens.TryGetValue(target, out token))
			{
				return token;
			}

			token = _nextObjectToken++;
			_objectTokens[target] = token;
			_objectsByToken[token] = target;
			return token;
		}

		public static Object ResolveObjectToken(int token)
		{
			Object target;
			if (!_objectsByToken.TryGetValue(token, out target) || target == null)
			{
				return null;
			}

			return target;
		}
	}
}

#endif
