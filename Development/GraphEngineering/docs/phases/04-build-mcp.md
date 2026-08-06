# Phase 4 — BuildMCP


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

Build configurationを検査し、Settings変更とBuildを別Approvalで実行し、BuildReportをEvidence化する。

## Initial scope

- Active build target inspection
- Scene list／profile／player settings validation
- Typed settings plan
- Build plan
- BuildPipeline.BuildPlayer
- BuildReport summary
- Artifact hash／size

## Deferred

- Store upload
- Signing credential management
- Platform SDK installation
- Remote farm
- Platform holder publishing

## Boundaries

```text
Inspect → Settings Plan → Settings Approval → Apply
       → Build Plan → Build Approval → Build → Report
```

## Rules

- Output overwrite separate confirmation
- Secrets redacted
- Cancel support truthfully reported
- Platform module absent is unsupported
- Build後Editor referencesを再取得

## Required evidence

- `build_plan_tests`
- `build_report_evidence`
- `secret_redaction`

## Done

Supported local targetでsample buildとfailure reportが再現可能。
