import json
import pathlib
import subprocess
import sys
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[3]


class StateReducerTests(unittest.TestCase):
    def test_state_reducer_rejects_stale_pass_evidence(self):
        result = subprocess.run(
            [sys.executable, "Development/GraphEngineering/scripts/state_reducer.py", "--check"],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        payload = json.loads(result.stdout)
        self.assertFalse(payload["terminal_goal_satisfied"])

    def test_roadmap_does_not_claim_terminal_completion(self):
        state = json.loads((ROOT / "Development/GraphEngineering/state/roadmap-state.json").read_text(encoding="utf-8"))
        self.assertFalse(state["terminalGoalSatisfied"])
        self.assertEqual(state["nodes"]["promotion_gate"], "blocked")


if __name__ == "__main__":
    unittest.main()
