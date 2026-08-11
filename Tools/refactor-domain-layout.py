from pathlib import Path
import hashlib
import re

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "Packages/com.darumappap.my-unity-mcp"
EDITOR = PACKAGE / "Editor"
TESTS = PACKAGE / "Tests/Editor"

PRODUCTION = {
    "UnityApiCompatibility.cs": "Compatibility/ApiCompatibility.cs",
    "UnityApiCompatibilityPackageInspection.cs": "Compatibility/PackageInspection.cs",
    "UnityGraphicsMcpAssemblyInfo.cs": "Core/AssemblyInfo.cs",
    "UnityGraphicsMcpSession.cs": "Core/Session.cs",
    "UnityGraphicsMcpCapabilityResolution.cs": "Core/CapabilityResolution.cs",
    "UnityGraphicsMcpBuiltinResourceDirtyCompatibility.cs": "Compatibility/BuiltinResourceDirtyCompatibility.cs",
    "UnityGraphicsMcpEditorSceneManagerCompatibility.cs": "Compatibility/EditorSceneManagerCompatibility.cs",
    "UnityGraphicsMcpIdentityCompatibility.cs": "Compatibility/IdentityCompatibility.cs",
    "UnityGraphicsMcpReflectionAssemblyCompatibility.cs": "Compatibility/ReflectionAssemblyCompatibility.cs",
    "UnityGraphicsMcpInspection.cs": "Inspection/Inspection.cs",
    "UnityGraphicsMcpProjectInspection.cs": "Inspection/ProjectInspection.cs",
    "UnityGraphicsMcpSceneInspection.cs": "Inspection/SceneInspection.cs",
    "UnityGraphicsMcpValidation.cs": "Inspection/Validation.cs",
    "UnityGraphicsMcpPlanning.cs": "Planning/Planning.cs",
    "UnityGraphicsMcpMutation.cs": "Mutation/LightMutation.cs",
    "UnityGraphicsMcpEnvironmentMutation.cs": "Mutation/EnvironmentMutation.cs",
    "UnityGraphicsMcpDependencyBake.cs": "Bake/DependencyBake.cs",
    "UnityGraphicsMcpAdaptiveProbeVolumeBake.cs": "Bake/AdaptiveProbeVolumeBake.cs",
    "UnityGraphicsMcpCaptureEvidence.cs": "Capture/CaptureEvidence.cs",
    "UnityGraphicsMcpVisualAcceptance.cs": "Capture/VisualAcceptance.cs",
    "UnityGraphicsMcpSaveEvaluation.cs": "Save/SaveEvaluation.cs",
    "UnityGraphicsMcpExecutionHardening.cs": "Execution/ExecutionHardening.cs",
    "UnityGraphicsMcpExecutionLifecycle.cs": "Execution/ExecutionLifecycle.cs",
    "UnityGraphicsMcpTools.cs": "Tools/CoreTools.cs",
    "UnityGraphicsMcpDependencyBakeTools.cs": "Tools/BakeTools.cs",
    "UnityGraphicsMcpCaptureEvidenceTools.cs": "Tools/CaptureTools.cs",
    "UnityGraphicsMcpSaveEvaluationTools.cs": "Tools/SaveTools.cs",
    "UnityGraphicsMcpExecutionHardeningTools.cs": "Tools/ExecutionTools.cs",
    "UnityGraphicsMcpApvVisualAcceptanceTools.cs": "Tools/VisualAcceptanceTools.cs",
}

TEST_FILES = {
    "UnityApiCompatibilityTests.cs": "Compatibility/ApiCompatibilityTests.cs",
    "UnityGraphicsMcpTestAsset.cs": "Core/TestAsset.cs",
    "UnityGraphicsMcpInspectionTests.cs": "Inspection/InspectionTests.cs",
    "UnityGraphicsMcpPlanningTests.cs": "Planning/PlanningTests.cs",
    "UnityGraphicsMcpMutationTests.cs": "Mutation/MutationTests.cs",
    "UnityGraphicsMcpEnvironmentMutationTests.cs": "Mutation/EnvironmentMutationTests.cs",
    "UnityGraphicsMcpDependencyBakeTests.cs": "Bake/DependencyBakeTests.cs",
    "UnityGraphicsMcpDirtyDependencySetTests.cs": "Bake/DirtyDependencySetTests.cs",
    "UnityGraphicsMcpCaptureEvidenceTests.cs": "Capture/CaptureEvidenceTests.cs",
    "UnityGraphicsMcpApvVisualAcceptanceTests.cs": "Capture/ApvVisualAcceptanceTests.cs",
    "UnityGraphicsMcpSaveEvaluationTests.cs": "Save/SaveEvaluationTests.cs",
    "UnityGraphicsMcpSaveEvaluationDiagnosticsTests.cs": "Save/SaveEvaluationDiagnosticsTests.cs",
    "UnityGraphicsMcpIntegrationHardeningTests.cs": "Execution/IntegrationHardeningTests.cs",
}


def verify_mapping(base: Path, mapping: dict[str, str], label: str) -> None:
    actual = {path.name for path in base.glob("*.cs")}
    unmapped = actual - set(mapping)
    absent = set(mapping) - actual
    if unmapped or absent:
        raise SystemExit(
            f"{label} mapping drift. unmapped={sorted(unmapped)} missing={sorted(absent)}"
        )


def write_folder_meta(folder: Path) -> None:
    meta = Path(str(folder) + ".meta")
    if meta.exists():
        return
    guid = hashlib.md5(folder.relative_to(PACKAGE).as_posix().encode("utf-8")).hexdigest()
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def move_group(base: Path, mapping: dict[str, str]) -> None:
    for old_name, new_relative in mapping.items():
        source = base / old_name
        source_meta = Path(str(source) + ".meta")
        destination = base / new_relative
        if not source_meta.is_file():
            raise SystemExit(f"Missing immutable package meta before move: {source_meta}")
        destination.parent.mkdir(parents=True, exist_ok=True)
        write_folder_meta(destination.parent)
        source.rename(destination)
        source_meta.rename(Path(str(destination) + ".meta"))


def build_path_replacements() -> dict[str, str]:
    replacements: dict[str, str] = {}
    for old_name, new_relative in PRODUCTION.items():
        replacements[
            f"Packages/com.darumappap.my-unity-mcp/Editor/{old_name}"
        ] = f"Packages/com.darumappap.my-unity-mcp/Editor/{new_relative}"
        replacements[f"Editor/{old_name}"] = f"Editor/{new_relative}"
        replacements[old_name] = Path(new_relative).name
    for old_name, new_relative in TEST_FILES.items():
        replacements[
            f"Packages/com.darumappap.my-unity-mcp/Tests/Editor/{old_name}"
        ] = f"Packages/com.darumappap.my-unity-mcp/Tests/Editor/{new_relative}"
        replacements[f"Tests/Editor/{old_name}"] = f"Tests/Editor/{new_relative}"
        replacements[old_name] = Path(new_relative).name
    return replacements


def rewrite_text_files() -> None:
    replacements = build_path_replacements()
    text_suffixes = {".cs", ".md", ".py", ".yaml", ".yml", ".json", ".toml"}
    for path in ROOT.rglob("*"):
        if not path.is_file() or ".git" in path.parts or path.suffix.lower() not in text_suffixes:
            continue
        text = path.read_text(encoding="utf-8")
        original = text
        for old, new in sorted(replacements.items(), key=lambda item: len(item[0]), reverse=True):
            text = text.replace(old, new)
        if path.suffix.lower() == ".cs":
            text = re.sub(r"\bUnityApiCompatibilityPackageInspection\b", "PackageInspection", text)
            text = re.sub(r"\bUnityApiCompatibility\b", "ApiCompatibility", text)
            text = re.sub(r"\bUnityGraphicsMcp(?=[A-Z][A-Za-z0-9_]*)", "", text)
            text = re.sub(r"\bGraphics([A-Z][A-Za-z0-9_]*Tool)\b", r"\1", text)
        if text != original:
            path.write_text(text, encoding="utf-8")


def update_repository_policy() -> None:
    naming = ROOT / "Specs/UnityGraphicsMCP/naming.md"
    naming_text = naming.read_text(encoding="utf-8")
    if "## Internal C# naming" not in naming_text:
        naming_text += """

## Internal C# naming

- Root namespaceは`UnityGraphicsMcp`を維持する。
- `UnityGraphicsMcp` namespace配下の型名へ`UnityGraphicsMcp` prefixを重ねない。
- 外部MCP Tool名の`graphics.*`は安定契約として変更しない。
- MCP Tool wrapperは`InspectProjectTool`のように責務名 + `Tool`で命名する。
- File名は主責務を表し、Domain実装は原則として主責務単位、Tool wrapperはDomain単位でまとめる。

## Editor layout

```text
Editor/
  Core/
  Compatibility/
  Inspection/
  Planning/
  Mutation/
  Save/
  Bake/
  Capture/
  Execution/
  Tools/
```

Testも同じDomain区分へ寄せる。1 Tool = 1 Fileのような過剰分割は行わない。
"""
        naming.write_text(naming_text, encoding="utf-8")

    agents = ROOT / "AGENTS.md"
    agents_text = agents.read_text(encoding="utf-8")
    csharp_anchor = "- namespaceはFeature単位の単一階層。"
    csharp_rule = "- `UnityGraphicsMcp` namespace配下の内部型名へ`UnityGraphicsMcp` prefixを重ねない。外部`graphics.*` Tool名は変更しない。"
    if csharp_rule not in agents_text:
        agents_text = agents_text.replace(csharp_anchor, csharp_anchor + "\n" + csharp_rule)
    layout_anchor = "- Package内のEditor実装はAssembly境界を維持したまま責務別サブフォルダへ整理可能とし、Release検証は再帰的にToolを検出する。"
    layout_rule = "- Editor実装の標準区分は`Core / Compatibility / Inspection / Planning / Mutation / Save / Bake / Capture / Execution / Tools`とし、Toolごとの過剰なFile分割は行わない。"
    if layout_rule not in agents_text:
        agents_text = agents_text.replace(layout_anchor, layout_anchor + "\n" + layout_rule)
    agents.write_text(agents_text, encoding="utf-8")


def update_semantic_guard() -> None:
    guard = ROOT / "Tests/Compatibility/verify-semantic-names.py"
    guard.write_text(
        '''from pathlib import Path\nimport re\nimport sys\n\nROOT = Path(__file__).resolve().parents[2]\nSCAN_ROOTS = [\n    ROOT / "Packages/com.darumappap.my-unity-mcp/Editor",\n    ROOT / "Packages/com.darumappap.my-unity-mcp/Tests/Editor",\n]\nPHASE_FORBIDDEN = re.compile(r"(?:class\\s+\\w*)Phase\\d|Phase4", re.IGNORECASE)\nREDUNDANT_PREFIX = re.compile(r"\\bUnityGraphicsMcp[A-Z][A-Za-z0-9_]*\\b")\nviolations = []\n\nfor scan_root in SCAN_ROOTS:\n    for path in scan_root.rglob("*"):\n        if not path.is_file():\n            continue\n        if "phase" in path.name.lower():\n            violations.append(f"phase-named file: {path.relative_to(ROOT)}")\n        if REDUNDANT_PREFIX.search(path.name):\n            violations.append(f"redundant UnityGraphicsMcp file prefix: {path.relative_to(ROOT)}")\n        if path.suffix.lower() != ".cs":\n            continue\n        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):\n            if PHASE_FORBIDDEN.search(line):\n                violations.append(f"phase-named identifier: {path.relative_to(ROOT)}:{line_number}: {line.strip()}")\n            if REDUNDANT_PREFIX.search(line):\n                violations.append(f"redundant UnityGraphicsMcp type prefix: {path.relative_to(ROOT)}:{line_number}: {line.strip()}")\n\nif violations:\n    print("Semantic naming violations detected:")\n    print("\\n".join(violations))\n    sys.exit(1)\n\nprint("Semantic naming guard passed.")\n''',
        encoding="utf-8",
    )


def verify_result() -> None:
    expected = {"Core", "Compatibility", "Inspection", "Planning", "Mutation", "Save", "Bake", "Capture", "Execution", "Tools"}
    for name in expected:
        folder = EDITOR / name
        if not folder.is_dir() or not Path(str(folder) + ".meta").is_file():
            raise SystemExit(f"Missing Editor domain folder or .meta: {name}")

    violations = []
    for scan_root in (EDITOR, TESTS):
        for cs in scan_root.rglob("*.cs"):
            text = cs.read_text(encoding="utf-8")
            if re.search(r"\bUnityGraphicsMcp[A-Z][A-Za-z0-9_]*\b", text):
                violations.append(str(cs.relative_to(ROOT)))
    if violations:
        raise SystemExit("Redundant type prefix remains:\n" + "\n".join(violations))


verify_mapping(EDITOR, PRODUCTION, "Editor")
verify_mapping(TESTS, TEST_FILES, "Tests")
move_group(EDITOR, PRODUCTION)
move_group(TESTS, TEST_FILES)
rewrite_text_files()
update_repository_policy()
update_semantic_guard()
verify_result()
