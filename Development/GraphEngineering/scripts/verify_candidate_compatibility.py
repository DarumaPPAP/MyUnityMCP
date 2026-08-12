#!/usr/bin/env python3
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
SOURCE_ROOT = ROOT / "Packages/com.darumappap.my-unity-mcp/Editor/Development"

BLOCKING_PATTERNS = {
    "instance_id": re.compile(r"\b(GetInstanceID|InstanceIDToObject|objectReferenceInstanceIDValue)\b"),
    "version_if_scattered": re.compile(r"^\s*#\s*if\s+UNITY_(?:6000|64)\b", re.MULTILINE),
    "development_build": re.compile(r"\bDEVELOPMENT_BUILD\b"),
    "legacy_uxml": re.compile(r"\b(UxmlFactory|UxmlTraits)\b"),
    "legacy_importer": re.compile(r"\b(isFileScaleUsed|normalImportMode|optimizeMesh)\b"),
    "legacy_entities": re.compile(r"\bEntities\.ForEach\b|\.WithCode\s*\(|\bIAspect\b"),
    "legacy_graphics_internal_alias": re.compile(
        r"\b(UnityGraphicsMcpSession|GraphicsInspectProjectTool|GraphicsInspectSceneTool|"
        r"GraphicsValidateSceneTool|GraphicsGetExecutionHistoryTool|GraphicsGetErrorCatalogTool|"
        r"GraphicsGetSupportMatrixTool)\b"
    ),
}

OBSERVATION_PATTERNS = {
    "scriptable_render_pass": re.compile(r"\bScriptableRenderPass\b"),
    "render_graph": re.compile(r"\bRenderGraph\b|\bRecordRenderGraph\b"),
    "legacy_xr": re.compile(r"\bUnityEngine\.XR\b"),
    "input_system": re.compile(r"\bUnityEngine\.InputSystem\b"),
    "netcode": re.compile(r"\bUnity\.Netcode\b"),
}


def scan(patterns):
    findings = []
    for path in sorted(SOURCE_ROOT.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for rule_id, pattern in patterns.items():
            for match in pattern.finditer(text):
                line = text.count("\n", 0, match.start()) + 1
                findings.append({
                    "rule": rule_id,
                    "path": str(path.relative_to(ROOT)),
                    "line": line,
                    "match": match.group(0),
                })
    return findings


def main():
    blockers = scan(BLOCKING_PATTERNS)
    observations = scan(OBSERVATION_PATTERNS)
    result = {
        "compatibility_source_of_truth": "Packages/com.darumappap.my-unity-mcp/Editor/Compatibility/ApiCompatibility.cs",
        "maintenance_buckets": ["BASE", "UNITY_6000_4", "UNITY_6000_5", "UNITY_6000_7"],
        "blocking_findings": blockers,
        "observations": observations,
        "status": "pass" if not blockers else "failed",
        "note": "UNITY_EDITOR and package versionDefines are allowed. Version-specific Unity API branches belong at the Compatibility boundary. Candidate source must use canonical main Graphics internal names directly."
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not blockers else 1


if __name__ == "__main__":
    sys.exit(main())
