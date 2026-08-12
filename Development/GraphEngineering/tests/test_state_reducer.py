import importlib.util
import json
import pathlib
import subprocess
import sys
import unittest
from unittest import mock

ROOT = pathlib.Path(__file__).resolve().parents[3]
REDUCER_PATH = ROOT / "Development/GraphEngineering/scripts/state_reducer.py"

spec = importlib.util.spec_from_file_location("graph_state_reducer", REDUCER_PATH)
state_reducer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(state_reducer)


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

    def test_scoped_evidence_can_survive_record_only_commit(self):
        state = {"nodes": {"multi_version_editor_matrix": "not_verified"}}
        evidence = [{
            "node": "multi_version_editor_matrix",
            "status": "pass",
            "source_revision": "validated-sha",
            "validated_paths": ["Packages/com.darumappap.my-unity-mcp"],
            "_path": "evidence.json",
        }]
        with mock.patch.object(
            state_reducer,
            "source_is_applicable",
            return_value=(True, "validated_paths_unchanged"),
        ):
            reduced = state_reducer.reduce_state(state, evidence, "record-sha")

        self.assertEqual(reduced["nodes"]["multi_version_editor_matrix"], "complete")
        self.assertEqual(reduced["evidenceReduction"]["accepted"][0]["sourceRevision"], "validated-sha")
        self.assertEqual(reduced["evidenceReduction"]["accepted"][0]["applicability"], "validated_paths_unchanged")

    def test_scoped_evidence_is_rejected_when_validated_source_changed(self):
        state = {"nodes": {"multi_version_editor_matrix": "not_verified"}}
        evidence = [{
            "node": "multi_version_editor_matrix",
            "status": "pass",
            "source_revision": "validated-sha",
            "validated_paths": ["Packages/com.darumappap.my-unity-mcp"],
            "_path": "evidence.json",
        }]
        with mock.patch.object(
            state_reducer,
            "source_is_applicable",
            return_value=(False, "validated_source_changed"),
        ):
            reduced = state_reducer.reduce_state(state, evidence, "record-sha")

        self.assertEqual(reduced["nodes"]["multi_version_editor_matrix"], "not_verified")
        self.assertEqual(reduced["evidenceReduction"]["rejected"][0]["reason"], "validated_source_changed")

    def test_legacy_pass_without_validated_paths_stays_strict(self):
        state = {"nodes": {"multi_version_editor_matrix": "not_verified"}}
        evidence = [{
            "node": "multi_version_editor_matrix",
            "status": "pass",
            "source_revision": "old-sha",
            "_path": "legacy.json",
        }]
        with mock.patch.object(
            state_reducer,
            "source_is_applicable",
            return_value=(False, "stale_revision"),
        ):
            reduced = state_reducer.reduce_state(state, evidence, "new-sha")

        self.assertEqual(reduced["nodes"]["multi_version_editor_matrix"], "not_verified")
        self.assertEqual(reduced["evidenceReduction"]["rejected"][0]["reason"], "stale_revision")

    def test_roadmap_does_not_claim_terminal_completion(self):
        state = json.loads((ROOT / "Development/GraphEngineering/state/roadmap-state.json").read_text(encoding="utf-8"))
        self.assertFalse(state["terminalGoalSatisfied"])
        self.assertEqual(state["nodes"]["promotion_gate"], "blocked")


if __name__ == "__main__":
    unittest.main()
