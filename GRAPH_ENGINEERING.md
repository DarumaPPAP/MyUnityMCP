# Graph Engineering Development Environment

MyUnityMCPの長期実装Graph、Roadmap State、Evidence Harness、Visual Dashboardは次を正本とします。

- [Graph Engineering Master](Development/GraphEngineering/README.md)
- [Master Goal](Development/GraphEngineering/MASTER_GOAL.md)
- [Codex Master Prompt](Development/GraphEngineering/CODEX_MASTER_PROMPT.md)
- [Implementation Graph](Development/GraphEngineering/graph/implementation-graph.json)
- [Roadmap State](Development/GraphEngineering/state/roadmap-state.json)
- [Artifact-only PR Policy](Development/GraphEngineering/docs/delivery/artifact-only-pr-policy.md)
- [Graph Dashboard](Development/GraphEngineering/visualize/MyUnityMCP_GraphDashboard.html)

## Branch policy

`feature/graph-engineering-master`は長期開発環境です。
Graph、State、Evidence、計画、失敗履歴、実装途中の製品変更を保持します。

このBranchを`main`へ直接Mergeせず、このBranchから`main`への直接PRも作りません。

成果物を公開するときは、最新`main`から`delivery/*`を作り、
Graph Engineering Branchから承認済みの成果物だけを移植します。

```text
delivery/* → main
```

成果物PRには`Development/GraphEngineering/`、Roadmap State、Evidence、
Source Archive、開発専用Dashboardを含めません。

この開発正本は既存のPackage Release正本を置き換えません。
Release／Safety／C#規約はRootの`AGENTS.md`を優先します。
