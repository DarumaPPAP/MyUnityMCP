# Artifact-only Pull Request Policy

## Decision

Graph EngineeringをMyUnityMCPの長期開発環境として使用する。

```text
feature/graph-engineering-master
```

このBranchは開発履歴を保持するが、Branch全体を`main`へMergeしない。
最終成果物だけを、最新`main`から作成したDelivery Branchへ移植してPull Requestを送る。

## Why

Graph Engineering Branchには製品成果物以外も含まれる。

- Implementation Graph
- Roadmap State
- Evidence
- ExecPlan
- Failure／Resume情報
- Visualize Dashboard
- Harness
- Source Archive
- 開発専用の評価資料

これらを製品Release履歴へ混入させず、`main`を配布・製品正本として保つ。

## Branch topology

```text
main
├─ delivery/phase-01-unity-agent-runtime
├─ delivery/phase-03-profiler-mcp
└─ delivery/<goal-or-capability>

feature/graph-engineering-master
└─ 長期開発環境
```

禁止:

```text
feature/graph-engineering-master → main
```

許可:

```text
delivery/<goal-or-capability> → main
```

## Development flow

1. `feature/graph-engineering-master`でCurrent Nodeを決定
2. Inspect／Plan／Implement／Validate
3. EvidenceとRoadmap Stateを更新
4. Human Reviewを受ける
5. Delivery対象の成果物PathをManifestへ固定
6. 最新`main`から`delivery/*`を作成
7. 対象成果物だけを移植
8. Delivery Branch上でCompile／Testを再実行
9. `delivery_guard.py`を実行
10. PR作成の明示承認を得る
11. `delivery/* → main`のPRを作成
12. Mergeは別の明示承認を得る
13. Merge後、最新`main`をGraph Engineering Branchへ同期
14. Merge SHAをRoadmap Evidenceへ記録

## Artifact manifest

各Deliveryでは、`docs/delivery/DELIVERY_MANIFEST_TEMPLATE.yaml`をコピーし、
対象GoalのActive ExecPlanまたはEvidence Directoryへ保存する。

Manifestには次を明記する。

- Delivery ID
- Base Main SHA
- Source Graph Engineering SHA
- Include Path
- Exclude Path
- Product Validation
- Human Review
- PR作成承認
- Merge承認

Include PathにないファイルをDelivery Branchへ持ち込まない。

## Always excluded

次は成果物PRへ含めない。

```text
Development/GraphEngineering/**
GRAPH_ENGINEERING.md
```

特に次を禁止する。

- `state/roadmap-state.json`
- `state/evidence/**`
- `docs/plans/**`
- `source-archive/**`
- `visualize/**`
- `tools/graph-viewer/**`
- Graph Engineering専用Harness／Test

## Allowed artifacts

GoalのScope内で、次を成果物として扱える。

- `Packages/**`の製品Code
- 製品に付随するEditor／Runtime Test
- Package公開Documentation
- Samples／Templates
- 製品に必要なCatalog／Manifest
- 製品検証に必要なCI
- Release時に明示承認されたVersion／Changelog変更

CIやRelease Workflowは自動的に許可せず、GoalのScopeとHuman Approvalが必要。

## Delivery validation

Graph Engineering Worktreeから実行する。

```powershell
py Development/GraphEngineering/scripts/delivery_guard.py `
  --base main `
  --head delivery/<goal-or-capability>
```

JSON出力:

```powershell
py Development/GraphEngineering/scripts/delivery_guard.py `
  --base main `
  --head delivery/<goal-or-capability> `
  --json
```

Guardは次を拒否する。

- 変更ファイルが0件
- Graph Engineering環境の混入
- Source Archive／一時展開Workflowの混入
- `main`をBaseにしていない比較指定
- Git Diff取得失敗

Guard PASSは製品品質の証明ではない。
Compile、Test、Evidence、Human Review、PR Approvalは別に必要。

## Pull request contract

PR本文には最低限次を含める。

- Goal
- Included artifacts
- Explicitly excluded development assets
- Base Main SHA
- Source Graph Engineering SHA
- Tests executed
- Evidence references
- Known limitations
- Human Review status
- Terminal Goal satisfied: true / false

個別成果物PRでは通常`Terminal Goal satisfied: false`である。

## Merge and sync

- PR作成承認とMerge承認を分離する
- Mergeはユーザーが明示した場合だけ実行
- Published Tagを自動移動しない
- Merge後は`main`をGraph Engineering Branchへ取り込む
- Graph Engineering Branchを`main`へ逆Mergeしない
