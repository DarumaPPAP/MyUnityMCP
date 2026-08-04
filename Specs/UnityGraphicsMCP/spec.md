# UnityGraphicsMCP 仕様書

- FeatureName: `UnityGraphicsMCP`
- DocumentVersion: `2.1.0`
- DesignStatus: `Draft`
- ImplementationStatus: `Not Started`
- VerificationStatus: `Not Run`
- PrimaryNamespace: `UnityGraphicsMcp`

## 1. 目的

参考画像、自然言語、既存Scene、Render Pipeline、Target PlatformからVisual Intentを作成し、Unityの画作りを解析、提案、適用、Bake、撮影、再調整できるDomain MCPを構築する。

UnityGraphicsMCPはGraphics領域の専門判断とTool Groupを所有する。完成目的のWorkflowはLiveCreator、MovieCreator等が所有する。

## 2. Project environment resolution

UnityGraphicsMCP全体へ特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph設定、Target Platformを固定しない。

BackendまたはCapabilityを選択する前に、対象Unity Projectから最低限次をRead-onlyで取得する。

- Unity Version
- Render Pipeline Kind
- Render Pipeline Package Version
- Active Renderer
- Rendering Path
- RenderGraph Mode
- Active Build Target
- Installed Build Targets
- Graphics API
- Scripting Backend
- Related Package Presence
- Graphics Capability Summary

情報の優先順位は次とする。

1. 対象Unity Projectから検出した事実
2. 今回の依頼で明示されたTargetと制約
3. Project固有Profile
4. UnityAgentの既定Preference

検出済みProject事実をProfileや既定Preferenceで上書きしない。

対応状態は最低でも次を区別する。

- `AVAILABLE`
- `UNAVAILABLE`
- `UNSUPPORTED`
- `UNVERIFIED`
- `PACKAGE_NOT_INSTALLED`
- `VERSION_NOT_SUPPORTED`
- `PROJECT_CONFIGURATION_REQUIRED`
- `BACKEND_NOT_IMPLEMENTED`

検証実績がない環境を、根拠なく`UNSUPPORTED`と判定しない。

## 3. 対象領域

### Scene Composition

- Foreground / Midground / Background
- Hero / Support / Landmark / Depth Cue
- Set Dressing
- Layer / Rendering Layer / Static Flag / LOD

### Surface

- Material
- Shader Property
- Base Color / Metallic / Smoothness / Specular
- Normal / Detail / Emission
- Render Queue / Surface Type / Blend / Cull / ZWrite
- Decal

### Direct Lighting

- Directional / Point / Spot / Baked Area Light
- Key / Fill / Rim / Practical / Motivated / Accent
- Color / Temperature / Intensity / Range
- Cookie
- Shadow / Bias / Normal Bias
- Culling Mask / Rendering Layer Mask
- Lightmap Bake Type

### Indirect Lighting

- Lighting Settings
- Lightmap
- LightingDataAsset
- Scale In Lightmap
- UV2 / Texel Density / Atlas
- Light Probe
- Light Probe Proxy Volume
- Adaptive Probe Volumes
- Baking Set / Scenario / Streaming / Invalid Probe / Leak

### Reflection

- Environment Reflection
- Reflection Probe
- Box Projection / Influence / Blend / Importance
- Baked / Custom / Realtime
- Anchor Override
- Pipeline固有Reflection機能は対応Backend実装時に追加する

### Atmosphere and VFX

- Skybox / Ambient / Fog / Wind / Time of Day
- Particle System
- VFX Graph Capability判定
- Decal

### Look and Rendering

- Volume / Volume Profile / Effective Volume Stack
- Pipeline Native Post Process
- Custom Volume Component
- Renderer Dataまたは同等のPipeline設定
- RendererFeature / Custom Pass / CommandBuffer等の存在、順序、Injection Point、Capability
- Render Scale
- Motion Vector / Depth / Opaque TextureのCapability

### Initial cinematic bridge

UnityCinematicMCPが未実装の間だけ、次のRead-only InspectionとPlan入力を許可する。

- PlayableDirector
- Timeline Asset / Track / Clip / Binding
- Cinemachine Brain / Camera / Blend
- Camera State

TimelineやCinemachineのMutation OwnershipはUnityCinematicMCP実装後に移管する。

## 4. Operation flow

```text
inspect → plan → mutate → bake → capture → refine
```

- `inspect`は状態を変更しない。
- `plan`はVisual Intent、Direction Plan、Pipeline Native Plan、Platform Budgetを生成する。
- `mutate`は承認済みPlanだけを適用する。
- `bake`はDirty Dependencyだけを処理する。
- `capture`はColor、Depth、Object ID等のEvidenceを取得する。
- `refine`は修正Planを作るが自動適用しない。

## 5. Visual Intent

「幻想的」「切ない」「華やか」等を直接Unityの単一数値へ変換しない。

最低限次を中間表現として持つ。

- Emotional intent
- Composition hierarchy
- Camera language
- Lighting hierarchy
- Color script
- Material and reflection intent
- Atmospheric depth
- Motion energy
- Performance priority

提案値は次を持つ。

- Recommended value
- Allowed range
- Reason
- Dependencies
- Confidence
- Pipeline impact
- Platform impact
- Verification level

## 6. Pipeline resolution

### Common

- Visual Intent
- Direction Plan
- Expected Visual Result
- Requested Target
- Platform Budget

### Built-in

- Lightmap / Light Probe / Reflection Probe
- Post Processing StackまたはProject固有Image Effect
- Projector Decal
- CommandBuffer / OnRenderImage等

### URP

- URP Volume
- Decal Renderer Feature
- RendererFeature / RenderGraph
- Lightmap / Light Probe / APV
- Reflection Probe

### HDRP

- HDRP Volume
- Visual Environment / Fog
- Decal Projector
- Custom Pass
- APV
- Reflection Hierarchy / Planar Reflection

### Custom SRP

- Pipeline AssetとRenderer実装を検出する
- 専用Backendが存在しない場合は`BACKEND_NOT_IMPLEMENTED`を返す

Pipeline非対応機能は黙って無視せず、Fallback候補、見た目の差、Validation状態を返す。ただし別Pipelineへ自動Fallbackしない。

## 7. Platform resolution

PipelineとPlatformを別軸で解決する。

Platform方針は今回の依頼、Project固有Profile、UnityAgent Preferenceから取得する。特定PlatformをMyUnityMCP全体の既定値にしない。

Projectが現在設定しているBuild Targetと、今回要求されたTargetが異なる場合は次を明示する。

- Detected Build Target
- Requested Target
- Installed / Not Installed
- Project Configuration Required
- Target Device Verification State

未計測の性能改善を断定しない。Editor結果だけでPlayerまたはTarget Deviceを保証しない。

## 8. Backend selection

1. `graphics.inspect_project`でProject事実を取得する。
2. Pipeline、Version、Renderer、Rendering Pathを解決する。
3. 実装済みBackendとCapabilityを照合する。
4. 選択されたBackendだけを読み込む。
5. Backend未実装の場合は`BACKEND_NOT_IMPLEMENTED`を返す。

最初の具象Backendは、開発時に利用可能な検証Projectに基づいて実装してよい。ただし、その環境をMyUnityMCP全体の固定対応条件とはしない。

二つ目の実在Backendが追加されるまで共通Pipeline Interfaceを作らない。

## 9. Tool contract

### inspect

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.inspect_frame`
- `graphics.validate_scene`

### plan

- `graphics.compile_direction`
- `graphics.preview_plan`

### mutate

- `graphics.apply_plan`
- `graphics.undo_transaction`

### bake

- `graphics.bake_dependencies`

### capture

- `graphics.capture_evaluation`
- `graphics.refine_direction`

現在は全Toolが`planned`であり、実装済みとして公開しない。

## 10. Mutation requirements

- `expectedRevision`
- Dry Run
- Machine-readable Diff
- Undo / Revert
- Save Policy
- Created / Modified / Dirty一覧
- Project Settings変更の別承認
- Pipeline Asset / Renderer Data変更の別承認
- Bakeの別承認

## 11. Bake dependency

変更から次の無効化を判定する。

- Lightmap
- Light Probe
- APVまたはPipeline同等機能
- Reflection Probe
- Evaluation Capture

一つの変更を理由に無条件の全Bakeを行わない。

## 12. Validation

- Detected Project Context
- Requested Target
- Pipeline / Package / Renderer Capability
- Light / Shadow
- Lightmap / UV2 / LightingDataAsset
- Light Probe / APV
- Reflection Probe
- Material / Decal / Particle
- Volume / Post Process
- RendererFeature / Custom Pass
- Timeline / Cinemachine Read-only state
- Platform Budget
- Compatibility Matrix
- Visual Evidence要件

## 13. Compatibility evidence

実際に検証した環境は`Tests/Compatibility/verification-matrix.yaml`へ記録する。

Matrixは対応条件ではなくEvidenceであり、次を分離して記録する。

- Editor Compile
- EditMode
- PlayMode
- Player
- Target Device

MatrixにEntryがない環境は`UNVERIFIED`とする。

## 14. Naming and architecture

- Namespaceは`UnityGraphicsMcp`。
- enum型は`E_UPPER_SNAKE_CASE`。
- private fieldは`_camelCase`。
- 一つのBackendしかない段階ではPipeline Interfaceを作らない。
- Controller、Manager、Service、Profile、Adapter、追加asmdefをPattern目的で作らない。
- 新規ファイルにはOwner、Lifetime、Consumers、Responsibility、Split Reasonを記録する。

## 15. Non-goals

- Asset生成AIそのもの
- TextureやModelの生成Model実装
- 全Pipelineの初回同時実装
- 特定Unity Version、Pipeline、Rendering Path、Platformの全体固定
- Runtime MCP
- 無承認Mutation
- Automatic Save
- 無条件の全Bake
- AI単独のVisual Acceptance

## 16. Acceptance criteria

- Project事実とRequested Targetを分離できる。
- 特定環境をRepository全体の固定前提にしない。
- Read-only InspectionがSceneとAssetをDirtyにしない。
- 参考画像または自然言語から理由付きDirection Planを生成できる。
- Pipeline、Rendering Path、Platformを別軸で解決できる。
- `UNSUPPORTED`と`UNVERIFIED`を区別できる。
- Backend未実装時に黙って別PipelineへFallbackしない。
- Mutation前にDiffと承認を要求できる。
- Bake対象をDirty Dependencyから限定できる。
- Capture後にEditor一時状態を復元できる。
- Human ReviewなしにVisual Acceptedと判定しない。
