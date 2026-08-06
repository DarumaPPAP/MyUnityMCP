import importlib.util
import json
from pathlib import Path
import unittest

MODULE_PATH = Path(__file__).resolve().parents[3] / "Tests/ExternalClient/run_mcp_http_e2e.py"
SPEC = importlib.util.spec_from_file_location("run_mcp_http_e2e", MODULE_PATH)
assert SPEC and SPEC.loader
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


class ExternalMcpE2eTests(unittest.TestCase):
    def test_parse_json_response(self):
        payload = {"jsonrpc": "2.0", "id": 1, "result": {"tools": []}}
        parsed = module.parse_response_body(
            "application/json",
            json.dumps(payload).encode("utf-8"),
        )
        self.assertEqual(parsed, payload)

    def test_parse_sse_response(self):
        body = (
            "event: message\n"
            "data: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"tools\":[]}}\n\n"
        ).encode("utf-8")
        parsed = module.parse_response_body("text/event-stream", body)
        self.assertEqual(parsed["result"]["tools"], [])

    def test_tool_names_ignore_invalid_items(self):
        result = module.find_tool_names(
            {
                "tools": [
                    {"name": "graphics.inspect_project"},
                    {"name": "agent.inspect_capabilities"},
                    {"description": "missing name"},
                    "invalid",
                ]
            }
        )
        self.assertEqual(
            result,
            {"graphics.inspect_project", "agent.inspect_capabilities"},
        )

    def test_endpoint_is_redacted(self):
        self.assertEqual(
            module.redact_endpoint("http://127.0.0.1:8090/mcp"),
            "http://loopback:<port>/mcp",
        )
        self.assertEqual(
            module.redact_endpoint("https://private.example.com/mcp"),
            "redacted_remote_endpoint",
        )


if __name__ == "__main__":
    unittest.main()
