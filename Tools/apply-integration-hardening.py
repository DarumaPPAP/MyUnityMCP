from pathlib import Path


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Expected one match in {path}, found {count}: {old[:80]!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


root = Path(__file__).resolve().parents[1]

inspection = root / "Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpInspection.cs"
replace_once(
    inspection,
    "\t\tpublic object data { get; set; }\n\t\tpublic List<UnityGraphicsMcpIssue> issues { get; set; } = new List<UnityGraphicsMcpIssue>();",
    "\t\tpublic object data { get; set; }\n"
    "\t\tpublic UnityGraphicsMcpStructuredError error { get; set; }\n"
    "\t\tpublic UnityGraphicsMcpExecutionMetadata execution { get; set; }\n"
    "\t\tpublic List<UnityGraphicsMcpIssue> issues { get; set; } = new List<UnityGraphicsMcpIssue>();",
)

tools = root / "Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpTools.cs"
replace_once(
    tools,
    "using System;\nusing MCPForUnity.Editor.Helpers;",
    "using System;\nusing System.Collections.Generic;\nusing MCPForUnity.Editor.Helpers;",
)
replace_once(
    tools,
    """\t\tpublic static object Execute<T>(
\t\t\tJObject @params,
\t\t\tFunc<T, UnityGraphicsMcpToolResult> operation)
\t\t\twhere T : new()
\t\t{
\t\t\ttry
\t\t\t{
\t\t\t\tT parameters = ParseParameters<T>(@params);
\t\t\t\treturn Wrap(operation(parameters));
\t\t\t}
\t\t\tcatch (Exception exception)
\t\t\t{
\t\t\t\treturn new ErrorResponse(
\t\t\t\t\tE_MCP_TOOL_STATUS.INVALID_REQUEST.ToString(),
\t\t\t\t\tnew
\t\t\t\t\t{
\t\t\t\t\t\tmessage = \"Tool Parameterを解釈できませんでした。\",
\t\t\t\t\t\texceptionType = exception.GetType().FullName,
\t\t\t\t\t\tdetail = exception.Message
\t\t\t\t\t});
\t\t\t}
\t\t}
""",
    """\t\tpublic static object Execute<T>(
\t\t\tJObject @params,
\t\t\tFunc<T, UnityGraphicsMcpToolResult> operation)
\t\t\twhere T : new()
\t\t{
\t\t\tstring requestId = @params == null || @params[\"requestId\"] == null
\t\t\t\t? null
\t\t\t\t: @params[\"requestId\"].ToString();
\t\t\tType declaringType = typeof(T).DeclaringType;
\t\t\tstring provisionalToolName = declaringType == null
\t\t\t\t? \"unknown\"
\t\t\t\t: declaringType.Name;
\t\t\tUnityGraphicsMcpExecutionScope scope =
\t\t\t\tUnityGraphicsMcpExecutionHardening.Begin(
\t\t\t\t\tprovisionalToolName,
\t\t\t\t\trequestId);

\t\t\tT parameters;
\t\t\ttry
\t\t\t{
\t\t\t\tparameters = ParseParameters<T>(@params);
\t\t\t}
\t\t\tcatch (Exception exception)
\t\t\t{
\t\t\t\tUnityGraphicsMcpToolResult invalid =
\t\t\t\t\tUnityGraphicsMcpInspection.CreateHardeningResult(
\t\t\t\t\t\tprovisionalToolName,
\t\t\t\t\t\trequestId,
\t\t\t\t\t\tE_MCP_TOOL_STATUS.INVALID_REQUEST,
\t\t\t\t\t\t\"Tool Parameterを解釈できませんでした。\",
\t\t\t\t\t\tnew Dictionary<string, object>
\t\t\t\t\t\t{
\t\t\t\t\t\t\t{ \"failureCode\", \"MCP_INVALID_REQUEST\" },
\t\t\t\t\t\t\t{ \"exceptionType\", exception.GetType().FullName },
\t\t\t\t\t\t\t{ \"detail\", exception.Message }
\t\t\t\t\t\t});
\t\t\t\treturn Wrap(UnityGraphicsMcpExecutionHardening.Complete(scope, invalid));
\t\t\t}

\t\t\ttry
\t\t\t{
\t\t\t\tUnityGraphicsMcpToolResult result = operation(parameters);
\t\t\t\tif (result == null)
\t\t\t\t{
\t\t\t\t\tresult = UnityGraphicsMcpInspection.CreateHardeningResult(
\t\t\t\t\t\tprovisionalToolName,
\t\t\t\t\t\trequestId,
\t\t\t\t\t\tE_MCP_TOOL_STATUS.FAILED,
\t\t\t\t\t\t\"MyUnityMCP ToolがResultを返しませんでした。\",
\t\t\t\t\t\tnew Dictionary<string, object>
\t\t\t\t\t\t{
\t\t\t\t\t\t\t{ \"failureCode\", \"MYUNITYMCP_NULL_RESULT\" }
\t\t\t\t\t\t});
\t\t\t\t}
\t\t\t\treturn Wrap(UnityGraphicsMcpExecutionHardening.Complete(scope, result));
\t\t\t}
\t\t\tcatch (OperationCanceledException exception)
\t\t\t{
\t\t\t\tUnityGraphicsMcpToolResult cancelled =
\t\t\t\t\tUnityGraphicsMcpInspection.CreateHardeningResult(
\t\t\t\t\t\tprovisionalToolName,
\t\t\t\t\t\trequestId,
\t\t\t\t\t\tE_MCP_TOOL_STATUS.FAILED,
\t\t\t\t\t\t\"Tool実行はCancellation Pointで停止しました。\",
\t\t\t\t\t\tnew Dictionary<string, object>
\t\t\t\t\t\t{
\t\t\t\t\t\t\t{ \"failureCode\", \"EXECUTION_CANCEL_REQUESTED\" },
\t\t\t\t\t\t\t{ \"detail\", exception.Message }
\t\t\t\t\t\t});
\t\t\t\treturn Wrap(UnityGraphicsMcpExecutionHardening.Complete(scope, cancelled));
\t\t\t}
\t\t\tcatch (Exception exception)
\t\t\t{
\t\t\t\tUnityEngine.Debug.LogException(exception);
\t\t\t\tUnityGraphicsMcpToolResult failed =
\t\t\t\t\tUnityGraphicsMcpInspection.CreateHardeningResult(
\t\t\t\t\t\tprovisionalToolName,
\t\t\t\t\t\trequestId,
\t\t\t\t\t\tE_MCP_TOOL_STATUS.FAILED,
\t\t\t\t\t\t\"Tool実行中に未処理例外が発生しました。\",
\t\t\t\t\t\tnew Dictionary<string, object>
\t\t\t\t\t\t{
\t\t\t\t\t\t\t{ \"failureCode\", \"MCP_FAILED\" },
\t\t\t\t\t\t\t{ \"exceptionType\", exception.GetType().FullName },
\t\t\t\t\t\t\t{ \"detail\", exception.Message }
\t\t\t\t\t\t});
\t\t\t\treturn Wrap(UnityGraphicsMcpExecutionHardening.Complete(scope, failed));
\t\t\t}
\t\t}
""",
)

runtime = root / "Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpExecutionHardening.cs"
replace_once(
    runtime,
    """\t\tinternal static void RecoverForTests(string code)
\t\t{
\t\t\tRecoverInterruptedExecutions(code);
\t\t}

\t\tinternal static void ResetForTests(string storageRoot)
""",
    """\t\tinternal static void RecoverForTests(string code)
\t\t{
\t\t\tRecoverInterruptedExecutions(code);
\t\t}

\t\tinternal static void SimulateProcessLossForTests()
\t\t{
\t\t\tlock (_sync)
\t\t\t{
\t\t\t\t_active.Clear();
\t\t\t}
\t\t}

\t\tinternal static void PruneRetentionForTests()
\t\t{
\t\t\tlock (_sync)
\t\t\t{
\t\t\t\tTrimHistoryInMemory();
\t\t\t\tRewriteJsonLines(HistoryPath(), _history);
\t\t\t\tPruneOwnedArtifacts(UtcNow().AddDays(-ARTIFACT_RETENTION_DAYS));
\t\t\t}
\t\t}

\t\tinternal static string OwnedArtifactRootForTests()
\t\t{
\t\t\treturn OwnedArtifactRoot();
\t\t}

\t\tinternal static void ResetForTests(string storageRoot)
""",
)

package_json = root / "Packages/com.darumappap.my-unity-mcp/package.json"
replace_once(package_json, '"version": "0.7.1"', '"version": "0.8.0"')
replace_once(
    package_json,
    '"description": "対象Unity Projectを解析し、Read-only Inspection、Direction Planning、承認制Graphics Mutation・Save・限定Bake、Color・Depth・Object ID Capture Evidence、APV Bake Job、およびAcceptance ProfileによるVisual Evaluationと構造化Refineを提供するUnity Editor向けMCP基盤です。Unity Version、Render Pipeline、Platformは対象Projectから解決します。"',
    '"description": "対象Unity Projectを解析し、Read-only Inspection、Direction Planning、承認制Graphics Mutation・Save・限定Bake、Capture Evidence、APV Bake、Visual Evaluation、構造化Refine、および長時間AI制作向けのTimeout・Cancellation・Progress・Structured Log・Execution History・Tool Call Trace・Recovery Contractを提供するUnity Editor向けMCP基盤です。"',
)

print("Integration hardening patches applied.")
