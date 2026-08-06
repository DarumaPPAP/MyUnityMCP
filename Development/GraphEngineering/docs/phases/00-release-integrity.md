# Phase 0 — Release Integrity


## Engineering-layer contract

### Prompt

現在PhaseでModelへ委ねる判断、Non-goals、Output schemaをNode Promptへ限定する。
権限、Test合格、完了判定はPromptで強制した扱いにしない。

### Context

このPhase spec、関連実装、関連Tests、該当Catalog／Manifest、Current failures、
直前DecisionだけをContext Bundleへ入れる。

### Harness

Phase固有Tool、Approval、Evidence、Failure injectionを機械検証する。

### Loop

Inspect → Plan → Implement → Validate → Observe → Adjust。
同じFailureの反復、自動Side-effect Retry、Evidenceなしの完了を禁止する。

### Graph

依存Node完了後のみ開始し、Done Gate通過後に次Edgeへ進む。
Phase完了はProject完了ではない。


## Goal

Release identity、Version、Tag、Main、Evidence、Artifactを一貫したPolicyへ統一する。

## Current issue to revalidate

Observed stateでは`main`が`v1.0.0`より1 commit進んでいた。
Published tagを自動移動せず、Immutable tag policyまたはPatch releaseで解決する。

## Requirements

- Release policy
- Version／Manifest／Changelog／Support matrix consistency
- Tag target validation
- Release evidence target validation
- Artifact checksum
- Workflow identity configuration
- Idempotent release check
- Roadmap backlog representation

## Human gate

- Existing published tag handling
- New patch release
- Release publication

## Tests

- Missing tag
- Wrong commit
- Version mismatch
- Changelog mismatch
- Checksum mismatch
- Dirty worktree
- Rerun
- Identity config missing

## Required evidence

- `release_policy`
- `release_integrity_tests`
- `current_release_resolution`

## Done

Current release stateとdocumented policyが一致し、CIが再発を拒否する。
