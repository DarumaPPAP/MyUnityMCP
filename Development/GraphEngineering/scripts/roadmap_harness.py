#!/usr/bin/env python3
"""Repository-owned roadmap state/evidence harness for MyUnityMCP development.

This script never invokes Codex, Unity, GitHub merge, tag, or release operations.
It validates and advances machine-readable development state only.
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
NODE_STATUSES = {
    "pending", "eligible", "running", "awaiting_approval",
    "blocked", "interrupted", "complete",
}
PROJECT_STATUSES = {
    "proposed", "running", "awaiting_approval",
    "blocked", "interrupted", "completed",
}


class HarnessError(RuntimeError):
    pass


def now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


def load_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise HarnessError(f"missing_file: {path}") from exc
    except json.JSONDecodeError as exc:
        raise HarnessError(f"json_parse_error: {path}: {exc}") from exc


def save_json(path: Path, data: dict[str, Any]) -> None:
    data["updated_at"] = now()
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
        dependencies = node.get("depends_on", [])
        if not isinstance(dependencies, list):
            raise HarnessError(f"invalid_graph: {node_id}.depends_on must be an array")
        for dependency in dependencies:
            if dependency not in nodes:
                raise HarnessError(f"invalid_graph: {node_id} depends on missing {dependency}")

    visiting: set[str] = set()
    visited: set[str] = set()

    def visit(node_id: str) -> None:
        if node_id in visiting:
            raise HarnessError(f"invalid_graph: cycle at {node_id}")
        if node_id in visited:
            return
        visiting.add(node_id)
        for dependency in nodes[node_id].get("depends_on", []):
            visit(dependency)
        visiting.remove(node_id)
        visited.add(node_id)

    for node_id in nodes:
        visit(node_id)


def validate_state(graph: dict[str, Any], state: dict[str, Any]) -> None:
    project_status = state.get("project_status")
    if project_status not in PROJECT_STATUSES:
        raise HarnessError(f"invalid_state: invalid project_status {project_status}")
    if state.get("terminal_goal_satisfied") and project_status != "completed":
        raise HarnessError("invalid_state: terminal goal true but project not completed")

    graph_nodes = set(graph["nodes"])
    state_nodes = set(state.get("nodes", {}))
    missing = graph_nodes - state_nodes
    extra = state_nodes - graph_nodes
    if missing or extra:
        raise HarnessError(f"invalid_state: missing={sorted(missing)} extra={sorted(extra)}")

    for node_id, node_state in state["nodes"].items():
        status = node_state.get("status")
        if status not in NODE_STATUSES:
            raise HarnessError(f"invalid_state: {node_id} has invalid status {status}")
        if not isinstance(node_state.get("attempts", []), list):
            raise HarnessError(f"invalid_state: {node_id}.attempts must be an array")
        if not isinstance(node_state.get("evidence", {}), dict):
            raise HarnessError(f"invalid_state: {node_id}.evidence must be an object")

    current = state.get("current_node")
    if current is not None and current not in graph_nodes:
        raise HarnessError(f"invalid_state: unknown current_node {current}")
    if not isinstance(state.get("blockers", []), list):
        raise HarnessError("invalid_state: blockers must be an array")
    if not isinstance(state.get("interruptions", []), list):
        raise HarnessError("invalid_state: interruptions must be an array")


def dependencies_complete(node_id: str, graph: dict[str, Any], state: dict[str, Any]) -> bool:
    return all(
        state["nodes"][dependency]["status"] == "complete"
        for dependency in graph["nodes"][node_id].get("depends_on", [])
    )


def eligible_nodes(graph: dict[str, Any], state: dict[str, Any]) -> list[str]:
    return [
        node_id
        for node_id, node_state in state["nodes"].items()
        if node_state["status"] in {"pending", "eligible"}
        and dependencies_complete(node_id, graph, state)
    ]


def evidence_complete(
    node_id: str,
    graph: dict[str, Any],
    state: dict[str, Any],
) -> tuple[bool, list[str]]:
    required = graph["nodes"][node_id].get("required_evidence", [])
    recorded = state["nodes"][node_id].get("evidence", {})
    missing = [key for key in required if key not in recorded]
    return not missing, missing


def git_branch() -> str:
    process = subprocess.run(
        ["git", "branch", "--show-current"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    return process.stdout.strip() if process.returncode == 0 else ""


def git_dirty() -> bool:
    process = subprocess.run(
        ["git", "status", "--porcelain"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    return bool(process.stdout.strip()) if process.returncode == 0 else False


def require_node(node_id: str, graph: dict[str, Any]) -> None:
    if node_id not in graph["nodes"]:
        raise HarnessError(f"unknown_node: {node_id}")


def resolve_current_node(args: argparse.Namespace, state: dict[str, Any]) -> str:
    node_id = getattr(args, "node", None) or state.get("current_node")
    if not node_id:
        raise HarnessError("current_node_missing: specify a node")
    return node_id


def command_validate(_: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    print("PASS: graph and state are valid")
    return 0


def status_payload(graph: dict[str, Any], state: dict[str, Any]) -> dict[str, Any]:
    interruptions = state.get("interruptions", [])
    return {
        "project_status": state["project_status"],
        "terminal_goal_satisfied": state["terminal_goal_satisfied"],
        "current_node": state.get("current_node"),
        "eligible_nodes": eligible_nodes(graph, state),
        "completed_nodes": [
            node_id for node_id, value in state["nodes"].items()
            if value["status"] == "complete"
        ],
        "blocked_nodes": [
            node_id for node_id, value in state["nodes"].items()
            if value["status"] == "blocked"
        ],
        "interrupted_nodes": [
            node_id for node_id, value in state["nodes"].items()
            if value["status"] == "interrupted"
        ],
        "latest_interruption": interruptions[-1] if interruptions else None,
        "branch": git_branch(),
        "worktree_dirty": git_dirty(),
    }


def command_status(_: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    print(json.dumps(status_payload(graph, state), ensure_ascii=False, indent=2))
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
    require_node(node_id, graph)
    if node_id not in eligible_nodes(graph, state):
        raise HarnessError(f"node_not_eligible: {node_id}")
    if git_branch() in {"main", "master"} and node_id != "bootstrap_development_harness":
        raise HarnessError("unsafe_branch: do not start product phase on main/master")

    state["project_status"] = "running"
    state["current_node"] = node_id
    state["nodes"][node_id]["status"] = "running"
    state["nodes"][node_id]["attempts"].append({
        "started_at": now(),
        "source_revision": args.revision,
        "iteration_budget": args.iteration_budget,
        "kind": "start",
    })
    state["source_revision"] = args.revision
    save_json(STATE_PATH, state)
    print(f"STARTED: {node_id}")
    return 0


def command_record_evidence(args: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    require_node(args.node, graph)

    artifact = Path(args.artifact)
    if not artifact.is_absolute():
        artifact = (ROOT / artifact).resolve()
    if not artifact.exists() or not artifact.is_file():
        raise HarnessError(f"missing_evidence_artifact: {artifact}")

    digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
    try:
        stored_path = str(artifact.relative_to(ROOT))
    except ValueError:
        stored_path = str(artifact)
    state["nodes"][args.node]["evidence"][args.key] = {
        "path": stored_path,
        "sha256": digest,
        "recorded_at": now(),
    }
    save_json(STATE_PATH, state)
    print(f"RECORDED: {args.node}:{args.key}")
    return 0


def command_complete(args: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    node_id = args.node
    require_node(node_id, graph)
    if state["nodes"][node_id]["status"] not in {"running", "awaiting_approval"}:
        raise HarnessError(f"node_not_running: {node_id}")

    complete, missing = evidence_complete(node_id, graph, state)
    if not complete:
        raise HarnessError(f"missing_required_evidence: {missing}")

    state["nodes"][node_id]["status"] = "complete"
    state["nodes"][node_id]["completed_at"] = now()
    state["current_node"] = None

    if node_id == graph["terminal_node"]:
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
    require_node(args.node, graph)
    state["nodes"][args.node]["status"] = "blocked"
    state["project_status"] = "blocked"
    state["current_node"] = args.node
    state["blockers"].append({
        "node_id": args.node,
        "reason": args.reason,
        "required_input": args.required_input,
        "created_at": now(),
    })
    save_json(STATE_PATH, state)
    print(f"BLOCKED: {args.node}")
    return 0


def command_interrupt(args: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    node_id = resolve_current_node(args, state)
    require_node(node_id, graph)
    status = state["nodes"][node_id]["status"]
    if status not in {"running", "awaiting_approval", "blocked"}:
        raise HarnessError(f"node_not_interruptible: {node_id}:{status}")

    _, missing = evidence_complete(node_id, graph, state)
    interruption = {
        "node_id": node_id,
        "reason": args.reason,
        "rejected_command": args.rejected_command,
        "missing_evidence": missing,
        "safe_resume_node": node_id,
        "preserved_changes": args.preserved_changes,
        "created_at": now(),
    }
    state.setdefault("interruptions", []).append(interruption)
    state["nodes"][node_id]["status"] = "interrupted"
    state["project_status"] = "interrupted"
    state["current_node"] = node_id
    save_json(STATE_PATH, state)
    print(json.dumps(interruption, ensure_ascii=False, indent=2))
    return 0


def command_reconcile(_: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    current = state.get("current_node")
    missing: list[str] = []
    if current:
        _, missing = evidence_complete(current, graph, state)
    payload = status_payload(graph, state)
    payload.update({
        "current_node_status": state["nodes"][current]["status"] if current else None,
        "missing_evidence": missing,
        "safe_resume_node": current if current and state["nodes"][current]["status"] in {"interrupted", "blocked"} else None,
    })
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


def command_resume(args: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    node_id = resolve_current_node(args, state)
    require_node(node_id, graph)
    status = state["nodes"][node_id]["status"]
    if status not in {"interrupted", "blocked"}:
        raise HarnessError(f"node_not_resumable: {node_id}:{status}")
    if not dependencies_complete(node_id, graph, state):
        raise HarnessError(f"dependencies_not_complete: {node_id}")

    state["nodes"][node_id]["status"] = "running"
    state["nodes"][node_id]["attempts"].append({
        "started_at": now(),
        "source_revision": args.revision,
        "iteration_budget": args.iteration_budget,
        "kind": "resume",
    })
    state["project_status"] = "running"
    state["current_node"] = node_id
    state["source_revision"] = args.revision
    save_json(STATE_PATH, state)
    print(f"RESUMED: {node_id}")
    return 0


def command_completion_check(_: argparse.Namespace) -> int:
    graph, state = graph_and_state()
    validate_graph(graph)
    validate_state(graph, state)
    terminal = graph["terminal_node"]
    terminal_complete = state["nodes"][terminal]["status"] == "complete"
    dependencies_complete_flag = all(
        state["nodes"][dependency]["status"] == "complete"
        for dependency in graph["nodes"][terminal].get("depends_on", [])
    )
    pass_gate = (
        terminal_complete
        and dependencies_complete_flag
        and state["project_status"] == "completed"
        and state["terminal_goal_satisfied"] is True
    )
    result = {
        "pass": pass_gate,
        "terminal_node": terminal,
        "terminal_node_complete": terminal_complete,
        "terminal_dependencies_complete": dependencies_complete_flag,
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
    sub.add_parser("reconcile").set_defaults(func=command_reconcile)

    start = sub.add_parser("start")
    start.add_argument("node")
    start.add_argument("--revision", required=True)
    start.add_argument("--iteration-budget", type=int, required=True)
    start.set_defaults(func=command_start)

    evidence = sub.add_parser("record-evidence")
    evidence.add_argument("node")
    evidence.add_argument("key")
    evidence.add_argument("artifact")
    evidence.set_defaults(func=command_record_evidence)

    complete = sub.add_parser("complete")
    complete.add_argument("node")
    complete.set_defaults(func=command_complete)

    block = sub.add_parser("block")
    block.add_argument("node")
    block.add_argument("--reason", required=True)
    block.add_argument("--required-input", default="")
    block.set_defaults(func=command_block)

    interrupt = sub.add_parser("interrupt")
    interrupt.add_argument("node", nargs="?")
    interrupt.add_argument("--reason", required=True)
    interrupt.add_argument("--rejected-command", default="")
    interrupt.add_argument("--preserved-changes", default="unknown")
    interrupt.set_defaults(func=command_interrupt)

    resume = sub.add_parser("resume")
    resume.add_argument("node", nargs="?")
    resume.add_argument("--revision", required=True)
    resume.add_argument("--iteration-budget", type=int, required=True)
    resume.set_defaults(func=command_resume)

    viewer = sub.add_parser("viewer")
    viewer.add_argument("--host", default="127.0.0.1")
    viewer.add_argument("--port", type=int, default=8765)
    viewer.add_argument("--no-open", action="store_true")
    viewer.set_defaults(func=command_viewer)

    sub.add_parser("completion-check").set_defaults(func=command_completion_check)
    return parser


def main() -> int:
    args = build_parser().parse_args()
    try:
        return int(args.func(args))
    except HarnessError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
