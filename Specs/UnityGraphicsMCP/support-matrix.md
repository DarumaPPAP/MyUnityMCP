# Support Matrix

Contract Version: `1.0`  
Package Version: `1.0.1`

## Environment

| Item | Contract |
|---|---|
| Execution surface | Unity Editor only |
| Minimum Unity | `6000.0` |
| CI verified Unity | `6000.0.75f1` |
| MCP bridge API | `com.coplaydev.unity-mcp 10.1.2` |
| Player runtime | Unsupported |
| Target device execution | Not verified |

Minimum VersionはAPI Contractの下限であり、全Unity 6000.x Patchの検証完了を意味しません。

## Pipeline capability

| Capability | Built-in | URP | HDRP | Conditions |
|---|---:|---:|---:|---|
| Project／Scene Inspection | Supported | Supported | Supported | Editor APIで取得可能な範囲 |
| Direction Planning | Supported | Supported | Supported | Pipeline非依存 |
| Light／Camera／Reflection Probe Mutation | Supported | Supported | Supported | Approval／Revision／Undo必須 |
| Volume Mutation | Conditional | Conditional | Conditional | Volume APIとProfile Typeが解決可能 |
| Explicit Scene Save | Supported | Supported | Supported | Prepared Loaded Scene一つだけ |
| Dependency Bake | Conditional | Conditional | Conditional | 明示BackendとDependency Baselineが必要 |
| Capture Evidence | Conditional | Conditional | Conditional | Graphics Deviceが必要 |
| APV Plan／Bake | Unsupported | Conditional | Conditional | Baking Set、Scenario、APV Backendが必要 |
| Visual Evaluation／Refine | Supported | Supported | Supported | 外部Measurement／Human Reviewを入力 |

`Conditional`では前提を検査し、解決不能時に`UNSUPPORTED`、`UNVERIFIED`、`BACKEND_NOT_IMPLEMENTED`を返します。Silent Fallbackは行いません。

## Execution services

| Service | Contract |
|---|---|
| Timeout | Cooperative、1～3600秒、既定60秒 |
| Cancellation | Cooperative、Native Cancelがあれば併用 |
| Progress | Polling + JSONL Trace |
| Execution History | 30日、最大1000件 |
| Runtime Artifact | 14日 |
| CI Evidence | 90日 |
| Unity restart | 未完了Executionを`UNITY_RESTARTED`として復旧し、自動再開しない |

## Not verified

- Player／Target Device上のTool実行
- 全Unity 6000.x Patch
- 全URP／HDRP Package Version
- 全MCP Client Adapterの切断Callback
- BatchMode NoGraphicsでの実画像Artifact生成
- 実Projectの全Baking Set／Lighting Scenario構成
