#!/usr/bin/env python3
"""Validate that a delivery branch contains product artifacts only."""

from __future__ import annotations

import argparse
import json
from pathlib import PurePosixPath
import subprocess
import sys
from typing import Iterable, Sequence

FORBIDDEN_PREFIXES = (
    "Development/GraphEngineering/",
)

FORBIDDEN_EXACT_PATHS = {
    "GRAPH_ENGINEERING.md",
    ".github/workflows/expand-graph-engineering-master.yml",
}

FORBIDDEN_PARTS = {
    "source-archive",
    "__pycache__",
}

ALLOWED_BASES = {
    "main",
    "origin/main",
    "refs/heads/main",
    "refs/remotes/origin/main",
}


class DeliveryGuardError(RuntimeError):
    """Raised when delivery validation cannot be completed."""


def normalize_path(value: str) -> str:
    return PurePosixPath(value.strip().replace("\\", "/")).as_posix()


def forbidden_reason(path: str) -> str | None:
    normalized = normalize_path(path)

    if normalized in FORBIDDEN_EXACT_PATHS:
        return "development-only exact path"

    if any(normalized.startswith(prefix) for prefix in FORBIDDEN_PREFIXES):
        return "Graph Engineering development environment"

    if any(part in FORBIDDEN_PARTS for part in PurePosixPath(normalized).parts):
        return "development-only archive or generated cache"

    return None


def validate_changed_paths(paths: Iterable[str]) -> tuple[list[str], list[dict[str, str]]]:
    normalized_paths = sorted({normalize_path(path) for path in paths if path.strip()})
    violations = []

    for path in normalized_paths:
        reason = forbidden_reason(path)
        if reason is not None:
            violations.append({"path": path, "reason": reason})

    return normalized_paths, violations


def git_changed_paths(base: str, head: str) -> list[str]:
    command = [
        "git",
        "diff",
        "--name-only",
        "--diff-filter=ACMRTUXB",
        f"{base}...{head}",
    ]
    completed = subprocess.run(
        command,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )

    if completed.returncode != 0:
        stderr = completed.stderr.strip() or "unknown git diff error"
        raise DeliveryGuardError(stderr)

    return completed.stdout.splitlines()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Reject Graph Engineering development assets from artifact delivery branches."
    )
    parser.add_argument("--base", required=True, help="Delivery base. Must resolve to main.")
    parser.add_argument("--head", required=True, help="Delivery branch or commit to inspect.")
    parser.add_argument("--json", action="store_true", help="Print machine-readable JSON.")
    return parser


def render_result(
    *,
    base: str,
    head: str,
    changed_paths: Sequence[str],
    violations: Sequence[dict[str, str]],
) -> dict[str, object]:
    return {
        "base": base,
        "head": head,
        "changed_file_count": len(changed_paths),
        "changed_paths": list(changed_paths),
        "violations": list(violations),
        "passed": bool(changed_paths) and not violations,
    }


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)

    if args.base not in ALLOWED_BASES:
        message = {
            "passed": False,
            "error": "delivery base must be main or origin/main",
            "base": args.base,
            "head": args.head,
        }
        print(json.dumps(message, ensure_ascii=False, indent=2) if args.json else message["error"])
        return 1

    try:
        changed_paths, violations = validate_changed_paths(
            git_changed_paths(args.base, args.head)
        )
    except DeliveryGuardError as error:
        message = {
            "passed": False,
            "error": str(error),
            "base": args.base,
            "head": args.head,
        }
        print(json.dumps(message, ensure_ascii=False, indent=2) if args.json else f"ERROR: {error}")
        return 2

    result = render_result(
        base=args.base,
        head=args.head,
        changed_paths=changed_paths,
        violations=violations,
    )

    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print(f"Base: {args.base}")
        print(f"Head: {args.head}")
        print(f"Changed files: {len(changed_paths)}")

        if not changed_paths:
            print("FAIL: delivery branch has no changed artifacts.")
        elif violations:
            print("FAIL: development-only paths were found.")
            for violation in violations:
                print(f"- {violation['path']}: {violation['reason']}")
        else:
            print("PASS: artifact-only delivery path check succeeded.")

    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
