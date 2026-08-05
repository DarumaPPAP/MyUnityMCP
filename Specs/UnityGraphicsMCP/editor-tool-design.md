# UnityGraphicsMCP Unity Editor C# Tool実装設計

- DocumentVersion: `4.1.0`
- DesignStatus: `Implemented / Unity Editor CI Verified`
- ImplementationStatus: `Phase 4B Dirty Dependency Bake Complete`
- VerificationStatus: `63 / 63 EditMode PASS`

## 1. 目的

対象Unity Projectの環境、Scene Graphics状態、Direction Plan、承認済みGraphics Mutation、Save、Dependency Bake、Capture Evidenceを機械可読Resultとして扱うEditor-only Toolを構築する。

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
- `graphics.prepare_save_plan`
- `graphics.prepare_bake_plan`

### Mutation / Persistence / Bake

- `graphics.apply_plan`
- `graphics.undo_last_transaction`
- `graphics.apply_environment_plan`
- `graphics.undo_last_environment_transaction`
- `graphics.apply_save_plan`
- `graphics.bake_dependencies`

### Evaluation

- `graphics.capture_evaluation`
- `graphics.refine_direction`

全17 Toolは`AutoRegister = false`とし、Activation PolicyまたはBridge設定から明示的に有効化する。

## 3. Bridge contract

- Package: `com.coplaydev.unity-mcp`
- Version: `10.1.2`
- Assembly: `MCPForUnity.Editor`
- Tool Attribute: `McpForUnityToolAttribute`
- Parameter Attribute: `ToolParameterAttribute`
- Entry Point: `public static object HandleCommand(JObject)`
- Response: `SuccessResponse` / `ErrorResponse`
- Command Dispatch: Unity Main Thread

Bridge固有依存はTool Entryへ閉じ込め、Domain本体をBridge Response型へ依存させない。

## 4. Physical architecture

```text
MCP for Unity Bridge
        ↓
UnityGraphicsMcp*Tools.cs
        ↓
UnityGraphicsMcpSession.cs
        ├─ Session / Revision
        ├─ Snapshot / Direction Plan
        └─ Read-only Guard
        ↓
UnityGraphicsMcpInspection.cs
UnityGraphicsMcpPlanning.cs
UnityGraphicsMcpMutation.cs
UnityGraphicsMcpEnvironmentMutation.cs
UnityGraphicsMcpPhase4.cs
UnityGraphicsMcpPhase4Bake.cs
        ↓
Unity Editor API
```

Phase 4実装は既存Applyへ混在させず、Save / Capture / RefineとBakeを独立Partialへ分離する。

## 5. Read-only contract

Inspection、Planning、Prepareの実行前後で次を比較する。

- Loaded Scene Dirty State
- Persistent Asset Dirty State
- Undo Group

Captureは一時RenderTextureを利用するが、Camera TargetTexture、Active RenderTexture、Scene / Asset Dirty、Undoを復元する。

## 6. Session and invalidation

Session-local state:

- Scene Snapshot
- Direction Plan
- Mutation Plan / Transaction
- Save Plan
- Capture Record
- Dirty Dependency Set
- Bake Plan

失効条件:

- Domain Reload
- Compile開始
- Play Mode遷移
- Editor終了
- Revision変更
- TTL超過

Dirty Dependency SetはScene Saveでは失効させない。Scene Closeでは該当Sceneを除去する。

## 7. Mutation and Save

Mutationは一つのUnity Undo Groupへ集約し、例外時Rollbackする。SaveはUndo Transactionから分離し、既存Dirty Loaded Scene一つだけを明示保存する。

Save後の永続化はUndo / 自動Rollbackを保証しない。

## 8. Dirty Dependency Set design

`EditorSceneManager.sceneDirtied`を入口に、保存済みLoaded Sceneの再Bake候補を記録する。

追跡Kind:

- `LIGHTMAP_SCENE`
- `REFLECTION_PROBE`
- `ADAPTIVE_PROBE_VOLUME`

一般Scene変更から実際のGI影響を意味解析せず、保守的にLightmap Dependencyを登録する。Baked Reflection ProbeとProbe Volume ComponentはScene構造から候補を追加する。

Dirty SetはSerialを持ち、Prepare後にSetが変化した場合はBake Applyを拒否する。

## 9. Bake Plan design

Prepareは次を固定する。

- Expected Revision
- Dirty Set Serial
- 全Loaded Contributing Scene Baseline
- Dependency Kind / Object ID / Output Asset
- Dependency Baseline Digest
- Native Backend
- Diff Digest
- Approval Token / TTL

Apply直前に全項目を再検証し、全DependencyのBackendをPreflightする。

## 10. Native Bake backend

### Lightmap Scene

ReflectionでScene引数付きBake APIを解決する。解決できない場合、Loaded Sceneが一つだけなら`Lightmapping.Bake()`を使用する。

複数Loaded Sceneで全Scene BakeへFallbackしない。

### Reflection Probe

`Lightmapping.BakeReflectionProbe`を使用する。既存Cubemap Assetへの上書きだけを許可し、新規Asset Pathを生成しない。

### APV

Component候補を検出するが、Baking Set / Lighting Scenario / Package Version契約を持たないため実行しない。

## 11. Bake failure model

BakeはUnity Undo外である。

- Planは実行開始時に消費
- 自動Saveなし
- 自動Rollbackなし
- 完了済みDependencyだけDirty Setから除去
- 途中失敗時は`PARTIAL`と完了 / 失敗Dependency Evidenceを返す

## 12. Test architecture

Editor Test Assemblyから次を検証する。

- 17 Tool Discovery / Default Disable
- Phase 1-3 Regression
- Save / Capture / Refine contract
- Bake Prepare Read-only
- Save後のDirty Dependency保持
- Approval / Revision / Baseline / Dirty Set Guard
- Scene限定Backend Invocation
- Dependency消費
- APV Backend rejection
- No Auto-save / No Silent Full Bake Fallback

Test用に公開範囲を広げず、`InternalsVisibleTo("MyUnityMcp.Editor.Tests")`だけを使用する。
