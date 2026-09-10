#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityAgentMcp
{
	internal sealed class AgentExecutionHistoryStore
	{
		private const string HISTORY_PATH = "Library/MyUnityMCP/AgentExecution/history.jsonl";
		private readonly List<JObject> _entries = new List<JObject>();

		internal static string StorageRootOverrideForTests { get; set; }
		internal string LastDiagnosticCode { get; private set; }
		internal string HistoryPath => ResolvePath();

		internal void Load()
		{
			_entries.Clear();
			LastDiagnosticCode = null;
			if (!File.Exists(ResolvePath()))
			{
				return;
			}

			List<JObject> migratedEntries = new List<JObject>();
			bool requiresRewrite = false;
			try
			{
				foreach (string line in File.ReadLines(ResolvePath()))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}
					JObject legacy = JObject.Parse(line);
					JObject sanitized = Sanitize(legacy);
					if (!JToken.DeepEquals(legacy, sanitized))
					{
						requiresRewrite = true;
					}
					migratedEntries.Add(sanitized);
				}
			}
			catch (Exception)
			{
				LastDiagnosticCode = "AGENT-HISTORY-PERSISTENCE-FAILED";
				_entries.Clear();
				return;
			}

			_entries.Clear();
			_entries.AddRange(migratedEntries);
			if (requiresRewrite && !TryAtomicRewrite())
			{
				LastDiagnosticCode = "AGENT-HISTORY-PERSISTENCE-FAILED";
			}
		}

		internal bool TryAppend(UnityAgentMcpExecutionRecord execution)
		{
			JObject sanitized = BuildProjection(execution);
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ResolvePath()));
				File.AppendAllText(ResolvePath(), sanitized.ToString(Formatting.None) + Environment.NewLine);
				_entries.Add(sanitized);
				return true;
			}
			catch (Exception)
			{
				LastDiagnosticCode = "AGENT-HISTORY-PERSISTENCE-FAILED";
				return false;
			}
		}

		internal JArray GetItems(int maxItems)
		{
			int count = Math.Max(1, Math.Min(maxItems, 100));
			JArray result = new JArray();
			int start = Math.Max(0, _entries.Count - count);
			for (int index = start; index < _entries.Count; index++)
			{
				result.Add(_entries[index].DeepClone());
			}
			return result;
		}

		internal int Count => _entries.Count;

		internal void ResetForTests()
		{
			_entries.Clear();
			LastDiagnosticCode = null;
		}

		internal static JObject BuildProjection(UnityAgentMcpExecutionRecord execution)
		{
			JArray summaries = new JArray();
			if (execution?.stepSummaries != null)
			{
				foreach (AgentExecutionStepSummary summary in execution.stepSummaries)
				{
					summaries.Add(new JObject
					{
						["stepId"] = summary.stepId,
						["domainId"] = summary.domainId,
						["toolName"] = summary.toolName,
						["resultCode"] = summary.resultCode,
						["durationMs"] = summary.durationMs
					});
				}
			}
			return new JObject
			{
				["schemaVersion"] = 1,
				["executionId"] = execution?.executionId,
				["graphId"] = execution?.graphId,
				["status"] = execution?.status.ToString(),
				["startedAtUtc"] = execution?.startedAtUtc == default ? null : execution.startedAtUtc.ToString("O"),
				["completedAtUtc"] = execution?.completedAtUtc == default ? null : execution.completedAtUtc.ToString("O"),
				["timeoutSeconds"] = execution?.timeoutSeconds ?? 0,
				["completedStepCount"] = execution?.stepSummaries?.Count ?? 0,
				["totalStepCount"] = execution?.orderedSteps?.Count ?? 0,
				["expectedRevision"] = execution?.expectedRevision ?? 0,
				["errorCode"] = execution?.errorCode,
				["stepSummaries"] = summaries
			};
		}

		private static JObject Sanitize(JObject legacy)
		{
			return new JObject
			{
				["schemaVersion"] = 1,
				["executionId"] = StringValue(legacy, "executionId"),
				["graphId"] = StringValue(legacy, "graphId"),
				["status"] = StringValue(legacy, "status"),
				["startedAtUtc"] = StringValue(legacy, "startedAtUtc"),
				["completedAtUtc"] = StringValue(legacy, "completedAtUtc"),
				["timeoutSeconds"] = IntegerValue(legacy, "timeoutSeconds"),
				["completedStepCount"] = IntegerValue(legacy, "completedStepCount"),
				["totalStepCount"] = IntegerValue(legacy, "totalStepCount"),
				["expectedRevision"] = IntegerValue(legacy, "expectedRevision"),
				["errorCode"] = StringValue(legacy, "errorCode"),
				["stepSummaries"] = SanitizeSummaries(legacy["stepSummaries"])
			};
		}

		private static JArray SanitizeSummaries(JToken token)
		{
			JArray result = new JArray();
			if (!(token is JArray summaries))
			{
				return result;
			}
			foreach (JToken value in summaries)
			{
				if (!(value is JObject summary))
				{
					continue;
				}
				result.Add(new JObject
				{
					["stepId"] = StringValue(summary, "stepId"),
					["domainId"] = StringValue(summary, "domainId"),
					["toolName"] = StringValue(summary, "toolName"),
					["resultCode"] = StringValue(summary, "resultCode"),
					["durationMs"] = NumberValue(summary, "durationMs")
				});
			}
			return result;
		}

		private bool TryAtomicRewrite()
		{
			string path = ResolvePath();
			string directory = Path.GetDirectoryName(path);
			string temporary = Path.Combine(directory, Path.GetFileName(path) + ".tmp-" + Guid.NewGuid().ToString("N"));
			try
			{
				Directory.CreateDirectory(directory);
				using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			using (StreamWriter writer = new StreamWriter(stream))
			{
				foreach (JObject entry in _entries)
				{
					writer.WriteLine(entry.ToString(Formatting.None));
				}
				writer.Flush();
				stream.Flush(true);
			}
			if (File.Exists(path))
			{
				File.Replace(temporary, path, null);
			}
			else
			{
				File.Move(temporary, path);
			}
			return true;
			}
			catch (Exception)
			{
				if (File.Exists(temporary))
				{
					File.Delete(temporary);
				}
				return false;
			}
		}

		private static string ResolvePath()
		{
			return string.IsNullOrEmpty(StorageRootOverrideForTests)
				? HISTORY_PATH
				: Path.Combine(StorageRootOverrideForTests, "history.jsonl");
		}

		private static JToken StringValue(JObject value, string field)
		{
			return value?[field]?.Type == JTokenType.String ? value[field] : JValue.CreateNull();
		}

		private static JToken IntegerValue(JObject value, string field)
		{
			return value?[field]?.Type == JTokenType.Integer ? value[field] : new JValue(0);
		}

		private static JToken NumberValue(JObject value, string field)
		{
			return value?[field]?.Type == JTokenType.Float || value?[field]?.Type == JTokenType.Integer
				? value[field]
				: new JValue(0.0);
		}
	}
}

#endif
