# Integration Hardening Contract

## Goal

個別Toolが動作するだけでなく、長時間のAI制作でEditor Lifecycle、Scene構成変更、Backend失敗、MCP Client切断を跨いでも、SceneやAssetを破損せずに停止・診断・再実行できることを保証する。

## Canonical workflow

正常系の統合Workflowは次のCheckpoint順を正本とする。

1. Inspect Project
2. Inspect Scene and create Snapshot
3. Compile Direction
4. Prepare executable Plan and exact Diff
5. Human or orchestrator Approval
6. Apply as one Undo Transaction
7. Prepare explicit Save Plan
8. Save only the approved Scene
9. Prepare explicit Bake Plan
10. Start and monitor Bake Job
11. Capture Color, Linear Depth, Object ID and Manifest
12. Evaluate against Acceptance Profile and Performance Budget
13. Refine from failed or incomplete Evaluation

各Checkpointは、返却された`revision`、Plan ID、Approval Token、Job ID、Capture ID、Evidence Digestを次のCheckpointへ明示的に渡す。古いIDを暗黙に再利用しない。

## Safety invariants

- Read-only ToolはScene Dirty、Persistent Asset Dirty、Undo Groupを変更しない。
- Mutationは一つのUnity Undo Transactionとして適用し、例外時はそのGroupをRollbackする。
- SaveはPrepare済みの一つのLoaded Sceneだけを保存する。
- BakeとCaptureは一時出力を使用し、Manifest成立前の不完全出力を正式Artifactにしない。
- Failure、Timeout、Cancellation、Lifecycle interruptionで自動保存しない。
- Scene Set、Camera、Pipeline、Revision、Output AssetのBaselineが変化した場合は処理を続行しない。
- Domain ReloadまたはUnity再起動を跨いだTransient IDとApproval Tokenは再利用しない。

## Execution Runtime

全MCP Bridge呼び出しはExecution Runtimeを通る。

返却されるExecution Metadata:

- `executionId`
- `traceId`
- `state`
- `startedUtc` / `completedUtc`
- `durationMs`
- `managedMemoryDeltaBytes`
- `progress`
- `timeoutSeconds`
- `cancellationRequested`
- `historyPath` / `tracePath`
- `artifactRetentionDays`

Execution State:

- `RUNNING`
- `SUCCEEDED`
- `PARTIAL`
- `FAILED`
- `CANCELLED`
- `TIMED_OUT`
- `INTERRUPTED`

## Timeout

- 既定60秒。
- 指定可能範囲は1～3600秒。
- TimeoutはEditor Updateで監視する。
- Timeout時は`EXECUTION_TIMED_OUT`として履歴化する。
- Unity APIを任意Threadから強制停止しない。長時間Backendは安全なCheckpointで停止する協調方式とする。
- Timeout延長前にTool Call Traceの停止Stageを確認する。

## Cancellation

`graphics.cancel_execution`はCancellation Requestを記録する。対象処理は次の安全なCancellation Pointで停止する。

- Mutation中はUndo Rollback完了前に強制終了しない。
- Native Cancellationを持つBake Backendは既存のCancel APIを使用する。
- Native CancellationがないBackendは`CANCEL_REQUESTED`として監視し、完了またはTimeoutまで状態を保持する。

## Progress

Progressは0～100の単調増加とする。

取得方法:

- `graphics.get_execution_status`によるPolling
- `Library/MyUnityMCP/Execution/tool-call-trace.jsonl`

代表Stage:

- `STARTED`
- `INSPECT`
- `SNAPSHOT`
- `PREPARED`
- `APPROVED`
- `APPLIED`
- `SAVED`
- `BAKING`
- `CAPTURED`
- `EVALUATED`
- `REFINED`
- `COMPLETED`

## Persistence and recovery

保存先:

- `Library/MyUnityMCP/Execution/active-executions.json`
- `Library/MyUnityMCP/Execution/execution-history.jsonl`
- `Library/MyUnityMCP/Execution/tool-call-trace.jsonl`
- `Library/MyUnityMCP/Execution/structured-log.jsonl`

Domain Reload、Compile、Play Mode遷移、Scene Close、Multi Scene構成変更、Editor終了ではActive Executionを`INTERRUPTED`として確定する。

Unity起動時に`active-executions.json`が残っている場合、前回Processで完了しなかった処理を`UNITY_RESTARTED`として復旧履歴へ移す。処理自体を自動再開しない。

## Fault injection matrix

| Fault | Expected result | Scene safety | Retry start |
|---|---|---|---|
| Plan後のScene変更 | `MCP_STALE_SNAPSHOT` | Applyしない | Inspect |
| Scene Close | `SCENE_CLOSED` | 自動保存しない | Scene Setを再LoadしてInspect |
| Domain Reload | `DOMAIN_RELOAD` | Transient ID失効 | Reload完了後Inspect |
| Compile開始 | `COMPILE_STARTED` | Transient ID失効 | Compile完了後Inspect |
| Play Mode遷移 | `PLAY_MODE_TRANSITION` | Editor Mutation停止 | Edit ModeでInspect |
| Approval Token不一致 | `APPROVAL_TOKEN_MISMATCH`または`MCP_INVALID_REQUEST` | Mutationしない | Prepare Plan |
| Plan期限切れ | `PLAN_EXPIRED`または`MCP_SESSION_EXPIRED` | Mutationしない | Prepare Plan |
| Bake開始失敗 | Backend固有Code | Save済みSceneを変更しない | Prepare Bake |
| Bake出力不足 | `APV_BAKE_NO_OUTPUT_DIFF` / `OUTPUT_ASSET_MISSING` | 不完全Artifactを採用しない | Output確認後Prepare Bake |
| Camera削除 | `CAMERA_NOT_FOUND` | Captureしない | Inspect Scene |
| Unsupported Pipeline | `UNSUPPORTED_PIPELINE` / `MCP_UNSUPPORTED` | Backendを呼ばない | Support Matrix確認 |
| Multi Scene構成変更 | `MULTI_SCENE_CONFIGURATION_CHANGED` | 古いBaselineを使用しない | Inspect Scene Set |
| MCP Client切断 | `MCP_CLIENT_DISCONNECTED` | Active処理を履歴化 | 再接続後History確認 |
| Unity再起動 | `UNITY_RESTARTED` | Active処理を自動再開しない | History確認後Inspect |

## Structured failure

失敗結果は次を持つ。

- `error.code`
- `error.category`
- `error.message`
- `error.retryable`
- `error.retryAction`
- `error.remediation`
- `error.details`

`retryAction`は単なる「再試行」ではなく、Inspect、Prepare Plan、Prepare Bake等の再開Checkpointを明記する。

## Retention

- Execution History: 30日、最大1000件。
- Runtime所有Artifact: 14日。
- GitHub Actions Evidence: 90日。
- Scene、Assets、外部Capture Bundle等、Runtimeが所有しない成果物は自動削除しない。

## Performance verification

- 各Tool CallでDurationとManaged Memory Deltaを記録する。
- History APIはP50、P95、Maximum Durationを返す。
- Editor Contract Testでは400 Lightを含むSceneをInspectionし、Scene Dirtyを変化させず20秒以内に完了することを検証する。
- この閾値はCIの安全上限であり、製品のFrame Budgetを示さない。

## Completion criteria

- InspectからRefineまでの正常系統合Testが成功する。
- 主要Fault Injectionで追加のScene Mutationや自動保存が発生しない。
- 全失敗がError Code、Retryability、Retry Checkpointを構造化して返す。
- Domain ReloadとUnity再起動後に未完了Executionを特定できる。
- Support MatrixがVersion管理された固定Contractとして取得できる。
