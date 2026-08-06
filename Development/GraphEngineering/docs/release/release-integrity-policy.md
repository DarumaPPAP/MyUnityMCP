# Release Integrity Policy

## Decision

MyUnityMCPの公開済みRelease TagはImmutableとして扱う。

- 公開済みTagを移動・削除・Force更新しない。
- 既存Tagの再検証・再配布は、Tagが指すCommitをSourceにする。
- `main`は公開後に進んでよい。
- 製品内容を変更する場合はVersionを上げ、新しいTagとReleaseを作成する。
- Release公開、Version更新、既存Tagの扱いはHuman Gateとする。

## Current v1.0.0 resolution

- `v1.0.0`: `baa9a1a5e0324560cea099a1dbddeea45e7a8527`
- `main`: `1ffec90b150b76a0cb40cac98aa25ae7fa31ba76`
- Relation: `main` is one commit ahead.
- The only changed path is `.github/workflows/release-tag.yml`.
- Resolution: keep `v1.0.0` immutable and use its tagged commit for reruns.
- Future product changes require a new version and patch-or-later release.

## Machine enforcement

- `.github/workflows/release-tag.yml`
  - resolves existing tags to their tagged commit;
  - never force-moves a tag;
  - builds artifacts from the immutable source commit.
- `scripts/release_integrity_guard.py`
  - validates version alignment, release evidence, workflow identity, immutable rerun behavior, and current tag/main relation.
- `tests/test_release_integrity_guard.py`
  - covers aligned, main-ahead, missing-tag, mismatch, missing-identity, mutable-rerun, and failed-evidence cases.
