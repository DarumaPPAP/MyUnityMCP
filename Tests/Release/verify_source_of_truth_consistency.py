#!/usr/bin/env python3
"""Verify that current production contracts and historical registries agree."""

from __future__ import annotations

import json
from pathlib import Path
import re
import sys

import yaml


ROOT = Path(__file__).resolve().parents[2]
VERSION = ROOT / "VERSION"
PACKAGE = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "package.json"
MANIFEST = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "MCP_MANIFEST.yaml"
CATALOG = ROOT / "Catalog" / "mcp-catalog.yaml"
CAPABILITY_CATALOG = ROOT / "Catalog" / "capability-catalog.yaml"
PRODUCTION_SURFACE = ROOT / "Catalog" / "production-surface-contract.yaml"
SUPPORT_MATRIX = ROOT / "Tests" / "Compatibility" / "support-matrix.yaml"
RELEASE_VERIFICATION = ROOT / "Tests" / "Compatibility" / "release-verification.yaml"
TOOL_REFERENCE = ROOT / "Packages" / "com.darumappap.my-unity-mcp" / "Documentation~" / "tool-reference.md"
RUNTIME_CATALOG = (
    ROOT
    / "Packages"
    / "com.darumappap.my-unity-mcp"
    / "Editor"
    / "Operational"
    / "Agent"
    / "UnityAgentMcpCatalog.json"
)
DESIGN_MODULE_CATALOG = ROOT / "Design" / "module-catalog.yaml"
DESIGN_CREATOR_CATALOG = ROOT / "Design" / "Creators" / "catalog.yaml"

TOOL_HEADING = re.compile(r"^## .+ — (\d+)\s*$", re.MULTILINE)
OPERATIONAL = "editor_operational"
HISTORICAL_DESIGN = "historical_design"
NOT_IMPLEMENTED = "not_implemented_current_wave"


def load_yaml(path: Path) -> dict:
    return yaml.safe_load(path.read_text(encoding="utf-8")) or {}


def fail(errors: list[str], message: str) -> None:
    errors.append(message)
    print(f"[ERROR] {message}")


def require_equal(errors: list[str], label: str, values: dict[str, object]) -> None:
    distinct = set(values.values())
    if len(distinct) > 1:
        detail = ", ".join(f"{name}={value!r}" for name, value in values.items())
        fail(errors, f"{label} disagree: {detail}")


def design_entries(registry: dict) -> dict:
    entries = {}
    for section in ("modules", "promoted_modules"):
        section_entries = registry.get(section, {})
        if isinstance(section_entries, dict):
            entries.update(section_entries)
    return entries


def check_versions(errors: list[str], expected_version: str, sources: dict[str, object]) -> None:
    require_equal(errors, "version identity", sources)
    if any(value != expected_version for value in sources.values()):
        fail(errors, f"version identity does not match VERSION={expected_version!r}")


def check_tool_counts(
    errors: list[str],
    manifest: dict,
    catalog: dict,
    production_surface: dict,
    support_matrix: dict,
    release_verification: dict,
) -> None:
    manifest_groups = manifest.get("production_tool_groups", {})
    group_total = sum(
        group.get("tools", 0)
        for group in manifest_groups.values()
        if isinstance(group, dict)
    )
    support_composition = support_matrix.get("release_surface", {}).get("composition", {})
    support_total = sum(support_composition.values())

    count_sources = {
        "manifest.production_verified_tool_count": manifest.get("production_verified_tool_count"),
        "manifest.source_surface_tool_count": manifest.get("source_surface_tool_count"),
        "manifest.bridge.discovered_tool_count": manifest.get("bridge", {}).get("discovered_tool_count"),
        "manifest.verification.tool_discovery_count": manifest.get("verification", {}).get("tool_discovery_count"),
        "manifest.verification.production_verified_tool_count": manifest.get("verification", {}).get("production_verified_tool_count"),
        "manifest.production_tool_groups": group_total,
        "catalog.production_policy.production_verified_tool_count": catalog.get("production_policy", {}).get("production_verified_tool_count"),
        "catalog.production_policy.exact_tool_surface": catalog.get("production_policy", {}).get("exact_tool_surface"),
        "production_surface.production_tool_count": production_surface.get("production_tool_count"),
        "production_surface.release_contract.production_operational_tools": production_surface.get("release_contract", {}).get("production_operational_tools"),
        "support_matrix.release_surface.production_tool_count": support_matrix.get("release_surface", {}).get("production_tool_count"),
        "support_matrix.release_surface.composition": support_total,
        "support_matrix.verification_contract.discovered_mcp_tools": support_matrix.get("verification_contract", {}).get("discovered_mcp_tools"),
        "release_verification.operational_surface.total_tools": release_verification.get("operational_surface", {}).get("total_tools"),
        "release_verification.editor_verification.tool_discovery": release_verification.get("editor_verification", {}).get("tool_discovery"),
    }
    require_equal(errors, "production tool counts", count_sources)

    reference_counts = [int(value) for value in TOOL_HEADING.findall(TOOL_REFERENCE.read_text(encoding="utf-8"))]
    require_equal(
        errors,
        "tool reference total and manifest",
        {
            "tool_reference.domain_headings": sum(reference_counts),
            "manifest.bridge.discovered_tool_count": manifest.get("bridge", {}).get("discovered_tool_count"),
        },
    )

    extended_manifest = {
        item.get("id"): item.get("tools")
        for item in manifest.get("operational_extended_domains", [])
    }
    for module_id, manifest_count in extended_manifest.items():
        catalog_count = catalog.get("modules", {}).get(module_id, {}).get("tools")
        surface_count = production_surface.get("modules", {}).get(module_id, {}).get("tools")
        require_equal(
            errors,
            f"{module_id} tool count",
            {
                "manifest": manifest_count,
                "catalog": catalog_count,
                "production_surface": surface_count,
            },
        )


def check_operational_registry_consistency(
    errors: list[str],
    catalog: dict,
    capability_catalog: dict,
    runtime_catalog: dict,
    design_modules: dict,
) -> None:
    operational_modules = {
        module_id: module
        for module_id, module in catalog.get("modules", {}).items()
        if isinstance(module, dict) and module.get("status") == OPERATIONAL
    }

    for module_id, module in operational_modules.items():
        design_entry = design_modules.get(module_id)
        if not design_entry:
            continue
        if design_entry.get("status") != HISTORICAL_DESIGN:
            fail(errors, f"{module_id} remains a current design-only entry")
        if design_entry.get("current_status") != OPERATIONAL:
            fail(errors, f"{module_id} historical entry does not point to current operational status")
        if design_entry.get("operational_catalog") != "Catalog/mcp-catalog.yaml":
            fail(errors, f"{module_id} historical entry lacks the operational catalog reference")

    for module_id, module in design_modules.items():
        if module.get("status") == "design_only_not_executable":
            fail(errors, f"stale design_only_not_executable status remains for {module_id}")

    runtime_domains = {
        item.get("domainId"): item.get("status")
        for item in runtime_catalog.get("domains", [])
        if isinstance(item, dict)
    }
    for module_id, runtime_status in runtime_domains.items():
        catalog_status = catalog.get("modules", {}).get(module_id, {}).get("status")
        if catalog_status != runtime_status:
            fail(
                errors,
                f"runtime/catalog status mismatch for {module_id}: runtime={runtime_status!r}, catalog={catalog_status!r}",
            )

    capability_entries = capability_catalog.get("capabilities", {})
    for module_id in operational_modules:
        owned_capabilities = [
            (name, capability)
            for name, capability in capability_entries.items()
            if isinstance(capability, dict) and capability.get("owner") == module_id
        ]
        if not owned_capabilities:
            fail(errors, f"{module_id} has no capability-catalog owner entry")
        for name, capability in owned_capabilities:
            if capability.get("status") != OPERATIONAL:
                fail(errors, f"capability-catalog {name} is not editor_operational")


def check_creator_registry_consistency(
    errors: list[str],
    catalog: dict,
    runtime_catalog: dict,
    design_creators: dict,
) -> None:
    runtime_creators = {
        creator.get("creatorId"): creator
        for creator in runtime_catalog.get("creators", [])
        if isinstance(creator, dict)
    }
    current_modules = {
        module_id: module.get("status")
        for module_id, module in catalog.get("modules", {}).items()
        if isinstance(module, dict)
    }

    for creator_id, runtime_creator in runtime_creators.items():
        entry = design_creators.get(creator_id)
        if not entry:
            fail(errors, f"runtime creator {creator_id} is missing from the Design creator registry")
            continue
        runtime_status = runtime_creator.get("status")
        if entry.get("status") != HISTORICAL_DESIGN:
            fail(errors, f"{creator_id} is not marked as historical_design in the Design registry")
        if entry.get("current_status") != runtime_status:
            fail(
                errors,
                f"creator status mismatch for {creator_id}: runtime={runtime_status!r}, design={entry.get('current_status')!r}",
            )

        blockers = entry.get("blocked_by", []) or []
        if runtime_status == OPERATIONAL:
            if blockers:
                fail(errors, f"operational creator {creator_id} still declares blockers: {blockers}")
            if entry.get("operational_catalog") != "Catalog/mcp-catalog.yaml":
                fail(errors, f"operational creator {creator_id} lacks the operational catalog reference")
        elif runtime_status == NOT_IMPLEMENTED:
            expected_blocker = f"{creator_id}_runtime_not_implemented_current_wave"
            if expected_blocker not in blockers:
                fail(errors, f"{creator_id} lacks its current runtime blocker {expected_blocker}")

        workflow_reference = entry.get("workflow")
        if workflow_reference:
            workflow = load_yaml(ROOT / workflow_reference)
            if workflow.get("status") != entry.get("status"):
                fail(errors, f"{creator_id} workflow status does not match its creator registry")
            if workflow.get("current_status") != runtime_status:
                fail(errors, f"{creator_id} workflow current status does not match runtime catalog")
            if (workflow.get("blocked_by") or []) != blockers:
                fail(errors, f"{creator_id} workflow blockers do not match its creator registry")

        for blocker in blockers:
            for suffix in ("_runtime_not_implemented", "_not_implemented"):
                candidate = blocker.removesuffix(suffix)
                if candidate in current_modules and current_modules[candidate] == OPERATIONAL:
                    fail(errors, f"{creator_id} has a stale blocker for operational module {candidate}: {blocker}")


def check_references(errors: list[str], design_modules: dict, design_creators: dict) -> None:
    for entry_id, entry in {**design_modules, **design_creators}.items():
        for field in (
            "operational_catalog",
            "current_spec",
            "capability_contract",
            "runtime_catalog",
            "current_status_source",
        ):
            reference = entry.get(field)
            if reference and not (ROOT / reference).is_file():
                fail(errors, f"{entry_id}.{field} points to missing file: {reference}")


def main() -> int:
    errors: list[str] = []
    manifest = load_yaml(MANIFEST)
    catalog = load_yaml(CATALOG)
    capability_catalog = load_yaml(CAPABILITY_CATALOG)
    production_surface = load_yaml(PRODUCTION_SURFACE)
    support_matrix = load_yaml(SUPPORT_MATRIX)
    release_verification = load_yaml(RELEASE_VERIFICATION)
    runtime_catalog = json.loads(RUNTIME_CATALOG.read_text(encoding="utf-8"))
    design_module_registry = load_yaml(DESIGN_MODULE_CATALOG)
    design_creator_registry = load_yaml(DESIGN_CREATOR_CATALOG)
    design_modules = design_entries(design_module_registry)
    design_creators = design_creator_registry.get("creators", {})

    expected_version = VERSION.read_text(encoding="utf-8").strip()
    package = json.loads(PACKAGE.read_text(encoding="utf-8"))
    check_versions(
        errors,
        expected_version,
        {
            "VERSION": expected_version,
            "package.json": package.get("version"),
            "MCP_MANIFEST": manifest.get("version"),
            "Catalog/mcp-catalog": catalog.get("release_version"),
            "Catalog/capability-catalog": capability_catalog.get("release_version"),
            "Catalog/production-surface-contract": production_surface.get("release_version"),
            "Tests/Compatibility/support-matrix": support_matrix.get("package_version"),
            "Tests/Compatibility/release-verification": release_verification.get("release_version"),
            "Design/module-catalog": design_module_registry.get("release_version"),
            "Design/Creators/catalog": design_creator_registry.get("release_version"),
        },
    )
    check_tool_counts(errors, manifest, catalog, production_surface, support_matrix, release_verification)
    check_operational_registry_consistency(
        errors,
        catalog,
        capability_catalog,
        runtime_catalog,
        design_modules,
    )
    check_creator_registry_consistency(errors, catalog, runtime_catalog, design_creators)
    check_references(errors, design_modules, design_creators)

    print(f"source-of-truth consistency: {len(errors)} error(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
