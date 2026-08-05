# UnityGraphicsMCP 仕様書

- FeatureName: `UnityGraphicsMCP`
- DocumentVersion: `4.1.0`
- DesignStatus: `Dependency Bake Stable`
- ImplementationStatus: `Phase 1-4B Implemented`
- VerificationStatus: `Unity Editor CI 62 / 62 PASS`
- PrimaryNamespace: `UnityGraphicsMcp`

## 1. 目的

対象Unity ProjectのGraphics状態を解析し、構造化Visual IntentからDirection Planを作成し、明示承認された限定Operation、Save、Bakeだけを安全境界ごとに実行する。

UnityGraphicsMCPはGraphics領域のProject Context、Plan、Mutation、Persistence、Bake、Capture契約を所有する。完成目的のWorkflowはLiveCreator、MovieCreator等が所有する。

## 2. Project environment resolution

Unity Version、Render Pipeline、Rendering Path、RenderGraph、Target PlatformをPackage全体へ固定しない。

Capability選択前に対象Projectから次をRead-onlyで取得する。

- Unity Version
- Render Pipeline Kind / Package Version
- Active Renderer / Rendering Path / RenderGraph Mode
- Active / Installed Build Targets
- Graphics API / Scripting Backend
- 関連PackageとCapability

優先順位:

1. Detected Project Facts
2. Explicit Requested Target and Constraints
3. Project-specific Profile
4. UnityAgent Preference

`UNVERIFIED`を`UNSUPPORTED`として扱わず、未実装BackendへSilent Fallbackしない。

## 3. Tool catalog

### Inspection

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`

### Direction planning

- `graphics.compile_direction`
- `graphics.preview_plan`

### Approval-gated mutation

- `graphics.prepare_light_plan`
- `graphics.apply_plan`
- `graphics.undo_last_transaction`
- `graphics.prepare_environment_plan`
- `graphics.apply_environment_plan`
- `graphics.undo_last_environment_transaction`

### Save / Capture / Refine

- `graphics.prepare_save_plan`
- `graphics.apply_save_plan`
- `graphics.capture_evaluation`
- `graphics.refine_direction`

### Dependency Bake

- `graphics.prepare_bake_plan`
- `graphics.bake_dependencies`

全17 Toolは`AutoRegister = false`とし、明示Activation時だけ公開する。

## 4. Read-only contract

Inspection、Planning、Prepareは実行前後で次を比較する。

- Loaded Scene Dirty State
- Persistent Asset Dirty State
- Undo Group

Material確認に`renderer.material`を使用せず、`sharedMaterials`を使用する。Read-only ToolからAsset生成、Scene保存、Bakeを実行しない。

違反時は`READ_ONLY_CONTRACT_VIOLATION`を返す。

Captureは一時Editor状態を利用できるが、Camera TargetTexture、Active RenderTexture、Scene / Asset Dirty、Undo状態を必ず復元する。

## 5. Session and revision

Snapshot、Direction Plan、Executable Plan、Capture、Dirty Dependency SetはEditor Session内だけで有効とする。

失効条件:

- Domain Reload
- Compile開始
- Play Mode遷移
- Editor終了
- Revision変更
- Plan / Snapshot TTL超過

大きなScene ResultはSnapshot IDとCursorで参照し、毎回全JSONを複製しない。

## 6. Planning contract

Unity C# Toolは自然言語や画像を独自解釈しない。UnityAgentまたはMCP Clientが構造化したVisual IntentとHuman Reviewを入力する。

PrepareはUnity状態を変更せず、次を返す。

- Exact Before / Requested AfterまたはExact Dependency
- Diff Digest
- Approval Token
- Expected Revision
- 副作用未実行の明示

## 7. Mutation contract

Mutation Apply必須条件:

- Direction Planが現在Sessionに存在する
- Executable Planが未使用
- Expected Revision一致
- Approval Token一致
- Preview Baseline一致
- `saveMode = NONE`

対応Operation:

- `LIGHT_CREATE` / `LIGHT_UPDATE`
- `CAMERA_CREATE` / `CAMERA_UPDATE`
- `REFLECTION_PROBE_CREATE` / `REFLECTION_PROBE_UPDATE`
- `VOLUME_CREATE` / `VOLUME_UPDATE`

Applyは一つのUnity Undo Groupへ集約する。途中例外時は`Undo.RevertAllDownToGroup`で全体Rollbackする。

## 8. Undo contract

Undo前に次を確認する。

- Transaction ID
- Expected Revision
- Transaction適用後State Digest
- TransactionがUndo Stackの最新Groupであること

外部変更、新しいUndo Group、Session失効がある場合は拒否する。

## 9. Save contract

SaveはMutation Applyから分離する。

- 一つの既存Loaded Sceneだけ
- Dirty Sceneだけ
- Scene Handle / Path / Content Digest / Dirty StateをPrepare時に固定
- Save専用Approval Token
- `saveMode = EXPLICIT_SCENE`
- Save As、自動保存、全Scene保存、Asset一括保存なし
- 永続化後のUnity Undo / 自動Rollback保証なし

## 10. Dirty Dependency Set

保存済みLoaded SceneがDirtyになった時点で、再Bakeが必要になった可能性をSession-local Setへ保守的に記録する。

- `LIGHTMAP_SCENE`
- `REFLECTION_PROBE`
- `ADAPTIVE_PROBE_VOLUME`

Scene Save後もSetを保持し、`scene.isDirty = false`だけでBake不要とは判定しない。

Scene Close、Play Mode、Compile、Domain Reloadで失効する。成功したDependencyだけを除去する。

## 11. Bake contract

BakeはSave、Mutationと別のApproval境界を持つ。

Prepareで固定する情報:

- Expected Revision
- Dirty Dependency Set Serial
- 全Loaded Contributing SceneのHandle / Path / Dirty / Content Digest
- Dependency Kind / Object ID / Output Asset Path
- Baseline Digest / Native Backend
- Exact Diff Digest
- 10分TTLのBake Approval Token

Apply必須条件:

- `bakeMode = EXPLICIT_DEPENDENCIES`
- Plan未使用 / TTL内
- Revision / Token一致
- Dirty Set / Loaded Scene / Baseline一致
- 全Dependency Backend Preflight成功

対応:

- Scene限定Lightmap Bake
- 既存Cubemap Assetを明示したBaked Reflection Probe Bake

制限:

- 自動Saveなし
- 複数Loaded Sceneで全Scene BakeへSilent Fallbackしない
- 新規Cubemap Asset Pathを推測しない
- Unity Undo / 自動Rollback保証なし
- 途中失敗時、完了済みBakeを巻き戻さない
- APVは検出のみ。Baking Set / Lighting Scenario Backend未実装時は`BACKEND_NOT_IMPLEMENTED`

## 12. Capture and Refine contract

Capture:

- 指定CameraのColor PNG
- `Library/MyUnityMCP/Captures`配下
- Unity C#側で画像意味解析を行わない
- Graphics Device Nullは`UNVERIFIED`

Refine:

- Direction PlanとCapture ID必須
- Human Reviewの観察または調整要求必須
- 明示入力だけを次Iterationへ追加
- Mutation / Save / Bakeを実行しない
- Human ReviewなしにVisual Acceptedと判定しない

## 13. Pipeline and platform policy

Pipeline共通APIで扱えるCapabilityを先に使用する。Pipeline固有設定が必要な場合は、対象ProjectでPackage、Version、APIを検出し、実装済みBackendがなければ`BACKEND_NOT_IMPLEMENTED`を返す。

PipelineとPlatformを別軸で扱う。Editor成功だけでPlayerまたはTarget Deviceを保証しない。

## 14. Explicit exclusions

- Delete Operation
- Area Light
- Camera Stack / Target Texture Mutation
- URP / HDRP Additional Camera Data Mutation
- Volume Profile内部Overrideの作成・変更
- Material / Renderer Feature Mutation
- 任意`SerializedProperty` Mutation
- APV Baking Set / Lighting Scenario Bake
- Reflection Probe新規Cubemap Asset生成
- Depth / Object ID Capture
- Human ReviewなしのVisual Acceptance

## 15. Naming and architecture

- Namespaceは`UnityGraphicsMcp`
- enum型は`E_UPPER_SNAKE_CASE`
- private fieldは`_camelCase`
- 一つの実装しかない段階でInterfaceを作らない
- Controller、Manager、Service、AdapterをPattern目的で作らない
- Runtime AssemblyやCapabilityごとのasmdefを増やさない
- Feature-local DTOとHelperは最も近いPrimary Typeと同一ファイルに置く

## 16. Compatibility evidence

実測Evidenceは`Tests/Compatibility/verification-matrix.yaml`へ記録する。

現在のEditor Gate:

- Unity `6000.0.75f1`
- Package Resolve
- Editor Compile
- 17 Tool Bridge Discovery
- Direct Handler Invocation
- EditMode `62 / 62 PASS`

EntryがないPlayer / Target Device環境は`UNVERIFIED`とする。
