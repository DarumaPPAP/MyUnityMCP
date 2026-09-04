# Changelog

このProjectは[Semantic Versioning](https://semver.org/)に従います。

## [1.1.1] - 2026-09-05

### Fixed

- UnityAgent delegated resultの正規化を強化し、失敗結果を成功として扱うfalse-success経路を閉じた
- Execution Historyの旧形式移行とResult migrationの欠落を修正
- `AgentDelegateRegistry`のReflection `Assembly`曖昧性を解消

### Changed

- UnityAgent Runtime Catalogをv5 Tool Object形式へ移行し、Catalog / Approval / Graph Compile / Execution / History / Trace / Result Normalizationを責務別Serviceへ分離
- Agent validator責務を整理し、Runtime Catalog / Safety Contract専用Release Validatorを追加
- Compatibility / Production Source-of-Truthをcanonical registryへ統一
- Historical Evidence、旧Sample Surface、obsolete Graphics Support MatrixをProduction `main`から除去
- Repository Hygiene Gateを追加し、古いPhase資産・一時ファイル・historical evidenceの再混入をRelease前に検出
- Release Publication WorkflowのStatic GateをRelease Gateと同期

### Verification

- Production Surfaceは引き続き **77 Tool**
- v1.1.0のUnity `6000.7.0a2` Direct Editor Evidenceをbaselineとして保持
- v1.1.1 Release PRでUnity `6000.0.75f1` EditMode / Compile / Production Tool Discovery / Agent Contractを再検証してから公開
- Target Device、Addressables Positive Backend Matrix、External Transport Disconnect/Reconnectは引き続き未検証範囲

### Release

- `VERSION` / Package / Manifest / Catalog / Support Matrix / Changelogを`1.1.1`へ整合
- `v1.1.1` TagはRelease Workflowからimmutableに作成

## [1.1.0] - 2026-08-13

### Added

- UnityAgentMCP Control Planeと10個の`agent.*` Tool
- WorldCreatorと3個の`world.*` Tool
- Profiler 8 Tool
- Addressables Entry管理 4 Tool
- UI 5 Tool
- Animation 5 Tool
- Audio 5 Tool
- Cinematic 5 Tool
- Extended DomainごとのOperational Capability Contract
- Direct Unity EditorをPrimary Verification AuthorityとするEditor-first Policy

### Changed

- Production Tool Surfaceを **77 Tool** へ昇格
- Profiler / Addressables / UI / Animation / Audio / Cinematicを`editor_operational`へ昇格
- UnityAgent Runtime Catalogを全Operational DomainへRouting可能な状態へ更新
- Release ContractのTool CountをManifest基準へ統一
- Stable Release Publication前にCapability Contract、77 Tool Promotion Contract、Release Evidenceを検証するようRelease Workflowを強化
- GitHub Actions CIはSupplemental Evidenceとし、利用不能だけではPromotion / ReleaseをBlockしない
- Build Domain、Addressables Content Build、MovieCreator runtime、LiveCreator runtimeをv1.1.0 Surfaceから除外

### Verification

Unity `6000.7.0a2` Direct Editor Evidence:

- Compile Error 0
- Exact 77/77 Tool Discovery、Duplicate 0
- Read-only Domain Smoke PASS
- Stale Revision / Approval / One-time Plan Safety PASS
- Profiler Capture PASS
- UI / Animation / Audio / Cinematic Scoped Mutation E2E PASS
- Addressables Package未導入境界は明示`UNSUPPORTED`としてPASS
- Agent Routing / Delegated Failure Propagation PASS
- Cross-domain Workflow PASS
- Timeout / Cancel / Domain Reload callbacks PASS
- Previous Production 45 Regression PASS

Automated CI、Package Editor Test Runner、Addressables Positive Backend Matrix、External Transport Disconnect/Reconnect、Player / Target Deviceは未検証範囲として明示的に保持します。

### Release

- `VERSION` / Package / Manifest / Support Matrix / Changelogを`1.1.0`へ整合
- v1.1.0 Publicationは明示Human Gate後に実行
- 公開Tagはimmutable

## [1.0.0] - 2026-08-11

### Added

- 32 Unity Editor MCP Toolの正式公開契約
- Inspect → Plan → Approval付きMutation / Save / Bake → Capture → Evaluate / Refineの安全なWorkflow
- Unity API Compatibility Registryを`BASE + UNITY_6000_4 + UNITY_6000_5 + UNITY_6000_7`の4保守Bucketとして導入
- Getting Started Package Sample、Standalone Sample Project、MCP Client / Acceptance Profile / CI Template

### Verification

- Unity `6000.0.75f1`: Compile / 32 Tool Discovery / 125以上のEditMode Contract PASS
- Unity `6000.4.12f1`: Compatibility EditMode / Compile Verify PASS
- Unity `6000.5.5f1`: Compatibility EditMode / Compile Verify PASS
- Unity `6000.7.0a2`: Manual Package Import / Compile / 32 Tool Discovery / Compatibility確認 PASS

### Known limitations

- Player／Target Device上のTool実行は非対応・未検証
- Built-in PipelineではAPV Bake非対応
- URP／HDRPの実APV Bakeは導入ProjectごとのBaking SetとBackend検証が必要

### Release history note

`v1.0.1`および`v1.0.2-test.*`は1.0系列のRepository / Compatibility検証履歴として保持します。公開済みTagはimmutableです。

## [0.8.0] - 2026-08-05

- 長時間AI制作向けIntegration Hardening、Fault Injection、Execution Runtimeを追加。
