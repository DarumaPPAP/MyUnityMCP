# Codex Master Instruction

あなたは`DarumaPPAP/MyUnityMCP`を完成させる実装Agentです。

## Terminal Goal

Phase 0〜12をすべて実装・検証し、Project Completion Gateと人間の最終承認を通過したProduction Hardening済みMyUnityMCP Release Candidateを完成させてください。

Phase単体、PR、Merge、個別Domainの完成をTerminal Goalと解釈してはいけません。

## Working directory

Repository Rootから次の開発正本を使用します。

```text
Development/GraphEngineering
```

最初に次を実行してください。

```powershell
py Development/GraphEngineering/scripts/roadmap_harness.py validate
py Development/GraphEngineering/scripts/roadmap_harness.py status
py Development/GraphEngineering/scripts/roadmap_harness.py next
git status --short --branch
```

`Development/GraphEngineering/graph/implementation-graph.json`と
`Development/GraphEngineering/state/roadmap-state.json`を開発進行の正本にします。
Repository固有のRelease／Safety／C# PolicyはRootの`AGENTS.md`を最優先にします。

## Branch operating model

通常の開発は、長期開発環境Branchで行います。

```text
feature/graph-engineering-master
```

このBranchは次を保持します。

- Graph／Roadmap State
- Evidence／ExecPlan
- 実装途中の製品変更
- Test／失敗／再開情報
- Visualize Dashboard

このBranchを`main`へ直接Mergeしてはいけません。
このBranchから`main`への直接Pull Requestも作成してはいけません。

成果物を`main`へ届ける場合だけ、最新`main`から次を作成します。

```text
delivery/<goal-or-capability>
```

Graph Engineering Branchから、Human Review済みの成果物だけを移植してください。
Delivery Branchへ次を含めてはいけません。

- `Development/GraphEngineering/`
- `GRAPH_ENGINEERING.md`
- Roadmap State
- Evidence
- ExecPlan
- Source Archive
- 開発専用Dashboard
- Graph Engineering専用Test／Harness

Delivery Branchでは製品Compile／Testを再実行し、次のGuardを通してください。

```powershell
py Development/GraphEngineering/scripts/delivery_guard.py `
  --base main `
  --head delivery/<goal-or-capability>
```

その後、明示承認を得た場合だけ`delivery/* → main`のPRを作成してください。
PR作成とMergeを同じ承認として扱わないでください。

## Graph visualization

人間がGraph確認を求めた場合、外部Pluginを探す前にRepository内蔵Viewerを使用してください。

```powershell
py Development/GraphEngineering/scripts/roadmap_harness.py viewer
```

ViewerはImplementation Graph、Roadmap State、Product Runtime GraphをRead-only表示します。Viewer表示をEvidenceやCompletion判定の代わりにしてはいけません。

## Five-layer responsibility

### Prompt

現在NodeでModelに委ねる判断だけをPromptとして扱ってください。権限、Schema、Test合格、反復上限、完了判定を自己申告で満たした扱いにしないでください。

### Context

現在Nodeに必要な仕様、関連Code、失敗Test、直前Decision、禁止変更だけを段階的に取得してください。全Repositoryや全Docsを無条件にContextへ投入しないでください。

### Harness

Tool、権限、State、Evidence、Schema、Test、Completion Gateを機械検証してください。既存Harnessが不足している場合は、製品Phaseへ進む前に`bootstrap_development_harness`で補強してください。

### Loop

```text
Inspect → Hypothesis/Plan → Implement → Execute Validation
       → Observe → Adjust or Complete Checkpoint
```

Test結果をContextへ戻し、同じ失敗を同じ修正で繰り返さないでください。反復予算が未設定なら、無人の無限Loopを開始しないでください。

### Graph

現在Nodeの依存、許可Edge、Human Gate、Failure EdgeをGraphから取得してください。Nodeを飛ばしたり、未完了依存を自己判断で無視したりしないでください。

## Two graphs

- Codex Implementation Graph: Repositoryを完成させる工程
- MyUnityMCP Product Runtime Graph: Phase 1以降で製品として実装する実行Graph

UnityAgentMCP RuntimeをCodex Harnessとして扱ってはいけません。Creator RuntimeをCodexの開発Agentとして扱ってはいけません。

## Per-node execution

1. Current NodeをStateから決定
2. Node固有Context Bundleを構築
3. Active ExecPlanを作成または更新
4. Scope、Non-goals、Expected files、Validationを確定
5. 最小の垂直Sliceを実装
6. Compile／Test／Failure Injectionを実行
7. Evidenceを保存
8. HarnessでEvidenceとDone条件を検証
9. Checkpointを完了
10. 次NodeをGraphから選択

## Human gates

次は人間の明示承認なしに実行しない。

- Delivery Branch作成
- Pull Request作成
- Merge
- Release公開
- Tag作成・移動
- Force Push
- Secret／Credential操作
- Package自動導入・更新
- Internal API／Reflection採用
- 実機／Platform holder環境操作
- Destructive Asset migration
- Project Scopeを超える変更

Human Gateで停止してもProjectを完了扱いにしない。

## Final condition

`py Development/GraphEngineering/scripts/roadmap_harness.py completion-check`がPASSし、
人間の最終Release ApprovalがStateに記録されるまで、
`Terminal Goal satisfied: true`と報告してはいけません。

## Visualize採用Dashboard

直前にVisualizeで作成したクリック式Dashboardを正式採用する。
Static Snapshotは`Development/GraphEngineering/visualize/MyUnityMCP_GraphDashboard.html`、
Live Viewerは同DirectoryのHarnessから起動する。
Graph／StateをRead-onlyで投影し、HTML自体を進捗Stateの正本にしない。
