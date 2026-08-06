import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

MODULE_PATH = Path(__file__).resolve().parents[1] / "scripts" / "roadmap_harness.py"
SPEC = importlib.util.spec_from_file_location("roadmap_harness", MODULE_PATH)
assert SPEC and SPEC.loader
h = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(h)


class HarnessTests(unittest.TestCase):
    def setUp(self):
        self.graph = {
            "terminal_node": "done",
            "nodes": {
                "a": {"depends_on": [], "required_evidence": ["x"]},
                "b": {"depends_on": ["a"], "required_evidence": []},
                "done": {"depends_on": ["b"], "required_evidence": []},
            },
        }
        self.state = {
            "project_status": "proposed",
            "terminal_goal_satisfied": False,
            "nodes": {
                "a": {"status": "pending", "evidence": {}},
                "b": {"status": "pending", "evidence": {}},
                "done": {"status": "pending", "evidence": {}},
            },
        }

    def test_graph_validates(self):
        h.validate_graph(self.graph)

    def test_cycle_rejected(self):
        graph = copy.deepcopy(self.graph)
        graph["nodes"]["a"]["depends_on"] = ["b"]
        with self.assertRaises(h.HarnessError):
            h.validate_graph(graph)

    def test_dependency_controls_eligibility(self):
        self.assertEqual(h.eligible_nodes(self.graph, self.state), ["a"])
        self.state["nodes"]["a"]["status"] = "complete"
        self.assertEqual(h.eligible_nodes(self.graph, self.state), ["b"])

    def test_missing_evidence_blocks_completion(self):
        ok, missing = h.evidence_complete("a", self.graph, self.state)
        self.assertFalse(ok)
        self.assertEqual(missing, ["x"])

    def test_phase_completion_does_not_complete_project(self):
        self.state["nodes"]["a"]["status"] = "complete"
        self.state["project_status"] = "running"
        self.assertFalse(self.state["terminal_goal_satisfied"])


if __name__ == "__main__":
    unittest.main()
