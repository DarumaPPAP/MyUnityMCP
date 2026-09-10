#!/usr/bin/env python3
"""Canonical static contract gate for UnityArtistCLI 2.0.0."""
from __future__ import annotations

import json
from pathlib import Path
import re
import subprocess
import sys

import yaml

ROOT = Path(__file__).resolve().parents[2]
VERSION_PATH = ROOT / "VERSION"
PACKAGE_PATH = ROOT / "Packages/com.darumappap.unity-artist/package.json"
CLI_PROJECT = ROOT / "src/UnityArtist.Cli/UnityArtist.Cli.csproj"
CLI_SOURCE = ROOT / "src/UnityArtist.Cli/Program.cs"
EDITOR_ROOT = ROOT / "Packages/com.darumappap.unity-artist/Editor"
MATRIX_PATH = ROOT / "Tests/Compatibility/support-matrix.yaml"
GATE_EVIDENCE_PATH = ROOT / "Tests/Compatibility/cli-pipeline-gate-evidence.yaml"
CATALOG_PATH = ROOT / "Catalog/unity-artist-catalog.yaml"
SURFACE_PATH = ROOT / "Catalog/production-surface-contract.yaml"
PLUGIN_ROOT = ROOT / ".agents/plugins/unity-artist"

REQUIRED_COMMANDS = {
    "help", "version", "doctor", "capabilities", "install", "inspect", "plan",
    "preview", "apply", "capture", "evaluate", "refine", "history", "cinematic",
}
FORBIDDEN_SOURCE_TOKENS = (
    "com.coplaydev.unity-mcp",
    "McpForUnityTool",
    "MCPForUnity",
    "using Unity.MCP",
    "AutoRegister",
)
EXPECTED_ROWS = {
    ("2022.3 LTS", "builtin"),
    ("Unity 6.x+", "builtin"),
    ("Unity 6.x+", "urp"),
    ("Unity 6.x+", "hdrp"),
}


def error(errors: list[str], message: str) -> None:
    errors.append(message)
    print(f"[ERROR] {message}")


def read_json(errors: list[str], path: Path) -> dict:
    if not path.is_file():
        error(errors, f"missing JSON file: {path.relative_to(ROOT)}")
        return {}
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        error(errors, f"invalid JSON {path.relative_to(ROOT)}: {exc}")
        return {}
    if not isinstance(value, dict):
        error(errors, f"JSON root must be an object: {path.relative_to(ROOT)}")
        return {}
    return value


def read_yaml(errors: list[str], path: Path) -> dict:
    if not path.is_file():
        error(errors, f"missing YAML file: {path.relative_to(ROOT)}")
        return {}
    try:
        value = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    except Exception as exc:  # noqa: BLE001
        error(errors, f"invalid YAML {path.relative_to(ROOT)}: {exc}")
        return {}
    if not isinstance(value, dict):
        error(errors, f"YAML root must be a mapping: {path.relative_to(ROOT)}")
        return {}
    return value


def check_identity(errors: list[str]) -> None:
    version = VERSION_PATH.read_text(encoding="utf-8").strip() if VERSION_PATH.is_file() else ""
    package = read_json(errors, PACKAGE_PATH)
    if version != "2.0.0":
        error(errors, f"VERSION must be 2.0.0, got {version!r}")
    if package.get("name") != "com.darumappap.unity-artist":
        error(errors, "current package name is not com.darumappap.unity-artist")
    if package.get("version") != version:
        error(errors, "VERSION and UnityArtist package version disagree")
    dependencies = package.get("dependencies") or {}
    if set(dependencies) != {"com.unity.pipeline"}:
        error(errors, f"current package dependencies must be Pipeline-only, got {sorted(dependencies)}")
    if package.get("unity") != "2022.3":
        error(errors, "package minimum Unity version must be 2022.3")


def check_cli_surface(errors: list[str]) -> None:
    source = CLI_SOURCE.read_text(encoding="utf-8") if CLI_SOURCE.is_file() else ""
    command_block = re.search(r"SupportedCommands.*?\{(?P<body>.*?)\};", source, re.DOTALL)
    commands = set(re.findall(r'"([a-z]+)"', command_block.group("body") if command_block else ""))
    missing = sorted(REQUIRED_COMMANDS - commands)
    if missing:
        error(errors, f"CLI is missing commands: {missing}")
    if '"official_unity_cli_pipeline"' not in source:
        error(errors, "CLI does not declare official_unity_cli_pipeline transport")
    if '"pipeline", "install"' not in source or '"command"' not in source:
        error(errors, "CLI does not expose install and official Pipeline command delegation")
    if not CLI_PROJECT.is_file():
        error(errors, "CLI project file is missing")


def check_editor_surface(errors: list[str]) -> None:
    sources = "\n".join(path.read_text(encoding="utf-8") for path in EDITOR_ROOT.rglob("*.cs")) if EDITOR_ROOT.is_dir() else ""
    for token in FORBIDDEN_SOURCE_TOKENS:
        if token in sources:
            error(errors, f"forbidden legacy/MCP token remains in current Editor source: {token}")
    if "CliCommand" not in sources or "Unity.Pipeline.Commands" not in sources:
        error(errors, "current Editor package does not expose official Pipeline CliCommand registrations")
    for command in ("artist.inspect", "artist.plan", "artist.preview", "artist.apply", "artist.capture", "artist.evaluate", "artist.refine", "artist.cinematic", "artist.history"):
        if command not in sources:
            error(errors, f"missing Pipeline command registration: {command}")
    for token in ("CinematicRequest", "InspectCinematicDirector", "CreateTrack", "CreateMarker", "SetGenericBinding", "Undo.RecordObject", "depth_channel", "object_id_channel"):
        if token not in sources:
            error(errors, f"current Editor source is missing bounded Artist/Cinematic contract token: {token}")
    compatibility = EDITOR_ROOT / "Compatibility/ArtistCompatibility.cs"
    compatibility_tests = ROOT / "Packages/com.darumappap.unity-artist/Tests/Editor/ArtistCompatibilityTests.cs"
    for path in (compatibility, compatibility_tests):
        if not path.is_file() or not Path(str(path) + ".meta").is_file():
            error(errors, f"compatibility asset or .meta is missing: {path.relative_to(ROOT)}")


def check_matrix(errors: list[str]) -> None:
    matrix = read_yaml(errors, MATRIX_PATH)
    rows = matrix.get("rows") or []
    actual = {(str(row.get("unity_version")), str(row.get("render_pipeline"))) for row in rows if isinstance(row, dict)}
    if actual != EXPECTED_ROWS:
        error(errors, f"formal release matrix drifted: {sorted(actual)}")
    if matrix.get("transport") != "official_unity_cli_pipeline":
        error(errors, "matrix transport must remain official_unity_cli_pipeline")
    if matrix.get("fallback_policy") != "concrete_cli_pipeline_gate_failure_only":
        error(errors, "fallback policy is not concrete CLI/Pipeline Gate Failure only")
    if matrix.get("verification_contract", {}).get("unsupported_result_before_mutation") is not True:
        error(errors, "unsupported result before mutation is not required")


def check_cli_pipeline_gate_evidence(errors: list[str]) -> None:
    evidence = read_yaml(errors, GATE_EVIDENCE_PATH)
    if evidence.get("case") != "unity-2022.3-builtin":
        error(errors, "CLI/Pipeline gate evidence must cover the 2022.3 Built-in case")
    candidate = evidence.get("official_first_candidate") or {}
    if candidate.get("transport") != "official_unity_cli_pipeline":
        error(errors, "2022.3 gate evidence must record Official Unity CLI + Pipeline as first candidate")
    pipeline = candidate.get("pipeline") or {}
    if pipeline.get("failure_class") != "concrete_cli_pipeline_gate_failure":
        error(errors, "2022.3 gate evidence must preserve a concrete Pipeline gate failure")
    plugin = evidence.get("artist_plugin") or {}
    if plugin.get("version_status") != "passed" or plugin.get("help_status") != "passed":
        error(errors, "Artist plugin version/help command evidence is incomplete")
    if plugin.get("global_help_status") != "blocked_by_official_cli_global_parser":
        error(errors, "global unity artist --help behavior must remain explicitly recorded")
    fallback = evidence.get("fallback_evaluation") or {}
    if fallback.get("selected") is not False:
        error(errors, "2022.3 fallback must not be silently selected")


def check_catalog(errors: list[str]) -> None:
    catalog = read_yaml(errors, CATALOG_PATH)
    surface = read_yaml(errors, SURFACE_PATH)
    if catalog.get("product") != "UnityArtistCLI" or surface.get("product") != "UnityArtistCLI":
        error(errors, "Catalog product identity is not UnityArtistCLI")
    if catalog.get("release_version") != "2.0.0" or surface.get("release_version") != "2.0.0":
        error(errors, "Catalog release version is not 2.0.0")
    if surface.get("commands") and set(surface["commands"]) != REQUIRED_COMMANDS:
        error(errors, "production surface command set disagrees with CLI contract")
    if surface.get("mcp_transport") is not False or surface.get("second_player_framework") is not False:
        error(errors, "production surface must explicitly disable MCP transport and second Player framework")


def check_plugins(errors: list[str]) -> None:
    for manifest in (PLUGIN_ROOT / "plugin.json", PLUGIN_ROOT / ".codex-plugin/plugin.json"):
        value = read_json(errors, manifest)
        if value.get("name") != "unity-artist":
            error(errors, f"plugin manifest has wrong name: {manifest.relative_to(ROOT)}")
        if value.get("version") != "2.0.0":
            error(errors, f"plugin manifest version mismatch: {manifest.relative_to(ROOT)}")
    if any(path.name == ".mcp.json" for path in PLUGIN_ROOT.rglob("*")):
        error(errors, "UnityArtist plugin must not contain .mcp.json")
    if not any(PLUGIN_ROOT.rglob("SKILL.md")):
        error(errors, "UnityArtist plugin must provide at least one skill")


def check_legacy_anchor(errors: list[str]) -> None:
    package = ROOT / "Legacy/MyUnityMCP-1.1.1/Package/package.json"
    value = read_json(errors, package)
    if value.get("version") != "1.1.1":
        error(errors, "Legacy MyUnityMCP v1.1.1 package anchor is missing or changed")
    completed = subprocess.run(
        ["git", "rev-parse", "--verify", "refs/tags/v1.1.1"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        error(errors, "immutable v1.1.1 tag is not present locally")


def main() -> int:
    errors: list[str] = []
    check_identity(errors)
    check_cli_surface(errors)
    check_editor_surface(errors)
    check_matrix(errors)
    check_cli_pipeline_gate_evidence(errors)
    check_catalog(errors)
    check_plugins(errors)
    check_legacy_anchor(errors)
    print(f"UnityArtistCLI production contract: {len(errors)} error(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
