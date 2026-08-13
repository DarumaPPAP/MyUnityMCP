# Troubleshooting

## ToolがClientに表示されない

仕様です。全Toolは既定非公開です。Bridge／ClientのAllowlistへ必要なTool名を追加してください。v1.1.0のMyUnityMCP Surfaceは**77 Tool**です。`agent.inspect_capabilities`とBridge Discoveryで利用中Sourceと一致しているか確認します。

## Addressables Toolが`UNSUPPORTED`

`com.unity.addressables`が導入されていない環境では正式な境界です。MyUnityMCPはPackage、AddressableAssetSettings、Groupを自動生成しません。Addressables Entry操作が必要なProjectではPackageをProject側で明示導入してから再確認してください。

## `SESSION_EXPIRED`／`STALE_SNAPSHOT`

古いSnapshot、Plan、Tokenを再利用しています。対象DomainのInspectからやり直し、Prepare Toolが返した最新IDを使用します。

## Approval Token mismatch

Tokenを手入力・保存・再生成しないでください。Prepare Responseの値を同一Execution内でApplyへ渡します。

## CompileまたはDomain Reload後に失敗する

一時IDは無効です。Compile完了後、Execution Historyを確認してInspectから再開します。Automatic Resumeは禁止です。

## Agent Workflowが`PARTIAL`になる

先行Step成功後に後続Delegateが失敗または`UNSUPPORTED`になった場合の正常なResult Integrityです。失敗を`SUCCEEDED`へ変換しません。各Step Resultの`errorCode` / `status`を確認してください。

## Captureが`UNVERIFIED`

BatchMode NoGraphicsまたは利用可能なGraphics Deviceがありません。通常のEditor Graphics環境で再実行してください。

## APVがUnsupported

Built-in Pipelineでは非対応です。URP／HDRPでもBaking Set、Scenario、Scene集合、APV APIを解決できない場合は実行しません。

## Bake後にOutput差分がない

Output Root、Baking Set、Lighting Scenario、Backend Logを確認し、古いPlanを再利用せずPrepareから再実行します。

## 詳細調査

`graphics.get_execution_history`、`graphics.get_execution_status`、`graphics.get_error_catalog`、`agent.get_execution_history`、`agent.get_error_catalog`を使用し、必要に応じて`Library/MyUnityMCP/Execution`のJSONL Traceを確認します。
