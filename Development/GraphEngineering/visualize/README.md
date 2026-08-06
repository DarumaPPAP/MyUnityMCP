# Visualize Graph Dashboard

直前にVisualizeで作成した`MyUnityMCP Graph Dashboard`を、
Masterの正式なStatic UIとして採用しています。

## 開くファイル

```text
MyUnityMCP_GraphDashboard.html
```

ブラウザで直接開けます。Server、Plugin、npm、CDNは不要です。

## Live Viewer

```bash
python scripts/roadmap_harness.py viewer
```

Live版は同じVisualize UIを使用し、
`state/roadmap-state.json`を3秒ごとにRead-only更新します。

Static SnapshotをCompletion EvidenceやRoadmap Stateの正本として使用しないでください。
