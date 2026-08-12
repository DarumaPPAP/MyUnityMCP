#!/usr/bin/env python3
"""Validate the Stage 2-8 implementation contract without claiming Unity runtime verification."""

from __future__ import annotations

from pathlib import Path
import re
import sys

import yaml

ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = ROOT / "Catalog" / "mcp-catalog.yaml"
CONTRACT_PATH = ROOT / "Catalog" / "stage2-8-integration-contracts.yaml"
MANIFEST_PATH = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "MCP_MANIFEST.yaml"
EDITOR_ROOT = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "Editor"

EXPECTED_PRODUCTION_TOOLS = 45
EXPECTED_CANDIDATE_TOOLS = 32
EXPECTED_COMBINED_TOOLS = 77
EXPECTED_CANDIDATES = {
    "unity_profiler_mcp": 8,
    "unity_addressables_mcp": 4,
    "unity_ui_mcp": 5,
    "unity_animation_mcp": 5,
    "unity_audio_mcp": 5,
    "unity_cinematic_mcp": 5,
}
FORBIDDEN_ADDRESSABLES_CONTENT_BUILD_RUNTIME_REFERENCES = {
    "addressables.prepare_content_build",
    "addressables.build_content",
    "AddressableAssetSettings.BuildPlayerContent",
}


def fail(message: str) -> int:
    print(f"[ERROR] {message}")
    return 1


def main() -> int:
    catalog = yaml.safe_load(CATALOG_PATH.read_text(encoding="utf-8")) or {}
    contract = yaml.safe_load(CONTRACT_PATH.read_text(encoding="utf-8")) or {}
    manifest = yaml.safe_load(MANIFEST_PATH.read_text(encoding="utf-8")) or {}
    errors = 0

    if catalog.get("integration_contracts") != "Catalog/stage2-8-integration-contracts.yaml":
        errors += fail("Catalog integration_contracts path is not canonical.")

    modules = catalog.get("modules", {})
    if "unity_build_mcp" in modules:
        errors += fail("Build domain must not be present in the current integration candidate.")
    for module_id, expected_tools in EXPECTED_CANDIDATES.items():
        module = modules.get(module_id)
        if not isinstance(module, dict):
            errors += fail(f"Missing integration candidate module: {module_id}")
            continue
        if module.get("status") != "integration_candidate":
            errors += fail(f"{module_id} must remain integration_candidate before validation.")
        if module.get("tools") != expected_tools:
            errors += fail(f"{module_id} tool count must be {expected_tools}.")
        if module.get("integration_contract") != "Catalog/stage2-8-integration-contracts.yaml":
            errors += fail(f"{module_id} must point to the Stage 2-8 integration contract.")

    contract_modules = contract.get("modules", {})
    if set(contract_modules) != set(EXPECTED_CANDIDATES):
        errors += fail("Integration contract module set does not match the 77-tool candidate set.")
    for module_id, expected_tools in EXPECTED_CANDIDATES.items():
        module = contract_modules.get(module_id, {})
        if module.get("status") != "integration_candidate":
            errors += fail(f"Contract {module_id} must remain integration_candidate.")
        if module.get("tools") != expected_tools:
            errors += fail(f"Contract {module_id} tool count must be {expected_tools}.")

    if contract.get("production_verified_tools") != EXPECTED_PRODUCTION_TOOLS:
        errors += fail("Integration contract must preserve the 45-tool Production baseline.")
    if contract.get("integration_candidate_tools") != EXPECTED_CANDIDATE_TOOLS:
        errors += fail("Integration contract candidate total must be 32.")
    if contract.get("combined_target_tools") != EXPECTED_COMBINED_TOOLS:
        errors += fail("Integration contract combined target must be 77.")

    bridge = manifest.get("bridge", {})
    verification = manifest.get("verification", {})
    if bridge.get("discovered_tool_count") != EXPECTED_COMBINED_TOOLS:
        errors += fail("Manifest bridge.discovered_tool_count must target 77.")
    if verification.get("tool_discovery_count") != EXPECTED_COMBINED_TOOLS:
        errors += fail("Manifest verification.tool_discovery_count must target 77.")
    if verification.get("production_verified_tool_count") != EXPECTED_PRODUCTION_TOOLS:
        errors += fail("Manifest must preserve production_verified_tool_count=45.")
    if verification.get("integration_candidate_tool_count") != EXPECTED_CANDIDATE_TOOLS:
        errors += fail("Manifest must declare integration_candidate_tool_count=32.")
    if verification.get("stage2_8_validation_status") not in {
        "reset_after_addressables_build_removal",
        "not_started",
    }:
        errors += fail("Current candidate must not claim completed Stage 2-8 validation.")

    source = "\n".join(
        path.read_text(encoding="utf-8")
        for path in EDITOR_ROOT.rglob("*.cs")
    )
    tool_count = len(re.findall(r"\[McpForUnityTool\s*\(", source))
    disabled_count = len(re.findall(r"AutoRegister\s*=\s*false", source))
    if tool_count != EXPECTED_COMBINED_TOOLS:
        errors += fail(f"Source declares {tool_count} MCP tools, expected 77.")
    if disabled_count != tool_count:
        errors += fail(f"AutoRegister=false count {disabled_count} does not match tool count {tool_count}.")
    if re.search(r"\[McpForUnityTool\s*\(\s*\"build\.", source):
        errors += fail("Build MCP tool declaration remains in current Editor runtime source.")
    for reference in sorted(FORBIDDEN_ADDRESSABLES_CONTENT_BUILD_RUNTIME_REFERENCES):
        if reference in source:
            errors += fail(
                "Addressables content-build runtime reference remains in current Editor source: "
                f"{reference}"
            )

    forbidden_runtime_paths = [
        EDITOR_ROOT / "Development" / "Build" / "UnityBuildMcp.cs",
        EDITOR_ROOT / "Development" / "Creators" / "UnityMovieCreatorMcp.cs",
        EDITOR_ROOT / "Development" / "Creators" / "UnityLiveCreatorMcp.cs",
    ]
    for path in forbidden_runtime_paths:
        if path.exists():
            errors += fail(f"Out-of-wave runtime is present: {path.relative_to(ROOT)}")

    if errors:
        print(f"Stage 2-8 integration contract: {errors} error(s). Runtime validation remains incomplete.")
        return 1

    print(
        "Stage 2-8 implementation contract is structurally consistent: "
        f"production={EXPECTED_PRODUCTION_TOOLS}, candidate={EXPECTED_CANDIDATE_TOOLS}, "
        f"combined={EXPECTED_COMBINED_TOOLS}. Runtime validation has NOT been executed for this candidate."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
