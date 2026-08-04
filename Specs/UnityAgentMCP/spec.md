# UnityAgentMCP 仕様書

- FeatureName: `UnityAgentMCP`
- DocumentVersion: `1.0.0`
- DesignStatus: `Draft`
- ImplementationStatus: `Not Started`
- VerificationStatus: `Not Run`

## 1. 目的

UnityAgentのユーザー固有規約・感性・Domain Knowledgeを受け取り、依頼ごとに必要なCreator、Domain MCP、Tool Groupだけを選択してUnity Editorへ接続するControl Planeを構築する。

UnityAgentMCPはUnity操作を直接実装せず、専門Moduleの選択、Activation、権限、共有Transaction、結果統合を所有する。

## 2. 論理階層

```text
UnityAgent
    ↓ policy and preferences
UnityAgentMCP
    ↓ selection and gates
Creator Workflow
    ↓ domain requests
Domain MCP
    ↓ bounded tool calls
Capability Module
    ↓
Unity Editor API
```

### UnityAgent

- ユーザーのコーディング規約
- Architecture方針
- Platform方針
- Visual Direction
- Knowledge Contract
- Evidence要件

### UnityAgentMCP

- 依頼分類
- CreatorまたはPrimary Domain MCP選択
- Conditional Domain MCP追加
- Manifest遅延読込
- Tool Group段階公開
- Approval Gate
- Shared Snapshot / Revision / Transaction契約
- 結果統合

### Creator

- 完成目的
- 制作工程
- Domain間の順序
- 完了条件

### Domain MCP

- 専門領域の技術判断
- Pipeline / Platform解決
- Tool Group Schema
- Capability Moduleの利用

### Capability Module

- 限定されたUnity API操作
- Read / Write分類
- Input / Output Schema
- Validation

## 3. Physical architecture

初期実装は一つのUPM Packageと一つのUnity Editor接続にまとめる。

```text
com.darumappap.my-unity-mcp
├─ Control Plane
├─ Creator Registry
├─ Domain Registry
├─ Tool Group Registry
└─ Capability Modules
```

論理Moduleを理由なく別Server、別Process、別Port、別Package、別asmdefへ分割しない。

## 4. Selection contract

- Primary CreatorまたはPrimary Domain MCPを必ず一つ選択する。
- 完成目的が複数Domainを横断する場合はCreatorを選択する。
- 限定された専門操作の場合はDomain MCPを直接選択する。
- Conditional Domain MCPは条件成立時だけ追加し、原則2つまでとする。
- CreatorはCapability Moduleを直接呼び出さない。
- Domain MCP同士を自由に相互呼び出しさせない。順序はCreatorまたはUnityAgentMCPが所有する。

## 5. Context loading contract

### 常時読込

- Catalogの短いEntry
- UnityAgentの選択Route / Context / Task / Knowledge

### 選択後だけ読込

- 選択されたCreator Workflow
- 選択されたDomain MCP Manifest
- 現在必要なTool Group Schema

### 通常利用では読まない

- 全Manifest
- 全Tool Schema
- MCP Package C# Source
- 未選択DomainのKnowledge

Package Sourceを読むのはMCP実装、MCPバグ修正、Schema監査、Unity Version移行、Security Reviewだけとする。

## 6. Tool Group contract

```text
inspect → plan → mutate → bake → capture
```

### inspect

- Read-only
- Scene / AssetをDirtyにしない
- Project / Scene / Frame / Capability / Validationを取得する

### plan

- Read-only
- Intent、Direction Plan、Shot Plan、Diff Previewを生成する

### mutate

必須条件:

- 承認済みPlan
- `expectedRevision`
- 明示的Mutation許可
- Diff
- Undo / Revert契約
- Save Policy

### bake

必須条件:

- 別の明示的Bake許可
- Dirty Dependency Set
- Bake対象と推定時間
- Cancel / Failure結果

無条件の全Bakeを禁止する。

### capture

- TimelineまたはEditor一時状態を保存する
- Capture後に元状態を復元する
- Capture生成をVisual Acceptanceと扱わない

## 7. Ownership

| 対象 | 正本 |
|---|---|
| ユーザー規約・Unity Domain Contract | `DarumaPPAP/UnityAgent` |
| MCP仕様・Catalog・Workflow・Package | `DarumaPPAP/MyUnityMCP` |
| MCP生成Scene / Prefab / Material等 | 対象Unity Project |
| 美的基準 | `DarumaPPAP/Beautiful-Definition` |
| 汎用Graph / Retry / Budget | `DarumaPPAP/Unity-Graph-Engineering` |

## 8. Safety requirements

- Read-only ToolはUnity状態を変更しない。
- Project Settings、Renderer Data、Pipeline Asset変更は別承認を要求する。
- Automatic Saveは禁止する。
- Silent Fallbackは禁止する。
- Tool Resultは機械可読Resultと人間向けSummaryを返す。
- Domain Reload、Compile開始、Editor終了時に未完了Transactionを中断できること。
- MCPの自己申告だけをEvidenceにしない。

## 9. Non-goals

- UnityAgentMCP自身へのGraphics、Timeline、UI等の専門処理実装
- 初回から複数の物理MCP Serverを起動すること
- 全Domain MCPの空実装
- Runtime MCP
- 無承認Mutation
- Automatic Save
- Human ReviewなしのVisual Acceptance

## 10. Acceptance criteria

- CatalogからPrimary CreatorまたはDomain MCPを一つ選択できる。
- 未選択Manifest、Tool Schema、Package Sourceを読み込まない。
- Tool Groupを段階公開できる。
- MutationとBakeを別Gateとして扱える。
- Creator、Domain MCP、Capability ModuleのOwnershipが重複しない。
- MyUnityMCPと対象Unity Projectの成果物境界が明確である。
- 未実装Toolを利用可能として公開しない。
