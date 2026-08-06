# Phase 9 — CinematicMCP


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

Timeline／Cinemachine／Graphics／Animation／AudioのBindingとShot sequenceを安全に作成・検証する。

## Package gates

- Installed Timeline version
- Installed Cinemachine major version
- Package absent時は自動導入しない
- Major API差は明示Adapter

## Scope

- Director／Timeline asset／tracks／clips／bindings
- Cinemachine cameras
- Lens／composition／follow／look-at
- Camera cuts
- Basic activation／animation／audio binding
- Shot continuity validation
- Preview／capture

## Deferred

- Acting generation
- Facial animation
- Recorder export
- Final encoding
- Complex custom playable

## Required evidence

- `package_version_gates`
- `timeline_cinemachine_e2e`
- `binding_validation`

## Done

Sample subjectsで複数Shot／Camera Cut／binding／captureがE2E動作する。
