#!/usr/bin/env python3
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
CATALOG = ROOT / "Development/GraphEngineering/catalog/development-mcp-catalog.yaml"
SOURCE_ROOT = ROOT / "Packages/com.darumappap.my-unity-mcp/Editor/Development"
EXPECTED_PRODUCTION_TOOLS = 42
EXPECTED_CANDIDATE_TOOLS = 49
EXPECTED_COMBINED_TOOLS = 91


def main():
    text = CATALOG.read_text(encoding="utf-8")
    production_match = re.search(r"^production_tool_count:\s*(\d+)\s*$", text, re.MULTILINE)
    candidate_match = re.search(r"^candidate_tool_count:\s*(\d+)\s*$", text, re.MULTILINE)
    combined_match = re.search(r"^combined_discovery_target_with_production:\s*(\d+)\s*$", text, re.MULTILINE)
    cs_files = sorted(SOURCE_ROOT.rglob("*.cs"))
    required_remaining = {
        "Profiler", "Build", "Addressables", "UI", "Animation", "Audio", "Cinematic", "Creators", "Security", "Shared"
    }
    present = {path.parent.name for path in cs_files}
    missing = sorted(required_remaining - present)
    production = int(production_match.group(1)) if production_match else -1
    candidate = int(candidate_match.group(1)) if candidate_match else -1
    combined = int(combined_match.group(1)) if combined_match else -1
    agent_source_present = any("/Agent/" in path.as_posix() for path in cs_files)

    status = (
        production == EXPECTED_PRODUCTION_TOOLS
        and candidate == EXPECTED_CANDIDATE_TOOLS
        and combined == EXPECTED_COMBINED_TOOLS
        and not missing
        and agent_source_present
    )
    result = {
        "production_tool_count": production,
        "remaining_candidate_tool_count": candidate,
        "combined_tool_target": combined,
        "development_source_files": len(cs_files),
        "promoted_agent_source_present": agent_source_present,
        "missing_remaining_module_roots": missing,
        "status": "pass" if status else "failed",
        "note": "Agent source remains physically shared for Graph integration but is counted in the 42-tool Production baseline, not in the 49 remaining candidate tools."
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["status"] == "pass" else 1


if __name__ == "__main__":
    sys.exit(main())
