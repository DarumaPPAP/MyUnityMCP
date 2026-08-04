# UnityGraphicsMCP Unity Editor C# Tool実装設計

- DocumentVersion: `4.0.0`
- DesignStatus: `Implemented / Unity Editor CI Verified`
- ImplementationStatus: `Phase 3 Graphics Mutation Complete`
- VerificationStatus: `46 / 46 EditMode PASS`

## 1. 目的

対象Unity Projectの環境、Scene Graphics状態、Direction Plan、承認済みGraphics Mutationを機械可読Resultとして扱うEditor-only Toolを構築する。

特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph、Target PlatformをMyUnityMCP全体へ固定しない。

## 2. Implemented tools

### Inspection

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`

### Planning

- `graphics.compile_direction`
- `graphics.preview_plan`
- `graphics.prepare_light_plan`
- `graphics.prepare_environment_plan`

### Mutation

- `graphics.apply_plan`
- `graphics.undo_last_transaction`
- `graphics.apply_environment_plan`
- `graphics.undo_last_environment_transaction`

全Toolは`AutoRegister = false`とし、Activation PolicyまたはBridge設定から明示的に有効化する。

## 3. Bridge contract

- Package: `com.coplaydev.unity-mcp`
- Version: `10.1.2`
- Assembly: `MCPForUnity.Editor`
- Tool Attribute: `McpForUnityToolAttribute`
- Parameter Attribute: `ToolParameterAttribute`
- Entry Point: `public static object HandleCommand(JObject)`
- Response: `SuccessResponse` / `ErrorResponse`
- Command Dispatch: Unity Main Thread

MCP Bridge固有依存はTool Entryへ閉じ込め、Inspection、Planning、Mutation本体をBridge Response型へ依存させない。

## 4. Physical architecture

```text
MCP for Unity Bridge
        ↓
UnityGraphicsMcpTools.cs
        ↓
UnityGraphicsMcpSession.cs
        ├─ Session / Revision
        ├─ Snapshot / Direction Plan
        └─ Read-only Dirty Guard
        ↓
UnityGraphicsMcpInspection.cs
UnityGraphicsMcpPlanning.cs
UnityGraphicsMcpMutation.cs
UnityGraphicsMcpEnvironmentMutation.cs
        ↓
Unity Editor API
```

### `UnityGraphicsMcpTools.cs`

- Tool Attribute
- Parameter Schema
- JObject変換
- Success / Error変換
- Default Disable

### `UnityGraphicsMcpSession.cs`

- Session ID
- Revision
- Snapshot / Direction Plan Lifetime
- Compile / Reload / Play Mode失効
- Read-only Guard

### `UnityGraphicsMcpInspection.cs`

- Project Environment
- Scene Snapshot
- Graphics Validation
- Tool Result Contract

### `UnityGraphicsMcpPlanning.cs`

- Structured Visual Intent
- Direction Recommendation
- Plan ID / Expected Revision
- Read-only Preview

### `UnityGraphicsMcpMutation.cs`

- Light Operation Schema
- Exact Diff
- Approval Token
- Light Transaction / Undo

### `UnityGraphicsMcpEnvironmentMutation.cs`

- Camera / Reflection Probe / Volume Operation Schema
- Property / Field対応Volume Member Access
- Multi-component Transaction / Rollback
- Guarded Undo

## 5. Read-only contract

Inspection、Planning、Prepareの実行前後で次を比較する。

- Loaded Scene Dirty State
- Persistent Asset Dirty State
- Undo Group

Material確認に`renderer.material`を使用せず、`sharedMaterials`を使用する。Read-only ToolからAsset生成、Scene保存、Bakeを実行しない。

違反時は`READ_ONLY_CONTRACT_VIOLATION`を返す。

## 6. Session and revision

Snapshot、Direction Plan、Executable PlanはEditor Session内だけで有効とする。

失効条件:

- Domain Reload
- Compile開始
- Play Mode遷移
- Editor終了
- Revision変更
- TTL超過

大きなScene ResultはSnapshot IDとCursorで参照し、毎回全JSONを複製しない。

## 7. Planning contract

Unity C# Toolは自然言語や画像を独自解釈しない。UnityAgentまたはMCP Clientが構造化したVisual Intentを入力する。

Direction PlanはProject Inspectionの検出事実とRequested Targetを別フィールドで保持する。

PrepareはUnity状態を変更せず、次を返す。

- Exact Before / Requested After
- Diff Digest
- Approval Token
- Expected Revision
- Mutation / Save / Bake未実行の明示

## 8. Mutation contract

Apply必須条件:

- Direction Planが現在Sessionに存在する
- Executable Planが未使用
- Expected Revision一致
- Approval Token一致
- Preview Baseline一致
- `saveMode = NONE`

Environment Planでは追加で次を要求する。

- Operation ID一意
- 同一既存ComponentへのUpdateは一回
- 指定Volume MemberをPrepare時に読み書き可能と確認

Applyは一つのUnity Undo Groupへ集約する。途中例外時は`Undo.RevertAllDownToGroup`で全体Rollbackする。

## 9. Undo contract

Undo前に次を確認する。

- Transaction ID
- Expected Revision
- Transaction適用後State Digest
- TransactionがUndo Stackの最新Groupであること

外部変更、新しいUndo Group、Session失効がある場合は拒否する。

## 10. Supported Phase 3 operations

### Light

- `LIGHT_CREATE`
- `LIGHT_UPDATE`

### Camera

- `CAMERA_CREATE`
- `CAMERA_UPDATE`

### Reflection Probe

- `REFLECTION_PROBE_CREATE`
- `REFLECTION_PROBE_UPDATE`

### Volume

- `VOLUME_CREATE`
- `VOLUME_UPDATE`
- 既存`sharedProfile`参照割当

Volume Profile内部Override、Save、Bake、Captureは実装しない。

## 11. Test architecture

Editor Test Assemblyから次を検証する。

- 11 Tool Discovery / Default Disable
- Read-only Guard
- Session / Revision / Cursor
- Direction Compile / Preview
- Approval / Baseline / Save Mode拒否
- Light Create / Update / Undo
- Camera Create / Update / Undo
- Reflection Probe Create / Update / Undo
- Volume Create / Update / sharedProfile / Undo
- Property / Field API形状差
- Duplicate Operation / Update Target拒否
- Atomic Transaction / Rollback
- External Change / Newer Undo Group拒否
- Phase 1～2 Regression

Test用に公開範囲を広げず、`InternalsVisibleTo("MyUnityMcp.Editor.Tests")`だけを使用する。

## 12. Phase 4 extension boundary

Save、Bake、Captureは既存Applyへ追加せず、独立Toolと別Approval Tokenで実装する。

- Save Plan
- Dirty Dependency Set
- Dependency限定Bake
- Capture State Restore
- Visual Evaluation / Refine

任意`SerializedProperty` Toolや空Backendを追加しない。
