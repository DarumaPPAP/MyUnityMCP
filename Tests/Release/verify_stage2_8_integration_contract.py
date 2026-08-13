#!/usr/bin/env python3
"""Verify the historical integration wave is fully promoted to the v1.1.0 77-tool Editor surface."""

from __future__ import annotations

from collections import Counter
import json
from pathlib import Path
import re
import sys

import yaml

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "Catalog" / "mcp-catalog.yaml"
CAPABILITY_CATALOG = ROOT / "Catalog" / "capability-catalog.yaml"
PROMOTION_RECORD = ROOT / "Catalog" / "stage2-8-integration-contracts.yaml"
MANIFEST = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "MCP_MANIFEST.yaml"
AGENT_CATALOG = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "Editor" / "Development" / "Agent" / "UnityAgentMcpCatalog.json"
EDITOR_ROOT = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "Editor"
VALIDATION = ROOT / "Tests" / "Compatibility" / "stage2-8-validation-progress.yaml"

EXPECTED_TOOLS = 77
EXPECTED_PROMOTED_MODULES = {
    "unity_profiler_mcp",
    "unity_addressables_mcp",
    "unity_ui_mcp",
    "unity_animation_mcp",
    "unity_audio_mcp",
    "unity_cinematic_mcp",
}
TOOL_PATTERN = re.compile(r'\[McpForUnityTool\s*\(\s*"([^"]+)"')


def load_yaml(path: Path) -> dict:
    return yaml.safe_load(path.read_text(encoding="utf-8")) or {}


def fail(errors: list[str], message: str) -> None:
    errors.append(message)
    print(f"[ERROR] {message}")


def main() -> int:
    errors: list[str] = []
    catalog = load_yaml(CATALOG)
    capability_catalog = load_yaml(CAPABILITY_CATALOG)
    promotion = load_yaml(PROMOTION_RECORD)
    manifest = load_yaml(MANIFEST)
    validation = load_yaml(VALIDATION)
    agent_catalog = json.loads(AGENT_CATALOG.read_text(encoding="utf-8"))

    modules = catalog.get("modules", {})
    for module_id in EXPECTED_PROMOTED_MODULES:
        module = modules.get(module_id, {})
        if module.get("status") != "editor_operational":
            fail(errors, f"{module_id} is not editor_operational in mcp-catalog.yaml")
        contract_ref = module.get("capability_contract")
        if not contract_ref or not (ROOT / contract_ref).is_file():
            fail(errors, f"{module_id} has no valid capability_contract")

    for name in ("profiler", "addressables", "ui", "animation", "audio", "cinematic"):
        if capability_catalog.get("capabilities", {}).get(name, {}).get("status") != "editor_operational":
            fail(errors, f"capability-catalog {name} is not editor_operational")

    if promotion.get("status") != "promoted_editor_operational":
        fail(errors, "promotion record is not promoted_editor_operational")
    if promotion.get("production_tools_after_promotion") != EXPECTED_TOOLS:
        fail(errors, "promotion record does not declare 77 production tools")
    if promotion.get("promotion", {}).get("status") != "completed":
        fail(errors, "promotion record is not completed")

    if manifest.get("production_verified_tool_count") != EXPECTED_TOOLS:
        fail(errors, "manifest production_verified_tool_count must be 77")
    if manifest.get("bridge", {}).get("discovered_tool_count") != EXPECTED_TOOLS:
        fail(errors, "manifest bridge.discovered_tool_count must be 77")
    verification = manifest.get("verification", {})
    if verification.get("tool_discovery_count") != EXPECTED_TOOLS:
        fail(errors, "manifest verification.tool_discovery_count must be 77")
    if verification.get("production_promotion") != "completed":
        fail(errors, "manifest production promotion must be completed")
    if verification.get("automated_ci") != "unavailable_not_verified":
        fail(errors, "automated CI must remain unavailable_not_verified")

    if validation.get("production_ready") is not True:
        fail(errors, "validation progress must be production_ready")
    if validation.get("production_promotion") != "completed":
        fail(errors, "validation progress promotion must be completed")
    discovery = validation.get("direct_editor_validation", {}).get("runtime_discovery", {})
    if discovery.get("total_my_unity_mcp_tools") != EXPECTED_TOOLS:
        fail(errors, "direct Editor evidence must record 77 discovered tools")
    if discovery.get("duplicate_tool_names") != 0:
        fail(errors, "direct Editor evidence must record zero duplicate tools")

    runtime_status = {
        item.get("domainId"): item.get("status")
        for item in agent_catalog.get("domains", [])
    }
    for module_id in EXPECTED_PROMOTED_MODULES | {"unity_graphics_mcp"}:
        if runtime_status.get(module_id) != "editor_operational":
            fail(errors, f"Agent runtime catalog does not route {module_id} as editor_operational")

    source = "\n".join(
        path.read_text(encoding="utf-8") for path in sorted(EDITOR_ROOT.rglob("*.cs"))
    )
    tools = TOOL_PATTERN.findall(source)
    if len(tools) != EXPECTED_TOOLS:
        fail(errors, f"source declares {len(tools)} tools, expected 77")
    duplicates = sorted(name for name, count in Counter(tools).items() if count > 1)
    if duplicates:
        fail(errors, f"duplicate tool declarations: {duplicates}")
    disabled = len(re.findall(r"AutoRegister\s*=\s*false", source))
    if disabled != EXPECTED_TOOLS:
        fail(errors, f"AutoRegister=false count is {disabled}, expected 77")

    print(f"77-tool promotion contract: {len(errors)} error(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
