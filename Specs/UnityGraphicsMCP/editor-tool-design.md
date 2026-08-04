# UnityGraphicsMCP Unity Editor C# Tool実装設計

- DocumentVersion: `3.0.0`
- DesignStatus: `Implemented / Unity Editor CI Verified`
- ImplementationStatus: `Phase 3A Light Mutation Complete`
- VerificationStatus: `30 / 30 EditMode PASS`

## 1. 目的

対象Unity Projectを解析し、Project環境、Scene Graphics状態、Direction Plan、承認済みLight Mutationを機械可読Resultとして扱うEditor-only Toolを構築する。

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

### Mutation

- `graphics.apply_plan`
- `graphics.undo_last_transaction`

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

MCP Bridge Packageへの直接依存は`UnityGraphicsMcpTools.cs`へ限定する。

## 4. Physical architecture

```text
MCP for Unity Bridge
        ↓
UnityGraphicsMcpTools.cs
        ↓
UnityGraphicsMcpSession.cs
        ├─ Snapshot / Direction Plan / Revision
        └─ Read-only Dirty Guard
        ↓
UnityGraphicsMcpInspection.cs
        ├─ UnityGraphicsMcpProjectInspection.cs
        ├─ UnityGraphicsMcpSceneInspection.cs
        ├─ UnityGraphicsMcpValidation.cs
        ├─ UnityGraphicsMcpPlanning.cs
        └─ UnityGraphicsMcpMutation.cs
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
- Snapshot / Direction Plan保持
- TTL / Count上限
- Hierarchy / Project / Undo / Scene Event監視
- Compile / Reload / Play Mode遷移時の失効
- Read-only Dirty Guard
- Mutation完了後のRevision更新

### `UnityGraphicsMcpPlanning.cs`

- 構造化Visual Intent
- Direction Plan
- Lighting / GI / Reflection / Atmosphere / Look / Platform Section
- Created / Modified / Dirty / Bake候補のRead-only Preview
- 自然言語・画像の意味理解をUnity C#側で偽装しない

### `UnityGraphicsMcpMutation.cs`

- Explicit Light Operation Schema
- Exact Before / After Preview
- Approval Token Hash
- Executable Plan TTL
- Light Create / Update
- Undo Group / Rollback
- Latest Transaction検証
- 外部変更後のUndo拒否

Capability単位の実装であり、任意のUnity Objectを書き換える汎用Mutation Backendではない。

## 5. Environment resolution

`graphics.inspect_project`はProjectから次をRead-only取得する。

- Unity Version
- Active / Installed Build Target
- Graphics API
- Scripting Backend
- Color Space
- Render Pipeline Kind / Asset / Package Version
- Renderer Data / Feature Count
- Rendering Path / RenderGraph ModeのRead-only推定
- Loaded Scene
- Relevant Package

優先順位:

1. 対象Projectから検出した事実
2. 今回明示されたTargetと制約
3. Project Profile
4. UnityAgent Preference

下位情報で検出済みProject事実を上書きしない。

## 6. Read-only contract

Inspection、Validation、Direction Planning、Light Plan Preparationでは次を禁止する。

- `Undo.RecordObject`
- `EditorUtility.SetDirty`
- `EditorSceneManager.MarkSceneDirty`
- `SerializedObject.ApplyModifiedProperties`
- `AssetDatabase.SaveAssets`
- `AssetDatabase.Refresh`
- `Renderer.material` / `Renderer.materials`
- `Volume.profile`
- Scene / Asset Save
- Bake

実行前後でLoaded Scene、Scene Dirty、Persistent Asset Dirty、Undo Groupを比較する。違反時は状態を自動解除して隠さず、`READ_ONLY_CONTRACT_VIOLATION`を返す。

## 7. Direction and executable plan

```text
compile_direction
→ Session-local Direction Plan
→ preview_plan
→ prepare_light_plan
→ Session-local Executable Light Plan
```

Executable Light Plan:

- 最大8件
- TTL 10分
- 一回使用
- Expected Revisionを保持
- Exact Diff Digestを保持
- Approval Tokenは平文保存せずSHA-256 Hashだけ保持
- Compile、Reload、Play Mode遷移で失効

## 8. Phase 3A mutation contract

`graphics.apply_plan`必須入力:

- `planId`
- `expectedRevision`
- `approvalToken`
- `saveMode = NONE`

適用順:

1. Session / Plan存在確認
2. Revision一致確認
3. Approval Token照合
4. Diff Digest再計算
5. Target Light Baseline再照合
6. Undo Group開始
7. `LIGHT_CREATE` / `LIGHT_UPDATE`適用
8. 対象SceneをDirty化
9. 一つのUndo TransactionへCollapse
10. Transaction IDと新Revisionを返す

例外時は`Undo.RevertAllDownToGroup`でTransaction全体をRollbackする。Scene / Assetは保存せず、Bakeも開始しない。

## 9. Light operation scope

対応:

- Directional
- Point
- Spot
- Name
- Color
- Intensity
- Range
- Spot Angle
- Shadows
- Position
- Euler Angles
- Enabled

`LIGHT_CREATE`では再現可能性のため主要値を明示必須とする。`LIGHT_UPDATE`では`inspect_scene`が返した`GlobalObjectId`を優先識別子として使用する。

未対応:

- Delete
- Area Light
- Pipeline固有Light Component
- Volume / Reflection Probe / Camera
- Material / Renderer Feature

## 10. Undo contract

`graphics.undo_last_transaction`は次の場合だけ実行する。

- transactionIdが直近MyUnityMCP Transactionと一致
- Expected Revisionが現在値と一致
- TransactionのUndo Groupが最新
- 適用後Light状態が記録値と一致
- 外部Hierarchy / Project / Undo変更がない

Undo後、Created Lightが削除され、Updated Lightが事前状態へ復元されたことを再検証する。検証失敗は成功として扱わない。

## 11. Session and identity

Revision更新条件:

- Hierarchy変更
- Project変更
- Undo / Redo
- Scene Open / Close / Save
- Active Scene変更
- Play Mode遷移
- Compile開始 / 終了
- MyUnityMCP Mutation完了

Object ID優先順位:

1. `GlobalObjectId`
2. Session限定Instance ID

GameObject名だけを識別子にしない。SnapshotとPlanには`UnityEngine.Object`参照を保持しない。

## 12. Verification

Unity `6000.0.75f1`のGitHub Actions環境で次を確認する。

- Package Resolve
- Editor / Test Assembly Compile
- 8 Tool Bridge Discovery
- Default Disable
- Direct Handler Invocation
- Inspection / Planning Regression
- Prepare Read-only Guard
- Approval Token拒否
- Stale Revision拒否
- Light Create / Update
- Atomic Undo
- Preview後Target変更拒否
- Undo前外部変更拒否
- No Auto-save / No Bake

EditMode結果: `30 / 30 PASS`

正確なWorkflow Run、Job、Artifactは`Tests/Compatibility/verification-matrix.yaml`を正本とする。PlayerとTarget DeviceはEditor-only Phase 3Aの完了条件外であり、未検証。

## 13. Next design scope

Phase 3Bでは、Phase 3AのPlan / Approval / Revision / Undo Contractを再利用し、Volume、Reflection Probe、Cameraを専用Operation Schemaで追加する。

二つ目の実在BackendやTransportが必要になるまで、抽象Interfaceを先行追加しない。
