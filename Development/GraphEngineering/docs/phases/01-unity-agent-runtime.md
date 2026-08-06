# Phase 1 — UnityAgentMCP Runtime


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

Catalog／Workflowを読み、必要Domainだけを有効化し、Policy／Approval付きProduct Runtime Graphを実行するControl Planeを実装する。

## Critical boundary

これはProduct Runtimeであり、Codex Development Harnessではない。
Unity APIを直接Mutationしない。

## Minimum vertical slice

```text
agent.inspect_capabilities
→ agent.validate_workflow
→ agent.compile_graph
→ agent.preview_execution
→ approval
→ delegate existing GraphicsMCP
→ aggregate evidence
```

## Components

- Catalog loader
- Domain registry
- Workflow loader
- Product graph compiler
- Policy engine
- Approval broker
- Execution engine
- Result aggregator
- History／trace
- Structured errors

## Initial tools

- `agent.inspect_capabilities`
- `agent.validate_workflow`
- `agent.compile_graph`
- `agent.preview_execution`
- `agent.start_execution`
- `agent.submit_approval`
- `agent.get_execution_status`
- `agent.cancel_execution`
- `agent.get_execution_history`
- `agent.get_error_catalog`

All default-disabled.

## Tests

- Valid Graphics workflow
- Design-only domain rejection
- Missing tool group
- Cycle
- Approval missing／expired
- Revision changed
- Domain reload／compile／scene set change
- Timeout／cancel／disconnect
- Partial success
- History persistence
- Direct mutation rejection

## Required evidence

- `agent_runtime_tests`
- `graphics_delegation_e2e`
- `safety_regression`

## Done

Existing GraphicsMCPをControl Plane経由でE2E実行し、直接利用も壊さない。
