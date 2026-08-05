from pathlib import Path
import subprocess

ROOT = Path('.')

MOVES = {
    'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpPhase4BakeTools.cs':
        'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpDependencyBakeTools.cs',
    'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpPhase4CaptureTools.cs':
        'Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpCaptureEvidenceTools.cs',
}

for source, destination in MOVES.items():
    if Path(source).exists():
        subprocess.run(['git', 'mv', source, destination], check=True)
    if Path(source + '.meta').exists():
        subprocess.run(['git', 'mv', source + '.meta', destination + '.meta'], check=True)

REPLACEMENTS = [
    ('PHASE4D_MAX_ACCEPTANCE_CRITERIA', 'MAX_ACCEPTANCE_CRITERIA'),
    ('PHASE4C_CAPTURE_SHADER', 'CAPTURE_EVIDENCE_SHADER'),
    ('PHASE4C_ACCEPTANCE_CONFIRMATION', 'VISUAL_ACCEPTANCE_CONFIRMATION'),
    ('DEFAULT_PHASE4C_RENDERER_LIMIT', 'DEFAULT_CAPTURE_RENDERER_LIMIT'),
    ('MAX_PHASE4C_RENDERER_LIMIT', 'MAX_CAPTURE_RENDERER_LIMIT'),
    ('PHASE4D_APV_BAKE_MODE', 'APV_BAKE_MODE'),
    ('PHASE4D_MIN_TIMEOUT_SECONDS', 'MIN_APV_TIMEOUT_SECONDS'),
    ('PHASE4D_MAX_TIMEOUT_SECONDS', 'MAX_APV_TIMEOUT_SECONDS'),
    ('PHASE4_CAPTURE_DIAGNOSTICS', 'SAVE_EVALUATION_CAPTURE_DIAGNOSTICS'),
    ('phase4d', 'apv-visual-acceptance'),
    ('phase4c', 'capture-evidence'),
    ('phase4b', 'dependency-bake'),
    ('phase4', 'save-evaluation'),
    ('Phase4D', 'ApvVisualAcceptance'),
    ('Phase4C', 'CaptureEvidence'),
    ('Phase4B', 'DependencyBake'),
    ('Phase4', 'SaveEvaluation'),
]

scan_roots = [
    ROOT / 'Packages/com.darumappap.my-unity-mcp/Editor',
    ROOT / 'Packages/com.darumappap.my-unity-mcp/Tests/Editor',
]

for scan_root in scan_roots:
    for path in scan_root.rglob('*.cs'):
        text = path.read_text(encoding='utf-8')
        updated = text
        for old, new in REPLACEMENTS:
            updated = updated.replace(old, new)
        if updated != text:
            path.write_text(updated, encoding='utf-8')
