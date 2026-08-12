#!/usr/bin/env python3
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
PACKAGE = ROOT / "Packages/com.darumappap.my-unity-mcp"
DEV_SOURCE = PACKAGE / "Editor/Development"
DEV_TESTS = PACKAGE / "Tests/Editor/Development"
PRODUCTION_FILES = [
    ROOT / "Catalog/mcp-catalog.yaml",
    PACKAGE / "MCP_MANIFEST.yaml",
]
FORBIDDEN_PRODUCTION_MARKERS = [
    "release_plus_development",
    "development_modules:",
    "unity_profiler_mcp:",
    "unity_build_mcp:",
    "unity_addressables_mcp:",
    "unity_ui_mcp:",
    "unity_animation_mcp:",
    "unity_audio_mcp:",
    "unity_cinematic_mcp:",
    "world_creator:",
    "movie_creator:",
    "live_creator:",
]


def package_meta_errors(root):
    errors = []
    for path in root.rglob("*"):
        if path.name.endswith(".meta"):
            continue
        if path.is_dir():
            meta = pathlib.Path(str(path) + ".meta")
            if not meta.exists():
                errors.append(f"missing folder meta: {path.relative_to(ROOT)}")
        elif path.suffix in {".cs", ".json", ".asmdef"}:
            meta = pathlib.Path(str(path) + ".meta")
            if not meta.exists():
                errors.append(f"missing asset meta: {path.relative_to(ROOT)}")
    return errors


def main():
    errors = []
    for path in PRODUCTION_FILES:
        text = path.read_text(encoding="utf-8")
        for marker in FORBIDDEN_PRODUCTION_MARKERS:
            if marker in text:
                errors.append(f"production contamination: {path.relative_to(ROOT)} contains {marker}")

    manifest = (PACKAGE / "MCP_MANIFEST.yaml").read_text(encoding="utf-8")
    if "id: unity_agent_mcp" not in manifest and "unity_agent_mcp" not in manifest:
        errors.append("promoted unity_agent_mcp missing from production manifest")

    if not DEV_SOURCE.exists():
        errors.append("development source root missing")
    if not DEV_TESTS.exists():
        errors.append("development test root missing")
    errors.extend(package_meta_errors(DEV_SOURCE))
    errors.extend(package_meta_errors(DEV_TESTS))

    compatibility_root = DEV_SOURCE / "Compatibility"
    temporary_blockers = [
        path.stem
        for path in sorted(compatibility_root.glob("*MigrationBridge.cs"))
    ] if compatibility_root.exists() else []
    if (PACKAGE / "Editor/UnityAgentMcpCatalog.json").exists():
        temporary_blockers.append("root Agent catalog compatibility copy")

    result = {
        "status": "pass" if not errors else "failed",
        "errors": errors,
        "promoted_production_capabilities": ["unity_agent_mcp"],
        "temporary_promotion_blockers": temporary_blockers,
        "promotion_ready": not errors and not temporary_blockers,
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
