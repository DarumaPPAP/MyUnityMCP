#!/usr/bin/env python3
"""Run an external MCP HTTP client E2E against a running Unity Editor bridge."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import pathlib
import sys
import urllib.error
import urllib.request
import uuid
from dataclasses import dataclass
from typing import Any, Iterable, Sequence

PROTOCOL_VERSION = "2025-03-26"
DEFAULT_ENDPOINT = "http://127.0.0.1:8090/mcp"


class McpE2eError(RuntimeError):
    pass


@dataclass
class McpResponse:
    payload: dict[str, Any]
    session_id: str | None


def parse_response_body(content_type: str, body: bytes) -> dict[str, Any]:
    text = body.decode("utf-8", errors="replace").strip()
    if not text:
        return {}
    if "text/event-stream" in (content_type or "").lower() or text.startswith("event:") or "\ndata:" in text:
        payloads: list[dict[str, Any]] = []
        for line in text.splitlines():
            if not line.startswith("data:"):
                continue
            value = line[5:].strip()
            if not value or value == "[DONE]":
                continue
            decoded = json.loads(value)
            if isinstance(decoded, dict):
                payloads.append(decoded)
        if not payloads:
            raise McpE2eError("SSE response did not contain JSON data")
        for payload in payloads:
            if "result" in payload or "error" in payload:
                return payload
        return payloads[-1]
    decoded = json.loads(text)
    if not isinstance(decoded, dict):
        raise McpE2eError("MCP response was not a JSON object")
    return decoded


class McpHttpClient:
    def __init__(self, endpoint: str, timeout_seconds: float) -> None:
        self.endpoint = endpoint
        self.timeout_seconds = timeout_seconds
        self.session_id: str | None = None
        self._next_id = 1

    def request(self, method: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
        request_id = self._next_id
        self._next_id += 1
        payload = {
            "jsonrpc": "2.0",
            "id": request_id,
            "method": method,
            "params": params or {},
        }
        response = self._post(payload)
        if response.payload.get("id") not in (request_id, None):
            raise McpE2eError(f"MCP response id mismatch for {method}")
        if "error" in response.payload:
            raise McpE2eError(f"{method} failed: {json.dumps(response.payload['error'], ensure_ascii=False)}")
        result = response.payload.get("result")
        if not isinstance(result, dict):
            raise McpE2eError(f"{method} did not return an object result")
        return result

    def notify(self, method: str, params: dict[str, Any] | None = None) -> None:
        self._post({"jsonrpc": "2.0", "method": method, "params": params or {}})

    def _post(self, payload: dict[str, Any]) -> McpResponse:
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
            "MCP-Protocol-Version": PROTOCOL_VERSION,
        }
        if self.session_id:
            headers["Mcp-Session-Id"] = self.session_id
        request = urllib.request.Request(
            self.endpoint,
            data=json.dumps(payload).encode("utf-8"),
            headers=headers,
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=self.timeout_seconds) as response:
                body = response.read()
                content_type = response.headers.get("Content-Type", "")
                returned_session = response.headers.get("Mcp-Session-Id") or response.headers.get("MCP-Session-Id")
        except urllib.error.HTTPError as error:
            body = error.read().decode("utf-8", errors="replace")
            raise McpE2eError(f"HTTP {error.code}: {body[:1000]}") from error
        except urllib.error.URLError as error:
            raise McpE2eError(f"MCP endpoint is unavailable: {error.reason}") from error
        if returned_session:
            self.session_id = returned_session
        return McpResponse(parse_response_body(content_type, body), self.session_id)


def find_tool_names(tools_result: dict[str, Any]) -> set[str]:
    tools = tools_result.get("tools")
    if not isinstance(tools, list):
        raise McpE2eError("tools/list result did not contain a tools array")
    return {
        item.get("name")
        for item in tools
        if isinstance(item, dict) and isinstance(item.get("name"), str)
    }


def redact_endpoint(endpoint: str) -> str:
    if endpoint.startswith("http://127.0.0.1:") or endpoint.startswith("http://localhost:"):
        return "http://loopback:<port>/mcp"
    return "redacted_remote_endpoint"


def run(endpoint: str, timeout_seconds: float, expected_tools: Iterable[str]) -> dict[str, Any]:
    client = McpHttpClient(endpoint, timeout_seconds)
    initialized = client.request(
        "initialize",
        {
            "protocolVersion": PROTOCOL_VERSION,
            "capabilities": {},
            "clientInfo": {"name": "myunitymcp-external-e2e", "version": "1.0"},
        },
    )
    client.notify("notifications/initialized")
    tools_result = client.request("tools/list")
    discovered = find_tool_names(tools_result)
    required = set(expected_tools)
    missing = sorted(required - discovered)
    if missing:
        raise McpE2eError(f"Required tools were not discovered: {missing}")

    calls: list[dict[str, Any]] = []
    for tool_name in sorted(required):
        arguments: dict[str, Any] = {}
        if tool_name == "agent.inspect_capabilities":
            arguments = {}
        elif tool_name == "graphics.inspect_project":
            arguments = {"requestedPlatforms": [], "requestedConstraints": []}
        else:
            continue
        result = client.request("tools/call", {"name": tool_name, "arguments": arguments})
        calls.append(
            {
                "tool": tool_name,
                "isError": bool(result.get("isError", False)),
                "contentItemCount": len(result.get("content", [])) if isinstance(result.get("content"), list) else 0,
            }
        )
        if result.get("isError"):
            raise McpE2eError(f"Read-only tool call failed: {tool_name}")

    return {
        "evidence_id": f"external-mcp-e2e-{uuid.uuid4().hex}",
        "kind": "external_mcp_client_e2e",
        "created_at": dt.datetime.now(dt.timezone.utc).isoformat(),
        "endpoint": redact_endpoint(endpoint),
        "protocol_version": initialized.get("protocolVersion"),
        "server_info": initialized.get("serverInfo", {}),
        "discovered_tool_count": len(discovered),
        "required_tools": sorted(required),
        "missing_tools": missing,
        "read_only_calls": calls,
        "security_mode": "CI",
        "credentials_collected": False,
        "project_paths_collected": False,
        "verdict": "pass",
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--endpoint", default=DEFAULT_ENDPOINT)
    parser.add_argument("--timeout-seconds", type=float, default=30.0)
    parser.add_argument(
        "--expected-tool",
        action="append",
        dest="expected_tools",
        default=[],
        help="Required tool name. Repeat for multiple tools.",
    )
    parser.add_argument("--output", required=True)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    required = args.expected_tools or ["graphics.inspect_project", "agent.inspect_capabilities"]
    try:
        evidence = run(args.endpoint, args.timeout_seconds, required)
    except (McpE2eError, ValueError, OSError) as error:
        evidence = {
            "kind": "external_mcp_client_e2e",
            "created_at": dt.datetime.now(dt.timezone.utc).isoformat(),
            "endpoint": redact_endpoint(args.endpoint),
            "required_tools": sorted(required),
            "security_mode": "CI",
            "credentials_collected": False,
            "project_paths_collected": False,
            "verdict": "fail",
            "error": str(error),
        }
        exit_code = 1
    else:
        exit_code = 0

    output = pathlib.Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(evidence, ensure_ascii=False, indent=2))
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
