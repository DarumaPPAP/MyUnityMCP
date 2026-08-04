# UnityGraphicsMCP Unity Editor C# Tool実装設計

- DocumentVersion: `1.1.0`
- DesignStatus: `Implemented / Verification Pending`
- ImplementationStatus: `Phase 1 Read-only Source Complete`
- VerificationStatus: `Unity Compile / Bridge Discovery / EditMode Not Run`

## 1. 目的

対象Unity ProjectをRead-onlyで解析し、Project環境、Scene Graphics状態、確定的不整合を機械可読Resultとして返すEditor-only C# Toolを構築する。

特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph、Target PlatformをMyUnityMCP全体へ固定しない。

## 2. Phase 1 Tool

Source実装済み:

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`

後続Phase:

- `graphics.inspect_frame`
- Plan / Preview
- Mutation / Undo
- Bake
- Capture / Refine

## 3. Bridge contract

Source API確認基準:

- Package: `com.coplaydev.unity-mcp`
- Version: `10.1.2`
- Assembly: `MCPForUnity.Editor`
- Tool Attribute: `McpForUnityToolAttribute`
- Parameter Attribute: `ToolParameterAttribute`
- Entry Point: `public static object HandleCommand(JObject)`
- Response: `SuccessResponse` / `ErrorResponse`
- Command Dispatch: Unity Main Thread

Phase 1 Toolは`AutoRegister = false`とする。

理由:

- Bridgeの`core` Groupへ所属させても初期公開しない。
- Unity CompileとEditMode Test前に利用可能Toolとして見せない。
- 将来UnityAgentMCPのActivation Policyから明示的に有効化できる境界を残す。

## 4. Environment resolution

`graphics.inspect_project`は次をProjectからRead-onlyで取得する。

- Unity Version
- Active / Installed Build Target
- Graphics API
- Scripting Backend
- Color Space
- Render Pipeline Kind
- Render Pipeline Asset
- Pipeline Package Version
- Renderer Data
- Renderer Feature Count
- Rendering PathのRead-only推定
- RenderGraph ModeのRead-only推定
- Loaded Scene
- Relevant Package

優先順位:

1. 対象Projectから検出した事実
2. 今回明示されたTargetと制約
3. Project Profile
4. UnityAgent Preference

下位情報で検出済みProject事実を上書きしない。

## 5. Physical architecture

```text
MCP for Unity Bridge
        ↓
UnityGraphicsMcpTools.cs
        ↓
UnityGraphicsMcpSession.cs
        ↓
UnityGraphicsMcpInspection.cs
        ├─ UnityGraphicsMcpProjectInspection.cs
        ├─ UnityGraphicsMcpSceneInspection.cs
        └─ UnityGraphicsMcpValidation.cs
        ↓
Unity Editor API
```

### `UnityGraphicsMcpTools.cs`

Owner: MCP外部境界

Lifetime: 一つのTool Call

Responsibility:

- Tool Attribute
- Parameter Schema
- JObject変換
- Success / Error Response変換
- Phase 1 ToolのDefault Disable

Split Reason:

- MCP Bridge Package依存をGraphics解析から隔離する。

### `UnityGraphicsMcpSession.cs`

Owner: Unity Editor Session

Lifetime: Domain ReloadまたはEditor終了まで

Responsibility:

- Session ID
- Revision
- In-memory Snapshot
- Snapshot TTL / Count上限
- Hierarchy / Project / Undo / Scene Event監視
- Compile / Domain Reload / PlayMode遷移時のSnapshot無効化
- Read-only Dirty Guard

Split Reason:

- Tool Callを越えて生存するEditor Lifecycleと状態を所有する。

### `UnityGraphicsMcpInspection.cs`

Owner: Graphics Read-only Operation

Lifetime: 一つのInspection

Responsibility:

- 共通Result Schema
- Tool Status
- Inspection実行順
- Snapshot Paging
- Read-only Guard適用
- Exception境界

Split Reason:

- Project / Scene / Validationへ共通するOperation契約を所有する。

### `UnityGraphicsMcpProjectInspection.cs`

Owner: Target Project Facts

Lifetime: 一つのProject Inspection

Responsibility:

- Pipeline非依存Project環境取得
- Serialized PropertyによるRenderer Capability読取
- Package / Build Target / Graphics API取得
- GlobalObjectIdと値正規化の共通Helper

Split Reason:

- Project環境とPackage境界はScene Hierarchy走査とは異なる変更軸を持つ。

### `UnityGraphicsMcpSceneInspection.cs`

Owner: Loaded Scene Snapshot

Lifetime: Snapshot TTLまで

Responsibility:

- Scene Hierarchy走査
- Camera / Light / Probe / Renderer / Material Summary
- Volume / Decal / Probe Volume / VFX / CinemachineのCapability読取
- Lightmap / Renderer Feature状態
- Unity Objectを含まないSnapshot DTO生成

Split Reason:

- 大規模Scene走査、Paging、Snapshot Sizeを独立して最適化する必要がある。

### `UnityGraphicsMcpValidation.cs`

Owner: Graphics Validation Rule

Lifetime: 一つのValidation

Responsibility:

- Invariant / Policy / Heuristic分類
- Severity / Confidence / Evidence
- Missing Material / Shader
- Lightmap Index
- Volume Profile
- LightingDataAsset Heuristic
- Renderer Data整合性

Split Reason:

- Rule追加頻度とTest価値がScene Snapshot構造と異なる。

## 6. Read-only contract

禁止API:

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

実行前後で次を比較する。

- Loaded Scene Count
- Scene Dirty状態
- Undo Group

違反時は`READ_ONLY_CONTRACT_VIOLATION`を返し、Dirty状態を自動解除して隠さない。

## 7. Session and revision

Revision更新条件:

- Hierarchy変更
- Project変更
- Undo / Redo
- Scene Open / Close / Save
- Active Scene変更
- Play Mode遷移
- Compile開始 / 終了

Snapshot:

- Editor Memoryだけに保存
- 最大8件
- TTL 10分
- Domain Reloadで破棄
- Revision不一致で`STALE_SNAPSHOT`
- 別Session IDで`SESSION_EXPIRED`

## 8. Object identity

優先:

1. `GlobalObjectId`
2. Session限定Instance ID

GameObject名だけを識別子にしない。

Snapshotには`UnityEngine.Object`、`SerializedObject`、Scene参照、Material参照を保持しない。

## 9. Scene inspection scope

実装済みSection:

- `CAMERA`
- `LIGHT`
- `LIGHTMAP`
- `LIGHT_PROBE`
- `APV`
- `REFLECTION_PROBE`
- `RENDERER_MATERIAL`
- `VOLUME`
- `DECAL`
- `PARTICLE`
- `VFX`
- `CINEMATIC`
- `RENDERER_FEATURE`

Package固有型は直接Assembly参照せず、型名と公開MemberをRead-onlyで解析する。

## 10. Validation rules

実装済み:

- `GFX-MATERIAL-001`: Missing Shared Material
- `GFX-MATERIAL-002`: Missing Shader
- `GFX-LIGHTMAP-001`: Lightmap Index範囲外
- `GFX-LIGHTMAP-002`: Lightmapあり / LightingDataAsset未確認
- `GFX-VOLUME-001`: Enabled VolumeのShared Profileなし
- `GFX-PIPELINE-001`: URP Renderer Data解決失敗

HeuristicはConfirmed Errorへ昇格させない。

## 11. Test source

実装済み:

- Project Inspection後のScene Dirty非変更
- Camera / Light Snapshot
- Renderer Material非インスタンス化
- Lightmap Index範囲外検出
- Snapshot Cursor範囲外拒否

実行状態:

- Unity Editor未接続のためNot Run

## 12. Compatibility evidence

検証結果は`Tests/Compatibility/verification-matrix.yaml`へ記録する。

Source Completeは次を保証しない。

- Unity Compile
- Bridge Tool Discovery
- EditMode成功
- Player成功
- Target Device成功

Environment Entryがない環境は`UNVERIFIED`であり、`UNSUPPORTED`ではない。

## 13. Phase 1 completion gate

1. Package dependency解決
2. Unity Editor Compile
3. 3 ToolのDiscovery
4. 明示的Tool Enable
5. 各ToolのMCP Invocation
6. EditMode Test成功
7. Read-only Dirty Guard成功
8. Compatibility Matrix更新

すべて通るまで`Operational Complete`としない。
