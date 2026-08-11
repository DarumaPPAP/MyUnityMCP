# Changelog

このProjectは[Semantic Versioning](https://semver.org/)に従います。

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

`v1.0.1`および`v1.0.2-test.*`は正式1.0.0を固める過程の検証Buildです。本ReleaseをCanonical v1.0.0とします。

## [0.8.0] - 2026-08-05

- 長時間AI制作向けIntegration Hardening、Fault Injection、Execution Runtimeを追加。
