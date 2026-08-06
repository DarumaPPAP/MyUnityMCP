# MyUnityMCP Agent Map

## Mission

Phase 0〜12をすべて実装・検証し、Production Hardening済みのMyUnityMCP Release Candidateを完成させる。
個別Phaseの完了をProject完了として扱わない。

## Source of truth

詳細規約をこのファイルへ詰め込まない。次を順に参照する。

1. `MASTER_GOAL.md`
2. `WORKFLOW.md`
3. `docs/index.md`
4. `docs/goals/acceptance-model.md`
5. `docs/architecture/five-layer-model.md`
6. `docs/architecture/two-graphs.md`
7. `graph/implementation-graph.json`
8. `state/roadmap-state.json`
9. 現在Nodeの`docs/phases/*.md`
10. 現在の`docs/plans/active/*.md`

## Required startup

作業開始時に必ず実行する。

```bash
python scripts/roadmap_harness.py validate
python scripts/roadmap_harness.py status
python scripts/roadmap_harness.py next
git status --short --branch
```

次NodeをPromptや記憶から推測せず、GraphとStateから決定する。

## Optional visual inspection

Graphを人間と共有して確認する場合:

```bash
python scripts/roadmap_harness.py viewer
```

ViewerはRead-only projectionであり、次Nodeと完了状態の正本はHarness CLIとStateである。

## Repository rules

- ユーザー固有のUnity開発ポリシーを一般論より優先
- Namespaceは単一階層
- Enumは`E_XXXX`
- Structは必要時のみ`S_XXXX`
- private fieldは`_camelCase`
- static、自動探索、巨大Controller、過剰なファイル分割を避ける
- 1 MonoBehaviour / 1 cs
- Editor UIは日本語・黒基調
- Asset参照はGUID解決を優先
- Public API優先
- Internal API、Reflection、Generic SerializedPropertyは明示承認なしに採用しない

## Safety

- Toolは既定非公開
- Mutation、Save、Bake、Build、Addressables Content Buildは別承認
- CreatorとUnityAgent Control PlaneはUnity APIを直接Mutationしない
- 自動Save、自動Full Bake、自動Visual Acceptanceは禁止
- Silent Fallbackは禁止
- Published Tag移動、Force Push、秘密情報出力は禁止
- Unsupportedを成功として返さない

## Context rule

- このAGENTS.mdは地図であり百科事典ではない
- 必要なPhase仕様、関連Code、失敗Test、直前Decisionだけを取得する
- 全Docs、全Log、全会話を一括でContextへ入れない
- Source、Observation、Expected、Actualを分離する
- 古い情報や重複情報はState/Evidenceへ圧縮する

## Harness rule

Promptに書いただけでは制約を満たした扱いにしない。
権限、Schema、Evidence、Test、完了条件はScript、CI、Branch protection、人間Gateで強制する。

## Completion report

毎回必ず次を出力する。

- Project Status
- Current Node
- Completed Checkpoints
- Remaining Checkpoints
- Evidence produced
- Tests executed
- Blocking conditions
- Next executable node
- Terminal Goal satisfied: true / false

`true`はProject Completion Gate通過後だけ。

## Visualization

人間がGraphの図示を求めた場合、用途に応じて次を使用する。

- 配布時点の確認: `visualize/MyUnityMCP_GraphDashboard.html`
- Current Stateの確認: `python scripts/roadmap_harness.py viewer`

ViewerはRead-only projectionであり、次Node判定、Evidence、CompletionはHarness CLIとGraph／Stateを正本とする。


## Visualize採用Dashboard

直前にVisualizeで作成したクリック式Dashboardを正式採用する。
Static Snapshotは`visualize/MyUnityMCP_GraphDashboard.html`、
Live Viewerは`tools/graph-viewer/`を使用する。
両者の基本UIを分岐させず、Graph／StateをRead-onlyで投影する。
