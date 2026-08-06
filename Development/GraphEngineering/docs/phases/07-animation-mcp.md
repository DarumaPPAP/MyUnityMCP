# Phase 7 — AnimationMCP


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

Animator ControllerのState Graphを検査・検証し、Typed state／transition／parameter operationを提供する。

## Scope

- Animator／controller／layers
- State／sub-state machine
- Parameter
- Transition／condition
- BlendTree references
- Existing clip assignment
- Graph validation
- Undo

## Deferred

- Massive curve editing
- Retarget automation
- Model importer mutation
- Motion capture generation
- Full Playables generation

## Findings

- Unreachable state
- Missing parameter／motion
- Impossible condition
- AnyState storm
- Invalid default
- Loop／exit issue
- Layer／avatar mask issue

## Required evidence

- `animator_graph_tests`
- `typed_mutation_tests`
- `undo_revision_tests`

## Done

Existing controllerをrevision-safeに検査・変更・undoできる。
