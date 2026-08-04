# UnityGraphicsMCP 仕様書

- FeatureName: `UnityGraphicsMCP`
- DocumentVersion: `4.0.0`
- DesignStatus: `Phase 3 Stable`
- ImplementationStatus: `Phase 1-3 Implemented`
- VerificationStatus: `Unity Editor CI 46 / 46 PASS`
- PrimaryNamespace: `UnityGraphicsMcp`

## 1. 目的

対象Unity ProjectのGraphics状態を解析し、構造化Visual IntentからDirection Planを作成し、明示承認された限定Operationだけを安全なUnity Undo Transactionとして適用する。

UnityGraphicsMCPはGraphics領域の判断、Project Context、Plan、Mutation Contractを所有する。完成目的のWorkflowはLiveCreator、MovieCreator等が所有する。

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

## 3. Tool contract

### Inspection

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`

InspectionはScene Dirty、Persistent Asset Dirty、Undo Group、Material Instanceを変更しない。

### Planning

- `graphics.compile_direction`
- `graphics.preview_plan`

Unity C#側で自然言語や画像の意味理解を偽装しない。UnityAgentまたはMCP ClientがVisual Intentを構造化する。

Direction Planは次を持つ。

- Session-local Plan ID
- Expected Revision
- Detected Project Context
- Visual Intent
- Recommendation / Range / Reason / Dependency
- Confidence / Pipeline Impact / Platform Impact / Verification Level

### Light Mutation

- `graphics.prepare_light_plan`
- `graphics.apply_plan`
- `graphics.undo_last_transaction`

対応Operation:

- `LIGHT_CREATE`
- `LIGHT_UPDATE`

対応Light Type:

- Directional
- Point
- Spot

### Environment Mutation

- `graphics.prepare_environment_plan`
- `graphics.apply_environment_plan`
- `graphics.undo_last_environment_transaction`

対応Operation:

- `CAMERA_CREATE`
- `CAMERA_UPDATE`
- `REFLECTION_PROBE_CREATE`
- `REFLECTION_PROBE_UPDATE`
- `VOLUME_CREATE`
- `VOLUME_UPDATE`

全Toolは`AutoRegister = false`とし、明示Activation時だけ公開する。

## 4. Mutation approval contract

PrepareはRead-onlyで次を生成する。

- Exact Before / Requested After
- Diff Digest
- Executable Plan ID
- 10分TTLの一時Approval Token
- Expected Revision
- Created / Modified / Dirty候補
- Save / Bake非実行の明示

Applyは次をすべて満たす場合だけ実行する。

- Direction Planが現在Sessionに存在する
- Executable Planが未使用かつ有効期限内
- Expected Revisionが一致する
- Approval Tokenが一致する
- Preview Baselineが適用直前状態と一致する
- Operation IDがPlan内で一意
- 同一既存ComponentへのUpdateが一回だけ
- 必要Unity APIをPrepare時に読み書き可能と確認済み
- `saveMode = NONE`

自然言語から数値を推測せず、明示値だけを適用する。

## 5. Transaction and Undo contract

- 一つのPlanを一つのUnity Undo Groupへ集約する
- Camera、Reflection Probe、Volumeは同一Environment Transactionへ混在可能
- 途中例外時は`Undo.RevertAllDownToGroup`で全体Rollbackする
- Planは一回だけ使用可能
- Mutationは対象SceneをDirtyにするが保存しない
- Bakeを実行しない

Undo前に次を再確認する。

- Transaction ID
- Expected Revision
- 対象Componentの適用後State Digest
- TransactionがUndo Stackの最新Groupであること

外部変更や新しいUndo操作が存在する場合はUndoを拒否する。

## 6. Phase 3 capability scope

### Light

- Type
- Name
- Color / Intensity
- Range / Spot Angle
- Shadow
- Transform
- Enabled

### Camera

- Projection
- Field of View / Orthographic Size
- Near / Far Clip
- Culling Mask
- Clear Flags / Background Color
- Depth
- HDR / MSAA
- Transform
- Enabled

### Reflection Probe

- Mode / Refresh Mode / Time Slicing
- Importance / Intensity
- Box Projection
- Size / Center / Blend Distance
- Resolution / Culling Mask
- Transform
- Enabled

### Volume

- Is Global
- Priority
- Blend Distance
- Weight
- Enabled
- 既存`sharedProfile`参照の割当

Render Pipelines CoreのVersion差でVolume Memberが公開Propertyまたは公開Fieldになる差を吸収する。指定MemberをPrepare時に読み書き可能か検証する。

## 7. Explicit exclusions

Phase 3では次を実装しない。

- Delete Operation
- Area Light
- Camera Stack / Target Texture
- URP / HDRP Additional Camera Data
- Reflection Probe Bake
- Volume Profile内部Overrideの作成・変更
- Material / Renderer Feature Mutation
- Scene / Asset Save
- Bake / Capture / Visual Refine
- 任意`SerializedProperty` Mutation

## 8. Pipeline and platform policy

Pipeline共通APIで扱えるCapabilityを先に使用する。Pipeline固有設定が必要な場合は、対象ProjectでPackage、Version、APIを検出し、実装済みBackendがなければ`BACKEND_NOT_IMPLEMENTED`を返す。

PipelineとPlatformを別軸で扱う。Editor成功だけでPlayerまたはTarget Deviceを保証しない。

## 9. Compatibility evidence

実測Evidenceは`Tests/Compatibility/verification-matrix.yaml`へ記録する。

MatrixはPackage全体の対応保証ではなく、次を分離した検証実績である。

- Package Resolve
- Editor Compile
- Bridge Discovery
- Direct Handler Invocation
- EditMode
- Player
- Target Device

Entryがない環境は`UNVERIFIED`とする。

## 10. Naming and architecture

- Namespaceは`UnityGraphicsMcp`
- enum型は`E_UPPER_SNAKE_CASE`
- private fieldは`_camelCase`
- 一つの実装しかない段階でInterfaceを作らない
- Controller、Manager、Service、AdapterをPattern目的で作らない
- Runtime AssemblyやCapabilityごとのasmdefを増やさない
- Feature-local DTOとHelperは最も近いPrimary Typeと同一ファイルに置く

## 11. Phase 3 acceptance criteria

- InspectionとPrepareがUnity状態をDirtyにしない
- Project事実とRequested Targetを分離する
- 11 ToolをBridgeからDiscoveryできる
- 全ToolがDefault Disableである
- Light Create / Update / Undoが成立する
- Camera Create / Update / Undoが成立する
- Reflection Probe Create / Update / Undoが成立する
- Volume Create / Update / sharedProfile / Undoが成立する
- Approval / Revision / Baseline Guardが成立する
- Duplicate Operation / Update Targetを拒否する
- Property / Field API差を解決する
- 複合TransactionがAtomicである
- 外部変更後のUndoを拒否する
- 新しいUndo Group追加後のUndoを拒否する
- Automatic SaveとBakeを実行しない

## 12. Phase 4 boundary

Phase 4では次をMutationとは別の明示承認境界として追加する。

- Save Plan
- Dirty Dependency Set
- Dependency限定Bake
- Capture時の一時Editor State復元
- Visual Evaluation
- Human Reviewを含むRefine Loop

Human ReviewなしにVisual Acceptedと判定しない。
