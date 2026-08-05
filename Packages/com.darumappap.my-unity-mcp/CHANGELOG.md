# Changelog

このProjectは[Semantic Versioning](https://semver.org/)に従います。

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
