# Bake Constraints

## Dependency Bake

- `graphics.prepare_bake_plan`でDirty Dependency Set、Scene Baseline、Backendを固定します。
- `graphics.bake_dependencies`は`EXPLICIT_DEPENDENCIES`だけを受理します。
- 全DependencyをPreflightしてから開始します。
- 複数Loaded Sceneで全Scene BakeへFallbackしません。
- Reflection Probe Bakeは既存Cubemap Assetを必要とします。
- 自動Save、Unity Undo、自動Rollbackはありません。

## APV Bake

- Built-in PipelineはPlan準備時に拒否します。
- URP／HDRPでもBaking Set、Lighting Scenario、Scene集合、APV Backend、Output Rootが必要です。
- 明示Scene集合はBaking SetのScene集合と完全一致する必要があります。
- Job終了後にOutput Asset SHA-256差分がなければ失敗です。
- Cancellation時に生成済みOutputがあれば`PARTIAL`として保持します。

## Operational guidance

Bake前に対象Sceneを保存し、Version Controlの作業Treeを確認してください。Bake中はScene構成、Compile、Play Mode移行を行わないでください。
