---
workflow_id: myunitymcp_codex_implementation
graph: graph/implementation-graph.json
state: state/roadmap-state.json
context_policy: docs/context/context-policy.md
evidence_schema: schemas/evidence.schema.json
completion_gate: project_completion_gate
default_concurrency: 1
unattended_iteration_budget: required
automatic_merge: false
automatic_release: false
automatic_tag_move: false
---

# Repository-owned Codex Workflow

## Purpose

Codex Implementation GraphをRepository Stateから進める。
これはMyUnityMCP Product Runtime Graphではない。

## Dispatch preflight

- Workflow、Graph、State、Schemaを読める
- GraphがDAGとして有効
- Current Nodeの依存が完了
- WorktreeとBranchが安全
- 必要Tool／Unity環境が利用可能
- 無人Loopの場合は反復Budgetが明示
- Human Gate待ちでない

## Successful run

1回の実行は、次のHandoff Stateで終了してよい。

- `checkpoint_completed`
- `awaiting_human_review`
- `blocked_external_environment`
- `ready_for_next_node`

これはProject全体の`completed`を意味しない。

## Failure

Workflow／Graph／StateのParse失敗はDispatchをBlockする。
PromptへSilent Fallbackしない。
