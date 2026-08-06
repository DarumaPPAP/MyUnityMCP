import importlib.util
from pathlib import Path
import unittest

MODULE_PATH = Path(__file__).resolve().parents[1] / "scripts" / "release_candidate_gate.py"
SPEC = importlib.util.spec_from_file_location("release_candidate_gate", MODULE_PATH)
assert SPEC and SPEC.loader
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


def passing_gates():
    return {
        "gates": {
            name: {"status": "pass", "evidence": f"evidence/{name}.json"}
            for name in module.REQUIRED_GATES
        }
    }


def roadmap(phase12="complete", completion_gate="pending", terminal=False):
    return {
        "terminal_goal_satisfied": terminal,
        "nodes": {
            "phase_12_production_hardening": {"status": phase12},
            "project_completion_gate": {"status": completion_gate},
        },
    }


def delivery(paths=None, base="main"):
    return {
        "base_branch": base,
        "include_paths": paths or ["Packages/com.darumappap.my-unity-mcp/Editor/Product.cs"],
    }


class ReleaseCandidateGateTests(unittest.TestCase):
    def test_all_gates_and_safe_delivery_pass(self):
        result = module.evaluate(passing_gates(), roadmap(), delivery())
        self.assertTrue(result["passed"], result["blockers"])
        self.assertTrue(result["release_candidate_ready"])
        self.assertFalse(result["terminal_goal_satisfied"])

    def test_pending_gate_blocks(self):
        gates = passing_gates()
        gates["gates"]["unity_editor_ci"] = {"status": "pending", "evidence": None}
        result = module.evaluate(gates, roadmap(), delivery())
        self.assertFalse(result["passed"])
        self.assertTrue(any(item["gate"] == "unity_editor_ci" for item in result["blockers"]))

    def test_passing_gate_without_evidence_blocks(self):
        gates = passing_gates()
        gates["gates"]["security_modes"] = {"status": "pass", "evidence": None}
        result = module.evaluate(gates, roadmap(), delivery())
        self.assertFalse(result["passed"])
        self.assertTrue(any(item["gate"] == "security_modes" for item in result["blockers"]))

    def test_graph_engineering_path_is_rejected(self):
        result = module.evaluate(
            passing_gates(),
            roadmap(),
            delivery(["Development/GraphEngineering/state/roadmap-state.json"]),
        )
        self.assertFalse(result["passed"])
        self.assertTrue(any("development-only path" in item["reason"] for item in result["blockers"]))

    def test_non_main_delivery_base_is_rejected(self):
        result = module.evaluate(passing_gates(), roadmap(), delivery(base="feature/graph-engineering-master"))
        self.assertFalse(result["passed"])
        self.assertTrue(any("delivery base must be main" in item["reason"] for item in result["blockers"]))

    def test_phase12_must_be_complete(self):
        result = module.evaluate(passing_gates(), roadmap(phase12="running"), delivery())
        self.assertFalse(result["passed"])
        self.assertTrue(any(item["gate"] == "phase_12_production_hardening" for item in result["blockers"]))

    def test_terminal_goal_cannot_be_predeclared(self):
        result = module.evaluate(passing_gates(), roadmap(terminal=True), delivery())
        self.assertFalse(result["passed"])
        self.assertTrue(any(item["gate"] == "terminal_goal_satisfied" for item in result["blockers"]))


if __name__ == "__main__":
    unittest.main()
