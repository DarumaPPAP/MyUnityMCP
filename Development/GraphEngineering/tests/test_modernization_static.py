import json
import pathlib
import subprocess
import sys
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[3]


class ModernizationStaticTests(unittest.TestCase):
    def test_canonical_graphics_matches_main_baseline(self):
        result = subprocess.run(
            [sys.executable, "Development/GraphEngineering/scripts/verify_canonical_graphics.py"],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_production_and_development_are_separated(self):
        result = subprocess.run(
            [sys.executable, "Development/GraphEngineering/scripts/verify_development_separation.py"],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_terminal_goal_is_not_falsely_marked_complete(self):
        state = json.loads((ROOT / "Development/GraphEngineering/state/roadmap-state.json").read_text(encoding="utf-8"))
        self.assertFalse(state["terminalGoalSatisfied"])
        self.assertTrue(state["blockers"])


if __name__ == "__main__":
    unittest.main()
