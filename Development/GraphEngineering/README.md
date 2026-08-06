# MyUnityMCP Graph Engineering Master

このDirectoryは、MyUnityMCP Phase 0〜12を実装・検証するための**長期開発環境**です。
既存のRelease Package、Root `AGENTS.md`、Version、Tag、Release Workflowを置き換えません。

## Branch role

```text
feature/graph-engineering-master
```

このBranchには、Graph Engineering環境と実装途中の製品変更を保持します。
このBranch自体を`main`へ直接Mergeしません。

成果物を`main`へ届ける場合は、最新`main`から`delivery/*`を作り、
承認済みの成果物だけを移植してPRを作成します。

```text
feature/graph-engineering-master
    └─ 開発、Graph、State、Evidence、検証

main
    └─ delivery/<goal>を分岐
          └─ 成果物だけ移植
                └─ Pull Request → main
```

詳細は`docs/delivery/artifact-only-pr-policy.md`を参照してください。

## Start here

1. `MASTER_GOAL.md`
2. `CODEX_MASTER_PROMPT.md`
3. `WORKFLOW.md`
4. `docs/delivery/artifact-only-pr-policy.md`
5. `graph/implementation-graph.json`
6. `state/roadmap-state.json`
7. `scripts/roadmap_harness.py`
8. `visualize/MyUnityMCP_GraphDashboard.html`

## Commands

Repository Rootから実行します。

```powershell
py Development/GraphEngineering/scripts/roadmap_harness.py validate
py Development/GraphEngineering/scripts/roadmap_harness.py status
py Development/GraphEngineering/scripts/roadmap_harness.py next
py Development/GraphEngineering/scripts/roadmap_harness.py viewer
```

成果物Branchの混入検査:

```powershell
py Development/GraphEngineering/scripts/delivery_guard.py `
  --base main `
  --head delivery/<goal-or-capability>
```

## Safety

- Graph Engineering Branchから`main`へ直接PRしない。
- Delivery Branchは必ず最新`main`から作る。
- `Development/GraphEngineering/`を成果物PRへ含めない。
- Merge、Tag、Releaseは明示承認なしに実行しない。
- Phase完了とProject完了を混同しない。
- Test未実行・Evidence不足を完了扱いしない。
- UnityAgentMCPと未実装Domainは、実装・E2E検証前に実行可能と表現しない。
- Repository固有PolicyはRootの`AGENTS.md`が最優先。

## Terminal goal

Phase 0〜12を実装・検証し、Project Completion Gateを通過し、
Human Final Release Approvalを得ること。
