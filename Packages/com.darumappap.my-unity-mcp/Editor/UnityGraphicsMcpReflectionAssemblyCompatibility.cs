#if UNITY_EDITOR

using System;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// UnityEditor.Compilation.AssemblyとSystem.Reflection.Assemblyの名前衝突を、
	/// APV Reflection探索に必要な最小APIだけへ限定して解消します。
	/// </summary>
	internal readonly struct Assembly
	{
		private readonly System.Reflection.Assembly _value;

		private Assembly(System.Reflection.Assembly value)
		{
			_value = value;
		}

		public Type GetType(string name, bool throwOnError)
		{
			return _value == null ? null : _value.GetType(name, throwOnError);
		}

		public static implicit operator Assembly(System.Reflection.Assembly value)
		{
			return new Assembly(value);
		}
	}
}

#endif
