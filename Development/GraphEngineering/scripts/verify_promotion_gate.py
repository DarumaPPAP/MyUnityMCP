#!/usr/bin/env python3
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
STATE_PATH = ROOT / "Development/GraphEngineering/state/roadmap-state.json"


def main():
    state = json.loads(STATE_PATH.read_text(encoding="utf-8"))
    blockers = list(state.get("blockers", []))
    nodes = state.get("nodes", {})
    incomplete = {
        name: value
        for name, value in nodes.items()
        if value not in {"complete", "source_complete", "integration_verified"}
    }
    terminal = bool(state.get("terminalGoalSatisfied"))
    result = {
        "terminal_goal_satisfied": terminal,
        "blockers": blockers,
        "incomplete_nodes": incomplete,
        "status": "pass" if terminal and not blockers and not incomplete else "blocked",
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["status"] == "pass" else 2


if __name__ == "__main__":
    sys.exit(main())
