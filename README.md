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

現在はPhase 0です。

- Architecture: 作成済み
- Catalog: 作成済み
- LiveCreator / MovieCreator Workflow: 仕様作成済み
- UnityGraphicsMCP: 仕様作成済み
- UPM Package: 骨格作成済み
- Unity Editor Tool C#実装: 未着手
- Unity Compile / EditMode / Player / Target Device検証: 未実行

仕様やManifestの存在を、MCP Toolが動作する証拠として扱いません。

## Repository map

```text
Catalog/
Specs/
  UnityAgentMCP/
  UnityGraphicsMCP/
Workflows/
Packages/
  com.darumappap.my-unity-mcp/
Tests/
  Compatibility/
```

## First implementation milestone

最初のMilestoneは特定のUnity Version、Pipeline、Rendering Path、Platformではなく、次の能力です。

- 対象ProjectのRead-only環境検出
- Project Contextと要求Targetの分離
- Capability / Backend解決
- `UNSUPPORTED` / `UNVERIFIED` / `PROJECT_CONFIGURATION_REQUIRED`の区別
- Editor-only Read-only Inspection
- 実装済みBackendだけの選択的公開

最初に利用できる検証Projectの環境はCompatibility Matrixへ記録し、MyUnityMCP全体の対応条件とは扱いません。
