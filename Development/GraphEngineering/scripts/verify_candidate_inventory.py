#!/usr/bin/env python3
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
CATALOG = ROOT / "Development/GraphEngineering/catalog/development-mcp-catalog.yaml"
SOURCE_ROOT = ROOT / "Packages/com.darumappap.my-unity-mcp/Editor/Development"
EXPECTED_CANDIDATE_TOOLS = 59
EXPECTED_COMBINED_TOOLS = 91


def main():
    text = CATALOG.read_text(encoding="utf-8")
    candidate_match = re.search(r"^candidate_tool_count:\s*(\d+)\s*$", text, re.MULTILINE)
    combined_match = re.search(r"^combined_discovery_target_with_graphics:\s*(\d+)\s*$", text, re.MULTILINE)
    cs_files = sorted(SOURCE_ROOT.rglob("*.cs"))
    required = {
        "Agent", "Profiler", "Build", "Addressables", "UI", "Animation", "Audio", "Cinematic", "Creators", "Security", "Shared"
    }
    present = {path.parent.name for path in cs_files}
    missing = sorted(required - present)
    candidate = int(candidate_match.group(1)) if candidate_match else -1
    combined = int(combined_match.group(1)) if combined_match else -1
    result = {
        "candidate_tool_count": candidate,
        "combined_tool_target": combined,
        "candidate_source_files": len(cs_files),
        "missing_module_roots": missing,
        "status": "pass" if candidate == EXPECTED_CANDIDATE_TOOLS and combined == EXPECTED_COMBINED_TOOLS and not missing else "failed",
        "note": "Static inventory sanity only; runtime MCP tool discovery still requires Unity/External E2E evidence."
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["status"] == "pass" else 1


if __name__ == "__main__":
    sys.exit(main())
