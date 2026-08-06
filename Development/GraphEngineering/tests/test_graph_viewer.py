import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "tools" / "graph-viewer" / "server.py"
SPEC = importlib.util.spec_from_file_location("graph_viewer_server", MODULE_PATH)
assert SPEC and SPEC.loader
viewer = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(viewer)


class GraphViewerTests(unittest.TestCase):
    def test_implementation_snapshot_contains_state(self):
        snapshot = viewer._snapshot("implementation")
        self.assertEqual(snapshot["graph_id"], "implementation")
        self.assertIsInstance(snapshot["state"], dict)
        self.assertIn("nodes", snapshot["graph"])

    def test_product_snapshot_is_separate_from_roadmap_state(self):
        snapshot = viewer._snapshot("product-runtime")
        self.assertEqual(snapshot["graph_id"], "product-runtime")
        self.assertIsNone(snapshot["state"])
        self.assertEqual(snapshot["graph"]["graph_id"], "myunitymcp_product_runtime")

    def test_unknown_graph_is_rejected(self):
        with self.assertRaises(KeyError):
            viewer._snapshot("unknown")

    def test_viewer_assets_exist(self):
        for name in ["index.html", "styles.css", "app.js"]:
            self.assertTrue((ROOT / "tools" / "graph-viewer" / name).is_file())

    def test_product_runtime_terminal_exists(self):
        graph = json.loads((ROOT / "graph" / "product-runtime-graph.json").read_text(encoding="utf-8"))
        self.assertIn(graph["terminal_node"], graph["nodes"])

    def test_visualize_dashboard_is_canonical_ui(self):
        live = (ROOT / "tools" / "graph-viewer" / "index.html").read_text(encoding="utf-8")
        static = (ROOT / "visualize" / "MyUnityMCP_GraphDashboard.html").read_text(encoding="utf-8")
        for marker in ["Codex実装グラフ", "製品Runtimeグラフ", "Node検索", "次の遷移条件"]:
            self.assertIn(marker, live)
            self.assertIn(marker, static)

    def test_visualize_static_snapshot_contains_graph_state(self):
        static = (ROOT / "visualize" / "MyUnityMCP_GraphDashboard.html").read_text(encoding="utf-8")
        self.assertIn("__MYUNITYMCP_STATIC_SNAPSHOTS__", static)
        self.assertIn("bootstrap_development_harness", static)


if __name__ == "__main__":
    unittest.main()
