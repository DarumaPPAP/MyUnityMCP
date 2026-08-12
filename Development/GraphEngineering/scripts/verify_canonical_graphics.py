#!/usr/bin/env python3
import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
BASELINE_PATH = ROOT / "Development/GraphEngineering/migration/baseline-main.json"
CANONICAL_PATHS = [
    "Packages/com.darumappap.my-unity-mcp/Editor/Core",
    "Packages/com.darumappap.my-unity-mcp/Editor/Compatibility",
    "Packages/com.darumappap.my-unity-mcp/Editor/Inspection",
    "Packages/com.darumappap.my-unity-mcp/Editor/Planning",
    "Packages/com.darumappap.my-unity-mcp/Editor/Mutation",
    "Packages/com.darumappap.my-unity-mcp/Editor/Save",
    "Packages/com.darumappap.my-unity-mcp/Editor/Bake",
    "Packages/com.darumappap.my-unity-mcp/Editor/Capture",
    "Packages/com.darumappap.my-unity-mcp/Editor/Execution",
    "Packages/com.darumappap.my-unity-mcp/Editor/Tools",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Bake",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Capture",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Compatibility",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Core",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Execution",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Inspection",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Mutation",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Planning",
    "Packages/com.darumappap.my-unity-mcp/Tests/Editor/Save",
    "Specs/UnityGraphicsMCP",
    "Specs/Compatibility",
    "Tests/Compatibility",
    "Tests/Release",
    "skills/myunitymcp-unity-api-compatibility",
    "Catalog/mcp-catalog.yaml",
    "Catalog/capability-catalog.yaml",
    "Catalog/capability-contracts.yaml",
    "Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml",
]


def main():
    baseline = json.loads(BASELINE_PATH.read_text(encoding="utf-8"))
    baseline_sha = baseline["head_sha"]
    command = ["git", "diff", "--name-only", baseline_sha, "HEAD", "--", *CANONICAL_PATHS]
    changed = subprocess.check_output(command, cwd=ROOT, text=True).splitlines()
    result = {
        "baseline_sha": baseline_sha,
        "canonical_paths": len(CANONICAL_PATHS),
        "changed": changed,
        "status": "pass" if not changed else "failed",
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not changed else 1


if __name__ == "__main__":
    sys.exit(main())
