#!/usr/bin/env python3
"""Validate the exact-77 contract and admitted Local CG evidence without claiming CI or promotion."""

from __future__ import annotations

from collections import Counter
import json
from pathlib import Path
import re
import sys

import yaml

ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = ROOT / "Catalog" / "mcp-catalog.yaml"
CAPABILITY_CATALOG_PATH = ROOT / "Catalog" / "capability-catalog.yaml"
CONTRACT_PATH = ROOT / "Catalog" / "stage2-8-integration-contracts.yaml"
WORLD_CREATOR_CONTRACT_PATH = ROOT / "Catalog" / "world-creator-capability-contract.yaml"
MANIFEST_PATH = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "MCP_MANIFEST.yaml"
EDITOR_ROOT = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "Editor"
AGENT_CATALOG_PATH = EDITOR_ROOT / "Development" / "Agent" / "UnityAgentMcpCatalog.json"
VALIDATION_PROGRESS_PATH = ROOT / "Tests" / "Compatibility" / "stage2-8-validation-progress.yaml"

CANONICAL_STATUS = "local_cg_runtime_verified_ci_unavailable"
EXPECTED_PRODUCTION_TOOLS = 45
EXPECTED_CANDIDATE_TOOLS = 32
EXPECTED_COMBINED_TOOLS = 77
EXPECTED_COMPOSITION = {
    "graphics": 32,
    "agent": 10,
    "world_creator": 3,
    "profiler": 8,
    "addressables": 4,
    "ui": 5,
    "animation": 5,
    "audio": 5,
    "cinematic": 5,
}
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
ACTIVE_CATALOG_STATUSES = {"editor_operational", "integration_candidate"}
EXPECTED_AGENT_TOOLS = {
    "agent.inspect_capabilities",
    "agent.validate_workflow",
    "agent.compile_graph",
    "agent.preview_execution",
    "agent.submit_approval",
    "agent.start_execution",
    "agent.get_execution_status",
    "agent.cancel_execution",
    "agent.get_execution_history",
    "agent.get_error_catalog",
}
TOOL_DECLARATION_PATTERN = re.compile(r'\[McpForUnityTool\s*\(\s*"([^"]+)"')
TOOL_PARAMETER_REQUIRED_PATTERN = re.compile(r"Required\s*=\s*(true|false)", re.IGNORECASE)


def fail(message: str) -> int:
    print(f"[ERROR] {message}")
    return 1


def parameter_order_violations(path: Path, source: str) -> list[str]:
    """Find schemas that break the bridge's required-before-optional signature adapter."""
    lines = source.splitlines()
    violations: list[str] = []
    index = 0
    while index < len(lines):
        if "public sealed class Parameters" not in lines[index]:
            index += 1
            continue

        declaration_line = index + 1
        depth = 0
        entered_body = False
        optional_seen = False
        while index < len(lines):
            line = lines[index]
            if "[ToolParameter" in line:
                required_match = TOOL_PARAMETER_REQUIRED_PATTERN.search(line)
                is_required = bool(required_match and required_match.group(1).lower() == "true")
                if is_required and optional_seen:
                    violations.append(
                        f"{path.relative_to(ROOT)}:{index + 1} "
                        f"Parameters declared at line {declaration_line}"
                    )
                elif not is_required:
                    optional_seen = True

            depth += line.count("{") - line.count("}")
            entered_body = entered_body or "{" in line
            index += 1
            if entered_body and depth == 0:
                break

    return violations


def main() -> int:
    catalog = yaml.safe_load(CATALOG_PATH.read_text(encoding="utf-8")) or {}
    capability_catalog = yaml.safe_load(
        CAPABILITY_CATALOG_PATH.read_text(encoding="utf-8")
    ) or {}
    contract = yaml.safe_load(CONTRACT_PATH.read_text(encoding="utf-8")) or {}
    world_creator_contract = yaml.safe_load(
        WORLD_CREATOR_CONTRACT_PATH.read_text(encoding="utf-8")
    ) or {}
    manifest = yaml.safe_load(MANIFEST_PATH.read_text(encoding="utf-8")) or {}
    agent_catalog = json.loads(AGENT_CATALOG_PATH.read_text(encoding="utf-8"))
    validation_progress = yaml.safe_load(
        VALIDATION_PROGRESS_PATH.read_text(encoding="utf-8")
    ) or {}
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

    if contract.get("status") != CANONICAL_STATUS:
        errors += fail(f"Integration contract status must be {CANONICAL_STATUS}.")
    if contract.get("validation_wave", {}).get("status") != CANONICAL_STATUS:
        errors += fail(f"Integration validation-wave status must be {CANONICAL_STATUS}.")

    promotion_policy = catalog.get("promotion_policy", {})
    if promotion_policy.get("stage2_8_validation_status") != CANONICAL_STATUS:
        errors += fail(f"MCP catalog promotion status must be {CANONICAL_STATUS}.")
    if promotion_policy.get("human_promotion_gate") != "pending":
        errors += fail("MCP catalog must keep the human promotion gate pending.")
    if promotion_policy.get("integration_candidate_is_not_production_operational") is not True:
        errors += fail("Integration candidates must not be marked Production operational.")

    capabilities = capability_catalog.get("capabilities", {})
    if "build" in capabilities:
        errors += fail("Capability catalog must not retain the retired Build runtime capability.")
    for capability_name in ("profiler", "addressables", "ui", "animation", "audio", "cinematic"):
        capability = capabilities.get(capability_name, {})
        if capability.get("status") != "integration_candidate":
            errors += fail(f"Capability {capability_name} must remain integration_candidate.")
        if capability.get("validation_status") != CANONICAL_STATUS:
            errors += fail(
                f"Capability {capability_name} validation_status must be {CANONICAL_STATUS}."
            )
    if capabilities.get("world_creator", {}).get("status") != "editor_operational":
        errors += fail("WorldCreator capability must be editor_operational.")
    if world_creator_contract.get("status") != "editor_operational":
        errors += fail("WorldCreator capability contract must be editor_operational.")

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
    if manifest.get("implementation_status") != CANONICAL_STATUS:
        errors += fail(f"Manifest implementation_status must be {CANONICAL_STATUS}.")
    if verification.get("stage2_8_validation_status") != CANONICAL_STATUS:
        errors += fail(f"Manifest validation status must be {CANONICAL_STATUS}.")
    if verification.get("automated_ci") != "unavailable_not_verified":
        errors += fail("Manifest must preserve automated CI as unavailable_not_verified.")
    expected_manifest_not_verified = {
        "package_editor_test_runner": "unavailable_not_verified",
        "fresh_project_sample_workflow": "not_verified",
        "addressables_positive_backend_matrix": "not_verified",
        "external_transport_disconnect_reconnect": "not_verified",
    }
    for key, expected in expected_manifest_not_verified.items():
        if verification.get(key) != expected:
            errors += fail(f"Manifest verification.{key} must remain {expected}.")
    if verification.get("production_promotion") != "prohibited":
        errors += fail("Manifest must prohibit automatic Production promotion.")
    if verification.get("human_promotion_gate") != "pending":
        errors += fail("Manifest must keep the human promotion gate pending.")

    current_validation = validation_progress.get("current_77_tool_validation", {})
    verdict = validation_progress.get("verdict", {})
    if validation_progress.get("status") != CANONICAL_STATUS:
        errors += fail(f"Validation progress status must be {CANONICAL_STATUS}.")
    if current_validation.get("status") != CANONICAL_STATUS:
        errors += fail(f"Current 77-tool validation status must be {CANONICAL_STATUS}.")
    if verdict.get("full_77_tool_validation") != CANONICAL_STATUS:
        errors += fail(f"Validation verdict must be {CANONICAL_STATUS}.")
    if current_validation.get("observed_composition") != EXPECTED_COMPOSITION:
        errors += fail("Observed Local CG tool composition must match the exact 77-tool contract.")
    discovery = current_validation.get("runtime_discovery", {})
    if discovery.get("status") != "passed":
        errors += fail("Local CG runtime discovery must be recorded as passed.")
    if discovery.get("total_my_unity_mcp_tools") != EXPECTED_COMBINED_TOOLS:
        errors += fail("Local CG runtime discovery must record exactly 77 MyUnityMCP tools.")
    if discovery.get("duplicate_tool_names") != 0:
        errors += fail("Local CG runtime discovery must record zero duplicate tool names.")
    if validation_progress.get("production_ready") is not False:
        errors += fail("Local Runtime verification must not mark the candidate Production ready.")
    if validation_progress.get("production_promotion") != "prohibited":
        errors += fail("Validation progress must keep Production promotion prohibited.")
    required_not_verified = {
        "package_editor_test_runner",
        "fresh_project_sample_workflow",
        "automated_ci",
        "addressables_positive_backend_matrix",
        "external_transport_disconnect_reconnect",
    }
    if set(current_validation.get("remaining_not_verified", [])) != required_not_verified:
        errors += fail("Validation progress remaining_not_verified set is not canonical.")
    expected_verdict_not_verified = {
        "package_editor_test_runner": "unavailable_not_verified",
        "fresh_project_sample_workflow": "not_verified",
        "automated_ci": "unavailable_not_verified",
        "external_transport_disconnect_reconnect": "not_verified",
    }
    for key, expected in expected_verdict_not_verified.items():
        if verdict.get(key) != expected:
            errors += fail(f"Validation verdict.{key} must remain {expected}.")
    if verdict.get("addressables_positive_backend_matrix") != "not_verified_allowed_by_package_absent_contract":
        errors += fail(
            "Validation verdict.addressables_positive_backend_matrix must remain "
            "not_verified_allowed_by_package_absent_contract."
        )

    source_files = sorted(EDITOR_ROOT.rglob("*.cs"))
    source_by_path = {path: path.read_text(encoding="utf-8") for path in source_files}
    source = "\n".join(source_by_path.values())
    tool_names = TOOL_DECLARATION_PATTERN.findall(source)
    tool_count = len(tool_names)
    disabled_count = len(re.findall(r"AutoRegister\s*=\s*false", source))
    if tool_count != EXPECTED_COMBINED_TOOLS:
        errors += fail(f"Source declares {tool_count} MCP tools, expected 77.")
    if disabled_count != tool_count:
        errors += fail(f"AutoRegister=false count {disabled_count} does not match tool count {tool_count}.")

    duplicate_tools = sorted(name for name, count in Counter(tool_names).items() if count > 1)
    if duplicate_tools:
        errors += fail(f"Source contains duplicate MCP tool declarations: {duplicate_tools}")
    if len(set(tool_names)) != EXPECTED_COMBINED_TOOLS:
        errors += fail(
            f"Source contains {len(set(tool_names))} unique MCP tool names, expected 77."
        )

    active_catalog_tools = {
        tool
        for domain in agent_catalog.get("domains", [])
        if domain.get("status") in ACTIVE_CATALOG_STATUSES
        for tool in domain.get("tools", [])
    }
    active_catalog_tools.update(
        tool
        for creator in agent_catalog.get("creators", [])
        if creator.get("status") in ACTIVE_CATALOG_STATUSES
        for tool in creator.get("tools", [])
    )
    source_tool_set = set(tool_names)
    source_agent_tools = {name for name in source_tool_set if name.startswith("agent.")}
    if source_agent_tools != EXPECTED_AGENT_TOOLS:
        errors += fail(
            "Source Agent tools differ from the exact ten-tool contract: "
            f"source_only={sorted(source_agent_tools - EXPECTED_AGENT_TOOLS)}, "
            f"contract_only={sorted(EXPECTED_AGENT_TOOLS - source_agent_tools)}"
        )
    expected_tool_set = active_catalog_tools | EXPECTED_AGENT_TOOLS
    if source_tool_set != expected_tool_set:
        errors += fail(
            "Source and active Agent catalog plus Agent contract tool sets differ: "
            f"source_only={sorted(source_tool_set - expected_tool_set)}, "
            f"contract_only={sorted(expected_tool_set - source_tool_set)}"
        )

    manifest_candidate_tools = {
        tool
        for group in manifest.get("integration_tool_groups", {}).values()
        for tool in group.get("tools", [])
    }
    candidate_namespaces = {
        tool.split(".", 1)[0] for tool in manifest_candidate_tools if "." in tool
    }
    source_candidate_tools = {
        name for name in tool_names if name.split(".", 1)[0] in candidate_namespaces
    }
    if source_candidate_tools != manifest_candidate_tools:
        errors += fail(
            "Source and MCP_MANIFEST integration-candidate tool sets differ: "
            f"source_only={sorted(source_candidate_tools - manifest_candidate_tools)}, "
            f"manifest_only={sorted(manifest_candidate_tools - source_candidate_tools)}"
        )

    ordering_violations = [
        violation
        for path, text in source_by_path.items()
        for violation in parameter_order_violations(path, text)
    ]
    if ordering_violations:
        errors += fail(
            "ToolParameter schemas must declare required properties before optional properties "
            "for bridge signature compatibility: " + "; ".join(ordering_violations)
        )
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
        print(f"Stage 2-8 integration contract: {errors} error(s).")
        return 1

    print(
        "Stage 2-8 contract is structurally and evidentially consistent: "
        f"production={EXPECTED_PRODUCTION_TOOLS}, candidate={EXPECTED_CANDIDATE_TOOLS}, "
        f"combined={EXPECTED_COMBINED_TOOLS}, status={CANONICAL_STATUS}. "
        "Production promotion remains behind the pending human gate."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
