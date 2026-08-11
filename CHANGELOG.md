# Changelog

このProjectは[Semantic Versioning](https://semver.org/)に従います。

## [1.0.2-test.2] - 2026-08-11

### Fixed

- Unity 6.7でError化した`SceneHandle`と`int` / `uint`の暗黙変換依存を除去し、Scene比較をSession Tokenへ移行
- `Object.GetInstanceID()` / `EditorUtility.InstanceIDToObject(int)`依存をMyUnityMCP Session Tokenへ分離
- `UnityApiCompatibility`系の新規Package Assetに`.meta`が無く、immutable Packageで無視される問題を修正
- Scene Handleを数値文字列化する処理をSession Token経由へ変更し、Unity 6.7での型不一致を修正

### Changed

- `SceneHandle`の実測Lifecycleを`UNITY_6000_4` Bucketへ移動し、Unity `6000.4.12f1`でWarning、`6000.5.5f1`でErrorとして記録
- Object / Sceneの一時識別をUnity内部ID表現から切り離す`UnityGraphicsMcpIdentityCompatibility`を追加
- Compatibility SkillへScene identityとimmutable Package `.meta`ルール、実Editor Evidenceを反映

### Verification

- Base Unity `6000.0.75f1`: Compile / 32 Tool Discovery / EditMode Contracts PASS
- Unity `6000.4.12f1`: Compatibility EditMode / Compile Verify PASS
- Unity `6000.5.5f1`: Compatibility EditMode / Compile Verify PASS
- Fresh Project / Sample Workflow / Release Contract PASS
- Unity 6.7はGameCI image未提供のためManual Test継続
- このVersionは検証用Pre-releaseであり正式Releaseではない

## [1.0.2-test.1] - 2026-08-11

### Added

- Unity API Compatibility Registryを`BASE + UNITY_6000_4 + UNITY_6000_5 + UNITY_6000_7`の4保守Bucketとして追加
- `skills/myunitymcp-unity-api-compatibility/SKILL.md`を追加し、MyUnityMCP変更時のCompatibility再評価をAGENTS.mdから必須化
- `graphics.inspect_project`へ`apiCompatibility`とPackage Version由来の互換判断情報を追加
- Unity API Compatibility Contract / Editor Matrix CIを追加

### Changed

- Unity 6.6由来の変更を6.7 Roll-up Bucketで保守しつつ、Ruleごとの実適用開始Versionを保持
- Pre-release Versionでは正式Release Evidence Gateと分離してGitHub Pre-releaseを発行できるRelease経路を追加

### Verification

- Base Unity `6000.0.75f1`の既存Editor CIは通過済み
- Unity 6.7はGameCI image未提供のため自動Editor検証未完了
- Unity 6.7での直接導入・コンパイル・Tool Discovery・主要Workflow確認をManual Test対象とする
- このVersionは検証用Pre-releaseであり正式Releaseではない

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

- 長時間AI制作向けIntegration Hardening、Fault Injection、Execution Runtimeを追加.