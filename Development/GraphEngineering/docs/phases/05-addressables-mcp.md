# Phase 5 — AddressablesMCP


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

Addressables Group／Profile／Entry／Dependencyを安全に検査・変更し、Content BuildをEvidence化する。

## Package gate

Installed package/versionを正本にする。
Absent時は自動導入せずUnavailableを返す。
Unsupported versionへSilent Reflection fallbackしない。

## Initial scope

- Settings／groups／profiles／entries inspection
- Duplicate address／missing dependency
- Typed move／label／address plan
- Explicit mutation／undo
- Content build plan／build/report

## Deferred

- Runtime load tools
- CDN／CCD upload
- Credential操作
- Remote hosting
- Secret profile values

## Safety

- GUID不変
- Rename impact
- Duplicate address prevention
- Secret redaction
- MutationとContent Build承認分離

## Required evidence

- `package_gate_tests`
- `addressables_mutation_tests`
- `content_build_evidence`

## Done

Package present/absent両契約、typed mutation、content buildが検証される。
