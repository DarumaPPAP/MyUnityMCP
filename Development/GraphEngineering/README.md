# MyUnityMCP Graph Engineering Master

このDirectoryは、MyUnityMCP Phase 0〜12を実装・検証するための開発正本です。
既存のRelease Package、`AGENTS.md`、Version、Tag、Release Workflowを置き換えません。

## Start here

1. `MASTER_GOAL.md`
2. `CODEX_MASTER_PROMPT.md`
3. `graph/implementation-graph.json`
4. `state/roadmap-state.json`
5. `scripts/roadmap_harness.py`
6. `visualize/MyUnityMCP_GraphDashboard.html`

## Commands

Repository Rootから実行します。

```powershell
py Development/GraphEngineering/scripts/roadmap_harness.py validate
py Development/GraphEngineering/scripts/roadmap_harness.py status
py Development/GraphEngineering/scripts/roadmap_harness.py next
py Development/GraphEngineering/scripts/roadmap_harness.py viewer
```

## Safety

- Merge、Tag、Releaseは明示承認なしに実行しない。
- Phase完了とProject完了を混同しない。
- Test未実行・Evidence不足を完了扱いしない。
- UnityAgentMCPと未実装Domainは、実装・E2E検証前に実行可能と表現しない。
- Repository固有PolicyはRootの`AGENTS.md`が最優先。

## Terminal goal

Phase 0〜12を実装・検証し、Project Completion Gateを通過し、Human Final Release Approvalを得ること。
