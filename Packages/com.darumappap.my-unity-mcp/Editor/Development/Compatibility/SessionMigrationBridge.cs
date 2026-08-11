#if UNITY_EDITOR

using System;

namespace UnityGraphicsMcp
{
	/// <summary>
	/// Graph Engineering migration-only bridge for pre-modernization candidate sources.
	/// Production code must use Session directly. This bridge blocks release_candidate promotion
	/// and must be deleted after Candidate BASE modernization replaces all old references.
	/// </summary>
	[Obsolete("Graph Engineering migration bridge only. Replace with Session before promotion.")]
	internal static class UnityGraphicsMcpSession
	{
		public static long Revision => Session.Revision;

		public static void NotifyMutationApplied()
		{
			Session.NotifyMutationApplied();
		}
	}
}

#endif
