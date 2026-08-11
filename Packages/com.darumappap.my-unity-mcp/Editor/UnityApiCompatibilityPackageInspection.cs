#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Unity本体Versionだけでは判断できないMigration対象PackageのVersionをRead-onlyで収集します。
	/// </summary>
	public static class UnityApiCompatibilityPackageInspection
	{
		private static readonly HashSet<string> RELEVANT_PACKAGES = new HashSet<string>
		{
			"com.unity.entities",
			"com.unity.entities.graphics",
			"com.unity.netcode",
			"com.unity.inputsystem",
			"com.unity.xr.management",
			"com.unity.xr.openxr",
			"com.unity.xr.oculus",
			"com.unity.xr.arcore",
			"com.unity.xr.arkit",
			"com.unity.render-pipelines.core",
			"com.unity.render-pipelines.universal",
			"com.unity.render-pipelines.high-definition"
		};

		public static List<Dictionary<string, object>> Inspect()
		{
			PackageInfo[] registeredPackages = PackageInfo.GetAllRegisteredPackages();
			if (registeredPackages == null)
			{
				return new List<Dictionary<string, object>>();
			}

			return registeredPackages
				.Where(item => item != null && RELEVANT_PACKAGES.Contains(item.name))
				.OrderBy(item => item.name)
				.Select(item => new Dictionary<string, object>
				{
					{ "name", item.name },
					{ "version", item.version },
					{ "source", item.source.ToString() },
					{ "compatibilityDecisionInput", true }
				})
				.ToList();
		}
	}
}

#endif