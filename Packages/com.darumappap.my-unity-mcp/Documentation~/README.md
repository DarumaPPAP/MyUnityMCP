# My Unity MCP Package

## Current state

Phase 1のRead-only Unity Editor Toolは実装・Unity検証まで完了しています。

実装済み:

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`
- Editor Session / Revision
- In-memory Snapshot / Paging
- Read-only Dirty Guard
- Project Environment / Requested Target分離
- Capability / Backend Status解決
- Graphics Validation Rule
- EditMode Test

Unity CIでPackage Resolve、Editor Compile、Bridge Tool Discovery、直接Handler Invocation、9件のEditMode Testを確認しました。検証実績は`Tests/Compatibility/verification-matrix.yaml`へ記録しています。

## Bridge dependency

現在の実装は次のMCP Bridge APIへ接続します。

- Package: `com.coplaydev.unity-mcp`
- 宣言API基準: `10.1.2`
- Unity検証Commit: `9f84072c38906e3ca903f14f6a8edc1a1c9012c3`
- Assembly: `MCPForUnity.Editor`
- Tool登録: `McpForUnityToolAttribute`
- Entry Point: `HandleCommand(JObject)`

MyUnityMCPを導入するProjectでは、このBridge Packageを解決できるPackage RegistryまたはGit Package導入経路が必要です。

## Tool activation

Phase 1の3Toolはすべて`AutoRegister = false`です。

```text
Package導入
→ Unity Compile
→ Bridge Tool Discovery
→ 必要なPhase 1 Toolを明示的にEnable
→ MCP Clientを再接続
```

未選択Toolや未実装Toolを常時公開しません。現段階ではMCP for UnityのTool設定から明示的に有効化し、将来はUnityAgentMCPのActivation Policyから制御します。

## Implemented operation flow

```text
graphics.inspect_project
→ Project EnvironmentをRead-only検出
→ Requested Targetと分離
→ Pipeline / Renderer / Build Target / Capabilityを返す

graphics.inspect_scene
→ Loaded SceneをRead-only解析
→ Snapshot IDとPageを返す
→ 同じSnapshot IDとCursorで続きを取得

graphics.validate_scene
→ Invariant / Policy / Heuristicを区別
→ Severity / Confidence / Evidence / Object IDを返す
```

## Project environment policy

特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph、PlatformをPackage全体へ固定しません。

- 対象Projectの検出済み事実を正とする。
- Requested Targetを検出済み事実と混同しない。
- `UNVERIFIED`を`UNSUPPORTED`として扱わない。
- 未実装Backendへ黙ってFallbackしない。
- 検証環境はCompatibility Matrixへ実績として記録する。

## Read-only safety

Inspectionでは次を行いません。

- `Renderer.material` / `Renderer.materials`の参照
- `Volume.profile`の参照
- `SerializedObject.ApplyModifiedProperties`
- `EditorUtility.SetDirty`
- `Undo.RecordObject`
- Scene Save
- Asset Save
- Bake

Tool実行前後でLoaded Scene、Persistent Asset、Undo Groupの状態を比較します。Renderer Materialをインスタンス化しないこともEditMode Testで検証しています。

## Verified environment

- Unity: `6000.0.75f1`
- Host: GitHub Actions Ubuntu 24.04
- Render Pipeline: Built-inの最小検証Project
- Package Resolve: PASS
- Editor Compile: PASS
- Bridge Discovery: PASS
- Direct Handler Invocation: PASS
- EditMode: `9 / 9 PASS`

この実績は一つの検証環境に対するEvidenceであり、Unity VersionやPipelineの固定要件ではありません。PlayerとTarget DeviceはEditor-onlyのPhase 1完了条件外で、未検証です。

## Next phase

Phase 2ではRead-only Plan Toolを追加します。

```text
graphics.compile_direction
graphics.preview_plan
```

Mutation、Undo、Bake、CaptureはPlan Contractが安定するまで公開しません。
