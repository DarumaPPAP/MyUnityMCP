# Support Matrix

Contract Version: `1.0`  
Package Version: `0.8.0`

## Environment

| Item | Contract |
|---|---|
| Execution surface | Unity Editor only |
| Minimum Unity | `6000.0` |
| CI verified Unity | `6000.0.75f1` |
| CI host | GitHub Actions Ubuntu, BatchMode, NoGraphics |
| MCP bridge package | `com.coplaydev.unity-mcp 10.1.2` |
| Player runtime | Not supported |
| Target device execution | Not verified |

Minimum VersionはAPI Contractの下限であり、すべてのUnity 6000.x Patchを実機検証済みという意味ではない。

## Capability by Render Pipeline

| Capability | Built-in | URP | HDRP | Conditions |
|---|---:|---:|---:|---|
| Project／Scene Inspection | Supported | Supported | Supported | Editor APIで取得可能な範囲 |
| Direction Planning | Supported | Supported | Supported | Pipeline非依存 |
| Light Mutation | Supported | Supported | Supported | 明示差分、Approval、Undo必須 |
| Camera Mutation | Supported | Supported | Supported | 明示差分、Approval、Undo必須 |
| Reflection Probe Mutation | Supported | Supported | Supported | 明示差分、Approval、Undo必須 |
| Volume Mutation | Unsupported without Volume API | Conditional | Conditional | Volume APIとProfile Typeが解決可能 |
| Save | Supported | Supported | Supported | 一つのPrepared Loaded Sceneのみ |
| Dependency Bake | Conditional | Conditional | Conditional | 明示BackendがCapabilityを提供 |
| Capture Evidence | Conditional | Conditional | Conditional | Null Graphics Deviceでは`UNVERIFIED` |
| APV Bake | Unsupported | Conditional | Conditional | Baking Set、Scenario、APV Backendが解決可能 |
| Visual Evaluation | Supported | Supported | Supported | 外部Measurement／Human Reviewが入力 |
| Structured Refine | Supported | Supported | Supported | Failed／Incomplete Evaluationのみ |

`Conditional`はSilent Fallbackを許可しない。前提が解決できない場合は`UNSUPPORTED`、`UNVERIFIED`、`BACKEND_NOT_IMPLEMENTED`を返す。

## Lifecycle and recovery

| Condition | Contract |
|---|---|
| Domain Reload | Active Executionを`INTERRUPTED`として履歴化 |
| Compile開始 | Active Executionを`INTERRUPTED`として履歴化 |
| Play Mode遷移 | Editor Mutationを継続しない |
| Scene Close | Scene Baselineを失効 |
| Active／Loaded Scene変更 | Multi Scene Baselineを失効 |
| MCP Client切断 | Adapter通知時にExecutionを中断・履歴化 |
| Unity終了 | Active Executionを永続化して停止 |
| Unity再起動 | Active Fileを`UNITY_RESTARTED`として復旧履歴へ移動 |
| Automatic resume | Prohibited |

## Execution services

| Service | Support |
|---|---|
| Timeout | Cooperative, 1～3600秒、既定60秒 |
| Cancellation | Cooperative、Backend Native Cancelがあれば併用 |
| Progress | Status PollingとJSONL Trace |
| Structured Log | JSONL |
| Execution History | JSONL、30日、最大1000件 |
| Tool Call Trace | JSONL |
| Runtime-owned Artifact retention | 14日 |
| CI Evidence retention | 90日 |
| Duration measurement | Supported |
| Managed memory delta | Supported |
| P50／P95／Maximum summary | Supported |

## Explicitly not verified

- Player Build内でのTool実行
- PC、PlayStation、Nintendo Switch等のTarget Device上でのTool実行
- すべてのUnity 6000.x Patch
- すべてのURP／HDRP Package Version
- すべての外部MCP Clientが切断Callbackを実装していること
- BatchMode NoGraphicsでの画像Artifact生成

未検証は非対応と同義ではないが、対応済みとして宣言しない。
