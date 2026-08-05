from pathlib import Path
import subprocess

ROOT = Path('.')

MOVES = {
    'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpPhase4Bake.cs':
        'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpDependencyBake.cs',
    'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpPhase4Capture.cs':
        'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpCaptureEvidence.cs',
    'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpPhase4DApv.cs':
        'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpAdaptiveProbeVolumeBake.cs',
    'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpPhase4DVisualAcceptance.cs':
        'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpVisualAcceptance.cs',
    'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpPhase4DTools.cs':
        'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpApvVisualAcceptanceTools.cs',
    'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpPhase4Tests.cs':
        'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpSaveEvaluationTests.cs',
    'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpPhase4DiagnosticsTests.cs':
        'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpSaveEvaluationDiagnosticsTests.cs',
    'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpPhase4BakeTests.cs':
        'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpDependencyBakeTests.cs',
    'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpPhase4BakeDirtySetTests.cs':
        'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpDirtyDependencySetTests.cs',
    'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpPhase4CaptureTests.cs':
        'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpCaptureEvidenceTests.cs',
    'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpPhase4DTests.cs':
        'Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpApvVisualAcceptanceTests.cs',
    'Specs/UnityGraphicsMCP/phase4b-bake.md':
        'Specs/UnityGraphicsMCP/dependency-bake.md',
    'Specs/UnityGraphicsMCP/phase4c-capture.md':
        'Specs/UnityGraphicsMCP/capture-evidence.md',
    'Specs/UnityGraphicsMCP/phase4d-apv-visual-acceptance.md':
        'Specs/UnityGraphicsMCP/apv-visual-acceptance.md',
    'Tests/Compatibility/phase4c-verification.yaml':
        'Tests/Compatibility/capture-evidence-verification.yaml',
    'Tests/Compatibility/phase4d-verification.yaml':
        'Tests/Compatibility/apv-visual-acceptance-verification.yaml',
    '.github/workflows/phase1-unity-verification.yml':
        '.github/workflows/unity-editor-verification.yml',
}

CODE_REPLACEMENTS = [
    ('Phase4DVisualAcceptance', 'VisualAcceptance'),
    ('Phase4DAcceptance', 'VisualAcceptance'),
    ('Phase4DApv', 'AdaptiveProbeVolumeBake'),
    ('Phase4DTools', 'ApvVisualAcceptanceTools'),
    ('Phase4Capture', 'CaptureEvidence'),
    ('Phase4Bake', 'DependencyBake'),
    ('Phase4D', 'ApvVisualAcceptance'),
    ('Phase4C', 'CaptureEvidence'),
    ('Phase4B', 'DependencyBake'),
    ('Phase4A', 'SaveEvaluation'),
    ('Phase4', 'SaveEvaluation'),
    ('Phase 4D', 'APV and Visual Acceptance'),
    ('Phase 4C', 'Capture Evidence'),
    ('Phase 4B', 'Dependency Bake'),
    ('Phase 4A', 'Save and Evaluation'),
]

DOCUMENT_REPLACEMENTS = [
    ('phase4d-apv-visual-acceptance.md', 'apv-visual-acceptance.md'),
    ('phase4c-capture.md', 'capture-evidence.md'),
    ('phase4b-bake.md', 'dependency-bake.md'),
    ('phase4d-verification.yaml', 'apv-visual-acceptance-verification.yaml'),
    ('phase4c-verification.yaml', 'capture-evidence-verification.yaml'),
    ('phase_4d_apv_visual_acceptance_complete', 'apv_visual_acceptance_complete'),
    ('phase_4d_editor_operational', 'editor_operational'),
    ('phase_4c_capture_evidence_complete', 'capture_evidence_complete'),
    ('phase_4c_editor_operational', 'editor_operational'),
    ('phase_4b_editor_verified', 'dependency_bake_editor_verified'),
    ('phase_4a_editor_verified', 'save_evaluation_editor_verified'),
    ('phase_4d', 'apv_visual_acceptance'),
    ('phase_4c', 'capture_evidence'),
    ('phase_4b', 'dependency_bake'),
    ('phase_4a', 'save_evaluation'),
    ('MUMCP-PHASE4D', 'MUMCP-APV-VISUAL-ACCEPTANCE'),
    ('MUMCP-PHASE4C', 'MUMCP-CAPTURE-EVIDENCE'),
    ('MyUnityMCP-Phase4D-Unity-Evidence', 'MyUnityMCP-APV-Visual-Acceptance-Evidence'),
    ('MyUnityMCP-Phase4C-Unity-Evidence', 'MyUnityMCP-Capture-Evidence'),
    ('Phase 4D candidates', 'Hardening candidates'),
    ('Phase 4D', 'APV and Visual Acceptance'),
    ('Phase 4C', 'Capture Evidence'),
    ('Phase 4B', 'Dependency Bake'),
    ('Phase 4A', 'Save and Evaluation'),
]


def git_move(source: str, destination: str) -> None:
    source_path = ROOT / source
    destination_path = ROOT / destination
    if source_path.exists():
        destination_path.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(['git', 'mv', source, destination], check=True)

    source_meta = ROOT / f'{source}.meta'
    destination_meta = ROOT / f'{destination}.meta'
    if source_meta.exists():
        destination_meta.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(['git', 'mv', f'{source}.meta', f'{destination}.meta'], check=True)


def update_text_files() -> None:
    suffixes = {'.cs', '.md', '.yaml', '.yml', '.json'}
    excluded = {
        '.github/workflows/semantic-naming-migration.yml',
        '.github/workflows/semantic-naming-pr-migration.yml',
        'Tools/Repository/semantic_naming_migration.py',
    }

    for path in ROOT.rglob('*'):
        if not path.is_file() or '.git' in path.parts:
            continue
        relative = path.as_posix()
        if relative in excluded or path.suffix.lower() not in suffixes:
            continue

        text = path.read_text(encoding='utf-8')
        original = text
        if path.suffix.lower() == '.cs':
            for old, new in CODE_REPLACEMENTS:
                text = text.replace(old, new)
        for old, new in DOCUMENT_REPLACEMENTS:
            text = text.replace(old, new)
        text = text.replace('"version": "0.7.0"', '"version": "0.7.1"')
        text = text.replace('version: "0.7.0"', 'version: "0.7.1"')
        if text != original:
            path.write_text(text, encoding='utf-8')


def normalize_verification_workflow() -> None:
    workflow = ROOT / '.github/workflows/unity-editor-verification.yml'
    if not workflow.exists():
        return

    text = workflow.read_text(encoding='utf-8')
    replacements = [
        ('name: APV and Visual Acceptance Unity Verification',
         'name: MyUnityMCP Unity Editor Verification'),
        ('group: apv-visual-acceptance-unity-',
         'group: my-unity-mcp-editor-'),
        ('Run APV and Visual Acceptance EditMode tests',
         'Run Unity EditMode contract tests'),
        ('Verify APV and Visual Acceptance NUnit result',
         'Verify Unity NUnit result'),
        ('APV and Visual Acceptance results:',
         'Unity Editor contract results:'),
        ('Expected at least 98 Phase 1-APV and Visual Acceptance contract tests',
         'Expected at least 98 Unity Editor contract tests'),
        ('Upload APV and Visual Acceptance Unity evidence',
         'Upload Unity Editor evidence'),
        ('MyUnityMCP-APV-Visual-Acceptance-Evidence',
         'MyUnityMCP-Unity-Editor-Evidence'),
        ('MyUnityMCP-APV-Visual-Acceptance-6000.0.75f1-',
         'MyUnityMCP-Editor-6000.0.75f1-'),
    ]
    for old, new in replacements:
        text = text.replace(old, new)

    text = '\n'.join(
        line for line in text.splitlines()
        if 'MyUnityMCP-Phase' not in line
    ) + '\n'

    marker = '      - name: Require Unity license secrets\n'
    guard_step = (
        '      - name: Verify semantic feature names\n'
        '        run: python3 Tests/Compatibility/verify-semantic-names.py\n\n'
    )
    if guard_step not in text and marker in text:
        text = text.replace(marker, guard_step + marker)

    workflow.write_text(text, encoding='utf-8')


def write_policy_and_guard() -> None:
    naming_doc = ROOT / 'Specs/UnityGraphicsMCP/naming.md'
    naming_doc.write_text(
        '# UnityGraphicsMCP Naming Rules\n\n'
        '## Rule\n\n'
        'Production code、Editor Test、Workflow、運用中の仕様書およびEvidenceは、'
        '実装順やDelivery Phaseではなく責務・Capabilityで命名する。\n\n'
        '## Prohibited\n\n'
        '- `Phase1`、`Phase2`、`Phase3`、`Phase4`等を型名、Method名、File名、Workflow名へ含めること\n'
        '- 一時的なRoadmap番号をPublic APIまたは内部Domain用語として固定すること\n'
        '- 実装時期だけを表し、責務を説明しない名前\n\n'
        '## Preferred capability names\n\n'
        '- Save Evaluation\n'
        '- Dependency Bake\n'
        '- Capture Evidence\n'
        '- Adaptive Probe Volume Bake\n'
        '- Visual Acceptance\n'
        '- APV Visual Acceptance Tools\n\n'
        'Delivery Phaseは、過去のPR、Release Note、Task履歴など履歴を説明する文脈だけで使用できる。\n',
        encoding='utf-8',
    )

    guard = ROOT / 'Tests/Compatibility/verify-semantic-names.py'
    guard.write_text(
        'from pathlib import Path\n'
        'import re\n'
        'import sys\n\n'
        'ROOT = Path(__file__).resolve().parents[2]\n'
        'SCAN_ROOTS = [\n'
        '    ROOT / "Packages/com.darumappap.my-unity-mcp/Editor",\n'
        '    ROOT / "Packages/com.darumappap.my-unity-mcp/Tests/Editor",\n'
        ']\n'
        'FORBIDDEN = re.compile(r"(?:UnityGraphicsMcp|class\\s+\\w*)Phase\\d|Phase4", re.IGNORECASE)\n'
        'violations = []\n\n'
        'for scan_root in SCAN_ROOTS:\n'
        '    for path in scan_root.rglob("*"):\n'
        '        if not path.is_file():\n'
        '            continue\n'
        '        if "phase" in path.name.lower():\n'
        '            violations.append(f"phase-named file: {path.relative_to(ROOT)}")\n'
        '        if path.suffix.lower() != ".cs":\n'
        '            continue\n'
        '        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):\n'
        '            if FORBIDDEN.search(line):\n'
        '                violations.append(\n'
        '                    f"phase-named identifier: {path.relative_to(ROOT)}:{line_number}: {line.strip()}"\n'
        '                )\n\n'
        'if violations:\n'
        '    print("Semantic naming violations detected:")\n'
        '    print("\\n".join(violations))\n'
        '    sys.exit(1)\n\n'
        'print("Semantic naming guard passed.")\n',
        encoding='utf-8',
    )


for source, destination in MOVES.items():
    git_move(source, destination)

update_text_files()
normalize_verification_workflow()
write_policy_and_guard()
