from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
SCAN_ROOTS = [
    ROOT / "Packages/com.darumappap.my-unity-mcp/Editor",
    ROOT / "Packages/com.darumappap.my-unity-mcp/Tests/Editor",
]
PHASE_FORBIDDEN = re.compile(r"(?:class\s+\w*)Phase\d|Phase4", re.IGNORECASE)
REDUNDANT_PREFIX = re.compile(r"\bUnityGraphicsMcp[A-Z][A-Za-z0-9_]*\b")
violations = []

for scan_root in SCAN_ROOTS:
    for path in scan_root.rglob("*"):
        if not path.is_file():
            continue
        if "phase" in path.name.lower():
            violations.append(f"phase-named file: {path.relative_to(ROOT)}")
        if REDUNDANT_PREFIX.search(path.name):
            violations.append(f"redundant UnityGraphicsMcp file prefix: {path.relative_to(ROOT)}")
        if path.suffix.lower() != ".cs":
            continue
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if PHASE_FORBIDDEN.search(line):
                violations.append(f"phase-named identifier: {path.relative_to(ROOT)}:{line_number}: {line.strip()}")
            if REDUNDANT_PREFIX.search(line):
                violations.append(f"redundant UnityGraphicsMcp type prefix: {path.relative_to(ROOT)}:{line_number}: {line.strip()}")

if violations:
    print("Semantic naming violations detected:")
    print("\n".join(violations))
    sys.exit(1)

print("Semantic naming guard passed.")
