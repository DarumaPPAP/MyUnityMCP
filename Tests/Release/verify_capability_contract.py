#!/usr/bin/env python3
"""Verify MyUnityMCP capability contracts cover every operational tool group."""

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


def error(message: str) -> None:
    print(f"[ERROR] {message}")


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    catalog_path = root / "Catalog" / "mcp-catalog.yaml"
    contract_path = root / "Catalog" / "capability-contracts.yaml"

    catalog = yaml.safe_load(catalog_path.read_text(encoding="utf-8"))
    contracts = yaml.safe_load(contract_path.read_text(encoding="utf-8"))

    errors = 0
    module = catalog.get("modules", {}).get("unity_graphics_mcp", {})
    tool_groups = module.get("available_tool_groups", [])
    capability_map = contracts.get("capabilities", {})

    if catalog.get("capability_contracts") != "Catalog/capability-contracts.yaml":
        error("Catalog must point to Catalog/capability-contracts.yaml.")
        errors += 1

    if module.get("capability_contract") != "Catalog/capability-contracts.yaml":
        error("Operational module must declare its capability_contract.")
        errors += 1

    if not isinstance(tool_groups, list) or not tool_groups:
        error("unity_graphics_mcp.available_tool_groups must be a non-empty list.")
        return 1

    if not isinstance(capability_map, dict) or not capability_map:
        error("capability-contracts.yaml must define capabilities.")
        return 1

    missing_contracts = sorted(set(tool_groups).difference(capability_map))
    extra_contracts = sorted(set(capability_map).difference(tool_groups))
    if missing_contracts:
        error("Missing capability contracts: " + ", ".join(missing_contracts))
        errors += 1
    if extra_contracts:
        error("Contracts exist for unexposed tool groups: " + ", ".join(extra_contracts))
        errors += 1

    for name in tool_groups:
        contract = capability_map.get(name)
        if not isinstance(contract, dict):
            continue

        missing_fields = sorted(REQUIRED_CAPABILITY_FIELDS.difference(contract))
        if missing_fields:
            error(f"{name}: missing fields: {', '.join(missing_fields)}")
            errors += 1
            continue

        for key in ("use_when", "requires", "must_not", "success_evidence"):
            values = contract.get(key)
            if not isinstance(values, list) or not values or not all(
                isinstance(value, str) and value.strip() for value in values
            ):
                error(f"{name}.{key} must be a non-empty list of strings.")
                errors += 1

    principles = contracts.get("principles", {})
    required_principles = {
        "progressive_disclosure": True,
        "explicit_activation_required": True,
        "unavailable_is_not_passed": True,
        "capture_is_not_visual_acceptance": True,
        "compile_is_not_runtime_acceptance": True,
        "no_silent_fallback": True,
    }
    for key, expected in required_principles.items():
        if principles.get(key) is not expected:
            error(f"principles.{key} must be {expected}.")
            errors += 1

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
            error(f"mutate.requires must contain {requirement}.")
            errors += 1

    bake = capability_map.get("bake", {})
    if "explicit_bake_permission" not in bake.get("requires", []):
        error("bake.requires must contain explicit_bake_permission.")
        errors += 1
    if "unconditional_full_bake" not in bake.get("must_not", []):
        error("bake.must_not must forbid unconditional_full_bake.")
        errors += 1

    capture = capability_map.get("capture", {})
    if "claim_visual_acceptance" not in capture.get("must_not", []):
        error("capture.must_not must forbid claim_visual_acceptance.")
        errors += 1

    evaluate = capability_map.get("evaluate", {})
    if "missing_evidence" not in evaluate.get("success_evidence", []):
        error("evaluate.success_evidence must report missing_evidence.")
        errors += 1

    print(
        f"Validated {len(tool_groups)} capability contract(s): {errors} error(s)."
    )
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
