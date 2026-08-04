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

現在はPhase 1のRead-only C# Tool Sourceまで構築済みです。

- Architecture / Catalog / Workflow: 作成済み
- UnityGraphicsMCP仕様: 作成済み
- `graphics.inspect_project`: Source実装済み・Unity未検証
- `graphics.inspect_scene`: Source実装済み・Unity未検証
- `graphics.validate_scene`: Source実装済み・Unity未検証
- Session / Revision / Snapshot / Paging: Source実装済み
- Read-only Dirty Guard: Source実装済み
- EditMode Test: Source実装済み・未実行
- Unity Compile / Bridge Discovery / Player / Target Device: 未実行
- Plan / Mutation / Bake / Capture: 未着手

Sourceの存在だけを運用可能性の証拠とは扱いません。対象Unity ProjectでCompile、Tool Discovery、EditMode Testを通過した後にCompatibility Matrixへ実績を記録します。

## Phase 1 tools

```text
graphics.inspect_project
graphics.inspect_scene
graphics.validate_scene
```

MCP BridgeのAPI確認基準は`com.coplaydev.unity-mcp 10.1.2`です。

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
Tests/
  Compatibility/
```

## Next gate

次は対象Unity ProjectへPackageを導入し、以下を検証します。

1. Package dependency解決
2. Unity Editor Compile
3. MCP Tool Discovery
4. Read-only Tool実行
5. EditMode Test
6. Compatibility Matrix更新
