#!/usr/bin/env python3
"""Lint MyUnityMCP development architecture and safety boundaries."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Iterable, Sequence

TOOL_ATTRIBUTE = "[McpForUnityTool("
AUTO_REGISTER_DISABLED = "AutoRegister = false"

DEVELOPMENT_SOURCE_NAMES = (
    "UnityAgentMcpTools.cs",
    "UnityAgentMcpRuntime.cs",
    "UnityWorldCreatorMcp.cs",
    "UnityProfilerMcp.cs",
    "UnityBuildMcp.cs",
    "UnityAddressablesMcp.cs",
    "UnityUiMcp.cs",
    "UnityAnimationMcpTools.cs",
    "UnityAnimationMcpRuntime.cs",
    "UnityAudioMcp.cs",
    "UnityCinematicMcp.cs",
    "UnityMovieCreatorMcp.cs",
    "UnityLiveCreatorMcp.cs",
    "UnityDomainMcpCommon.cs",
    "UnityMcpSecurityPolicy.cs",
)

CREATOR_SOURCE_NAMES = (
    "UnityWorldCreatorMcp.cs",
    "UnityMovieCreatorMcp.cs",
    "UnityLiveCreatorMcp.cs",
)

CONTROL_PLANE_SOURCE_NAMES = (
    "UnityAgentMcpTools.cs",
    "UnityAgentMcpRuntime.cs",
)

DIRECT_MUTATION_TOKENS = (
    "Undo.RecordObject",
    "EditorUtility.SetDirty",
    "AssetDatabase.CreateAsset",
    "AssetDatabase.DeleteAsset",
    "EditorSceneManager.SaveScene",
    "BuildPipeline.BuildPlayer",
    "AddressableAssetSettings.BuildPlayerContent",
)

FORBIDDEN_DEVELOPMENT_TOKENS = (
    "using System.Reflection;",
    "BindingFlags.",
    "SerializedProperty",
)


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _count_tool_attributes(texts: Iterable[str]) -> tuple[int, int]:
    tools = 0
    disabled = 0
    for text in texts:
        tools += text.count(TOOL_ATTRIBUTE)
        disabled += text.count(AUTO_REGISTER_DISABLED)
    return tools, disabled


def lint(repo_root: Path) -> dict[str, object]:
    editor_root = repo_root / "Packages/com.darumappap.my-unity-mcp/Editor"
    manifest_path = repo_root / "Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml"
    violations: list[dict[str, str]] = []
    checked_files: list[str] = []

    development_texts: dict[str, str] = {}
    for name in DEVELOPMENT_SOURCE_NAMES:
        path = editor_root / name
        if not path.is_file():
            violations.append({"path": path.as_posix(), "reason": "required development source is missing"})
            continue
        text = _read(path)
        development_texts[name] = text
        checked_files.append(path.relative_to(repo_root).as_posix())

    all_editor_files = sorted(editor_root.glob("*.cs"))
    all_editor_texts = [_read(path) for path in all_editor_files]
    total_tools, disabled_tools = _count_tool_attributes(all_editor_texts)
    if total_tools == 0:
        violations.append({"path": editor_root.relative_to(repo_root).as_posix(), "reason": "no MCP tools were discovered"})
    if disabled_tools != total_tools:
        violations.append({
            "path": editor_root.relative_to(repo_root).as_posix(),
            "reason": f"AutoRegister=false count {disabled_tools} does not match tool count {total_tools}",
        })

    manifest = _read(manifest_path) if manifest_path.is_file() else ""
    match = re.search(r"^\s*development_candidate_tool_count:\s*(\d+)\s*$", manifest, re.MULTILINE)
    if not match:
        violations.append({"path": manifest_path.relative_to(repo_root).as_posix(), "reason": "development candidate tool count is missing"})
        manifest_tool_count = None
    else:
        manifest_tool_count = int(match.group(1))
        if manifest_tool_count != total_tools:
            violations.append({
                "path": manifest_path.relative_to(repo_root).as_posix(),
                "reason": f"manifest tool count {manifest_tool_count} does not match discovered count {total_tools}",
            })

    for name in CREATOR_SOURCE_NAMES:
        text = development_texts.get(name, "")
        for token in DIRECT_MUTATION_TOKENS:
            if token in text:
                violations.append({"path": name, "reason": f"creator directly uses mutation API: {token}"})

    for name in CONTROL_PLANE_SOURCE_NAMES:
        text = development_texts.get(name, "")
        for token in DIRECT_MUTATION_TOKENS:
            if token in text:
                violations.append({"path": name, "reason": f"control plane directly uses mutation API: {token}"})

    for name, text in development_texts.items():
        for token in FORBIDDEN_DEVELOPMENT_TOKENS:
            if token in text:
                violations.append({"path": name, "reason": f"unapproved generic/internal mechanism: {token}"})

    return {
        "passed": not violations,
        "checked_file_count": len(checked_files),
        "checked_files": checked_files,
        "discovered_tool_count": total_tools,
        "auto_register_disabled_count": disabled_tools,
        "manifest_tool_count": manifest_tool_count,
        "violations": violations,
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Lint MyUnityMCP architecture and safety contracts.")
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--json", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    result = lint(Path(args.repo_root).resolve())
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print(f"Tools: {result['discovered_tool_count']}")
        print(f"AutoRegister=false: {result['auto_register_disabled_count']}")
        if result["passed"]:
            print("PASS: architecture and safety contracts are valid.")
        else:
            print("FAIL: architecture violations were found.")
            for item in result["violations"]:
                print(f"- {item['path']}: {item['reason']}")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
