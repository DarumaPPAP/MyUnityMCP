# UnityGraphicsMCP 実装計画

- PlanVersion: `2.0.0`
- Status: `Draft`
- CurrentPhase: `Phase 0`

## 1. Goal

UnityAgentMCP配下で必要時だけActivationされる最初のDomain MCPとして、UnityGraphicsMCPをURP 17向けRead-only Inspectionから段階実装する。

## 2. Scope classification

分類は`Project Infrastructure`とする。

理由:

- Unity Editor外部Agentとの契約境界を持つ
- Scene、Asset、Lighting Data、Renderer Data等を横断する
- Read / Mutation / Bake / Captureの異なる安全境界を持つ
- 将来Built-in / URP / HDRPの実在Backend Variationを持つ

ただし初期実装はURPだけであり、複数Backend Interfaceはまだ作らない。

## 3. Ownership and lifetime

### UnityAgentMCP

- Owner: MyUnityMCP
- Lifetime: MCP接続Session
- Responsibility: Selection、Activation、Gate、Shared Transaction Contract
- Consumers: AI Host、Creator Workflow、Domain MCP

### UnityGraphicsMCP

- Owner: MyUnityMCP
- Lifetime: 選択されたMCP Session
- Responsibility: Graphics Domain判断、Tool Group、Pipeline / Platform解決
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
- Render PipelineとPackage Version
- Platform
- Tool Group
- Capability Module
- Read / Write権限
- Scene / Asset Ownership
- Visual Intent
- Bake Dependency
- Evidence Level

## 5. Selected architecture

### Phase 0

宣言的なCatalog、Manifest、Workflow、Specだけを作る。未検証C#は追加しない。

### Phase 1

一つのEditor-only UPM PackageへRead-only Inspectionを実装する。

```text
Packages/com.darumappap.my-unity-mcp/
├─ package.json
├─ MCP_MANIFEST.yaml
├─ Editor/
│  ├─ UnityAgentMcpTools.cs
│  └─ MyUnityMcp.Editor.asmdef
└─ Tests/Editor/
```

初期のPrimary C#ファイルは一つから開始する。Tool登録API、Unity Main Thread実行、Result Schemaを同一責務として理解可能な間は分割しない。

### Phase 2以降

実在する責務分離理由が発生した時点で、Graphics Inspection、Mutation Transaction、Capture等を分割する。

## 6. Rejected alternatives

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

- Built-in、URP、HDRP固有設定がnullableで混在する
- Capability差を隠す
- Version差分に弱い

### 最初からPipeline Interfaceを作成

不採用理由:

- 初期実装はURPだけ
- 実装が一つしかない
- 具象依存で不足する証拠がない

## 7. Initial file plan

| Path | Primary responsibility | Owner | Lifetime | Consumers | Split reason |
|---|---|---|---|---|---|
| `Catalog/mcp-catalog.yaml` | Domain MCP選択 | MyUnityMCP | Repository | UnityAgentMCP | 複数MCPを横断する独立契約 |
| `Catalog/creator-catalog.yaml` | Creator選択 | MyUnityMCP | Repository | UnityAgentMCP | Workflow種別の独立契約 |
| `Catalog/capability-catalog.yaml` | Capability所有関係 | MyUnityMCP | Repository | Domain MCP | Module所有権の独立契約 |
| `Specs/UnityAgentMCP/spec.md` | Control Plane仕様 | MyUnityMCP | Repository | 実装者・Reviewer | MCP横断仕様 |
| `Specs/UnityGraphicsMCP/spec.md` | Graphics Domain仕様 | UnityGraphicsMCP | Repository | 実装者・Creator | Domain固有仕様 |
| `Specs/UnityGraphicsMCP/plan.md` | Architecture Decision | UnityGraphicsMCP | Repository | 実装者・Reviewer | ファイル計画の正本 |
| `Specs/UnityGraphicsMCP/tasks.md` | Task境界 | UnityGraphicsMCP | Repository | 実装者 | 実装進行の独立契約 |
| `Packages/.../MCP_MANIFEST.yaml` | Tool GroupとCapability公開 | UnityGraphicsMCP | Package Version | UnityAgentMCP | Sourceを読まない選択契約 |
| `Workflows/LiveCreator.yaml` | Live制作工程 | LiveCreator | Workflow Version | UnityAgentMCP | 複数Domainを横断 |
| `Workflows/MovieCreator.yaml` | Movie制作工程 | MovieCreator | Workflow Version | UnityAgentMCP | 複数Domainを横断 |
| `Tests/Routing/cases.yaml` | MCP誤選択防止 | MyUnityMCP | Test Run | Reviewer | Routingの独立検証価値 |

## 8. Types kept in the same file

初期C#実装では次をPrimary Typeと同一ファイルへ保持する。

- Feature-local enum
- Tool Result
- Request DTO
- Revision State
- Tool Group State
- private helper class

Tool SchemaのPublic Contractが安定し、複数Toolから共有されるまで別ファイルへ分離しない。

## 9. Intentionally not created types

- `UnityMcpManager`
- `UnityMcpService`
- `UnityMcpController`
- `UnityMcpCoordinator`
- `IGraphicsPipelineAdapter`
- `ILightModule`
- `ITimelineModule`
- 空のBuiltIn Backend
- 空のHDRP Backend
- Runtime Assembly
- Capabilityごとのasmdef

## 10. Dependency direction

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

## 11. Data and execution flow

```text
Catalog Entry
→ Selected Manifest
→ Inspect Snapshot
→ Visual Intent / Direction Plan
→ Dry Run Diff
→ Approved Mutation
→ Dirty Dependency
→ Approved Bake
→ Capture
→ Human Review
```

Snapshot、Plan、Transaction、CaptureはIDで参照し、大きなJSONを毎回Contextへ複製しない。

## 12. Serialization contracts

- GameObject名だけで対象を識別しない。
- GlobalObjectId、Asset GUID、Local File ID、Scene GUID等を優先する。
- Tool ResultはSchema Versionを持つ。
- Planは`expectedRevision`を持つ。
- Unsupported / Fallback / Skipped / Failedを区別する。
- Save Modeを明示する。

## 13. Validation plan

### Phase 0

- CatalogとManifestの参照整合性
- WorkflowとDomain ID整合性
- UnityAgent側Routeとの整合性
- Namespace / Enum規約
- File Plan / Split Reason

### Phase 1

- Package Structure
- Unity 6000.3 Compile
- EditMode Test
- Inspect後にScene / AssetがDirtyでないこと
- Missing Package時のCompile境界

### Phase 2以降

- Mutation Diff
- Undo / Revert
- Domain Reload中断
- Bake Dependency
- Capture状態復元
- Player / Target Device Evidence

## 14. Re-evaluation conditions

次の場合にArchitectureを再評価する。

- Built-inまたはHDRPの実装が実際に追加される
- Domain MCPごとに異なるPackage依存が発生する
- Tool登録APIの外部境界が複数実装を要求する
- 一つのC#ファイルが複数OwnerまたはLifetimeを持つ
- Mutation TransactionがInspectionから独立した複雑性を持つ
- Physical Server分離が必要なSecurityまたはDeployment要件が発生する
