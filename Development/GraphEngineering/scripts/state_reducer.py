#!/usr/bin/env python3
import argparse
import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
STATE_PATH = ROOT / "Development/GraphEngineering/state/roadmap-state.json"
EVIDENCE_ROOT = ROOT / "Development/GraphEngineering/state/evidence"
PASS_STATUSES = {"pass", "passed", "success", "complete", "integration_verified"}
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
            if source_revision != head:
                rejected.append({"node": node, "reason": "stale_revision", "evidence": item.get("_path")})
                continue
            nodes[node] = "complete"
            accepted.append({"node": node, "status": "complete", "evidence": item.get("_path")})
        elif status in NON_PASS_STATUSES:
            nodes[node] = status
            accepted.append({"node": node, "status": status, "evidence": item.get("_path")})

    reduced["evidenceReduction"] = {
        "sourceRevision": head,
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

    stale_passes = reduced.get("evidenceReduction", {}).get("rejected", [])
    result = {
        "head": head,
        "accepted_evidence": len(reduced.get("evidenceReduction", {}).get("accepted", [])),
        "stale_pass_evidence_rejected": len(stale_passes),
        "terminal_goal_satisfied": bool(reduced.get("terminalGoalSatisfied")),
        "status": "pass",
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))

    if args.check and reduced.get("terminalGoalSatisfied") and state.get("blockers"):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
