from pathlib import Path

source_path = Path('Tools/Repository/semantic_naming_migration.py')
source = source_path.read_text(encoding='utf-8')
source = source.replace(
    "    '.github/workflows/phase1-unity-verification.yml':\n"
    "        '.github/workflows/unity-editor-verification.yml',\n",
    '',
)
source = source.replace(
    "        if relative in excluded or path.suffix.lower() not in suffixes:\n",
    "        if (relative in excluded or relative.startswith('.github/workflows/') or "
    "path.suffix.lower() not in suffixes):\n",
)
source = source.replace('normalize_verification_workflow()\n', '')
exec(compile(source, str(source_path), 'exec'), {'__name__': '__main__'})
