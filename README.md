# MyUnityMCP

MyUnityMCPは、Unity EditorをMCP Clientから安全に操作するためのEditor拡張Packageです。Project／SceneのInspection、構造化Planning、明示承認付きMutation／Save／Bake、Capture Evidence、Visual Evaluation、Refine、長時間実行の履歴・Timeout・Cancellationに加え、複数Toolを安全に組み立てるUnityAgentMCP Control Planeを提供します。

## v1.0 scope

- Unity Editor専用
- Unity `6000.0`以上
- CI検証環境: Unity `6000.0.75f1`
- Compatibility CI: Unity `6000.4.12f1` / `6000.5.5f1`
- Manual Compatibility: Unity `6000.7.0a2`
- MCP Tool: 32 Graphics Tools
- Toolはすべて既定で非公開（`AutoRegister = false`）
- Mutation、Save、Bakeはそれぞれ別の承認境界
- Player／実機上でのTool実行は非対応

`v1.0.0` Tagは上記32 Graphics Toolのimmutable release baselineです。

## Current main scope

`main`の次期製品SourceではUnityAgentMCPをCapability単位で追加し、**32 Graphics + 10 Agent = 42 Tool**をProduction Tool Surfaceとして扱います。Version／Tagの更新とPublicationは別Release操作で行うため、`v1.0.0`の内容は変更しません。

UnityAgentMCPはControl Planeです。WorkflowのValidation、Dependency Graph Compile、Preview、Approval Orchestration、協調Execution、Cancellation／Timeout／Historyを担当し、Unity APIを直接Mutationしません。現在Productionで実行委譲できるDomainは`unity_graphics_mcp`のみです。

## Repository Layout

```text
MyUnityMCP/
├─ Packages/        # 実行可能なUPM Package
├─ Catalog/         # 実行可能MCPのCatalog / Capability Contract
├─ Specs/           # 現行製品の開発・契約仕様
├─ Tests/           # Release / Compatibility / Routing検証
├─ TestProjects/    # Unity Editor検証Project
├─ SampleProjects/  # Standalone Sample Project
├─ Templates/       # MCP Client / CI / Acceptance Profile配布物
├─ Design/          # 未実装Control Plane / Domain / Creatorの設計専用資産
└─ .github/         # GitHub Actions等のRepository automation
```

Package内Graphics実装は`Core / Compatibility / Inspection / Planning / Mutation / Save / Bake / Capture / Execution / Tools`へ責務別に整理しています。UnityAgentMCPは`Editor/Development/Agent`に独立したControl Planeとして配置し、Domain実装へ処理を委譲します。

## Quick Start

1. Unity Package ManagerからMCP for Unity Bridgeを導入します。
2. このRepositoryのPackageをGit URL、`.tgz`、またはEmbedded Packageとして導入します。
3. Unityで `Window > MCP for Unity` を開き、検出されたMCP Clientを設定します。
4. Client側では必要なToolだけを許可します。
5. 直接確認は `graphics.inspect_project` → `graphics.inspect_scene`、複数StepのWorkflowは `agent.inspect_capabilities` → `agent.validate_workflow` から開始します。

詳細は[Quick Start](Packages/com.darumappap.my-unity-mcp/Documentation~/quick-start.md)と[Installation](Packages/com.darumappap.my-unity-mcp/Documentation~/installation.md)を参照してください。

## Safety model

```text
Direct Domain:
Inspect → Snapshot → Prepare Plan → Human/Client Approval → Apply
       → Prepare Save → Save
       → Prepare Bake → Bake
       → Capture → Evaluate → Human Review → Refine

Agent Control Plane:
Inspect Capabilities → Validate Workflow → Compile Graph → Preview
                    → Explicit Approval when required → Delegate to Domain MCP
                    → Status / Cancel / History
```

- Read-only ToolはScene、Asset、Undo Groupを変更しません。
- ApplyはPlan ID、Expected Revision、Approval Token、Baselineを再検証します。
- SaveとBakeはMutationへ暗黙統合しません。
- UnityAgentMCPはUnity APIを直接Mutationせず、Operational Domainだけへ委譲します。
- 自動Save、自動Full Bake、任意SerializedProperty書換え、Silent Fallbackは禁止です。
- Visual EvaluationのPASSはHuman Acceptanceを代替しません。
- Domain Reload、Compile、Play Mode移行、Scene構成変更、Client切断、Unity再起動はExecution Historyへ構造化して残します。

## Documentation

- [Tool Reference](Packages/com.darumappap.my-unity-mcp/Documentation~/tool-reference.md)
- [Status / Error Codes](Packages/com.darumappap.my-unity-mcp/Documentation~/status-and-error-codes.md)
- [Safety Model](Packages/com.darumappap.my-unity-mcp/Documentation~/safety-model.md)
- [Bake Constraints](Packages/com.darumappap.my-unity-mcp/Documentation~/bake-constraints.md)
- [Pipeline Support](Packages/com.darumappap.my-unity-mcp/Documentation~/pipeline-support.md)
- [MCP Client Configuration](Packages/com.darumappap.my-unity-mcp/Documentation~/mcp-client-configuration.md)
- [Sample Workflow](Packages/com.darumappap.my-unity-mcp/Documentation~/sample-workflow.md)
- [Troubleshooting](Packages/com.darumappap.my-unity-mcp/Documentation~/troubleshooting.md)
- [Upgrade Guide](Packages/com.darumappap.my-unity-mcp/Documentation~/upgrade-guide.md)
- [Known Issues](Packages/com.darumappap.my-unity-mcp/Documentation~/known-issues.md)
- [Support Matrix](Specs/UnityGraphicsMCP/support-matrix.md)

## Distribution

- Stable baseline Tag: `v1.0.0`
- UPM Package: `Packages/com.darumappap.my-unity-mcp`
- Package Sample: `Samples~/Getting Started`
- Standalone Sample Project: `SampleProjects/MyUnityMCPGettingStarted`
- MCP Client Templates: `Templates/McpClients`
- Acceptance Profile Example: `Templates/AcceptanceProfiles`
- CI Template: `Templates/CI`

`VERSION`変更はRelease Workflowを起動するため、CapabilityのSource PromotionとVersion／Tag Publicationは分離して扱います。

## Verification

Current main向けGateは次を検証します。

- Package Resolve／Editor Compile
- Manifestと一致するProduction Tool Discovery（UnityAgent昇格後は42）
- 125以上のEditor Contract Testに加え、昇格Capability固有Contract
- 新規Sample ProjectでのPackage CompileとTool Discovery
- Getting Started Sample Workflow
- Version／Manifest／Changelog／Support Matrix整合
- 必須文書／配布物／Known Issuesの存在

`v1.0.0`のimmutable release evidenceは`Tests/Compatibility/release-verification.yaml`、current mainの対応範囲は`Tests/Compatibility/support-matrix.yaml`を参照します。

## Design-only assets

`Design/`は将来設計専用です。実装済みCapabilityとして数えません。`v1.0.0` Tagで実行可能なのは32 Graphics Tool、current mainではそれにUnityAgentMCPの10 Toolを追加しています。Profiler、Build、Addressables、UI、Animation、Audio、Cinematic、Creator群は個別Deliveryが完了するまでProduction Operationalとして扱いません。

## License

MIT License。詳細は[LICENSE](LICENSE)を参照してください。
