# Changelog

このProjectは[Semantic Versioning](https://semver.org/)に従います。

## [1.1.0] - Unreleased

### Added

- UnityAgentMCP Control Planeと10個の`agent.*` Tool
- Workflow Validation、Graph Compile、Preview、Approval、Execution／Cancel／History Contract
- UnityAgent Operational SpecとCapability Contract
- WorldCreatorと3個の`world.*` Tool
- Visual GoalからRead-only Graphics Preflightを組み立てる`world.compile_workflow`
- Agent経由でPreflightを開始する`world.start_preflight`
- Human Review必須の`world.create_review_handoff`
- WorldCreator専用Editor ContractとProduction Spec

### Changed

- Current SourceのTool Surfaceを32 Graphics + 10 Agent + 3 WorldCreator = 45 Toolへ拡張するDelivery Candidateを追加
- Fresh Project Tool DiscoveryとEditor Contract GateをWorldCreatorへ対応
- WorldCreatorはGraph Engineering候補全体をMergeせず、3 ToolだけをCapability-scoped Deliveryとして移植
- Source Versionを`1.1.0`へ移行し、GitHub Release Publicationとは分離
- `VERSION`変更による暗黙Releaseを廃止し、明示Publish操作だけでReleaseする構成へ変更
- Stage 2〜8 CandidateをExact 77 Toolへ確定し、Build DomainとAddressables Content BuildをRuntime Surfaceから撤去
- Local Runtime ValidationとProduction Promotionを分離し、Candidate 6 Domainは`integration_candidate`を維持

### Verification

- Graph Engineering Run #52でAgent SourceのUnity 6000.0 / 6000.4 / 6000.5 Contractを検証
- Unity 6000.7.0a2 Manual CanaryでAgentを含むCombined Tool Discoveryを確認
- Stage 0の42 Tool Production baselineは実Editor E2Eで`integration_verified_manual`
- exact 42 Tool Production CIはGitHub Actions JobがRunner Step開始前にFailureするため`not_verified`を維持
- WorldCreatorを含むProduction 45 Toolは実Editor Evidenceで`integration_verified_manual`
- Stage 2〜8 Exact 77 CandidateはLocal CG / Unity `6000.7.0a2`でRuntime ValidationとProduction 45 RegressionをPASS
- Addressables Package未導入時の明示`UNSUPPORTED`境界はPASS。Package Editor Test Runner、Fresh-project Sample Workflow、Positive Backend Matrix、Automated CI、External Transport Disconnect/Reconnectは`not_verified`
- Candidate Production PromotionはHuman Gate pending

`1.1.0`はCurrent Source Versionです。この変更自体ではTag／GitHub Releaseを作成しません。

## [1.0.0] - 2026-08-11

### Added

- 32 Unity Editor MCP Toolの正式公開契約
- Inspect → Plan → Approval付きMutation / Save / Bake → Capture → Evaluate / Refineの安全なWorkflow
- Unity API Compatibility Registryを`BASE + UNITY_6000_4 + UNITY_6000_5 + UNITY_6000_7`の4保守Bucketとして導入
- MyUnityMCP変更時にCompatibility Registry / Skill / Testsを同時再評価する`myunitymcp-unity-api-compatibility` Skill
- `graphics.inspect_project`へのUnity Version、Compatibility Bucket、関連Package Versionの検出情報
- Dependency Bake、APV Bake Job、Capture Evidence、Human Visual Review、Execution History、Timeout、Cancellation

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
- Unity `6000.7.0a2`: Manual Package Import / Compile / 32 Tool Discovery / `graphics.inspect_project` / Compatibility Bucket確認 PASS

### Known limitations

- Player／Target Device上のTool実行は非対応・未検証
- Built-in PipelineではAPV Bake非対応
- URP／HDRPの実APV Bakeは導入ProjectごとのBaking SetとBackend検証が必要

### Release history note

`v1.0.1`および`v1.0.2-test.*`は1.0系列のRepository / Compatibility検証履歴として保持します。公開済みTagはimmutableです。

## [0.8.0] - 2026-08-05

- 長時間AI制作向けIntegration Hardening、Fault Injection、Execution Runtimeを追加。
