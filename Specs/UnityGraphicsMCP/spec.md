# UnityGraphicsMCP 仕様書

- FeatureName: `UnityGraphicsMCP`
- DocumentVersion: `2.0.0`
- DesignStatus: `Draft`
- ImplementationStatus: `Not Started`
- VerificationStatus: `Not Run`
- PrimaryNamespace: `UnityGraphicsMcp`

## 1. 目的

参考画像、自然言語、既存Scene、Render Pipeline、Target PlatformからVisual Intentを作成し、Unityの画作りを解析、提案、適用、Bake、撮影、再調整できるDomain MCPを構築する。

UnityGraphicsMCPはGraphics領域の専門判断とTool Groupを所有する。完成目的のWorkflowはLiveCreator、MovieCreator等が所有する。

## 2. 初期対象環境

- Unity: `6000.3`
- Render Pipeline: `URP 17+`
- Rendering Path: `Forward`
- RenderGraph: `Enabled`
- Primary Platform: `Nintendo Switch`
- Secondary Platforms: Switch 2 / PS4 / PS5 / PC

Built-inとHDRPはCapability仕様だけを登録し、実装が存在するまで空Backendや共通Interfaceを作らない。

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
- HDRP Planar ReflectionはHDRP実装時に追加する

### Atmosphere and VFX

- Skybox / Ambient / Fog / Wind / Time of Day
- Particle System
- VFX Graph Capability判定
- Decal

### Look and Rendering

- Volume / Volume Profile / Effective Volume Stack
- URP Post Process
- Custom Volume Component
- Renderer Data
- RendererFeatureの存在、順序、Injection Point、Capability
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
- Platform impact

## 6. Pipeline resolution

### Common

- Visual Intent
- Direction Plan
- Expected Visual Result
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

Pipeline非対応機能は黙って無視せず、Fallback、見た目の差、Validation状態を返す。

## 7. Platform resolution

PipelineとPlatformを別軸で解決する。

### Nintendo Switch既定

- Lightmap中心
- Light Probeを標準Fallbackとする
- APVは明示採用と実機検証を要求する
- Baked Reflection Probeを優先する
- Particle Systemを優先する
- Realtime Light / Shadow / Decal / Transparent OverdrawへBudgetを設定する
- RendererFeature追加時にFullscreen PassとGPU Costを記録する

未計測の性能改善を断定しない。

## 8. Tool contract

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

## 9. Mutation requirements

- `expectedRevision`
- Dry Run
- Machine-readable Diff
- Undo / Revert
- Save Policy
- Created / Modified / Dirty一覧
- Project Settings変更の別承認
- Pipeline Asset / Renderer Data変更の別承認
- Bakeの別承認

## 10. Bake dependency

変更から次の無効化を判定する。

- Lightmap
- Light Probe
- APV
- Reflection Probe
- Evaluation Capture

一つの変更を理由に無条件の全Bakeを行わない。

## 11. Validation

- Pipeline / Package / Renderer Capability
- Light / Shadow
- Lightmap / UV2 / LightingDataAsset
- Light Probe / APV
- Reflection Probe
- Material / Decal / Particle
- Volume / Post Process
- RendererFeature
- Timeline / Cinemachine Read-only state
- Platform Budget
- Visual Evidence要件

## 12. Naming and architecture

- Namespaceは`UnityGraphicsMcp`。
- enum型は`E_UPPER_SNAKE_CASE`。
- private fieldは`_camelCase`。
- URP-only段階ではPipeline Interfaceを作らない。
- Controller、Manager、Service、Profile、Adapter、追加asmdefをPattern目的で作らない。
- 新規ファイルにはOwner、Lifetime、Consumers、Responsibility、Split Reasonを記録する。

## 13. Non-goals

- Asset生成AIそのもの
- TextureやModelの生成Model実装
- 全Pipelineの初回同時実装
- Runtime MCP
- 無承認Mutation
- Automatic Save
- 無条件の全Bake
- AI単独のVisual Acceptance

## 14. Acceptance criteria

- Read-only InspectionがSceneとAssetをDirtyにしない。
- 参考画像または自然言語から理由付きDirection Planを生成できる。
- PipelineとPlatformを別軸で解決できる。
- Unsupported機能のFallbackとVisual Differenceを返せる。
- Mutation前にDiffと承認を要求できる。
- Bake対象をDirty Dependencyから限定できる。
- Capture後にEditor一時状態を復元できる。
- Human ReviewなしにVisual Acceptedと判定しない。
