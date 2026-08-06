#!/usr/bin/env python3
"""Local, dependency-free Graph Dashboard server for MyUnityMCP.

Serves only the dashboard assets plus a small read-only API for graph/state files.
It binds to localhost by default and performs no repository mutation.
"""
from __future__ import annotations

import argparse
import json
import mimetypes
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import threading
import time
import urllib.parse
import webbrowser

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
VIEWER_ROOT = Path(__file__).resolve().parent

GRAPH_FILES = {
    "implementation": REPOSITORY_ROOT / "graph" / "implementation-graph.json",
    "product-runtime": REPOSITORY_ROOT / "graph" / "product-runtime-graph.json",
}
STATE_FILE = REPOSITORY_ROOT / "state" / "roadmap-state.json"


def _load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def _snapshot(graph_id: str) -> dict:
    if graph_id not in GRAPH_FILES:
        raise KeyError(graph_id)

    graph = _load_json(GRAPH_FILES[graph_id])
    state = _load_json(STATE_FILE) if graph_id == "implementation" else None
    return {
        "graph_id": graph_id,
        "graph": graph,
        "state": state,
        "generated_at_unix_ms": int(time.time() * 1000),
        "read_only": True,
    }


class DashboardHandler(BaseHTTPRequestHandler):
    server_version = "MyUnityMCPGraphViewer/1.0"

    def log_message(self, format: str, *args) -> None:
        print(f"[GraphViewer] {self.address_string()} - {format % args}")

    def _send_json(self, payload: dict, status: HTTPStatus = HTTPStatus.OK) -> None:
        body = json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(body)

    def _send_file(self, path: Path) -> None:
        if not path.is_file() or VIEWER_ROOT not in path.parents:
            self.send_error(HTTPStatus.NOT_FOUND)
            return
        body = path.read_bytes()
        content_type = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
        if content_type.startswith("text/") or content_type in {"application/javascript", "application/json"}:
            content_type += "; charset=utf-8"
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:
        parsed = urllib.parse.urlparse(self.path)

        if parsed.path == "/api/health":
            self._send_json({"status": "ok", "read_only": True})
            return

        if parsed.path == "/api/snapshot":
            query = urllib.parse.parse_qs(parsed.query)
            graph_id = query.get("graph", ["implementation"])[0]
            try:
                self._send_json(_snapshot(graph_id))
            except KeyError:
                self._send_json(
                    {"error": "unknown_graph", "allowed": sorted(GRAPH_FILES)},
                    HTTPStatus.BAD_REQUEST,
                )
            except (OSError, json.JSONDecodeError) as exc:
                self._send_json(
                    {"error": "snapshot_load_failed", "detail": str(exc)},
                    HTTPStatus.INTERNAL_SERVER_ERROR,
                )
            return

        relative = "index.html" if parsed.path in {"", "/"} else parsed.path.lstrip("/")
        candidate = (VIEWER_ROOT / relative).resolve()
        if VIEWER_ROOT != candidate and VIEWER_ROOT not in candidate.parents:
            self.send_error(HTTPStatus.FORBIDDEN)
            return
        self._send_file(candidate)


def main() -> int:
    parser = argparse.ArgumentParser(description="MyUnityMCP Graph Dashboard")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--no-open", action="store_true")
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), DashboardHandler)
    url = f"http://{args.host}:{args.port}/"
    print(f"MyUnityMCP Graph Dashboard: {url}")
    print("Read-only. Press Ctrl+C to stop.")

    if not args.no_open:
        threading.Timer(0.4, lambda: webbrowser.open(url)).start()

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nGraph Dashboard stopped.")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
