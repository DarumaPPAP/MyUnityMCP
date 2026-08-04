# MyUnityMCP

UnityAgentの判断規則と連携し、目的別Creator、専門Domain MCP、Unity操作Capability Moduleを必要時だけ有効化するUnity制作基盤です。

## Architecture

```text
UnityAgent
ユーザーの規約・感性・Unity知識
        ↓
UnityAgentMCP
Creator / Domain MCPの選択、権限、実行順序
        ↓
Creator Workflow
LiveCreator / MovieCreator / WorldCreator
        ↓
Domain MCP
Graphics / Cinematic / UI / Addressables / Profiler ...
        ↓
Capability Module
Light / Lightmap / Probe / Timeline / Cinemachine ...
        ↓
Unity Editor API
```

## Ownership

このRepositoryは次を所有します。

- UnityAgentMCP Control Plane
- Creator Workflow
- Domain MCP
- Capability Module
- MCP Catalog / Manifest / Tool Schema
- UPM Package
- MCP固有仕様とTest

MCPが生成・変更するScene、Prefab、Material、Timeline、Volume Profile等は対象Unity Projectが所有します。

UnityAgentはユーザー固有のコーディング規約、Architecture方針、Visual Direction、Route、Context Pack、Task Contract、Knowledge Contractを所有します。

## Project environment resolution

MyUnityMCPはUnity Version、Render Pipeline、Rendering Path、RenderGraph、Target PlatformをRepository全体の固定前提にしません。

```text
対象Unity ProjectをInspect
→ 検出したProject事実を確定
→ ユーザーが今回指定したTargetと比較
→ 利用可能なBackend / Capabilityだけを選択
→ 未対応・未検証・未設定を区別して返す
```

情報の優先順位は次です。

1. 対象Unity Projectから検出した事実
2. 今回の依頼で明示されたTargetと制約
3. Project固有Profile
4. UnityAgentの既定Preference

`UNVERIFIED`を`UNSUPPORTED`として扱わず、Project事実をProfileや個人既定値で上書きしません。

## On-demand activation

通常はCatalogの短いEntryだけを参照します。

```text
Catalog
→ 選択Manifest
→ 必要Tool Group
→ Tool実行
```

全Manifest、全Tool Schema、Package C# Sourceを毎回読み込みません。

Tool Groupは次の順序で段階公開します。

```text
inspect → plan → mutate → bake → capture
```

## Current status

Phase 1のRead-only Unity Editor Toolは実装・Unity検証まで完了しています。

- Architecture / Catalog / Workflow: 作成済み
- UnityGraphicsMCP仕様: 作成済み
- `graphics.inspect_project`: 実装・Bridge Discovery・直接Invocation検証済み
- `graphics.inspect_scene`: 実装・Snapshot / Paging・Read-only検証済み
- `graphics.validate_scene`: 実装・Rule検証済み
- Session / Revision / Snapshot / Paging: 実装済み
- Read-only Dirty Guard: Scene / Persistent Assetで検証済み
- Renderer Material非インスタンス化: 検証済み
- Unity Editor Compile: 成功
- EditMode Test: 9 / 9成功
- Player / Target Device: Phase 1 Editor Toolの完了条件外、未実行
- Plan / Mutation / Bake / Capture: 未着手

検証環境は一つの実績であり、Package全体の固定対応条件ではありません。詳細は`Tests/Compatibility/verification-matrix.yaml`を正本とします。

## Phase 1 tools

```text
graphics.inspect_project
graphics.inspect_scene
graphics.validate_scene
```

3Toolは`AutoRegister = false`で、明示的にActivationした場合だけ公開します。未実装Toolは公開しません。

MCP Bridgeの宣言API基準は`com.coplaydev.unity-mcp 10.1.2`、Unity CIで検証したBridge SourceはCommit `9f84072c38906e3ca903f14f6a8edc1a1c9012c3`です。

## Verified Phase 1 evidence

- Unity: `6000.0.75f1`
- Host: GitHub Actions Ubuntu 24.04
- Package Resolve: PASS
- Editor Compile: PASS
- Bridge Tool Discovery: PASS
- Direct Handler Invocation: PASS
- EditMode: `9 / 9 PASS`
- Workflow Run: `30909837287`
- Evidence Artifact: `MyUnityMCP-Phase1-Unity-Evidence`

この実績はPlayer、実機、すべてのUnity Version、すべてのRender Pipeline対応を意味しません。

## Repository map

```text
Catalog/
Specs/
  UnityAgentMCP/
  UnityGraphicsMCP/
Workflows/
Packages/
  com.darumappap.my-unity-mcp/
    Editor/
    Tests/Editor/
TestProjects/
  MyUnityMCPPhase1/
Tests/
  Compatibility/
```

## Next phase

Phase 2ではUnity状態を変更せず、Visual Intentを技術Planへ変換する次のToolを設計・実装します。

```text
graphics.compile_direction
graphics.preview_plan
```

Mutation、Undo、Bake、CaptureはPhase 2のPlan Contractが安定した後に開放します。
