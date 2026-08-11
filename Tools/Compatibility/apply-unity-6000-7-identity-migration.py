#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / "Packages/com.darumappap.my-unity-mcp"

OBJECT_FILES = [
    PACKAGE / "Editor/UnityGraphicsMcpCaptureEvidence.cs",
    PACKAGE / "Editor/UnityGraphicsMcpEnvironmentMutation.cs",
    PACKAGE / "Editor/UnityGraphicsMcpMutation.cs",
    PACKAGE / "Editor/UnityGraphicsMcpProjectInspection.cs",
    PACKAGE / "Editor/UnityGraphicsMcpSaveEvaluation.cs",
    PACKAGE / "Editor/UnityGraphicsMcpSceneInspection.cs",
    PACKAGE / "Tests/Editor/UnityGraphicsMcpInspectionTests.cs",
]

SCENE_FILES = [
    PACKAGE / "Editor/UnityGraphicsMcpDependencyBake.cs",
    PACKAGE / "Editor/UnityGraphicsMcpCaptureEvidence.cs",
    PACKAGE / "Editor/UnityGraphicsMcpEnvironmentMutation.cs",
    PACKAGE / "Editor/UnityGraphicsMcpMutation.cs",
    PACKAGE / "Editor/UnityGraphicsMcpSaveEvaluation.cs",
    PACKAGE / "Tests/Editor/UnityGraphicsMcpCaptureEvidenceTests.cs",
    PACKAGE / "Tests/Editor/UnityGraphicsMcpApvVisualAcceptanceTests.cs",
    PACKAGE / "Tests/Editor/UnityGraphicsMcpIntegrationHardeningTests.cs",
]

OBJECT_PATTERN = re.compile(
    r"(?P<expr>\b[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\.GetInstanceID\(\)"
)
SCENE_HANDLE_PATTERN = re.compile(
    r"(?P<expr>\b[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\.handle\b"
)


def replace_object_ids(path: Path) -> int:
    text = path.read_text(encoding="utf-8")
    text, get_id_count = OBJECT_PATTERN.subn(
        r"UnityGraphicsMcpIdentityCompatibility.GetObjectToken(\g<expr>)",
        text,
    )
    instance_to_object_count = text.count("EditorUtility.InstanceIDToObject(")
    text = text.replace(
        "EditorUtility.InstanceIDToObject(",
        "UnityGraphicsMcpIdentityCompatibility.ResolveObjectToken(",
    )
    path.write_text(text, encoding="utf-8")
    return get_id_count + instance_to_object_count


def replace_scene_handles(path: Path) -> int:
    text = path.read_text(encoding="utf-8")
    text, count = SCENE_HANDLE_PATTERN.subn(
        r"UnityGraphicsMcpIdentityCompatibility.GetSceneToken(\g<expr>)",
        text,
    )
    path.write_text(text, encoding="utf-8")
    return count


def main() -> None:
    for path in OBJECT_FILES + SCENE_FILES:
        if not path.is_file():
            raise SystemExit(f"Missing migration target: {path.relative_to(ROOT)}")

    changed = {}
    for path in OBJECT_FILES:
        count = replace_object_ids(path)
        changed[str(path.relative_to(ROOT))] = changed.get(str(path.relative_to(ROOT)), 0) + count

    for path in SCENE_FILES:
        count = replace_scene_handles(path)
        changed[str(path.relative_to(ROOT))] = changed.get(str(path.relative_to(ROOT)), 0) + count

    for path in OBJECT_FILES:
        text = path.read_text(encoding="utf-8")
        if ".GetInstanceID()" in text:
            raise SystemExit(f"Legacy GetInstanceID remains: {path.relative_to(ROOT)}")
        if "EditorUtility.InstanceIDToObject(" in text:
            raise SystemExit(f"Legacy InstanceIDToObject remains: {path.relative_to(ROOT)}")

    for path in SCENE_FILES:
        text = path.read_text(encoding="utf-8")
        if SCENE_HANDLE_PATTERN.search(text):
            raise SystemExit(f"Legacy Scene.handle access remains: {path.relative_to(ROOT)}")

    total = sum(changed.values())
    if total == 0:
        print("Unity 6.7 identity migration already applied.")
        return

    print(f"Unity 6.7 identity migration applied: replacements={total}")
    for path, count in sorted(changed.items()):
        if count:
            print(f"  {path}: {count}")


if __name__ == "__main__":
    main()
