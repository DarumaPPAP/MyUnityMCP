# Phase 6 — UIMCP


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

uGUI／UI Toolkitの構造、Layout、Asset referenceを検査し、Typed Planで変更する。

## Backends

- uGUI
- UI Toolkit Runtime
- UI Toolkit Editor

Backend migrationは自動で行わない。
最初のvertical sliceは実Projectで優先される一方から開始する。

## uGUI scope

- Canvas／Scaler／sorting
- RectTransform
- Graphic hierarchy
- EventSystem
- Basic text/image/button references

## UI Toolkit scope

- UXML／USS
- UIDocument／PanelSettings
- Visual tree
- Class/reference validation
- Controlled file patch

## Validation

- Multiple aspect ratios
- Overflow／truncation
- Missing assets
- Duplicate EventSystem
- Raycast obstruction
- Broken UXML／USS references

## Required evidence

- `ui_backend_tests`
- `layout_validation`
- `visual_review_handoff`

## Done

最低一BackendのE2Eと他Backendの正確なSupport Matrix。
