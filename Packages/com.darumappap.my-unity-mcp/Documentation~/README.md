# My Unity MCP Package

## Current state

Phase 1のRead-only C# Tool Sourceを実装しています。

実装済みSource:

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`
- Editor Session / Revision
- In-memory Snapshot / Paging
- Read-only Dirty Guard
- Graphics Validation Rule
- EditMode Test Source

ただし、対象Unity EditorでのCompile、Tool Discovery、EditMode Testはまだ実行していません。現時点の状態は`source_complete_unverified`です。

## Bridge dependency

現在の実装は次のMCP Bridge APIへ接続します。

- Package: `com.coplaydev.unity-mcp`
- API確認Version: `10.1.2`
- Assembly: `MCPForUnity.Editor`
- Tool登録: `McpForUnityToolAttribute`
- Entry Point: `HandleCommand(JObject)`

MyUnityMCPを導入するProjectでは、このBridge Packageを解決できるPackage Registryまたは導入経路が必要です。

## Tool activation

Phase 1の3Toolはすべて`AutoRegister = false`です。

```text
Package導入
→ Unity Compile
→ EditMode Test
→ Bridge Tool Discovery
→ Phase 1 Toolを明示的にEnable
→ MCP Clientを再接続
```

検証前に`core` Toolとして常時公開しません。現段階ではMCP for UnityのTool設定から明示的に有効化し、将来はUnityAgentMCPのActivation Policyから制御します。

## Implemented operation flow

```text
graphics.inspect_project
→ Project EnvironmentをRead-only検出
→ Pipeline / Renderer / Build Target / Packageを返す

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
- 検証環境は`Tests/Compatibility/verification-matrix.yaml`へ記録する。

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

Tool実行前後でLoaded SceneのDirty状態とUndo Groupを比較し、変化を検出した場合は`READ_ONLY_CONTRACT_VIOLATION`を返します。

## Verification required

運用可能と判断する前に、対象Unity Projectで次を実行します。

1. Package dependency解決
2. Unity Editor Compile
3. EditMode Test
4. MCP Bridge Tool Discovery
5. Phase 1 Toolの明示Enable
6. `graphics.inspect_project`実行
7. `graphics.inspect_scene`実行
8. `graphics.validate_scene`実行
9. Compatibility Matrix更新

未実行GateをPassedとして扱いません。
