#!/usr/bin/env python3
"""Validate MyUnityMCP release identity without moving published tags."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


class ReleaseIntegrityError(RuntimeError):
    """Raised when release identity is inconsistent."""


@dataclass(frozen=True)
class ReleaseSnapshot:
    version: str
    package_version: str
    manifest_version: str
    support_version: str
    changelog_has_version: bool
    workflow_has_identity: bool
    workflow_preserves_published_tag: bool
    tag_exists: bool
    tag_commit: str
    main_commit: str
    release_evidence_status: str


def _extract(pattern: str, text: str, label: str) -> str:
    match = re.search(pattern, text, re.MULTILINE)
    if not match:
        raise ReleaseIntegrityError(f"{label} was not found")
    return match.group(1)


def _git(repo_root: Path, *args: str) -> str:
    completed = subprocess.run(
        ["git", "-C", str(repo_root), *args],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if completed.returncode != 0:
        raise ReleaseIntegrityError(
            completed.stderr.strip() or f"git {' '.join(args)} failed"
        )
    return completed.stdout.strip()


def _git_optional(repo_root: Path, *args: str) -> str:
    completed = subprocess.run(
        ["git", "-C", str(repo_root), *args],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return completed.stdout.strip() if completed.returncode == 0 else ""


def _resolve_main_commit(repo_root: Path) -> str:
    # actions/checkoutでfeature branchを明示Checkoutした場合、ローカルmainは作られず
    # origin/mainだけが存在します。Releaseの基準Branchを変えず、その2形だけを許可します。
    for reference in (
        "main^{commit}",
        "origin/main^{commit}",
        "refs/remotes/origin/main^{commit}",
    ):
        resolved = _git_optional(repo_root, "rev-parse", reference)
        if resolved:
            return resolved
    raise ReleaseIntegrityError(
        "main commit could not be resolved from local main or origin/main"
    )


def _workflow_preserves_published_tag(workflow: str) -> bool:
    required_fragments = (
        'release_mode="verify_existing"',
        'source_commit=$(git rev-parse "$tag^{commit}")',
        'git checkout --detach "$source_commit"',
    )
    forbidden_fragments = (
        "git tag -f",
        "git push --force",
        "git push -f",
    )
    return all(value in workflow for value in required_fragments) and not any(
        value in workflow for value in forbidden_fragments
    )


def load_snapshot(repo_root: Path, *, tag: str | None = None) -> ReleaseSnapshot:
    version = (repo_root / "VERSION").read_text(encoding="utf-8").strip()
    package = json.loads(
        (repo_root / "Packages/com.darumappap.my-unity-mcp/package.json").read_text(
            encoding="utf-8"
        )
    )
    manifest = (
        repo_root / "Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml"
    ).read_text(encoding="utf-8")
    support = (repo_root / "Tests/Compatibility/support-matrix.yaml").read_text(
        encoding="utf-8"
    )
    changelog = (repo_root / "CHANGELOG.md").read_text(encoding="utf-8")
    workflow = (repo_root / ".github/workflows/release-tag.yml").read_text(
        encoding="utf-8"
    )
    evidence = (repo_root / "Tests/Compatibility/release-verification.yaml").read_text(
        encoding="utf-8"
    )

    resolved_tag = tag or f"v{version}"
    tag_exists = (
        subprocess.run(
            ["git", "-C", str(repo_root), "rev-parse", "--verify", resolved_tag],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
        ).returncode
        == 0
    )
    tag_commit = _git(repo_root, "rev-parse", f"{resolved_tag}^{{commit}}") if tag_exists else ""
    main_commit = _resolve_main_commit(repo_root)

    return ReleaseSnapshot(
        version=version,
        package_version=str(package.get("version", "")),
        manifest_version=_extract(
            r'^version:\s*"([^"]+)"\s*$', manifest, "manifest version"
        ),
        support_version=_extract(
            r'^package_version:\s*"([^"]+)"\s*$', support, "support matrix version"
        ),
        changelog_has_version=bool(
            re.search(rf"^## \[{re.escape(version)}\] - ", changelog, re.MULTILINE)
        ),
        workflow_has_identity=(
            'git config user.name "github-actions[bot]"' in workflow
            and "41898282+github-actions[bot]@users.noreply.github.com" in workflow
        ),
        workflow_preserves_published_tag=_workflow_preserves_published_tag(workflow),
        tag_exists=tag_exists,
        tag_commit=tag_commit,
        main_commit=main_commit,
        release_evidence_status=(
            "passed" if "verification_status: passed" in evidence else "not_passed"
        ),
    )


def validate_snapshot(
    snapshot: ReleaseSnapshot,
    *,
    published_tag_policy: str,
) -> dict[str, object]:
    mismatches: list[str] = []

    for label, value in (
        ("package.json", snapshot.package_version),
        ("MCP manifest", snapshot.manifest_version),
        ("support matrix", snapshot.support_version),
    ):
        if value != snapshot.version:
            mismatches.append(
                f"{label} version {value!r} does not match VERSION {snapshot.version!r}"
            )

    if not snapshot.changelog_has_version:
        mismatches.append("CHANGELOG does not contain the current VERSION")
    if not snapshot.workflow_has_identity:
        mismatches.append("release workflow git identity is missing")
    if not snapshot.workflow_preserves_published_tag:
        mismatches.append("release workflow does not preserve immutable published tags")
    if not snapshot.tag_exists:
        mismatches.append(f"published tag v{snapshot.version} is missing")
    if snapshot.release_evidence_status != "passed":
        mismatches.append("release evidence is not passed")

    release_state = "aligned"
    next_release_action = "none"

    if snapshot.tag_exists and snapshot.tag_commit != snapshot.main_commit:
        if published_tag_policy != "immutable":
            mismatches.append("published tag differs from main and immutable policy is absent")
        else:
            release_state = "published_tag_immutable_main_ahead"
            next_release_action = "create_patch_release_for_future_product_changes"

    if mismatches:
        raise ReleaseIntegrityError("; ".join(mismatches))

    return {
        "passed": True,
        "version": snapshot.version,
        "release_state": release_state,
        "published_tag_policy": published_tag_policy,
        "tag_commit": snapshot.tag_commit,
        "main_commit": snapshot.main_commit,
        "next_release_action": next_release_action,
        "tag_moved": False,
        "rerun_source": "tag_commit" if snapshot.tag_commit != snapshot.main_commit else "main_commit",
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--published-tag-policy", choices=["immutable"], default="immutable")
    parser.add_argument("--tag", default="")
    parser.add_argument("--json", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        snapshot = load_snapshot(
            Path(args.repo_root).resolve(),
            tag=args.tag or None,
        )
        result = validate_snapshot(
            snapshot,
            published_tag_policy=args.published_tag_policy,
        )
    except (OSError, ValueError, ReleaseIntegrityError) as error:
        result = {"passed": False, "error": str(error), "tag_moved": False}
        print(json.dumps(result, ensure_ascii=False, indent=2) if args.json else result["error"])
        return 1

    print(json.dumps(result, ensure_ascii=False, indent=2) if args.json else "PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
