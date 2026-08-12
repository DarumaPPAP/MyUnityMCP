# Changelog

このProjectは[Semantic Versioning](https://semver.org/)に従います。

## [1.1.0] - Unreleased

### Added

- UnityAgentMCP Control Planeと10個の`agent.*` Tool
- Workflow Validation、Dependency Graph Compile、Execution Preview、Approval Orchestration、Cancellation／Timeout／History
- `Catalog/unity-agent-capability-contracts.yaml`と`Specs/UnityAgentMCP/spec.md`
- WorldCreatorと3個の`world.*` Tool
- `world.compile_workflow`によるVisual Goal → Read-only Graphics Preflight Graph生成
- `world.start_preflight`によるAgent経由Preflight実行
- `world.create_review_handoff`によるHuman Review必須Handoff
- `Specs/UnityWorldCreatorMCP/spec.md`とWorldCreator専用Editor Contract Test

### Changed

- Current mainのProduction Tool Surfaceを32 Graphics + 10 Agent + 3 WorldCreator = 45 Toolへ拡張するDelivery Candidateを追加
- Release ContractのTool CountをManifest基準へ変更し、将来のCapability昇格で固定値を書き換えない構成へ変更
- Editor VerificationでGraphics限定Filterを外し、昇格済みCapabilityのContract Testを含める
- UnityAgentMCPをDesign Only RegistryからOperational Catalogへ昇格
- WorldCreatorをGraph Engineering候補からCapability-scoped Deliveryへ移植し、MovieCreator／LiveCreatorを分離
- Source versionを次期Minor `1.1.0`へ移行し、GitHub Release Publicationとは分離
- `VERSION`変更による暗黙Releaseを廃止し、`workflow_dispatch`または明示Publish承認だけでReleaseする構成へ変更

### Verification

- Agent SourceはGraph Engineering Run #52でUnity `6000.0.75f1` / `6000.4.12f1` / `6000.5.5f1`のEditMode ContractをPASS
- Unity `6000.7.0a2`のGraph Engineering Manual CanaryでPackage Compile／Recognition／Agentを含む91 Tool Discoveryを確認
- Stage 0の42 Tool Production baselineはUnity `6000.7.0a2`実EditorでGraphics Read-only、Agent Orchestration、Approval、Light Mutation、Normal Undoまで`integration_verified_manual`
- Current exact 42 Tool Production CIはGitHub Actions JobがRunner Step開始前にFailureするため`not_verified`を維持
- WorldCreator 45 Tool Deliveryは専用Contractを追加済み。Automated CIと実Editor E2EはDelivery Gateとして別途確定する

### Release note

`1.1.0`はCurrent Source Versionです。この変更自体ではTag／GitHub Releaseを作成しません。Publicationは別の明示承認操作です。

## [1.0.0] - 2026-08-11

### Added

- 32 Unity Editor MCP Toolの正式公開契約
- Inspect → Plan → Approval付きMutation / Save / Bake → Capture → Evaluate / Refineの安全なWorkflow
- Unity API Compatibility Registryを`BASE + UNITY_6000_4 + UNITY_6000_5 + UNITY_6000_7`の4保守Bucketとして導入
- MyUnityMCP変更時にCompatibility Registry / Skill / Testsを同時再評価する`myunitymcp-unity-api-compatibility` Skill
- `graphics.inspect_project`へのUnity Version、Compatibility Bucket、関連Package Versionの検出情報
- Dependency Bake、APV Bake Job、Capture Evidence、Human Visual Review、Execution History、Timeout、Cancellation
- Getting Started Package Sample、Standalone Sample Project、MCP Client / Acceptance Profile / CI Template

### Changed

- Unity 6.4 / 6.5 / 6.7のAPI移行へ対応し、Object / Sceneの一時識別をMyUnityMCP Session Tokenへ分離
- Unity 6.7でError化した`SceneHandle`暗黙変換、`GetInstanceID()`、`InstanceIDToObject(int)`依存を除去
- Editor内部実装を`Core / Compatibility / Inspection / Planning / Mutation / Save / Bake / Capture / Execution / Tools`へ責務別整理
- `UnityGraphicsMcp` namespaceは維持しつつ、内部型の冗長な`UnityGraphicsMcp` prefixを削除
- Tool wrapperはDomain単位でまとめ、外部MCP Tool名`graphics.*`は維持
- Package Assetの`.meta`契約とSemantic Naming GuardをCIへ追加

### Verification

- Unity `6000.0.75f1`: Compile / 32 Tool Discovery / 125以上のEditMode Contract PASS
- Unity `6000.4.12f1`: Compatibility EditMode / Compile Verify PASS
- Unity `6000.5.5f1`: Compatibility EditMode / Compile Verify PASS
- Fresh Project / Sample Workflow / Release Contract PASS
- Unity `6000.7.0a2`: Manual Package Import / Compile / 32 Tool Discovery / `graphics.inspect_project` / Compatibility Bucket確認 PASS
- Unity 6.7 automated CanaryはGameCI Editor image未提供のためManual Evidenceを正本とする

### Known limitations

- Player／Target Device上のTool実行は非対応・未検証
- Built-in PipelineではAPV Bake非対応
- URP／HDRPの実APV Bakeは導入ProjectごとのBaking SetとBackend検証が必要
- BatchMode NoGraphicsでは実画像Captureを検証しない

### Release history note

`v1.0.1`および`v1.0.2-test.*`は1.0系列のRepository / Compatibility検証履歴として保持します。公開済みTagはimmutableです。

## [0.8.0] - 2026-08-05

- 長時間AI制作向けIntegration Hardening、Fault Injection、Execution Runtimeを追加。
