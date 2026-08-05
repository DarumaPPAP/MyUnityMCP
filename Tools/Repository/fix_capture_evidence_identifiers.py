from pathlib import Path

path = Path('Packages/com.darumappap.my-unity-mcp/Editor/UnityGraphicsMcpCaptureEvidence.cs')
text = path.read_text(encoding='utf-8')
replacements = {
    'TryResolveCaptureEvidenceamera': 'TryResolveSaveEvaluationCamera',
    'IsValidCaptureEvidenceSize': 'IsValidSaveEvaluationCaptureSize',
    'CaptureSaveEvaluationssetDirtyState': 'CaptureSaveEvaluationAssetDirtyState',
    'HasCaptureEvidenceReadOnlyViolation': 'HasSaveEvaluationCaptureReadOnlyViolation',
    'HashDependencyBakeytes': 'HashSaveEvaluationBytes',
}
for old, new in replacements.items():
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'Expected exactly one {old}, found {count}')
    text = text.replace(old, new)
path.write_text(text, encoding='utf-8')
