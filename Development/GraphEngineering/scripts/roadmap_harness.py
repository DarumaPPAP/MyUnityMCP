#!/usr/bin/env python3
"""Minimal repository-level state/evidence harness for the Codex implementation graph.

This script does not invoke Codex, Unity, GitHub, merge, tag, or release operations.
It validates and advances machine-readable roadmap state.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
GRAPH_PATH = ROOT / "graph" / "implementation-graph.json"
STATE_PATH = ROOT / "state" / "roadmap-state.json"


class HarnessError(RuntimeError):
    pass


def load_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise HarnessError(f"missing_file: {path}") from exc
    except json.JSONDecodeError as exc:
        raise HarnessError(f"json_parse_error: {path}: {exc}") from exc


def save_json(path: Path, data: dict[str, Any]) -> None:
    data["updated_at"] = dt.datetime.now(dt.timezone.utc).isoformat()
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def graph_and_state() -> tuple[dict[str, Any], dict[str, Any]]:
    return load_json(GRAPH_PATH), load_json(STATE_PATH)


def validate_graph(graph: dict[str, Any]) -> None:
    nodes = graph.get("nodes")
    if not isinstance(nodes, dict) or not nodes:
        raise HarnessError("invalid_graph: nodes must be a non-empty map")
    terminal = graph.get("terminal_node")
    if terminal not in nodes:
        raise HarnessError("invalid_graph: terminal_node is missing")

    for node_id, node in nodes.items():
        for dep in node.get("depends_on", []):
            if dep not in nodes:
                raise HarnessError(f"invalid_graph: {node_id} depends on missing {dep}")

    visiting: set[str] = set()
    visited: set[str] = set()

    def visit(node_id: str) -> None:
        if node_id in visiting:
            raise HarnessError(f"invalid_graph: cycle at {node_id}")
        if node_id in visited:
            return
        visiting.add(node_id)
        for dep in nodes[node_id].get("depends_on", []):
            visit(dep)
        visiting.remove(node_id)
        visited.add(node_id)

    for node_id in nodes:
        visit(node_id)


def validate_state(graph: dict[str, Any], state: dict[str, Any]) -> None:
    if state.get("terminal_goal_satisfied") and state.get("project_status") != "completed":
        raise HarnessError("invalid_state: terminal goal true but project not completed")

    graph_nodes = set(graph["nodes"])
    state_nodes = set(state.get("nodes", {}))
    missing = graph_nodes - state_nodes
    extra = state_nodes - graph_nodes
    if missing or extra:
        raise HarnessError(f"invalid_state: missing={sorted(missing)} extra={sorted(extra)}")

    valid_status = {"pending", "eligible", "running", "awaiting_approval", "blocked", "complete"}
    for node_id, node_state in state["nodes"].items():
        if node_state.get("status") not in valid_status:
            raise HarnessError(f"invalid_state: {node_id} has invalid status")


def dependencies_complete(node_id: str, graph: dict[str, Any], state: dict[str, Any]) -> bool:
    return all(state["nodes"][dep]["status"] == "complete"
               for dep in graph["nodes"][node_id].get("depends_on", []))


def eligible_nodes(graph: dict[str, Any], state: dict[str, Any]) -> list[str]:
    result: list[str] = []
    for node_id, node_state in state["nodes"].items():
        if node_state["status"] in {"pending", "eligible"} and dependencies_complete(node_id, graph, state):
            result.append(node_id)
    return result


def evidence_complete(node_id: str, graph: dict[str, Any], state: dict[str, Any]) -> tuple[bool, list[str]]:
    required = graph["nodes"][node_id].get("required_evidence", [])
    recorded = state["nodes"][node_id].get("evidence", {})
    missing = [key for key in required if key not in recorded]
    return not missing, missing


def git_branch() -> str:
    proc = subprocess.run(
        ["git", "branch", "--show-current"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    return proc.stdout.strip() if proc.returncode == 0 else ""


def git_dirty() -> bool:
    proc = subprocess.run(
        ["git", "status", "--porcelain"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    return bool(proc.stdout.strip()) if proc.returncode == 0 else False


def command_validate(_: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    print("PASS: graph and state are valid")
    return 0


def command_status(_: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    payload = {
        "project_status": state["project_status"],
        "terminal_goal_satisfied": state["terminal_goal_satisfied"],
        "current_node": state.get("current_node"),
        "eligible_nodes": eligible_nodes(graph, state),
        "completed_nodes": [k for k, v in state["nodes"].items() if v["status"] == "complete"],
        "blocked_nodes": [k for k, v in state["nodes"].items() if v["status"] == "blocked"],
        "branch": git_branch(),
        "worktree_dirty": git_dirty(),
    }
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


def command_next(_: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    print(json.dumps({"eligible_nodes": eligible_nodes(graph, state)}, ensure_ascii=False, indent=2))
    return 0


def command_start(args: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)

    node_id = args.node
    if node_id not in graph["nodes"]:
        raise HarnessError(f"unknown_node: {node_id}")
    if node_id not in eligible_nodes(graph, state):
        raise HarnessError(f"node_not_eligible: {node_id}")
    if git_branch() in {"main", "master"} and node_id != "bootstrap_development_harness":
        raise HarnessError("unsafe_branch: do not start product phase on main/master")

    state["project_status"] = "running"
    state["current_node"] = node_id
    state["nodes"][node_id]["status"] = "running"
    state["nodes"][node_id]["attempts"].append({
        "started_at": dt.datetime.now(dt.timezone.utc).isoformat(),
        "source_revision": args.revision,
        "iteration_budget": args.iteration_budget,
    })
    state["source_revision"] = args.revision
    save_json(STATE_PATH, state)
    print(f"STARTED: {node_id}")
    return 0


def command_record_evidence(args: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    node_id = args.node
    if node_id not in graph["nodes"]:
        raise HarnessError(f"unknown_node: {node_id}")

    artifact = Path(args.artifact)
    if not artifact.is_absolute():
        artifact = (ROOT / artifact).resolve()
    if not artifact.exists() or not artifact.is_file():
        raise HarnessError(f"missing_evidence_artifact: {artifact}")

    digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
    state["nodes"][node_id]["evidence"][args.key] = {
        "path": str(artifact.relative_to(ROOT)) if ROOT in artifact.parents else str(artifact),
        "sha256": digest,
        "recorded_at": dt.datetime.now(dt.timezone.utc).isoformat(),
    }
    save_json(STATE_PATH, state)
    print(f"RECORDED: {node_id}:{args.key}")
    return 0


def command_complete(args: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    node_id = args.node
    if node_id not in graph["nodes"]:
        raise HarnessError(f"unknown_node: {node_id}")
    if state["nodes"][node_id]["status"] not in {"running", "awaiting_approval"}:
        raise HarnessError(f"node_not_running: {node_id}")

    ok, missing = evidence_complete(node_id, graph, state)
    if not ok:
        raise HarnessError(f"missing_required_evidence: {missing}")

    state["nodes"][node_id]["status"] = "complete"
    state["nodes"][node_id]["completed_at"] = dt.datetime.now(dt.timezone.utc).isoformat()
    state["current_node"] = None

    terminal = graph["terminal_node"]
    if node_id == terminal:
        state["project_status"] = "completed"
        state["terminal_goal_satisfied"] = True
    else:
        state["project_status"] = "running"

    save_json(STATE_PATH, state)
    print(f"COMPLETED CHECKPOINT: {node_id}")
    return 0


def command_block(args: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    node_id = args.node
    if node_id not in graph["nodes"]:
        raise HarnessError(f"unknown_node: {node_id}")
    state["nodes"][node_id]["status"] = "blocked"
    state["project_status"] = "blocked"
    state["current_node"] = node_id
    state["blockers"].append({
        "node_id": node_id,
        "reason": args.reason,
        "required_input": args.required_input,
        "created_at": dt.datetime.now(dt.timezone.utc).isoformat(),
    })
    save_json(STATE_PATH, state)
    print(f"BLOCKED: {node_id}")
    return 0


def command_completion_check(_: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)

    terminal = graph["terminal_node"]
    terminal_complete = state["nodes"][terminal]["status"] == "complete"
    all_dependencies = all(
        state["nodes"][dep]["status"] == "complete"
        for dep in graph["nodes"][terminal].get("depends_on", [])
    )
    pass_gate = (
        terminal_complete
        and all_dependencies
        and state["project_status"] == "completed"
        and state["terminal_goal_satisfied"] is True
    )
    result = {
        "pass": pass_gate,
        "terminal_node": terminal,
        "terminal_node_complete": terminal_complete,
        "terminal_dependencies_complete": all_dependencies,
        "project_status": state["project_status"],
        "terminal_goal_satisfied": state["terminal_goal_satisfied"],
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if pass_gate else 2


def command_viewer(args: argparse.Namespace) -> int:
    viewer = ROOT / "tools" / "graph-viewer" / "server.py"
    command = [sys.executable, str(viewer), "--host", args.host, "--port", str(args.port)]
    if args.no_open:
        command.append("--no-open")
    return subprocess.call(command, cwd=ROOT)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("validate").set_defaults(func=command_validate)
    sub.add_parser("status").set_defaults(func=command_status)
    sub.add_parser("next").set_defaults(func=command_next)

    p_start = sub.add_parser("start")
    p_start.add_argument("node")
    p_start.add_argument("--revision", required=True)
    p_start.add_argument("--iteration-budget", type=int, required=True)
    p_start.set_defaults(func=command_start)

    p_ev = sub.add_parser("record-evidence")
    p_ev.add_argument("node")
    p_ev.add_argument("key")
    p_ev.add_argument("artifact")
    p_ev.set_defaults(func=command_record_evidence)

    p_complete = sub.add_parser("complete")
    p_complete.add_argument("node")
    p_complete.set_defaults(func=command_complete)

    p_block = sub.add_parser("block")
    p_block.add_argument("node")
    p_block.add_argument("--reason", required=True)
    p_block.add_argument("--required-input", default="")
    p_block.set_defaults(func=command_block)

    p_viewer = sub.add_parser("viewer")
    p_viewer.add_argument("--host", default="127.0.0.1")
    p_viewer.add_argument("--port", type=int, default=8765)
    p_viewer.add_argument("--no-open", action="store_true")
    p_viewer.set_defaults(func=command_viewer)

    sub.add_parser("completion-check").set_defaults(func=command_completion_check)
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    try:
        return int(args.func(args))
    except HarnessError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
