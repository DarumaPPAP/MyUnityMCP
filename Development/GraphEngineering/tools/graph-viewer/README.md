# MyUnityMCP Graph Dashboard

直前にVisualizeで作成したクリック式Dashboardを正式なUI正本として採用しています。

## 起動

```bash
python scripts/roadmap_harness.py viewer
```

既定URL:

```text
http://127.0.0.1:8765/
```

## 採用UI

- Codex実装グラフ／製品RuntimeグラフのTab切替
- 4つのSummary Metric
- Node検索／Status Filter
- Status Legend
- クリック可能なSVG Graph
- Nodeの役割、依存、必須Evidence、State、遷移条件
- Live版は3秒ごとにRoadmap StateをRead-only更新

## Source of truth

- `graph/implementation-graph.json`
- `state/roadmap-state.json`
- `graph/product-runtime-graph.json`

UIのTitle、説明、座標は表示用Projectionです。
Dependency、Status、Evidence、Terminal Nodeの正本はGraph／Stateです。

## Safety

- localhost Binding
- Read-only API
- Repository mutationなし
- Approval／Phase完了操作なし
- Secret／環境変数を読み取らない
- CDN／npm／外部Plugin依存なし
