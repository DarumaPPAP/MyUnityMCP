#if UNITY_EDITOR

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityAgentMcp
{
	internal sealed class AgentExecutionTraceStore
	{
		private const string TRACE_PATH = "Library/MyUnityMCP/AgentExecution/trace.jsonl";

		internal static string StorageRootOverrideForTests { get; set; }
		internal string LastDiagnosticCode { get; private set; }
		internal string TracePath => ResolvePath();

		internal bool TryAppend(AgentTraceEvent traceEvent)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ResolvePath()));
				JObject payload = new JObject
				{
					["schemaVersion"] = 1,
					["timestampUtc"] = traceEvent.timestampUtc.ToString("O"),
					["executionId"] = traceEvent.executionId,
					["graphId"] = traceEvent.graphId,
					["stepId"] = traceEvent.stepId,
					["domainId"] = traceEvent.domainId,
					["toolName"] = traceEvent.toolName,
					["event"] = traceEvent.eventName,
					["revision"] = traceEvent.revision,
					["resultCode"] = traceEvent.resultCode,
					["durationMs"] = traceEvent.durationMs
				};
				File.AppendAllText(ResolvePath(), payload.ToString(Formatting.None) + Environment.NewLine);
				return true;
			}
			catch (Exception)
			{
				LastDiagnosticCode = "AGENT-TRACE-PERSISTENCE-FAILED";
				return false;
			}
		}

		internal void ResetForTests()
		{
			LastDiagnosticCode = null;
		}

		private static string ResolvePath()
		{
			return string.IsNullOrEmpty(StorageRootOverrideForTests)
				? TRACE_PATH
				: Path.Combine(StorageRootOverrideForTests, "trace.jsonl");
		}
	}
}

#endif
