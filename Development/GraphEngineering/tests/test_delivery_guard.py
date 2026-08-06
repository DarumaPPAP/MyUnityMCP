import importlib.util
from pathlib import Path
import unittest

MODULE_PATH = Path(__file__).resolve().parents[1] / "scripts" / "delivery_guard.py"
SPEC = importlib.util.spec_from_file_location("delivery_guard", MODULE_PATH)
assert SPEC and SPEC.loader
guard = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(guard)


class DeliveryGuardTests(unittest.TestCase):
    def test_product_artifacts_are_allowed(self):
        paths, violations = guard.validate_changed_paths(
            [
                "Packages/com.darumappap.myunitymcp/Runtime/Feature.cs",
                "Packages/com.darumappap.myunitymcp/Tests/Editor/FeatureTests.cs",
                "README.md",
            ]
        )

        self.assertEqual(len(paths), 3)
        self.assertEqual(violations, [])

    def test_graph_engineering_directory_is_rejected(self):
        _, violations = guard.validate_changed_paths(
            ["Development/GraphEngineering/state/roadmap-state.json"]
        )

        self.assertEqual(len(violations), 1)
        self.assertIn("Graph Engineering", violations[0]["reason"])

    def test_graph_engineering_entrypoint_is_rejected(self):
        _, violations = guard.validate_changed_paths(["GRAPH_ENGINEERING.md"])

        self.assertEqual(len(violations), 1)

    def test_source_archive_is_rejected_anywhere(self):
        _, violations = guard.validate_changed_paths(
            ["Temporary/source-archive/master.zip.b64.001"]
        )

        self.assertEqual(len(violations), 1)

    def test_empty_delivery_does_not_pass(self):
        paths, violations = guard.validate_changed_paths([])

        result = guard.render_result(
            base="main",
            head="delivery/example",
            changed_paths=paths,
            violations=violations,
        )

        self.assertFalse(result["passed"])


if __name__ == "__main__":
    unittest.main()
