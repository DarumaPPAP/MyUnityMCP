# Changelog

このProjectは[Semantic Versioning](https://semver.org/)に従います。

## [1.0.1] - 2026-08-11

### Changed

- Repository構造を整理し、実行可能な製品資産とDesign-only資産を物理的に分離
- UnityAgentMCP、LiveCreator、MovieCreatorなど未実装設計を`Design/`へ集約
- `Catalog/mcp-catalog.yaml`を実行可能な`unity_graphics_mcp`中心のCatalogへ整理
- Release ContractのTool Discoveryを再帰検索へ変更し、Editor実装の責務別サブフォルダ化に対応
- Release GateのDistribution Previewを`VERSION`から動的生成する構成へ変更
- Package、Manifest、Catalog、Support Matrix、Installation Guideを`1.0.1`へ同期

### Compatibility

- MCP Tool数、Tool Contract、Safety Boundary、対応Unity Versionに変更なし
- Player／Target Device上のTool実行は引き続き非対応
- `v1.0.0` Tagはimmutableのまま維持し、`v1.0.1`を新しいReleaseとして発行

## [1.0.0] - 2026-08-06

### Added

- 32 Unity Editor MCP Toolの正式公開契約
- InspectからRefineまでの承認制Workflow
- Dependency限定Bake、APV Bake Job、Capture Evidence、Human Visual Review
- Timeout、Cancellation、Progress、Structured Log、Execution History、Tool Call Trace
- Domain Reload／Compile／Play Mode／Scene変更／Client切断／Unity再起動のRecovery Contract
- Getting Started Package Sampleと独立Sample Project
- MCP Client、Acceptance Profile、CI Template
- Release Gate、配布Artifact生成、Version整合検証

### Changed

- 開発段階名を現行Tool説明、Error Code、Undo名、Catalogから除去
- README、Manifest、Catalog、Support Matrixを実装実績へ同期
- Package Versionを`1.0.0`へ更新

### Removed

- 実装状況と矛盾した旧Implementation Plan／Task Tracker／Editor Design文書
- Unity Package外Markdownへ誤って付与されていた`.meta`

### Known limitations

- Player／Target Device上のTool実行は非対応
- Built-in PipelineではAPV Bake非対応
- URP／HDRPの実APV Bakeは導入ProjectごとのBaking SetとBackend検証が必要
- BatchMode NoGraphicsでは実画像Captureを検証しない
- MCP Client切断検知はClient AdapterからのCallbackが必要

## [0.8.0] - 2026-08-05

- 長時間AI制作向けIntegration Hardening、Fault Injection、Execution Runtimeを追加。
