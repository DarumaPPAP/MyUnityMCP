# UnityGraphicsMCP 実装計画

- PlanVersion: `2.1.0`
- Status: `Draft`
- CurrentPhase: `Phase 0`

## 1. Goal

UnityAgentMCP配下で必要時だけActivationされる最初のDomain MCPとして、対象Unity Projectの環境をRead-onlyで検出し、利用可能なGraphics BackendとCapabilityを解決してからInspectionを行う。

特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph設定、Target PlatformをMyUnityMCP全体の初期前提にしない。

## 2. Scope classification

分類は`Project Infrastructure`とする。

理由:

- Unity Editor外部Agentとの契約境界を持つ
- Scene、Asset、Lighting Data、Renderer設定等を横断する
- Read / Mutation / Bake / Captureの異なる安全境界を持つ
- Unity Version、Pipeline、Rendering Path、Platformの実在Variationを持つ

ただし、実装が一つしかない段階で複数Backend Interfaceを作らない。

## 3. Ownership and lifetime

### UnityAgentMCP

- Owner: MyUnityMCP
- Lifetime: MCP接続Session
- Responsibility: Selection、Activation、Gate、Shared Transaction Contract
- Consumers: AI Host、Creator Workflow、Domain MCP

### UnityGraphicsMCP

- Owner: MyUnityMCP
- Lifetime: 選択されたMCP Session
- Responsibility: Graphics Domain判断、Tool Group、Project Context、Pipeline / Platform解決
- Consumers: UnityAgentMCP、LiveCreator、MovieCreator、WorldCreator

### Graphics Capability Module

- Owner: UnityGraphicsMCP
- Lifetime: Tool Invocation
- Responsibility: 限定されたUnity Editor API操作
- Consumers: UnityGraphicsMCPだけ

### Generated Unity Asset

- Owner: 対象Unity Project
- Lifetime: Project Asset
- Responsibility: 実際のScene / Material / Timeline等
- Consumers: 対象ゲームProject

## 4. Change axes

- Unity Version
- Render Pipeline Kind
- Render Pipeline Package Version
- Active Renderer
- Rendering Path
- RenderGraph Mode
- Active / Requested Platform
- Graphics API
- Scripting Backend
- Tool Group
- Capability Module
- Read / Write権限
- Scene / Asset Ownership
- Visual Intent
- Bake Dependency
- Evidence Level

## 5. Environment information model

次を混在させない。

### Detected Project Facts

対象Unity ProjectからRead-onlyで取得した事実。

### Requested Target

今回の依頼で指定されたPlatform、品質目標、禁止事項。

### Project Profile

Project固有だが、現在のEditor状態から検出されていない補助情報。

### UnityAgent Preference

ユーザー個人の既定方針。

優先順位:

```text
Detected Project Facts
→ Explicit Requested Target
→ Project Profile
→ UnityAgent Preference
```

下位情報で上位情報を上書きしない。

## 6. Selected architecture

### Phase 0

宣言的なCatalog、Manifest、Workflow、Spec、Environment Resolution Contractだけを作る。未検証C#は追加しない。

### Phase 1A: Bridge and environment inspection

一つのEditor-only UPM Packageへ次を実装する。

```text
Packages/com.darumappap.my-unity-mcp/
├─ package.json
├─ MCP_MANIFEST.yaml
├─ Editor/
│  ├─ UnityAgentMcpTools.cs
│  ├─ UnityMcpEditorSession.cs
│  ├─ UnityGraphicsMcpInspection.cs
│  └─ MyUnityMcp.Editor.asmdef
└─ Tests/Editor/
```

最初に`graphics.inspect_project`をPipeline非依存で実装し、対象Projectの環境とCapability Statusを返す。

### Phase 1B: First concrete backend

1. 利用可能な検証ProjectをInspectする。
2. 検出された環境に対応する最初の具象Backendを実装する。
3. 実際の検証結果をCompatibility Matrixへ記録する。
4. その環境をMyUnityMCP全体の固定対応条件とは扱わない。

最初の具象Backendは開発環境によって決まり、仕様上は固定しない。

### Phase 2以降

実在する責務分離理由が発生した時点で、Graphics Inspection、Mutation Transaction、Bake、Capture等を分割する。

二つ目の実在Backendが追加された時点で、実際に共通する操作だけをInterfaceへ抽出する。

## 7. Capability status

最低限次を使用する。

- `AVAILABLE`
- `UNAVAILABLE`
- `UNSUPPORTED`
- `UNVERIFIED`
- `PACKAGE_NOT_INSTALLED`
- `VERSION_NOT_SUPPORTED`
- `PROJECT_CONFIGURATION_REQUIRED`
- `BACKEND_NOT_IMPLEMENTED`

`UNVERIFIED`を`UNSUPPORTED`と扱わない。

## 8. Rejected alternatives

### 特定Project環境をPackage全体へ固定

不採用理由:

- ProjectごとにUnity Version、Pipeline、Rendering Path、Platformが異なる
- Project Profileと製品対応条件が混同される
- 新しいProjectへ導入するたびに設計変更が必要になる

### 全Domain MCPを最初から別Package化

不採用理由:

- 空Moduleを量産する
- asmdefと依存が増える
- Domain ReloadとVersion管理が複雑になる
- 実在するOwnership差がまだない

### CapabilityごとにMCP Serverを作成

不採用理由:

- Light、Probe、Volume等は一つのGraphics依頼で同時利用する
- TransactionとSnapshot共有が難しくなる
- Tool選択数が増える

### 全Pipeline共通Settings型

不採用理由:

- Pipeline固有設定がnullableで混在する
- Capability差を隠す
- Version差分に弱い

### 最初からPipeline Interfaceを作成

不採用理由:

- 初期実装は一つの具象Backendから始まる
- 実装が一つしかない
- 実際の共通操作がまだ確定していない

## 9. Initial file plan

| Path | Primary responsibility | Owner | Lifetime | Consumers | Split reason |
|---|---|---|---|---|---|
| `Catalog/mcp-catalog.yaml` | Domain MCP選択 | MyUnityMCP | Repository | UnityAgentMCP | 複数MCPを横断する独立契約 |
| `Catalog/creator-catalog.yaml` | Creator選択 | MyUnityMCP | Repository | UnityAgentMCP | Workflow種別の独立契約 |
| `Catalog/capability-catalog.yaml` | Capability所有関係 | MyUnityMCP | Repository | Domain MCP | Module所有権の独立契約 |
| `Specs/UnityAgentMCP/spec.md` | Control Plane仕様 | MyUnityMCP | Repository | 実装者・Reviewer | MCP横断仕様 |
| `Specs/UnityGraphicsMCP/spec.md` | Graphics Domain仕様 | UnityGraphicsMCP | Repository | 実装者・Creator | Domain固有仕様 |
| `Specs/UnityGraphicsMCP/plan.md` | Architecture Decision | UnityGraphicsMCP | Repository | 実装者・Reviewer | ファイル計画の正本 |
| `Specs/UnityGraphicsMCP/editor-tool-design.md` | Editor C# Tool設計 | UnityGraphicsMCP | Repository | 実装者 | Main Thread、Snapshot、環境解決の独立設計 |
| `Specs/UnityGraphicsMCP/tasks.md` | Task境界 | UnityGraphicsMCP | Repository | 実装者 | 実装進行の独立契約 |
| `Packages/.../MCP_MANIFEST.yaml` | Tool Group、環境解決、Capability公開 | UnityGraphicsMCP | Package Version | UnityAgentMCP | Sourceを読まない選択契約 |
| `Tests/Compatibility/verification-matrix.yaml` | 実際の検証実績 | MyUnityMCP | Test Evidence | Reviewer | 対応条件と検証実績の分離 |
| `Workflows/LiveCreator.yaml` | Live制作工程 | LiveCreator | Workflow Version | UnityAgentMCP | 複数Domainを横断 |
| `Workflows/MovieCreator.yaml` | Movie制作工程 | MovieCreator | Workflow Version | UnityAgentMCP | 複数Domainを横断 |
| `Tests/Routing/cases.yaml` | MCP誤選択防止 | MyUnityMCP | Test Run | Reviewer | Routingの独立検証価値 |

## 10. Types kept in the same file

初期C#実装では次を最も近いPrimary Typeと同一ファイルへ保持する。

- Feature-local enum
- Tool Result
- Request DTO
- Revision State
- Tool Group State
- private helper class

Tool SchemaのPublic Contractが安定し、複数Toolから共有されるまで別ファイルへ分離しない。

## 11. Intentionally not created types

- `UnityMcpManager`
- `UnityMcpService`
- `UnityMcpController`
- `UnityMcpCoordinator`
- 1実装だけの`IGraphicsPipelineAdapter`
- `ILightModule`
- `ITimelineModule`
- 空のPipeline Backend
- Runtime Assembly
- Capabilityごとのasmdef

## 12. Dependency direction

```text
UnityAgent Policy
    ↓
UnityAgentMCP
    ↓
Creator Workflow
    ↓
Domain MCP
    ↓
Capability Module
    ↓
Unity Editor API
```

逆方向依存を禁止する。

- Capability ModuleはCreatorを知らない。
- Domain MCPは特定Creatorへ依存しない。
- CreatorはUnity Editor APIを直接呼ばない。
- UnityAgentはPackage Sourceを通常利用時に読まない。

## 13. Data and execution flow

```text
Catalog Entry
→ Selected Manifest
→ Project Environment Inspection
→ Backend / Capability Resolution
→ Scene Snapshot
→ Visual Intent / Direction Plan
→ Dry Run Diff
→ Approved Mutation
→ Dirty Dependency
→ Approved Bake
→ Capture
→ Human Review
```

Snapshot、Plan、Transaction、CaptureはIDで参照し、大きなJSONを毎回Contextへ複製しない。

## 14. Serialization contracts

- GameObject名だけで対象を識別しない。
- GlobalObjectId、Asset GUID、Local File ID、Scene GUID等を優先する。
- Tool ResultはSchema Versionを持つ。
- Detected ProjectとRequested Targetを別フィールドにする。
- Planは`expectedRevision`を持つ。
- Unsupported / Unverified / Fallback / Skipped / Failedを区別する。
- Save Modeを明示する。

## 15. Validation plan

### Phase 0

- CatalogとManifestの参照整合性
- WorkflowとDomain ID整合性
- UnityAgent側Routeとの整合性
- 固定Project環境がGlobal Contractへ残っていないこと
- Namespace / Enum規約
- File Plan / Split Reason

### Phase 1A

- MCP Bridge API確認
- Package Structure
- 導入先ProjectでのCompile
- `graphics.inspect_project`のRead-only保証
- Unknown FactとCapability Status
- Project Profileが検出済み事実を上書きしないこと

### Phase 1B

- 最初の具象BackendのCompile
- EditMode Test
- Inspect後にScene / AssetがDirtyでないこと
- Compatibility Matrix Entry

### Phase 2以降

- Mutation Diff
- Undo / Revert
- Domain Reload中断
- Bake Dependency
- Capture状態復元
- Player / Target Device Evidence

## 16. Re-evaluation conditions

次の場合にArchitectureを再評価する。

- 二つ目のPipeline Backendが実際に追加される
- Domain MCPごとに異なるPackage依存が発生する
- Tool登録APIの外部境界が複数実装を要求する
- 一つのC#ファイルが複数OwnerまたはLifetimeを持つ
- Mutation TransactionがInspectionから独立した複雑性を持つ
- Physical Server分離が必要なSecurityまたはDeployment要件が発生する
