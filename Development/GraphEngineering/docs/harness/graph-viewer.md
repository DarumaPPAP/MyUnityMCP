# Graph Viewer Contract

## Adopted UI

直前にVisualizeで作成したクリック式`MyUnityMCP Graph Dashboard`を正式なUI正本として採用する。

Static SnapshotとLive Viewerは、次の同一UI構造を維持する。

- Codex実装グラフ／製品RuntimeグラフTab
- Summary Metric
- Node検索／Status Filter
- Status Legend
- SVG Graph
- Node Detail Inspector
- Required Evidence
- State
- 次の遷移条件

## Source of truth

- `graph/implementation-graph.json`
- `state/roadmap-state.json`
- `graph/product-runtime-graph.json`

ViewerのTitle、Subtitle、説明、座標、表示用簡略EdgeはUI Projectionであり、
Dependency、Status、Evidenceの正本ではない。
Detail InspectorではGraphに記録された完全なDependencyを表示する。

## Prohibited behavior

- ViewerからStateを変更
- Approval操作
- Phase完了操作
- Merge／Tag／Release操作
- Secret表示
- 任意Repository File公開

## Codex usage

```bash
python scripts/roadmap_harness.py next
python scripts/roadmap_harness.py viewer
```

次Nodeの機械判定はHarness CLIを正本とする。
Viewerの見た目をCompletion Evidenceにしない。
