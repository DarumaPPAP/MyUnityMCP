#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / "Packages/com.darumappap.my-unity-mcp"
REGISTRY = PACKAGE / "Editor/UnityApiCompatibility.cs"
REGISTRY_TESTS = PACKAGE / "Tests/Editor/UnityApiCompatibilityTests.cs"

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

SCENE_HANDLE_RULE = '''\t\t\t\tCreateRule(
\t\t\t\t\t"UNITY-6000-7-SCENE-HANDLE-RAW-DATA",
\t\t\t\t\tE_UNITY_API_PATCH_BUCKET.UNITY_6000_7,
\t\t\t\t\t"Core",
\t\t\t\t\t"SceneHandleとintの暗黙変換",
\t\t\t\t\t"SceneHandle.GetRawData() / SceneHandle.FromRawData(ulong)、またはMyUnityMCP Session Token",
\t\t\t\t\t"6000.7", null, "6000.7", null, null,
\t\t\t\t\tE_UNITY_API_SOURCE_STATUS.CONFIRMED,
\t\t\t\t\t"Unity 6000.7 alpha実Editor CompilerのCS0619で確認。永続ID用途へraw Handleを流用しません。"),
'''

SCENE_HANDLE_TEST = '''\t\t[Test]
\t\tpublic void Resolve_6000_7_ContainsConfirmedSceneHandleBoundary()
\t\t{
\t\t\tDictionary<string, object> summary =
\t\t\t\tUnityApiCompatibility.BuildProjectSummary("6000.7.0a3");
\t\t\tList<Dictionary<string, object>> rules =
\t\t\t\tsummary["rules"] as List<Dictionary<string, object>>;

\t\t\tAssert.That(
\t\t\t\trules.Any(item =>
\t\t\t\t\t(string)item["ruleId"] == "UNITY-6000-7-SCENE-HANDLE-RAW-DATA" &&
\t\t\t\t\t(string)item["state"] == "ERROR" &&
\t\t\t\t\t(string)item["sourceStatus"] == E_UNITY_API_SOURCE_STATUS.CONFIRMED.ToString()),
\t\t\t\tIs.True);
\t\t}

'''


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


def update_registry() -> int:
    text = REGISTRY.read_text(encoding="utf-8")
    if "UNITY-6000-7-SCENE-HANDLE-RAW-DATA" in text:
        return 0
    marker = "\t\t\t\t// Unity 6.7 roll-up bucket. 6.6由来の変更もここで保守します。\n"
    if marker not in text:
        raise SystemExit("Unity 6.7 registry marker not found")
    REGISTRY.write_text(text.replace(marker, SCENE_HANDLE_RULE + "\n" + marker, 1), encoding="utf-8")
    return 1


def update_registry_tests() -> int:
    text = REGISTRY_TESTS.read_text(encoding="utf-8")
    if "Resolve_6000_7_ContainsConfirmedSceneHandleBoundary" in text:
        return 0
    marker = "\t\t[Test]\n\t\tpublic void Resolve_6000_7_ExposesPlannedRenderGraphBehaviorChanges()\n"
    if marker not in text:
        raise SystemExit("Unity 6.7 test marker not found")
    REGISTRY_TESTS.write_text(text.replace(marker, SCENE_HANDLE_TEST + marker, 1), encoding="utf-8")
    return 1


def main() -> None:
    for path in OBJECT_FILES + SCENE_FILES + [REGISTRY, REGISTRY_TESTS]:
        if not path.is_file():
            raise SystemExit(f"Missing migration target: {path.relative_to(ROOT)}")

    changed = {}
    for path in OBJECT_FILES:
        count = replace_object_ids(path)
        changed[str(path.relative_to(ROOT))] = changed.get(str(path.relative_to(ROOT)), 0) + count

    for path in SCENE_FILES:
        count = replace_scene_handles(path)
        changed[str(path.relative_to(ROOT))] = changed.get(str(path.relative_to(ROOT)), 0) + count

    changed[str(REGISTRY.relative_to(ROOT))] = update_registry()
    changed[str(REGISTRY_TESTS.relative_to(ROOT))] = update_registry_tests()

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
