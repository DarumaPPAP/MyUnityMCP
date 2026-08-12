# Troubleshooting

## ToolがClientに表示されない

仕様です。全Toolは既定非公開です。Bridge／ClientのAllowlistへ必要なTool名を追加してください。Bridge Registryの期待値はProduction mainで45 Tool、Stage 2〜8 Integration Candidateで77 Toolです。利用中のSource Surfaceと一致しているか確認します。

## `SESSION_EXPIRED`／`STALE_SNAPSHOT`

古いSnapshot、Plan、Tokenを再利用しています。`inspect_project`と`inspect_scene`からやり直し、Prepare Toolが返した最新IDを使用します。

## Approval Token mismatch

Tokenを手入力・保存・再生成しないでください。Prepare Responseの値を同一Execution内でApplyへ渡します。

## CompileまたはDomain Reload後に失敗する

一時IDは無効です。Compile完了後、Execution Historyを確認してInspectから再開します。

## Captureが`UNVERIFIED`

BatchMode NoGraphicsまたは利用可能なGraphics Deviceがありません。通常のEditor Graphics環境で再実行してください。

## APVがUnsupported

Built-in Pipelineでは非対応です。URP／HDRPでもBaking Set、Scenario、Scene集合、APV APIを解決できない場合は実行しません。

## Bake後にOutput差分がない

Output Root、Baking Set、Lighting Scenario、Backend Logを確認し、古いPlanを再利用せずPrepareから再実行します。

## 詳細調査

`graphics.get_execution_history`、`graphics.get_execution_status`、`graphics.get_error_catalog`を使用し、`Library/MyUnityMCP/Execution`のJSONL Traceを確認します。
