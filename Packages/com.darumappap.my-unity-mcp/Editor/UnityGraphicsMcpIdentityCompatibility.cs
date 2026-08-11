#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Unity 6世代で64bit化されるScene/Objectの一時Handleを、intへ戻さず共通形式へ正規化します。
	/// 永続識別には使用せず、同一Editor Session内の比較・並び順・Dirty監視だけに使用します。
	/// </summary>
	internal static class UnityGraphicsMcpIdentityCompatibility
	{
		public static ulong GetSceneHandle(Scene scene)
		{
#if UNITY_6000_7_OR_NEWER
			return scene.handle.GetRawData();
#else
			return unchecked((ulong)(uint)scene.handle);
#endif
		}

		public static ulong GetObjectHandle(Object target)
		{
			if (target == null)
			{
				return 0UL;
			}

#if UNITY_6000_4_OR_NEWER
			return target.GetEntityId().ToULong();
#else
			return unchecked((ulong)(uint)target.GetInstanceID());
#endif
		}
	}
}

#endif
