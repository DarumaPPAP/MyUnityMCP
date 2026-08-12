#!/usr/bin/env python3
import argparse
import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
STATE_PATH = ROOT / "Development/GraphEngineering/state/roadmap-state.json"
EVIDENCE_ROOT = ROOT / "Development/GraphEngineering/state/evidence"
PASS_STATUSES = {"pass", "passed", "success", "complete", "integration_verified", "manual_verified"}
NON_PASS_STATUSES = {"blocked", "unavailable", "not_verified", "awaiting_approval", "failed"}


def current_head():
    return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()


def load_evidence():
    items = []
    if not EVIDENCE_ROOT.exists():
        return items
    for path in sorted(EVIDENCE_ROOT.rglob("*.json")):
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        if isinstance(payload, dict):
            payload["_path"] = str(path.relative_to(ROOT))
            items.append(payload)
    return items


def source_is_applicable(source_revision, head, validated_paths):
    if not source_revision:
        return False, "missing_source_revision"
    if source_revision == head:
        return True, "exact_revision"
    if not isinstance(validated_paths, list) or not validated_paths:
        return False, "stale_revision"
    if not all(isinstance(path, str) and path.strip() for path in validated_paths):
        return False, "invalid_validated_paths"

    ancestor = subprocess.run(
        ["git", "merge-base", "--is-ancestor", source_revision, head],
        cwd=ROOT,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    if ancestor.returncode != 0:
        return False, "source_revision_not_ancestor"

    diff = subprocess.run(
        ["git", "diff", "--quiet", source_revision, head, "--", *validated_paths],
        cwd=ROOT,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    if diff.returncode == 0:
        return True, "validated_paths_unchanged"
    if diff.returncode == 1:
        return False, "validated_source_changed"
    return False, "source_comparison_failed"


def reduce_state(state, evidence, head):
    reduced = json.loads(json.dumps(state))
    nodes = reduced.setdefault("nodes", {})
    accepted = []
    rejected = []

    for item in evidence:
        node = item.get("node")
        status = str(item.get("status") or item.get("verdict") or item.get("verification") or "").lower()
        source_revision = item.get("source_revision")
        if not node or node not in nodes:
            continue
        if status in PASS_STATUSES:
            applicable, reason = source_is_applicable(source_revision, head, item.get("validated_paths"))
            if not applicable:
                rejected.append({"node": node, "reason": reason, "evidence": item.get("_path")})
                continue
            nodes[node] = "complete"
            accepted.append({
                "node": node,
                "status": "complete",
                "sourceRevision": source_revision,
                "applicability": reason,
                "evidence": item.get("_path"),
            })
        elif status in NON_PASS_STATUSES:
            nodes[node] = status
            accepted.append({"node": node, "status": status, "evidence": item.get("_path")})

    reduced["evidenceReduction"] = {
        "recordRevision": head,
        "accepted": accepted,
        "rejected": rejected,
    }
    return reduced


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    state = json.loads(STATE_PATH.read_text(encoding="utf-8"))
    head = current_head()
    reduced = reduce_state(state, load_evidence(), head)

    if args.write:
        STATE_PATH.write_text(json.dumps(reduced, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    rejected_passes = reduced.get("evidenceReduction", {}).get("rejected", [])
    result = {
        "head": head,
        "accepted_evidence": len(reduced.get("evidenceReduction", {}).get("accepted", [])),
        "rejected_pass_evidence": len(rejected_passes),
        "terminal_goal_satisfied": bool(reduced.get("terminalGoalSatisfied")),
        "status": "pass",
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))

    if args.check and reduced.get("terminalGoalSatisfied") and state.get("blockers"):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
