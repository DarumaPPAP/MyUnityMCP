# Status and Error Codes

## Tool status

`SUCCESS`、`PARTIAL`、`INVALID_REQUEST`、`UNSUPPORTED`、`UNVERIFIED`、`BACKEND_NOT_IMPLEMENTED`、`READ_ONLY_CONTRACT_VIOLATION`、`SESSION_EXPIRED`、`STALE_SNAPSHOT`、`STALE_DURING_SCAN`、`EDITOR_RELOADING`、`FAILED`を返します。

`PARTIAL`は失敗の隠蔽ではありません。`issues`と不足Evidenceを確認してください。

## Structured error contract

失敗Resultは`status`だけでなく、`error`へCode、Category、Retryability、Retry Action、Remediationを返す。

同一Codeは意味を変更しない。意味変更が必要な場合は新しいCodeを追加する。

## Core status mapping

| Code | Category | Retryable | Retry action |
|---|---|---:|---|
| `MCP_INVALID_REQUEST` | REQUEST | Yes | Parameterを修正して同じToolを再実行 |
| `MCP_UNSUPPORTED` | COMPATIBILITY | No | Support Matrixから対応Capabilityを選択 |
| `MCP_UNVERIFIED` | COMPATIBILITY | Yes | 検証済みEditor／Graphics Device環境で再実行 |
| `MCP_BACKEND_NOT_IMPLEMENTED` | BACKEND | No | 明示Backendを導入または実装 |
| `MCP_READ_ONLY_CONTRACT_VIOLATION` | SAFETY | Yes | Sceneを復元してInspectから再開 |
| `MCP_SESSION_EXPIRED` | CONCURRENCY | Yes | Inspectから新しいID群を作成 |
| `MCP_STALE_SNAPSHOT` | CONCURRENCY | Yes | Inspect、Snapshot、Prepare Planを再実行 |
| `MCP_STALE_DURING_SCAN` | CONCURRENCY | Yes | Editor変更が止まってからInspectを再実行 |
| `MCP_EDITOR_RELOADING` | EDITOR_LIFECYCLE | Yes | Reload完了後にInspectから再開 |
| `MCP_FAILED` | INTERNAL | Yes | Trace確認後、最後の成功Checkpointから再開 |

## Authorization and lifecycle

| Code | Meaning | Retry action |
|---|---|---|
| `APPROVAL_TOKEN_MISMATCH` | PlanとApproval Tokenが一致しない | Prepare Plan |
| `PLAN_EXPIRED` | Plan TTL超過 | Snapshot取得後Prepare Plan |
| `EXECUTION_CANCEL_REQUESTED` | 協調Cancellationが要求された | Cancellation完了確認後、最後の成功Checkpoint |
| `EXECUTION_TIMED_OUT` | Timeout超過 | Traceで停止Stage確認後、適切なTimeoutで再実行 |
| `DOMAIN_RELOAD` | Domain Reloadにより中断 | Reload完了後Inspect |
| `COMPILE_STARTED` | Script Compileにより中断 | Compile完了後Inspect |
| `PLAY_MODE_TRANSITION` | Play Mode遷移により中断 | 安定したEdit ModeでInspect |
| `SCENE_CLOSED` | 実行対象SceneがCloseされた | SceneをLoadしてInspect |
| `MULTI_SCENE_CONFIGURATION_CHANGED` | Loaded／Active Scene Setが変化 | Scene Set全体をInspect |
| `MCP_CLIENT_DISCONNECTED` | MCP Client接続が切断された | 再接続後Execution History確認 |
| `UNITY_SHUTDOWN` | Unity終了により中断 | Unity再起動後Execution History確認 |
| `UNITY_RESTARTED` | 前回Processの未完了Executionを検出 | History確認後Inspect |

## Output and compatibility

| Code | Meaning | Retry action |
|---|---|---|
| `APV_BAKE_NO_OUTPUT_DIFF` | Bake完了扱いだがOutput差分がない | Baking Set／Scenario／Output Root確認後Prepare Bake |
| `OUTPUT_ASSET_MISSING` | 必須Artifactが不足 | 不完全一時出力を削除してPrepare BakeまたはCapture |
| `CAMERA_NOT_FOUND` | Camera GlobalObjectIdを解決できない | Inspect SceneでCameraを再選択 |
| `UNSUPPORTED_PIPELINE` | CapabilityがActive Pipelineに非対応 | Support Matrix確認 |

## Domain-specific Codes

既存Toolが`data.failureCode`または`issues[].code`を返す場合、Execution RuntimeはそのCodeを最優先する。Catalogに未登録のDomain Codeでも失われず、Fallbackの再実行契約を付与する。

## Retry rules

- Retry前に`graphics.get_execution_history`で前回Callが実際に失敗したか確認する。
- `SESSION_EXPIRED`、`STALE`、Reload、Restart後は古いPlan、Token、Job、Capture IDを再利用しない。
- Mutation失敗後はScene Dirty、Undo Stack、対象ObjectをInspectしてから再実行する。
- Bake／Capture失敗後はManifest成立前の一時出力を正式成果物として扱わない。
