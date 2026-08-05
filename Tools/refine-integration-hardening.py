from pathlib import Path


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Expected one match in {path}, found {count}: {old[:120]!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


root = Path(__file__).resolve().parents[1]
runtime = root / "Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpExecutionHardening.cs"
inspection = root / "Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpInspection.cs"
tests = root / "Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpIntegrationHardeningTests.cs"

replace_once(
    inspection,
    'public string schemaVersion { get; set; } = "1.0";',
    'public string schemaVersion { get; set; } = "1.1";',
)

replace_once(
    runtime,
    """\t\t\tUnityGraphicsMcpExecutionRecord record;
\t\t\tlock (_sync)
\t\t\t{
\t\t\t\tif (!_active.TryGetValue(scope.ExecutionId, out record))
\t\t\t\t{
\t\t\t\t\trecord = CreateDetachedRecord(scope.ExecutionId, result);
\t\t\t\t}
\t\t\t}

\t\t\tif (!string.IsNullOrWhiteSpace(result.tool))
""",
    """\t\t\tUnityGraphicsMcpExecutionRecord record;
\t\t\tUnityGraphicsMcpExecutionRecord completedRecord = null;
\t\t\tlock (_sync)
\t\t\t{
\t\t\t\tif (!_active.TryGetValue(scope.ExecutionId, out record))
\t\t\t\t{
\t\t\t\t\tcompletedRecord = _history.LastOrDefault(item =>
\t\t\t\t\t\titem.executionId == scope.ExecutionId);
\t\t\t\t\tif (completedRecord == null)
\t\t\t\t\t{
\t\t\t\t\t\trecord = CreateDetachedRecord(scope.ExecutionId, result);
\t\t\t\t\t}
\t\t\t\t}
\t\t\t}

\t\t\tif (completedRecord != null)
\t\t\t{
\t\t\t\tscope.Stopwatch?.Stop();
\t\t\t\tresult.status = E_MCP_TOOL_STATUS.FAILED.ToString();
\t\t\t\tresult.summary = completedRecord.summary;
\t\t\t\tresult.error = BuildStructuredError(completedRecord);
\t\t\t\tresult.execution = BuildMetadata(completedRecord);
\t\t\t\tscope.Completed = true;
\t\t\t\treturn result;
\t\t\t}

\t\t\tif (!string.IsNullOrWhiteSpace(result.tool))
""",
)

replace_once(
    runtime,
    """\t\t\trecord.status = result.status;
\t\t\trecord.summary = result.summary;
\t\t\trecord.state = ResolveExecutionState(result).ToString();
\t\t\trecord.errorCode = ResolveFailureCode(result);
""",
    """\t\t\trecord.status = result.status;
\t\t\trecord.summary = result.summary;
\t\t\trecord.errorCode = ResolveFailureCode(result);
\t\t\trecord.state = ResolveExecutionState(
\t\t\t\tresult,
\t\t\t\trecord.errorCode).ToString();
""",
)

replace_once(
    runtime,
    """\t\tprivate static string ResolveFailureCode(UnityGraphicsMcpToolResult result)
\t\t{
\t\t\tif (result == null || result.IsSuccessful)
\t\t\t{
\t\t\t\treturn null;
\t\t\t}

\t\t\tDictionary<string, object> data = result.data as Dictionary<string, object>;
\t\t\tobject failureCode;
\t\t\tif (data != null &&
\t\t\t\tdata.TryGetValue(\"failureCode\", out failureCode) &&
\t\t\t\tfailureCode != null &&
\t\t\t\t!string.IsNullOrWhiteSpace(failureCode.ToString()))
\t\t\t{
\t\t\t\treturn failureCode.ToString();
\t\t\t}

\t\t\tUnityGraphicsMcpIssue issue = result.issues == null
\t\t\t\t? null
\t\t\t\t: result.issues.FirstOrDefault(item => item != null && !string.IsNullOrWhiteSpace(item.code));
\t\t\tif (issue != null)
\t\t\t{
\t\t\t\treturn issue.code;
\t\t\t}

\t\t\treturn \"MCP_\" + (result.status ?? E_MCP_TOOL_STATUS.FAILED.ToString());
\t\t}

\t\tprivate static E_MCP_EXECUTION_STATE ResolveExecutionState(
\t\t\tUnityGraphicsMcpToolResult result)
\t\t{
\t\t\tif (result == null)
\t\t\t{
\t\t\t\treturn E_MCP_EXECUTION_STATE.FAILED;
\t\t\t}
\t\t\tif (result.status == E_MCP_TOOL_STATUS.SUCCESS.ToString())
\t\t\t{
\t\t\t\treturn E_MCP_EXECUTION_STATE.SUCCEEDED;
\t\t\t}
\t\t\tif (result.status == E_MCP_TOOL_STATUS.PARTIAL.ToString())
\t\t\t{
\t\t\t\treturn E_MCP_EXECUTION_STATE.PARTIAL;
\t\t\t}
\t\t\treturn E_MCP_EXECUTION_STATE.FAILED;
\t\t}
""",
    """\t\tprivate static string ResolveFailureCode(UnityGraphicsMcpToolResult result)
\t\t{
\t\t\tif (result == null || result.IsSuccessful)
\t\t\t{
\t\t\t\treturn null;
\t\t\t}

\t\t\tDictionary<string, object> data = result.data as Dictionary<string, object>;
\t\t\tobject failureCode;
\t\t\tif (data != null &&
\t\t\t\tdata.TryGetValue(\"failureCode\", out failureCode) &&
\t\t\t\tfailureCode != null &&
\t\t\t\t!string.IsNullOrWhiteSpace(failureCode.ToString()))
\t\t\t{
\t\t\t\treturn failureCode.ToString();
\t\t\t}

\t\t\tUnityGraphicsMcpIssue issue = result.issues == null
\t\t\t\t? null
\t\t\t\t: result.issues.FirstOrDefault(item => item != null && !string.IsNullOrWhiteSpace(item.code));
\t\t\tif (issue != null)
\t\t\t{
\t\t\t\treturn issue.code;
\t\t\t}

\t\t\tstring summary = result.summary ?? string.Empty;
\t\t\tif (summary.IndexOf(\"承認Token\", StringComparison.Ordinal) >= 0 &&
\t\t\t\t(summary.IndexOf(\"一致しません\", StringComparison.Ordinal) >= 0 ||
\t\t\t\t summary.IndexOf(\"不足\", StringComparison.Ordinal) >= 0))
\t\t\t{
\t\t\t\treturn \"APPROVAL_TOKEN_MISMATCH\";
\t\t\t}
\t\t\tif (summary.IndexOf(\"有効期限切れ\", StringComparison.Ordinal) >= 0)
\t\t\t{
\t\t\t\treturn \"PLAN_EXPIRED\";
\t\t\t}
\t\t\tif (summary.IndexOf(\"Camera\", StringComparison.OrdinalIgnoreCase) >= 0 &&
\t\t\t\t(summary.IndexOf(\"存在しません\", StringComparison.Ordinal) >= 0 ||
\t\t\t\t summary.IndexOf(\"解決\", StringComparison.Ordinal) >= 0))
\t\t\t{
\t\t\t\treturn \"CAMERA_NOT_FOUND\";
\t\t\t}
\t\t\tif (result.status == E_MCP_TOOL_STATUS.UNSUPPORTED.ToString() &&
\t\t\t\tsummary.IndexOf(\"Pipeline\", StringComparison.OrdinalIgnoreCase) >= 0)
\t\t\t{
\t\t\t\treturn \"UNSUPPORTED_PIPELINE\";
\t\t\t}
\t\t\tif (summary.IndexOf(\"Artifact\", StringComparison.OrdinalIgnoreCase) >= 0 &&
\t\t\t\t(summary.IndexOf(\"不足\", StringComparison.Ordinal) >= 0 ||
\t\t\t\t summary.IndexOf(\"存在しません\", StringComparison.Ordinal) >= 0))
\t\t\t{
\t\t\t\treturn \"OUTPUT_ASSET_MISSING\";
\t\t\t}

\t\t\treturn \"MCP_\" + (result.status ?? E_MCP_TOOL_STATUS.FAILED.ToString());
\t\t}

\t\tprivate static E_MCP_EXECUTION_STATE ResolveExecutionState(
\t\t\tUnityGraphicsMcpToolResult result,
\t\t\tstring failureCode)
\t\t{
\t\t\tif (result == null)
\t\t\t{
\t\t\t\treturn E_MCP_EXECUTION_STATE.FAILED;
\t\t\t}
\t\t\tif (result.status == E_MCP_TOOL_STATUS.SUCCESS.ToString())
\t\t\t{
\t\t\t\treturn E_MCP_EXECUTION_STATE.SUCCEEDED;
\t\t\t}
\t\t\tif (result.status == E_MCP_TOOL_STATUS.PARTIAL.ToString())
\t\t\t{
\t\t\t\treturn E_MCP_EXECUTION_STATE.PARTIAL;
\t\t\t}
\t\t\tif (string.Equals(
\t\t\t\tfailureCode,
\t\t\t\t\"EXECUTION_CANCEL_REQUESTED\",
\t\t\t\tStringComparison.Ordinal))
\t\t\t{
\t\t\t\treturn E_MCP_EXECUTION_STATE.CANCELLED;
\t\t\t}
\t\t\treturn E_MCP_EXECUTION_STATE.FAILED;
\t\t}
""",
)

replace_once(
    runtime,
    """\t\tprivate static string ResolveFailureCode(UnityGraphicsMcpToolResult result)
""",
    """\t\tprivate static UnityGraphicsMcpStructuredError BuildStructuredError(
\t\t\tUnityGraphicsMcpExecutionRecord record)
\t\t{
\t\t\tUnityGraphicsMcpErrorCatalogEntry catalog;
\t\t\tif (!_errorCatalog.TryGetValue(record.errorCode ?? string.Empty, out catalog))
\t\t\t{
\t\t\t\tcatalog = Catalog(
\t\t\t\t\trecord.errorCode ?? \"MCP_FAILED\",
\t\t\t\t\t\"INTERNAL\",
\t\t\t\t\ttrue,
\t\t\t\t\t\"Inspect current state and restart from the last successful checkpoint.\",
\t\t\t\t\t\"Read the Tool Call Trace and do not reuse stale IDs.\");
\t\t\t}

\t\t\tUnityGraphicsMcpStructuredError error = new UnityGraphicsMcpStructuredError
\t\t\t{
\t\t\t\tcode = catalog.code,
\t\t\t\tcategory = catalog.category,
\t\t\t\tmessage = record.summary,
\t\t\t\tretryable = catalog.retryable,
\t\t\t\tretryAction = catalog.retryAction,
\t\t\t\tremediation = catalog.remediation
\t\t\t};
\t\t\terror.details[\"executionId\"] = record.executionId;
\t\t\terror.details[\"traceId\"] = record.traceId;
\t\t\terror.details[\"state\"] = record.state;
\t\t\treturn error;
\t\t}

\t\tprivate static string ResolveFailureCode(UnityGraphicsMcpToolResult result)
""",
)

replace_once(tests, "S_DUMMY_PARAMETERS", "DummyParameters")
replace_once(
    tests,
    "UnityGraphicsMcpExecutionHardening.NotifyClientDisconnected(\"mcp-client\");",
    "UnityGraphicsMcpExecutionLifecycle.NotifyClientDisconnected(\"mcp-client\");",
)

print("Integration hardening refinements applied.")
