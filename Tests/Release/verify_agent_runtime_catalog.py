#!/usr/bin/env python3
"""Validate the UnityAgent Runtime Catalog v5 Tool Object contract."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = ROOT / "Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpCatalog.json"
EDITOR_PATH = ROOT / "Packages/com.darumappap.my-unity-mcp/Editor"
AGENT_PATH = EDITOR_PATH / "Operational/Agent"

EXPECTED_DOMAINS = [
    "unity_graphics_mcp",
    "unity_profiler_mcp",
    "unity_addressables_mcp",
    "unity_ui_mcp",
    "unity_animation_mcp",
    "unity_audio_mcp",
    "unity_cinematic_mcp",
]
EXPECTED_CREATORS = ["world_creator", "movie_creator", "live_creator"]
DELEGATED_PREFIXES = (
    "graphics.",
    "profiler.",
    "addressables.",
    "ui.",
    "animation.",
    "audio.",
    "cinematic.",
)
ALLOWED_EFFECTS = {"none", "scene_mutation", "asset_mutation", "save", "bake", "capture_control"}
ALLOWED_REVISION_POLICIES = {"must_remain", "may_advance"}
APPROVAL_SET = {
    "graphics.apply_plan",
    "graphics.undo_last_transaction",
    "graphics.apply_environment_plan",
    "graphics.undo_last_environment_transaction",
    "graphics.apply_save_plan",
    "graphics.bake_dependencies",
    "graphics.start_apv_bake",
    "graphics.get_apv_bake_status",
    "graphics.cancel_apv_bake",
    "addressables.apply_entry",
    "ui.apply_rect_transform",
    "animation.apply_parameter",
    "audio.apply_source",
    "cinematic.apply_director",
}
GRAPHICS_GROUPS = {
    "graphics.inspect_project": "inspect",
    "graphics.inspect_scene": "inspect",
    "graphics.validate_scene": "inspect",
    "graphics.compile_direction": "plan",
    "graphics.preview_plan": "plan",
    "graphics.prepare_light_plan": "plan",
    "graphics.prepare_environment_plan": "plan",
    "graphics.prepare_save_plan": "plan",
    "graphics.prepare_bake_plan": "plan",
    "graphics.prepare_apv_bake_plan": "plan",
    "graphics.prepare_acceptance_profile": "plan",
    "graphics.apply_plan": "mutate",
    "graphics.undo_last_transaction": "mutate",
    "graphics.apply_environment_plan": "mutate",
    "graphics.undo_last_environment_transaction": "mutate",
    "graphics.apply_save_plan": "save",
    "graphics.bake_dependencies": "bake",
    "graphics.start_apv_bake": "bake",
    "graphics.get_apv_bake_status": "bake",
    "graphics.cancel_apv_bake": "bake",
    "graphics.capture_evaluation": "capture",
    "graphics.capture_evidence": "capture",
    "graphics.refine_direction": "evaluate_and_refine",
    "graphics.submit_visual_review": "evaluate_and_refine",
    "graphics.refine_from_visual_review": "evaluate_and_refine",
    "graphics.evaluate_capture": "evaluate_and_refine",
    "graphics.refine_from_evaluation": "evaluate_and_refine",
    "graphics.get_execution_status": "execution",
    "graphics.cancel_execution": "execution",
    "graphics.get_execution_history": "execution",
    "graphics.get_error_catalog": "execution",
    "graphics.get_support_matrix": "execution",
}
EXPECTED_DOMAIN_TOOLS = {
    "unity_graphics_mcp": list(GRAPHICS_GROUPS),
    "unity_profiler_mcp": [
        "profiler.inspect_environment", "profiler.inspect_counters", "profiler.prepare_capture", "profiler.start_capture",
        "profiler.get_capture_status", "profiler.cancel_capture", "profiler.summarize_capture", "profiler.compare_baseline",
    ],
    "unity_addressables_mcp": ["addressables.inspect", "addressables.prepare_entry", "addressables.apply_entry", "addressables.get_support_matrix"],
    "unity_ui_mcp": ["ui.inspect", "ui.validate", "ui.prepare_rect_transform", "ui.apply_rect_transform", "ui.get_support_matrix"],
    "unity_animation_mcp": ["animation.inspect", "animation.validate", "animation.prepare_parameter", "animation.apply_parameter", "animation.get_support_matrix"],
    "unity_audio_mcp": ["audio.inspect", "audio.validate", "audio.prepare_source", "audio.apply_source", "audio.get_support_matrix"],
    "unity_cinematic_mcp": ["cinematic.inspect", "cinematic.validate", "cinematic.prepare_director", "cinematic.apply_director", "cinematic.get_support_matrix"],
}
EXPECTED_GROUP_ORDER = {
    "unity_graphics_mcp": ["inspect", "plan", "mutate", "save", "bake", "capture", "evaluate_and_refine", "execution"],
    "unity_profiler_mcp": ["profiler"],
    "unity_addressables_mcp": ["addressables"],
    "unity_ui_mcp": ["ui"],
    "unity_animation_mcp": ["animation"],
    "unity_audio_mcp": ["audio"],
    "unity_cinematic_mcp": ["cinematic"],
}


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def main() -> int:
    errors: list[str] = []
    try:
        catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    except Exception as exc:  # pragma: no cover - command-line failure path
        print(f"[ERROR] Unable to load {CATALOG_PATH}: {exc}")
        return 1

    if catalog.get("schemaVersion") != 5:
        fail(errors, "schemaVersion must be 5")
    domains = catalog.get("domains")
    creators = catalog.get("creators")
    if not isinstance(domains, list):
        fail(errors, "domains must be an array")
        domains = []
    if not isinstance(creators, list):
        fail(errors, "creators must be an array")
        creators = []

    domain_ids = [domain.get("domainId") for domain in domains if isinstance(domain, dict)]
    if domain_ids != EXPECTED_DOMAINS:
        fail(errors, f"domain order/identity changed: {domain_ids}")
    creator_ids = [creator.get("creatorId") for creator in creators if isinstance(creator, dict)]
    if creator_ids != EXPECTED_CREATORS:
        fail(errors, f"creator order/identity changed: {creator_ids}")

    all_tools: list[tuple[str, str, dict]] = []
    domain_tool_names: list[str] = []
    for domain in domains:
        if not isinstance(domain, dict):
            fail(errors, "domain entries must be objects")
            continue
        domain_id = domain.get("domainId")
        if domain.get("directUnityMutationAllowed") is not False:
            fail(errors, f"directUnityMutationAllowed must be false: {domain_id}")
        if "toolGroups" in domain:
            fail(errors, f"legacy toolGroups remains in domain: {domain_id}")
        tools = domain.get("tools")
        if not isinstance(tools, list):
            fail(errors, f"tools must be an array: {domain_id}")
            continue
        expected_tools = EXPECTED_DOMAIN_TOOLS.get(domain_id)
        actual_tools = [tool.get("name") for tool in tools if isinstance(tool, dict)]
        if actual_tools != expected_tools:
            fail(errors, f"Tool order/identity changed in {domain_id}: {actual_tools}")
        actual_groups = []
        for tool in tools:
            if not isinstance(tool, dict):
                fail(errors, f"tool must be an object: {domain_id}")
                continue
            name = tool.get("name")
            group = tool.get("group")
            policy = tool.get("policy")
            domain_tool_names.append(name)
            all_tools.append((name, domain_id, tool))
            if not isinstance(name, str) or not name:
                fail(errors, f"tool name is required: {domain_id}")
            if not isinstance(group, str) or not group:
                fail(errors, f"tool group is required: {name}")
            if group not in actual_groups:
                actual_groups.append(group)
            if isinstance(name, str) and name.startswith("graphics.") and GRAPHICS_GROUPS.get(name) != group:
                fail(errors, f"canonical graphics group mismatch: {name}={group}")
            domain_group = str(domain_id).replace("unity_", "", 1).replace("_mcp", "")
            if isinstance(name, str) and name.startswith(DELEGATED_PREFIXES[1:]) and group != domain_group:
                fail(errors, f"canonical domain group mismatch: {name}={group}")
            if not isinstance(policy, dict):
                fail(errors, f"policy is required: {name}")
                continue
            required = {"effect", "approvalRequired", "approvalGroup", "revisionPolicy", "retryPolicy"}
            if not required.issubset(policy):
                fail(errors, f"policy fields are incomplete: {name}")
            if policy.get("effect") not in ALLOWED_EFFECTS:
                fail(errors, f"invalid effect: {name}")
            if not isinstance(policy.get("approvalRequired"), bool):
                fail(errors, f"approvalRequired must be boolean: {name}")
            if policy.get("revisionPolicy") not in ALLOWED_REVISION_POLICIES:
                fail(errors, f"invalid revisionPolicy: {name}")
            if policy.get("retryPolicy") != "none":
                fail(errors, f"retryPolicy must be none: {name}")
            if policy.get("approvalRequired"):
                if policy.get("approvalGroup") != group or policy.get("revisionPolicy") != "may_advance":
                    fail(errors, f"approved policy invariant failed: {name}")
            elif policy.get("approvalGroup") is not None or policy.get("revisionPolicy") != "must_remain":
                fail(errors, f"non-approved policy invariant failed: {name}")
        if actual_groups != EXPECTED_GROUP_ORDER.get(domain_id):
            fail(errors, f"group order changed in {domain_id}: {actual_groups}")

    names = [name for name, _, _ in all_tools]
    if len(names) != len(set(names)):
        fail(errors, "duplicate Tool name exists in catalog")
    if len(names) != 64:
        fail(errors, f"delegated domain tool count must be 64, got {len(names)}")
    expected_tool_order = [name for domain_id in EXPECTED_DOMAINS for name in EXPECTED_DOMAIN_TOOLS[domain_id]]
    if names != expected_tool_order:
        fail(errors, "catalog Tool identity/order differs from the canonical 64-tool set")

    approved = {name for name, _, tool in all_tools if isinstance(tool.get("policy"), dict) and tool["policy"].get("approvalRequired") is True}
    if approved != APPROVAL_SET:
        fail(errors, f"approval set changed: {sorted(approved)}")

    for creator in creators:
        if not isinstance(creator, dict) or not isinstance(creator.get("tools"), list) or not all(isinstance(value, str) for value in creator.get("tools", [])):
            fail(errors, f"creator tools must remain string arrays: {creator}")

    source_text = "\n".join(path.read_text(encoding="utf-8") for path in EDITOR_PATH.rglob("*.cs"))
    tool_pattern = re.compile(r"\[\s*McpForUnityTool\s*\(\s*\"([^\"]+)\"")
    source_tools = tool_pattern.findall(source_text)
    delegated_source_tools = {name for name in source_tools if name.startswith(DELEGATED_PREFIXES)}
    if len(source_tools) != 77:
        fail(errors, f"source Tool count must be 77, got {len(source_tools)}")
    agent_source = "\n".join(path.read_text(encoding="utf-8") for path in AGENT_PATH.rglob("*.cs"))
    if len(re.findall(r"\[\s*McpForUnityTool\s*\(", agent_source)) != 10:
        fail(errors, "Agent public Tool count must remain 10")
    if "APPROVAL_TOOLS" in agent_source:
        fail(errors, "APPROVAL_TOOLS hard-code remains in Agent sources")
    if delegated_source_tools != set(names):
        fail(errors, "catalog delegated Tool identity differs from registered source Tool identity")

    if errors:
        for error in errors:
            print(f"[ERROR] {error}")
        return 1

    print(f"PASS: schema=5 domains={len(domains)} creators={len(creators)} delegated_tools={len(names)} source_tools={len(source_tools)} agent_tools=10 approvals={len(approved)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
