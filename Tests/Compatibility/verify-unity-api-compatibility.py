#!/usr/bin/env python3

from __future__ import annotations

import argparse
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
PACKAGE_ROOT = ROOT / "Packages/com.darumappap.my-unity-mcp"
COMPATIBILITY_CS = PACKAGE_ROOT / "Editor/UnityApiCompatibility.cs"
COMPATIBILITY_TESTS = PACKAGE_ROOT / "Tests/Editor/UnityApiCompatibilityTests.cs"
IDENTITY_COMPATIBILITY_CS = PACKAGE_ROOT / "Editor/UnityGraphicsMcpIdentityCompatibility.cs"
SKILL = ROOT / "skills/myunitymcp-unity-api-compatibility/SKILL.md"
SPEC = ROOT / "Specs/Compatibility/unity-api-compatibility.md"
AGENTS = ROOT / "AGENTS.md"

EXPECTED_BUCKETS = {
    "BASE",
    "UNITY_6000_4",
    "UNITY_6000_5",
    "UNITY_6000_7",
}

FORBIDDEN_NEW_LEGACY_PATTERNS = {
    r"\bGetInstanceID\s*\(": "Use EntityId/compatibility identity handling instead of adding new GetInstanceID calls.",
    r"\bInstanceIDToObject\s*\(": "Use EntityIdToObject or MyUnityMCP session-local identity handling.",
    r"\.renderer\b": "Use GetComponent<Renderer>() or a cached Renderer reference.",
    r"\.camera\b": "Use GetComponent<Camera>() or a cached Camera reference.",
    r"\.audio\b": "Use GetComponent<AudioSource>() or a cached AudioSource reference.",
    r"\bEntities\.ForEach\b": "Use IJobEntity/SystemAPI.Query for new ECS code.",
    r"\bUxmlFactory\b": "Use UxmlElement/UxmlAttribute authoring.",
    r"\bUxmlTraits\b": "Use UxmlElement/UxmlAttribute authoring.",
    r"\bURP_COMPATIBILITY_MODE\b": "New URP code must use RenderGraph rather than Compatibility Mode.",
}

REQUIRED_PACKAGE_META_PAIRS = [
    PACKAGE_ROOT / "CHANGELOG.md",
    PACKAGE_ROOT / "README.md",
    PACKAGE_ROOT / "LICENSE.md",
    COMPATIBILITY_CS,
    PACKAGE_ROOT / "Editor/UnityApiCompatibilityPackageInspection.cs",
    IDENTITY_COMPATIBILITY_CS,
    COMPATIBILITY_TESTS,
]


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def read(path: pathlib.Path) -> str:
    if not path.exists():
        fail(f"Required compatibility artifact is missing: {path.relative_to(ROOT)}")
    return path.read_text(encoding="utf-8")


def check_required_package_meta() -> None:
    for asset in REQUIRED_PACKAGE_META_PAIRS:
        read(asset)
        meta = pathlib.Path(str(asset) + ".meta")
        if not meta.is_file():
            fail(
                "Unity immutable package asset is missing its .meta file: "
                + str(asset.relative_to(ROOT))
            )


def check_static_contract() -> None:
    source = read(COMPATIBILITY_CS)
    tests = read(COMPATIBILITY_TESTS)
    identity_source = read(IDENTITY_COMPATIBILITY_CS)
    skill = read(SKILL)
    spec = read(SPEC)
    agents = read(AGENTS)

    check_required_package_meta()

    enum_match = re.search(
        r"public enum E_UNITY_API_PATCH_BUCKET\s*\{(?P<body>.*?)\}",
        source,
        re.DOTALL,
    )
    if enum_match is None:
        fail("E_UNITY_API_PATCH_BUCKET enum was not found.")

    enum_body = enum_match.group("body")
    buckets = {
        item.strip().rstrip(",")
        for item in enum_body.splitlines()
        if item.strip() and not item.strip().startswith("//")
    }
    if buckets != EXPECTED_BUCKETS:
        fail(
            "Patch buckets drifted. Expected exactly "
            + ", ".join(sorted(EXPECTED_BUCKETS))
            + f" but found {sorted(buckets)}"
        )

    if re.search(r"\bUNITY_6000_6\s*[,}]", source):
        fail("Unity 6.6 must remain rolled into UNITY_6000_7; do not create a 6000.6 patch bucket.")

    required_source_tokens = [
        "UNITY-6000-4-OBJECT-ENTITY-ID",
        "UNITY-6000-4-URP-COMPATIBILITY-MODE",
        "UNITY-6000-5-LEGACY-COMPONENT-REMOVAL",
        "UNITY-6000-5-ENTITIES-FOREACH",
        "UNITY-6000-7-SCENE-HANDLE-RAW-DATA",
        "UNITY-6000-7-ROLLUP-UXML-FACTORY",
        "UNITY-6000-7-ROLLUP-HIERARCHY-API",
        "UNITY-6000-7-RENDERGRAPH-Y-FLIP",
        "UNITY-6000-7-RENDERGRAPH-BLIT-SLICE",
        "E_UNITY_API_SOURCE_STATUS.PLANNED",
        "BASE_THEN_6000_4_THEN_6000_5_THEN_6000_7_ROLLUP",
    ]
    for token in required_source_tokens:
        if token not in source:
            fail(f"Compatibility registry is missing required contract token: {token}")

    required_test_tokens = [
        "Resolve_6000_0_UsesBaseWithoutEntityIdRule",
        "Resolve_6000_2_ActivatesEntityIdInside6000_4MaintenanceBucket",
        "Resolve_6000_5_TreatsLegacyComponentShortcutsAsRemoved",
        "Resolve_6000_6_Uses6000_7RollupInsteadOfCreating6000_6Bucket",
        "Resolve_6000_7_ContainsConfirmedSceneHandleBoundary",
        "Resolve_6000_7_ExposesPlannedRenderGraphBehaviorChanges",
    ]
    for token in required_test_tokens:
        if token not in tests:
            fail(f"Compatibility tests are missing required coverage: {token}")

    required_identity_tokens = [
        "GetSceneHandle",
        "GetSceneToken",
        "GetObjectToken",
        "ResolveObjectToken",
        "UNITY_6000_7_OR_NEWER",
        "scene.handle.GetRawData()",
    ]
    for token in required_identity_tokens:
        if token not in identity_source:
            fail(f"Identity compatibility helper is missing required contract token: {token}")

    if "GetInstanceID(" in identity_source or "InstanceIDToObject(" in identity_source:
        fail("Identity compatibility helper must not reintroduce legacy InstanceID APIs.")

    if "name: myunitymcp-unity-api-compatibility" not in skill:
        fail("Compatibility skill front matter is missing or renamed.")
    if "Unity 6.6専用Bucketは作りません" not in skill:
        fail("Skill must explicitly keep Unity 6.6 changes in the Unity 6.7 roll-up bucket.")
    if "UnityApiCompatibility.cs" not in skill or "UnityApiCompatibilityTests.cs" not in skill:
        fail("Skill must require compatibility registry and tests to be maintained together.")
    if "Immutable package asset rule" not in skill or "Scene identity rule" not in skill:
        fail("Skill must preserve the Unity 6.7 SceneHandle and package .meta lessons.")

    if "BASE" not in spec or "UNITY_6000_7" not in spec:
        fail("Compatibility spec no longer documents the Base + 6.4 + 6.5 + 6.7 policy.")

    if "skills/myunitymcp-unity-api-compatibility/SKILL.md" not in agents:
        fail("AGENTS.md must require the compatibility skill for Unity-version-sensitive changes.")

    print("Unity API compatibility static contract: PASS")


def git_diff(base: str) -> str:
    completed = subprocess.run(
        ["git", "diff", "--unified=0", f"{base}...HEAD", "--"],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if completed.returncode != 0:
        fail(f"git diff failed for base {base}: {completed.stderr.strip()}")
    return completed.stdout


def changed_files(base: str) -> set[str]:
    completed = subprocess.run(
        ["git", "diff", "--name-only", f"{base}...HEAD"],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if completed.returncode != 0:
        fail(f"git diff --name-only failed for base {base}: {completed.stderr.strip()}")
    return {line.strip() for line in completed.stdout.splitlines() if line.strip()}


def check_diff_contract(base: str) -> None:
    files = changed_files(base)
    diff = git_diff(base)

    compatibility_path = COMPATIBILITY_CS.relative_to(ROOT).as_posix()
    tests_path = COMPATIBILITY_TESTS.relative_to(ROOT).as_posix()
    if compatibility_path in files and tests_path not in files:
        fail("UnityApiCompatibility.cs changed without UnityApiCompatibilityTests.cs in the same change.")

    current_file = None
    violations: list[str] = []
    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            current_file = line[6:]
            continue
        if not line.startswith("+") or line.startswith("+++"):
            continue
        if current_file is None or not current_file.endswith(".cs"):
            continue
        if current_file == compatibility_path or current_file == tests_path:
            continue

        added = line[1:]
        for pattern, guidance in FORBIDDEN_NEW_LEGACY_PATTERNS.items():
            if re.search(pattern, added):
                violations.append(f"{current_file}: {added.strip()} -> {guidance}")

    if violations:
        fail(
            "New Unity legacy API usage was added. Base modernization is required before version patches:\n"
            + "\n".join(f"- {item}" for item in violations)
        )

    print(f"Unity API compatibility diff contract against {base}: PASS")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--diff-base",
        help="Optional git ref used to validate newly added legacy Unity API usage.",
    )
    args = parser.parse_args()

    check_static_contract()
    if args.diff_base:
        check_diff_contract(args.diff_base)


if __name__ == "__main__":
    main()
