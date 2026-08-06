#!/usr/bin/env python3
"""Evaluate whether MyUnityMCP development artifacts form a releasable candidate."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Sequence

REQUIRED_GATES = (
    "architecture_lint",
    "python_harness_tests",
    "unity_editor_ci",
    "external_mcp_client_e2e",
    "addressables_package_absent",
    "addressables_package_present",
    "security_modes",
    "fault_injection",
    "world_visual_review",
    "movie_visual_review",
    "live_operator_review",
    "release_version_approval",
    "artifact_only_delivery_validation",
)

PASS_STATUSES = {"pass", "passed", "approved"}
FORBIDDEN_DELIVERY_PREFIXES = (
    "Development/GraphEngineering/",
    "GRAPH_ENGINEERING.md",
)


class ReleaseCandidateGateError(RuntimeError):
    pass


def load_json(path: Path) -> dict[str, object]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ReleaseCandidateGateError(f"cannot read {path}: {error}") from error
    if not isinstance(value, dict):
        raise ReleaseCandidateGateError(f"{path} must contain a JSON object")
    return value


def evaluate(
    gate_state: dict[str, object],
    roadmap_state: dict[str, object],
    delivery_manifest: dict[str, object] | None = None,
) -> dict[str, object]:
    blockers: list[dict[str, str]] = []
    gates = gate_state.get("gates")
    if not isinstance(gates, dict):
        raise ReleaseCandidateGateError("development gate state has no gates object")

    for gate_name in REQUIRED_GATES:
        gate = gates.get(gate_name)
        if not isinstance(gate, dict):
            blockers.append({"gate": gate_name, "reason": "gate is missing"})
            continue
        status = str(gate.get("status", "missing")).lower()
        if status not in PASS_STATUSES:
            blockers.append({"gate": gate_name, "reason": f"status is {status}"})
        if status in PASS_STATUSES and not gate.get("evidence"):
            blockers.append({"gate": gate_name, "reason": "passing gate has no evidence"})

    nodes = roadmap_state.get("nodes")
    if not isinstance(nodes, dict):
        raise ReleaseCandidateGateError("roadmap state has no nodes object")
    phase12 = nodes.get("phase_12_production_hardening")
    if not isinstance(phase12, dict) or phase12.get("status") != "complete":
        blockers.append({"gate": "phase_12_production_hardening", "reason": "phase is not complete"})

    completion_gate = nodes.get("project_completion_gate")
    if isinstance(completion_gate, dict) and completion_gate.get("status") == "complete":
        blockers.append({
            "gate": "project_completion_gate",
            "reason": "project completion gate must run after release candidate gate",
        })

    if roadmap_state.get("terminal_goal_satisfied") is True:
        blockers.append({
            "gate": "terminal_goal_satisfied",
            "reason": "terminal goal cannot be true before human final release approval",
        })

    if delivery_manifest is not None:
        include_paths = delivery_manifest.get("include_paths")
        if not isinstance(include_paths, list) or not include_paths:
            blockers.append({"gate": "delivery_manifest", "reason": "include_paths are missing"})
        else:
            for raw_path in include_paths:
                path = str(raw_path).replace("\\", "/").lstrip("./")
                if any(path == prefix or path.startswith(prefix) for prefix in FORBIDDEN_DELIVERY_PREFIXES):
                    blockers.append({
                        "gate": "delivery_manifest",
                        "reason": f"development-only path is included: {path}",
                    })
        if delivery_manifest.get("base_branch") != "main":
            blockers.append({"gate": "delivery_manifest", "reason": "delivery base must be main"})
    else:
        blockers.append({"gate": "delivery_manifest", "reason": "delivery manifest is missing"})

    return {
        "passed": not blockers,
        "release_candidate_ready": not blockers,
        "terminal_goal_satisfied": False,
        "blockers": blockers,
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--gate-state", default="Development/GraphEngineering/state/development-gates.json")
    parser.add_argument("--roadmap-state", default="Development/GraphEngineering/state/roadmap-state.json")
    parser.add_argument("--delivery-manifest", default="")
    parser.add_argument("--json", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    root = Path(args.repo_root).resolve()
    try:
        gate_state = load_json(root / args.gate_state)
        roadmap_state = load_json(root / args.roadmap_state)
        delivery_manifest = load_json(root / args.delivery_manifest) if args.delivery_manifest else None
        result = evaluate(gate_state, roadmap_state, delivery_manifest)
    except ReleaseCandidateGateError as error:
        result = {
            "passed": False,
            "release_candidate_ready": False,
            "terminal_goal_satisfied": False,
            "blockers": [{"gate": "gate_runtime", "reason": str(error)}],
        }

    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print("PASS: release candidate gate passed." if result["passed"] else "FAIL: release candidate gate is blocked.")
        for blocker in result["blockers"]:
            print(f"- {blocker['gate']}: {blocker['reason']}")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
