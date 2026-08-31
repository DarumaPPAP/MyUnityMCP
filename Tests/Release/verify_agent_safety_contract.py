#!/usr/bin/env python3
"""Validate UnityAgent safety, execution, history, trace, and migration static contracts."""

from __future__ import annotations

import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
AGENT_PATH = ROOT / "Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent"
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

    if errors:
        for error in errors:
            print(f"[ERROR] {error}")
        return 1
    print("PASS: UnityAgent safety/history/trace static contracts")
    return 0


if __name__ == "__main__":
    sys.exit(main())
