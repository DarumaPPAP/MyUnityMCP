from __future__ import annotations

import ast
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
RUNNER = ROOT / "scripts" / "run_minimal_live_smoke.py"


class MinimalSmokeContractTests(unittest.TestCase):
    def test_runner_is_valid_python_and_has_no_eval_call(self):
        source = RUNNER.read_text(encoding="utf-8")
        tree = ast.parse(source)
        calls = [node for node in ast.walk(tree) if isinstance(node, ast.Call)]
        called_names = {
            node.func.id
            for node in calls
            if isinstance(node.func, ast.Name)
        }
        self.assertIn("command", called_names)
        self.assertNotIn("eval", called_names)

    def test_runner_covers_the_minimal_artist_lifecycle(self):
        source = RUNNER.read_text(encoding="utf-8")
        for command in (
            "artist.inspect",
            "artist.plan",
            "artist.preview",
            "artist.apply",
            "artist.capture",
            "artist.evaluate",
            "artist.refine",
            "artist.history",
        ):
            self.assertIn(f'"{command}"', source)
        self.assertIn("official_unity_cli_pipeline", source)
        self.assertIn("mutation_evidence", source)
        self.assertIn("undo_registration", source)
        self.assertIn("save_not_performed", source)
        self.assertIn("PNG", source)


if __name__ == "__main__":
    unittest.main()
