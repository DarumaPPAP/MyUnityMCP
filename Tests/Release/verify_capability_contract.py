#!/usr/bin/env python3
"""Verify MyUnityMCP capability contracts cover every operational module/tool group."""

from __future__ import annotations

from pathlib import Path
import sys

import yaml

REQUIRED_CAPABILITY_FIELDS = {
    "mode",
    "use_when",
    "requires",
    "must_not",
    "success_evidence",
}

REQUIRED_PRINCIPLES = {
    "progressive_disclosure": True,
    "explicit_activation_required": True,
    "unavailable_is_not_passed": True,
    "capture_is_not_visual_acceptance": True,
    "compile_is_not_runtime_acceptance": True,
    "no_silent_fallback": True,
}


def error(message: str) -> None:
    print(f"[ERROR] {message}")


def validate_contract(root: Path, module_name: str, module: dict) -> tuple[int, dict]:
    errors = 0
    contract_ref = module.get("capability_contract")
    if not isinstance(contract_ref, str) or not contract_ref.strip():
        error(f"{module_name}: operational module must declare capability_contract.")
        return 1, {}

    contract_path = root / contract_ref
    if not contract_path.is_file():
        error(f"{module_name}: capability contract does not exist: {contract_ref}")
        return 1, {}

    contract = yaml.safe_load(contract_path.read_text(encoding="utf-8")) or {}
    if contract.get("module") != module_name:
        error(f"{module_name}: contract module field must match module id.")
        errors += 1

    tool_groups = module.get("available_tool_groups", [])
    capability_map = contract.get("capabilities", {})
    if not isinstance(tool_groups, list) or not tool_groups:
        error(f"{module_name}.available_tool_groups must be a non-empty list.")
        return errors + 1, contract
    if not isinstance(capability_map, dict) or not capability_map:
        error(f"{contract_ref} must define capabilities.")
        return errors + 1, contract

    missing_contracts = sorted(set(tool_groups).difference(capability_map))
    extra_contracts = sorted(set(capability_map).difference(tool_groups))
    if missing_contracts:
        error(f"{module_name}: missing capability contracts: " + ", ".join(missing_contracts))
        errors += 1
    if extra_contracts:
        error(f"{module_name}: contracts exist for unexposed tool groups: " + ", ".join(extra_contracts))
        errors += 1

    for name in tool_groups:
        capability = capability_map.get(name)
        if not isinstance(capability, dict):
            continue
        missing_fields = sorted(REQUIRED_CAPABILITY_FIELDS.difference(capability))
        if missing_fields:
            error(f"{module_name}.{name}: missing fields: {', '.join(missing_fields)}")
            errors += 1
            continue
        for key in ("use_when", "requires", "must_not", "success_evidence"):
            values = capability.get(key)
            if not isinstance(values, list) or not values or not all(
                isinstance(value, str) and value.strip() for value in values
            ):
                error(f"{module_name}.{name}.{key} must be a non-empty list of strings.")
                errors += 1

    principles = contract.get("principles", {})
    for key, expected in REQUIRED_PRINCIPLES.items():
        if principles.get(key) is not expected:
            error(f"{module_name}: principles.{key} must be {expected}.")
            errors += 1

    return errors, contract


def validate_graphics_contract(contract: dict) -> int:
    errors = 0
    capability_map = contract.get("capabilities", {})

    mutate = capability_map.get("mutate", {})
    for requirement in (
        "approved_plan",
        "expected_revision",
        "explicit_mutation_permission",
        "exact_diff",
        "undo_contract",
        "baseline_revalidation",
    ):
        if requirement not in mutate.get("requires", []):
            error(f"unity_graphics_mcp.mutate.requires must contain {requirement}.")
            errors += 1

    bake = capability_map.get("bake", {})
    if "explicit_bake_permission" not in bake.get("requires", []):
        error("unity_graphics_mcp.bake.requires must contain explicit_bake_permission.")
        errors += 1
    if "unconditional_full_bake" not in bake.get("must_not", []):
        error("unity_graphics_mcp.bake.must_not must forbid unconditional_full_bake.")
        errors += 1

    capture = capability_map.get("capture", {})
    if "claim_visual_acceptance" not in capture.get("must_not", []):
        error("unity_graphics_mcp.capture.must_not must forbid claim_visual_acceptance.")
        errors += 1

    evaluate = capability_map.get("evaluate", {})
    if "missing_evidence" not in evaluate.get("success_evidence", []):
        error("unity_graphics_mcp.evaluate.success_evidence must report missing_evidence.")
        errors += 1

    return errors


def validate_agent_contract(module: dict, contract: dict) -> int:
    errors = 0
    agent = contract.get("capabilities", {}).get("agent", {})
    if module.get("direct_unity_mutation_allowed") is not False:
        error("unity_agent_mcp.direct_unity_mutation_allowed must be false.")
        errors += 1
    for forbidden in (
        "directly_mutate_unity",
        "execute_non_operational_domain",
        "bypass_required_approval_groups",
        "silently_resume_after_reload_or_disconnect",
    ):
        if forbidden not in agent.get("must_not", []):
            error(f"unity_agent_mcp.agent.must_not must contain {forbidden}.")
            errors += 1
    return errors


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    catalog_path = root / "Catalog" / "mcp-catalog.yaml"
    catalog = yaml.safe_load(catalog_path.read_text(encoding="utf-8")) or {}

    if catalog.get("capability_contracts") != "Catalog/capability-contracts.yaml":
        error("Catalog default capability contract must point to Catalog/capability-contracts.yaml.")
        return 1

    operational_modules = {
        name: module
        for name, module in catalog.get("modules", {}).items()
        if isinstance(module, dict) and module.get("status") == "editor_operational"
    }
    if not operational_modules:
        error("Catalog must contain at least one editor_operational module.")
        return 1

    errors = 0
    contracts: dict[str, dict] = {}
    for module_name, module in operational_modules.items():
        module_errors, contract = validate_contract(root, module_name, module)
        errors += module_errors
        contracts[module_name] = contract

    if "unity_graphics_mcp" not in operational_modules:
        error("unity_graphics_mcp must remain editor_operational.")
        errors += 1
    else:
        errors += validate_graphics_contract(contracts.get("unity_graphics_mcp", {}))

    if "unity_agent_mcp" in operational_modules:
        errors += validate_agent_contract(
            operational_modules["unity_agent_mcp"],
            contracts.get("unity_agent_mcp", {}),
        )

    group_count = sum(
        len(module.get("available_tool_groups", []))
        for module in operational_modules.values()
    )
    print(
        f"Validated {len(operational_modules)} operational module(s) / "
        f"{group_count} capability group(s): {errors} error(s)."
    )
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
