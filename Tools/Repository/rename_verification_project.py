from pathlib import Path
import subprocess

source = Path('TestProjects/MyUnityMCPPhase1')
destination = Path('TestProjects/MyUnityMCPVerification')

if source.exists() and not destination.exists():
    subprocess.run(['git', 'mv', str(source), str(destination)], check=True)

for path in Path('.').rglob('*'):
    if not path.is_file() or '.git' in path.parts or path.as_posix().startswith('.github/workflows/'):
        continue
    if path.suffix.lower() not in {'.cs', '.md', '.yaml', '.yml', '.json', '.asset', '.meta'}:
        continue
    try:
        text = path.read_text(encoding='utf-8')
    except UnicodeDecodeError:
        continue
    updated = text.replace('TestProjects/MyUnityMCPPhase1', 'TestProjects/MyUnityMCPVerification')
    updated = updated.replace('MyUnityMCPPhase1', 'MyUnityMCPVerification')
    if updated != text:
        path.write_text(updated, encoding='utf-8')
