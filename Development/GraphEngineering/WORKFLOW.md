---
workflow_id: myunitymcp_codex_implementation
graph: graph/implementation-graph.json
state: state/roadmap-state.json
context_policy: docs/context/context-policy.md
evidence_schema: schemas/evidence.schema.json
completion_gate: project_completion_gate
development_branch: feature/graph-engineering-master
delivery_base_branch: main
delivery_branch_pattern: delivery/*
delivery_policy: docs/delivery/artifact-only-pr-policy.md
delivery_guard: scripts/delivery_guard.py
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

`feature/graph-engineering-master`は、Graph、State、Evidence、計画、検証補助と
製品実装を一緒に育てる長期開発環境として扱う。
このBranch自体を`main`へMergeしない。

## Branch model

### Graph Engineering development branch

```text
feature/graph-engineering-master
```

次を保持する。

- `Development/GraphEngineering/`
- Roadmap State／Evidence／ExecPlan
- 実装途中の製品変更
- Test／検証結果
- Visualize Dashboard
- 失敗と再開情報

### Artifact delivery branch

成果物を`main`へ届けるときだけ、最新`main`から次を作る。

```text
delivery/<goal-or-capability>
```

Graph Engineering Branchから、承認済みの成果物だけを移植する。
`Development/GraphEngineering/`、Roadmap State、Evidence、Source Archive、
開発専用DashboardをDelivery Branchへ含めない。

### Pull request

```text
delivery/<goal-or-capability> → main
```

PRには製品Code、製品Test、必要な公開Documentation、
必要なRelease／CI変更だけを含める。
Graph Engineering Branchから`main`への直接PRは禁止する。

## Dispatch preflight

- Workflow、Graph、State、Schemaを読める
- GraphがDAGとして有効
- Current Nodeの依存が完了
- WorktreeとBranchが安全
- 通常開発は`feature/graph-engineering-master`上
- 必要Tool／Unity環境が利用可能
- 無人Loopの場合は反復Budgetが明示
- Human Gate待ちでない

## Delivery preflight

1. `main`の最新状態を取得
2. `main`起点で`delivery/*`を作成
3. 承認済み成果物だけを移植
4. 製品Compile／Testを再実行
5. Graph Engineering側のEvidenceとDelivery側の実測結果を照合
6. `delivery_guard.py`で開発環境混入を拒否
7. Human Review後にPRを作成
8. Mergeは明示指示がある場合だけ実行

検証例:

```powershell
py Development/GraphEngineering/scripts/delivery_guard.py `
  --base main `
  --head delivery/<goal-or-capability>
```

Delivery BranchにScriptを含めないため、Graph Engineering BranchのWorktree、
または絶対Pathから実行する。

## Successful run

1回の実行は、次のHandoff Stateで終了してよい。

- `checkpoint_completed`
- `awaiting_human_review`
- `blocked_external_environment`
- `ready_for_next_node`
- `artifact_delivery_ready`

これはProject全体の`completed`を意味しない。

## After merge

成果物PRが`main`へMergeされた後は、最新`main`をGraph Engineering Branchへ取り込み、
Roadmap StateへMerge Commitと採用Evidenceを記録する。
Graph Engineeringの履歴を`main`へ逆流させない。

## Failure

Workflow／Graph／StateのParse失敗はDispatchをBlockする。
PromptへSilent Fallbackしない。
Delivery Guard失敗時はPRを作成せず、禁止Pathを除去して再検証する。
