#!/usr/bin/env python3
"""Validate the UnityAgent remaining P1 hardening structure and safety contract."""

from __future__ import annotations

import importlib.util
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
AGENT_PATH = ROOT / "Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent"
CATALOG_PATH = AGENT_PATH / "UnityAgentMcpCatalog.json"
COMPONENTS = {
    "AgentWorkflowValidator.cs",
    "AgentGraphCompiler.cs",
    "AgentApprovalService.cs",
    "AgentDelegateRegistry.cs",
    "AgentResultNormalizer.cs",
    "AgentExecutionEngine.cs",
    "AgentExecutionHistoryStore.cs",
    "AgentExecutionTraceStore.cs",
}
TRACE_FIELDS = {
    "schemaVersion",
    "timestampUtc",
    "executionId",
    "graphId",
    "stepId",
    "domainId",
    "toolName",
    "event",
    "revision",
    "resultCode",
    "durationMs",
}
FORBIDDEN_PERSISTED_FIELDS = {
    "approvalToken",
    "approvalTokenHash",
    "parameters",
    "delegatedResult",
    "delegatedPayload",
    "stackTrace",
    "secret",
    "credential",
}
PREFIXES = (
    "graphics.",
    "profiler.",
    "addressables.",
    "ui.",
    "animation.",
    "audio.",
    "cinematic.",
)


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def load_catalog_validator() -> int:
    path = ROOT / "Tests/Release/verify_agent_runtime_catalog.py"
    spec = importlib.util.spec_from_file_location("verify_agent_runtime_catalog", path)
    if spec is None or spec.loader is None:
        return 1
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.main()


def main() -> int:
    errors: list[str] = []
    missing = sorted(COMPONENTS - {path.name for path in AGENT_PATH.glob("Agent*.cs")})
    if missing:
        fail(errors, f"missing runtime components: {missing}")

    implementation_files = [path for path in AGENT_PATH.glob("*.cs") if not path.name.endswith("Tests.cs")]
    implementation_text = "\n".join(path.read_text(encoding="utf-8") for path in implementation_files)
    if "MAX_GRAPH_STEPS = 64" not in implementation_text:
        fail(errors, "MAX_GRAPH_STEPS = 64 is missing")
    if "AgentResultNormalizer" not in implementation_text or "Normalize(" not in implementation_text:
        fail(errors, "AgentResultNormalizer is not wired into execution")
    if "APPROVAL_TOOLS" in implementation_text:
        fail(errors, "APPROVAL_TOOLS hard-code remains")
    if "IsRuntimeDomainTool" in implementation_text:
        fail(errors, "IsRuntimeDomainTool remains")
    if "IsDelegatedSuccess" in implementation_text:
        fail(errors, "legacy truthy delegate success classifier remains")
    if "AGENT-CATALOG-CHANGED" not in implementation_text:
        fail(errors, "catalog change rejection is missing")
    if "ComputeFingerprint" not in implementation_text or "ReadAllBytes" not in implementation_text:
        fail(errors, "byte-based catalog fingerprint path is missing")

    runtime_text = (AGENT_PATH / "UnityAgentMcpRuntime.cs").read_text(encoding="utf-8")
    for prefix in PREFIXES:
        if prefix in runtime_text:
            fail(errors, f"runtime domain prefix hard-code remains: {prefix}")

    trace_text = (AGENT_PATH / "AgentExecutionTraceStore.cs").read_text(encoding="utf-8")
    trace_payload_match = re.search(r"JObject payload = new JObject\s*\{(?P<body>.*?)\n\s*\};", trace_text, re.S)
    if not trace_payload_match:
        fail(errors, "Trace allowlist payload was not found")
    else:
        payload_fields = set(re.findall(r'\["([^"]+)"\]', trace_payload_match.group("body")))
        if payload_fields != TRACE_FIELDS:
            fail(errors, f"Trace field allowlist changed: {sorted(payload_fields)}")
    for forbidden in FORBIDDEN_PERSISTED_FIELDS:
        if forbidden in trace_text:
            fail(errors, f"forbidden Trace field appears: {forbidden}")

    history_text = (AGENT_PATH / "AgentExecutionHistoryStore.cs").read_text(encoding="utf-8")
    if "BuildProjection" not in history_text or "TryAtomicRewrite" not in history_text:
        fail(errors, "History allowlist projection or atomic migration is missing")
    for forbidden in FORBIDDEN_PERSISTED_FIELDS:
        if forbidden in history_text:
            fail(errors, f"forbidden History field appears: {forbidden}")

    try:
        catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    except Exception as exc:
        fail(errors, f"Catalog load failed: {exc}")
        catalog = {}
    if catalog.get("schemaVersion") != 5:
        fail(errors, "Catalog schemaVersion must remain 5")
    tools = [tool for domain in catalog.get("domains", []) for tool in domain.get("tools", [])]
    if len(tools) != 64:
        fail(errors, f"delegated Tool count changed: {len(tools)}")
    if sum(1 for tool in tools if tool.get("policy", {}).get("approvalRequired") is True) != 14:
        fail(errors, "approval Tool count changed")
    if any(tool.get("policy", {}).get("retryPolicy") != "none" for tool in tools):
        fail(errors, "automatic retry policy was introduced")

    source_text = "\n".join(path.read_text(encoding="utf-8") for path in (ROOT / "Packages/com.darumappap.my-unity-mcp/Editor").rglob("*.cs"))
    tool_names = re.findall(r"\[\s*McpForUnityTool\s*\(\s*\"([^\"]+)\"", source_text)
    if len(tool_names) != 77:
        fail(errors, f"Production Tool count changed: {len(tool_names)}")
    public_agent_tools = re.findall(r"\[\s*McpForUnityTool\s*\(\s*\"agent\.[^\"]+\"", runtime_text + (AGENT_PATH / "UnityAgentMcpTools.cs").read_text(encoding="utf-8"))
    if len(public_agent_tools) != 10:
        fail(errors, f"Agent Tool count changed: {len(public_agent_tools)}")

    if not errors and load_catalog_validator() != 0:
        fail(errors, "verify_agent_runtime_catalog.py failed")
    if errors:
        for error in errors:
            print(f"[ERROR] {error}")
        return 1
    print("PASS: UnityAgent remaining P1 harness structure and safety contract")
    return 0


if __name__ == "__main__":
    sys.exit(main())
