import json
import pathlib
import subprocess
import sys
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[3]


class ModernizationStaticTests(unittest.TestCase):
    def test_canonical_production_matches_current_main_baseline(self):
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

    def test_terminal_goal_records_delivery_promotion_readiness(self):
        state = json.loads((ROOT / "Development/GraphEngineering/state/roadmap-state.json").read_text(encoding="utf-8"))
        self.assertTrue(state["terminalGoalSatisfied"])
        self.assertFalse(state["blockers"])
        self.assertEqual(state["productionToolCount"], 42)
        self.assertEqual(state["remainingDevelopmentToolCount"], 49)
        self.assertEqual(state["finalCombinedTarget"], 91)
        self.assertEqual(state["nextRecommendedCapability"], "world_creator")


if __name__ == "__main__":
    unittest.main()
