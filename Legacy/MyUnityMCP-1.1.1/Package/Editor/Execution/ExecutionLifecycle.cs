#if UNITY_EDITOR

namespace UnityGraphicsMcp
{
	/// <summary>
	/// MCP Transportまたは長時間BackendがExecution Runtimeへ状態を通知する公開Adapterです。
	/// Scene、Asset、Undoへ触れません。
	/// </summary>
	public static class ExecutionLifecycle
	{
		public static void NotifyClientDisconnected(string clientId)
		{
			ExecutionHardening.NotifyClientDisconnected(clientId);
		}

		public static bool ReportProgress(
			string executionId,
			double progress,
			string stage,
			string message)
		{
			return ExecutionHardening.ReportProgress(
				executionId,
				progress,
				stage,
				message);
		}

		public static bool IsCancellationRequested(string executionId)
		{
			return ExecutionHardening.IsCancellationRequested(
				executionId);
		}

		public static void ThrowIfCancellationRequested(string executionId)
		{
			ExecutionHardening.ThrowIfCancellationRequested(
				executionId);
		}
	}
}

#endif