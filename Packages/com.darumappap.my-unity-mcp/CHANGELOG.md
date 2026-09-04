# Changelog

このProjectは[Semantic Versioning](https://semver.org/)に従います。

## [1.1.1] - 2026-09-05

### Fixed

- UnityAgent delegated result normalizationとHistory migrationを修正
- `AgentDelegateRegistry`のReflection `Assembly`曖昧性を修正

### Changed

- UnityAgent Runtime Catalogをv5 Tool Objectへ移行
- Control Plane RuntimeをCatalog / Approval / Graph / Execution / History / Trace / Result責務へ分離
- Agent Runtime / Safety ValidatorとRepository Hygiene Gateを追加
- Historical Evidence / Sample Surface / obsolete support matrixを現行Package配布面から除去

### Verification

- Production Editor Surfaceは **77 Tool** を維持
- v1.1.0 Direct Editor Evidenceをbaselineに、v1.1.1 PRでUnity Editor CIを再実行してから公開

## [1.1.0] - 2026-08-13

### Added

- UnityAgentMCP Control Plane 10 Tool
- WorldCreator 3 Tool
- Profiler 8 Tool
- Addressables Entry管理 4 Tool
- UI 5 Tool
- Animation 5 Tool
- Audio 5 Tool
- Cinematic 5 Tool
- 6 Extended Domain専用Capability Contract
- Direct Unity EditorをPrimary Verification AuthorityとするEditor-first Promotion Policy

### Changed

- Production Editor Surfaceを **77 Tool = 32 Graphics + 10 Agent + 3 WorldCreator + 32 Extended Domain** へ昇格
- Profiler / Addressables / UI / Animation / Audio / Cinematicを`editor_operational`へ昇格
- UnityAgent Runtime Catalogを77 Tool Operational Routingへ更新
- Release前にCapability Contract / 77 Tool Promotion Contract / Stable Release Evidenceを検証するようPublication Workflowを強化
- GitHub Actions CIはSupplemental Evidenceとし、Runner未開始・利用不能だけではReleaseをBlockしない
- Build Domain、Addressables Content Build、MovieCreator runtime、LiveCreator runtimeはv1.1.0 Surfaceから除外

### Verification

Unity `6000.7.0a2` Direct Editor Evidence:

- Compile Error 0
- Exact 77/77 Tool Discovery
- Duplicate Tool 0
- Read-only Domain Smoke PASS
- Stale Revision / Approval / One-time Plan Safety PASS
- Profiler Capture PASS
- UI / Animation / Audio / Cinematic Scoped Mutation E2E PASS
- Addressables Package未導入時の明示`UNSUPPORTED`境界 PASS
- Agent Routing / Delegated Failure Propagation PASS
- Cross-domain Workflow PASS
- Timeout / Cancel / Domain Reload callbacks PASS
- Previous Production 45 Regression PASS

未検証範囲は削除せず保持します: Automated CI、Package Editor Test Runner、Addressables Positive Backend Matrix、External Transport Disconnect/Reconnect、Target Device。

## [1.0.0] - 2026-08-11

### Added

- 32 Unity Editor MCP Toolの正式公開契約
- Inspect → Plan → Approval付きMutation / Save / Bake → Capture → Evaluate / Refineの安全なWorkflow
- Unity API Compatibility Registryを`BASE + UNITY_6000_4 + UNITY_6000_5 + UNITY_6000_7`の4保守Bucketとして導入
- MyUnityMCP変更時にCompatibility Registry / Skill / Testsを同時再評価する`myunitymcp-unity-api-compatibility` Skill
- `graphics.inspect_project`へのUnity Version、Compatibility Bucket、関連Package Versionの検出情報
- Dependency Bake、APV Bake Job、Capture Evidence、Human Visual Review、Execution History、Timeout、Cancellation

### Changed

- Unity 6.4 / 6.5 / 6.7のAPI移行へ対応
- Editor内部実装を責務別に整理
- Tool wrapperをDomain単位へ整理し、外部MCP Tool名`graphics.*`を維持

### Known limitations

- Player／Target Device上のTool実行は非対応・未検証
- Built-in PipelineではAPV Bake非対応
- URP／HDRPの実APV Bakeは導入ProjectごとのBaking SetとBackend検証が必要

### Release history note

`v1.0.1`および`v1.0.2-test.*`は1.0系列のRepository / Compatibility検証履歴として保持します。公開済みTagはimmutableです。

## [0.8.0] - 2026-08-05

- 長時間AI制作向けIntegration Hardening、Fault Injection、Execution Runtimeを追加。
