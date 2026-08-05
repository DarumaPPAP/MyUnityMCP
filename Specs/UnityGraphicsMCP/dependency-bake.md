# UnityGraphicsMCP Dependency Bake Dirty Dependency Bake

- DocumentVersion: `1.0.0`
- Capability: `Dependency Bake`
- Scope: `Dirty Dependency Set / Bake Plan / Dependency-limited Bake`
- PrimaryNamespace: `UnityGraphicsMcp`

## 1. 目的

Graphics Mutationと明示Saveの結果から、再生成が必要なGraphics依存をEditor Session内で追跡し、明示承認されたDependencyだけをBakeする。

Save、Bake、Captureは独立した副作用境界として扱う。Mutation ApplyへBakeを混在させない。

## 2. Tool

- `graphics.prepare_bake_plan`
- `graphics.bake_dependencies`

両Toolは`AutoRegister = false`であり、明示Activation時だけ公開する。

## 3. Dirty Dependency Set

保存済みLoaded SceneがDirtyになった時点で、Session-localなDirty Dependency Setへ登録する。

追跡対象:

- `LIGHTMAP_SCENE`
- `REFLECTION_PROBE`
- `ADAPTIVE_PROBE_VOLUME`

重要契約:

- Scene Save後もDirty Dependencyを保持する。
- `scene.isDirty = false`だけを理由にBake不要とは判定しない。
- Scene Closeで対象SceneのDependencyを失効する。
- Play Mode遷移、Compile、Domain Reload、Editor終了で全Setを失効する。
- Bake完了したDependencyだけをSetから除去する。

Dirty Dependency Setは「再Bakeが必要になった可能性」を示す保守的集合であり、Unity C#側で変更内容の意味を推測しない。

## 4. Prepare contract

`graphics.prepare_bake_plan`はRead-onlyで次を固定する。

- Expected Revision
- Dirty Dependency Set Serial
- 全Loaded Contributing SceneのHandle / Path / Dirty / Content Digest
- 明示されたDependency Kind
- Reflection Probe GlobalObjectId
- Reflection Probeの既存出力Cubemap Asset Path
- DependencyごとのBaseline Digest
- Native Backend
- Exact Diff Digest
- 10分TTLの一時Approval Token

Prepare中にSave、Bake、Asset生成、Undo操作を行わない。

## 5. Apply contract

`graphics.bake_dependencies`は次を全て満たした場合だけ実行する。

- `bakeMode = EXPLICIT_DEPENDENCIES`
- Bake Planが現在Session内に存在する
- Planが未使用かつ有効期限内
- Expected Revision一致
- Approval Token一致
- Dirty Dependency Set Serial一致
- Loaded Contributing Scene Set一致
- Scene / Probe Baseline一致
- 全DependencyのNative Backend Preflight成功

Planは一回だけ使用できる。

## 6. 対応Backend

### Scene Lightmap

対象Unity VersionでScene限定Bake APIを解決できる場合のみ使用する。

Scene限定APIが解決できない場合、Loaded Sceneが一つだけなら`Lightmapping.Bake()`を使用できる。複数Loaded Sceneで全Scene BakeへSilent Fallbackしない。

### Baked Reflection Probe

`Lightmapping.BakeReflectionProbe`を使用する。

安全境界:

- Baked Modeのみ
- GlobalObjectIdで明示指定
- 対象Scene一致
- 既存Cubemap Assetへの上書きのみ
- 新規出力Asset Pathの推測・生成なし

### Adaptive Probe Volume

APVはDirty Dependencyとして検出できるが、Dependency Bakeでは実行しない。

理由:

- Baking Set
- Lighting Scenario
- Pipeline / Package Version
- Scene Membership
- Disk Streaming等のProject設定

これらの契約を固定せずにBakeすると、意図しないScene SetやScenarioへ副作用が及ぶため、`BACKEND_NOT_IMPLEMENTED`を返す。

## 7. Failure and rollback

Bakeは永続的かつ高コストな副作用である。

- Unity Undo対象外
- 自動Rollback保証なし
- 自動Saveなし
- 途中失敗時、完了済みDependencyは巻き戻さない
- 完了済みDependency IDと失敗DependencyをResultへ返す
- 実行前Preflightで未実装Backend混在による部分実行を可能な限り防ぐ

## 8. Test contract

最低限、次をEditMode Testで検証する。

- Tool Discovery / Default Disable
- Prepare Read-only
- Save後のDirty Dependency保持
- Approval不足拒否
- Revision / Baseline / Dirty Set変更拒否
- 明示Scene Backendだけを一回実行
- 成功DependencyのSet除去
- APV Backend拒否
- Dirty Dependency Set外Scene拒否
- Save非実行
- Undo / Automatic Rollback非保証のResult明示

## 9. Deferred

- APV Baking Set Backend
- Lighting Scenario選択
- Reflection Probe新規Cubemap Asset生成Plan
- Async Bake Job / Cancel / Progress
- Bake Artifact Digestと再現性比較
- Player / Target Device検証
