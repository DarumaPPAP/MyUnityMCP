#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
SELF = Path("Tests/Release/verify_repository_hygiene.py")
CONTENT_EXCLUSIONS = {
    SELF,
    Path("Specs/UnityGraphicsMCP/naming.md"),
}

ACTIVE_ROOTS = (
    Path("Packages/com.darumappap.my-unity-mcp/Editor"),
    Path("Packages/com.darumappap.my-unity-mcp/Tests"),
    Path("Catalog"),
    Path("Specs"),
    Path("Tests/Release"),
    Path("Tests/Compatibility"),
    Path(".github/workflows"),
)

ACTIVE_TEXT_ROOTS = (
    Path("AGENTS.md"),
    Path("README.md"),
    *ACTIVE_ROOTS,
)

FORBIDDEN_PATHS = (
    Path("Specs/UnityGraphicsMCP/support-matrix.md"),
    Path("Specs/UnityGraphicsMCP/plan.md"),
    Path("Specs/UnityGraphicsMCP/tasks.md"),
    Path("Specs/UnityGraphicsMCP/editor-tool-design.md"),
    Path("Design/UnityAgentMCP/spec.md"),
    Path("Development"),
    Path(".github/workflows/release-source-audit-export.yml"),
    Path(".github/workflows/apply-v1-release.yml"),
    Path("Tools/apply-v1-release.py"),
    Path("Packages/com.darumappap.my-unity-mcp/Samples~"),
    Path("SampleProjects"),
)

FORBIDDEN_CURRENT_EVIDENCE = (
    "production-baseline-verification.yaml",
    "world-creator-production-verification.yaml",
    "integration-hardening-verification.yaml",
    "apv-visual-acceptance-verification.yaml",
    "capture-evidence-verification.yaml",
    "verification-matrix.yaml",
)

PHASE_NAME_RE = re.compile(
    r"(?:_p1(?![A-Za-z0-9])|(?<![A-Za-z0-9])phase[_\-\s]?(?:1|2|11)(?![A-Za-z0-9]))",
    re.IGNORECASE,
)

TEXT_SUFFIXES = {".cs", ".json", ".md", ".py", ".toml", ".yaml", ".yml"}


def iter_files(base: Path):
    path = ROOT / base
    if path.is_file():
        yield path
    elif path.is_dir():
        yield from (item for item in path.rglob("*") if item.is_file())


def fail(message: str) -> None:
    raise SystemExit(message)


for relative in FORBIDDEN_PATHS:
    if (ROOT / relative).exists():
        fail(f"Obsolete path resurrected: {relative.as_posix()}")

compatibility_root = ROOT / "Tests/Compatibility"
for name in FORBIDDEN_CURRENT_EVIDENCE:
    if (compatibility_root / name).exists():
        fail(f"Historical-only evidence reintroduced as current evidence: Tests/Compatibility/{name}")

for root in ACTIVE_ROOTS:
    for path in iter_files(root):
        name = path.name.lower()
        if path.suffix.lower() in {".bak", ".old", ".tmp"} or "_backup" in name or "_copy" in name:
            fail(f"Temporary or backup artifact in active path: {path.relative_to(ROOT).as_posix()}")

for root in ACTIVE_TEXT_ROOTS:
    for path in iter_files(root):
        relative = path.relative_to(ROOT)
        relative_text = relative.as_posix()
        if PHASE_NAME_RE.search(relative_text):
            fail(f"Delivery-phase naming in active path: {relative_text}")
        if relative in CONTENT_EXCLUSIONS or path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        text = path.read_text(encoding="utf-8")
        if PHASE_NAME_RE.search(text):
            fail(f"Delivery-phase wording in active file: {relative_text}")

print(
    "Repository hygiene PASS: "
    f"active_roots={len(ACTIVE_ROOTS)}, "
    f"forbidden_paths={len(FORBIDDEN_PATHS)}, "
    f"historical_evidence={len(FORBIDDEN_CURRENT_EVIDENCE)}"
)
