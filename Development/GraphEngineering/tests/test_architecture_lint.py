import importlib.util
from pathlib import Path
import tempfile
import unittest

MODULE_PATH = Path(__file__).resolve().parents[1] / "scripts" / "architecture_lint.py"
SPEC = importlib.util.spec_from_file_location("architecture_lint", MODULE_PATH)
assert SPEC and SPEC.loader
lint_module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(lint_module)


class ArchitectureLintTests(unittest.TestCase):
    def test_repository_contracts_pass(self):
        repository_root = Path(__file__).resolve().parents[3]
        result = lint_module.lint(repository_root)
        self.assertTrue(result["passed"], result["violations"])
        self.assertEqual(result["discovered_tool_count"], 91)
        self.assertEqual(result["auto_register_disabled_count"], 91)
        self.assertEqual(result["manifest_tool_count"], 91)

    def test_creator_direct_mutation_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            editor = root / "Packages/com.darumappap.my-unity-mcp/Editor"
            editor.mkdir(parents=True)
            for name in lint_module.DEVELOPMENT_SOURCE_NAMES:
                text = ""
                if name == "UnityWorldCreatorMcp.cs":
                    text = "Undo.RecordObject(target, name);"
                (editor / name).write_text(text, encoding="utf-8")
            (root / "Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml").write_text(
                "development_candidate_tool_count: 0\n",
                encoding="utf-8",
            )

            result = lint_module.lint(root)

            reasons = [item["reason"] for item in result["violations"]]
            self.assertTrue(any("creator directly uses mutation API" in reason for reason in reasons))

    def test_reflection_is_rejected_in_development_module(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            editor = root / "Packages/com.darumappap.my-unity-mcp/Editor"
            editor.mkdir(parents=True)
            for name in lint_module.DEVELOPMENT_SOURCE_NAMES:
                text = "using System.Reflection;" if name == "UnityAgentMcpRuntime.cs" else ""
                (editor / name).write_text(text, encoding="utf-8")
            (root / "Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml").write_text(
                "development_candidate_tool_count: 0\n",
                encoding="utf-8",
            )

            result = lint_module.lint(root)

            reasons = [item["reason"] for item in result["violations"]]
            self.assertTrue(any("unapproved generic/internal mechanism" in reason for reason in reasons))


if __name__ == "__main__":
    unittest.main()
