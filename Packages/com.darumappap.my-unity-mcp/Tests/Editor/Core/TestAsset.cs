#if UNITY_EDITOR

using UnityEngine;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Persistent AssetのDirty状態を検証するためのEditMode Test専用Assetです。
	/// </summary>
	public sealed class TestAsset : ScriptableObject
	{
		public int value;
	}
}

#endif
