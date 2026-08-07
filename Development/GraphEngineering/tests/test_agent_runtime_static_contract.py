import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
RUNTIME = ROOT / "Packages/com.darumappap.my-unity-mcp/Editor/UnityAgentMcpRuntime.cs"
MANIFEST = ROOT / "Development/GraphEngineering/state/source-implementation-manifest.json"


class AgentRuntimeStaticContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = RUNTIME.read_text(encoding="utf-8")
        cls.manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))

    def test_compile_graph_binds_to_actual_editor_revision(self):
        self.assertIn(
            "expectedRevision != UnityGraphicsMcpSession.Revision",
            self.source,
        )

    def test_start_execution_rechecks_actual_editor_revision(self):
        self.assertIn(
            "graph.expectedRevision != currentRevision || currentRevision != UnityGraphicsMcpSession.Revision",
            self.source,
        )

    def test_step_boundary_rechecks_revision_and_timeout(self):
        self.assertIn(
            "UnityGraphicsMcpSession.Revision != execution.expectedRevision",
            self.source,
        )
        self.assertIn("AGENT-EXECUTION-TIMEOUT", self.source)

    def test_execution_is_cooperative_and_cancellable(self):
        self.assertIn("EditorApplication.update += _instance.Tick", self.source)
        self.assertIn("Execution accepted and queued.", self.source)
        self.assertIn("E_AGENT_EXECUTION_STATUS.CANCELLED", self.source)
        self.assertIn("NotifyClientDisconnected", self.source)

    def test_approval_token_is_not_retained_in_plaintext(self):
        self.assertIn("approvalTokenHash", self.source)
        self.assertIn("HashToken(approvalToken)", self.source)
        self.assertNotIn("public string approvalToken;", self.source)

    def test_control_plane_has_no_direct_mutation_calls(self):
        for forbidden in (
            "Undo.RecordObject",
            "EditorUtility.SetDirty",
            "AssetDatabase.CreateAsset",
            "EditorSceneManager.SaveScene",
            "BuildPipeline.BuildPlayer",
        ):
            self.assertNotIn(forbidden, self.source)

    def test_source_manifest_does_not_claim_complete_before_validation(self):
        self.assertEqual(
            self.manifest["status"],
            "implementation_in_progress_validation_pending",
        )
        self.assertFalse(self.manifest["terminal_goal_satisfied"])
        self.assertNotIn(
            "implemented_unverified",
            [module["status"] for module in self.manifest["modules"].values()],
        )


if __name__ == "__main__":
    unittest.main()
