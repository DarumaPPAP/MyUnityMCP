# My Unity MCP Package

## Current state

Phase 1のInspection、Phase 2のDirection Planning、Phase 3の承認制Graphics Mutationまで実装・Unity Editor CI検証済みです。

実装済みTool:

```text
graphics.inspect_project
graphics.inspect_scene
graphics.validate_scene
graphics.compile_direction
graphics.preview_plan
graphics.prepare_light_plan
graphics.apply_plan
graphics.undo_last_transaction
graphics.prepare_environment_plan
graphics.apply_environment_plan
graphics.undo_last_environment_transaction
```

全Toolは`AutoRegister = false`です。Package導入だけでは外部公開されず、必要なToolを明示的にActivationします。

## Bridge dependency

- Package: `com.coplaydev.unity-mcp`
- 宣言API基準: `10.1.2`
- Unity検証Commit: `9f84072c38906e3ca903f14f6a8edc1a1c9012c3`
- Assembly: `MCPForUnity.Editor`
- Tool登録: `McpForUnityToolAttribute`
- Entry Point: `HandleCommand(JObject)`

MyUnityMCPを導入するProjectでは、このBridge Packageを解決できるPackage RegistryまたはGit Package導入経路が必要です。

## Operation flow

```text
graphics.inspect_project
→ Project EnvironmentとCapabilityを検出

graphics.inspect_scene / graphics.validate_scene
→ Scene状態とGraphics不整合をRead-onlyで取得

graphics.compile_direction
→ 構造化Visual IntentからDirection Planを作成

graphics.preview_plan
→ 抽象Planが要求するCreated / Modified / Dirty / Bake候補をRead-only予告
```

Light:

```text
graphics.prepare_light_plan
→ LIGHT_CREATE / LIGHT_UPDATEをExact Diffへ変換

graphics.apply_plan
→ Approval / Revision / Baselineを再確認して一括適用

graphics.undo_last_transaction
→ 直近Light Transactionだけを安全に復元
```

Camera / Reflection Probe / Volume:

```text
graphics.prepare_environment_plan
→ CAMERA / REFLECTION_PROBE / VOLUMEのCreate / UpdateをExact Diffへ変換

graphics.apply_environment_plan
→ 一つのEnvironment Transactionとして一括適用

graphics.undo_last_environment_transaction
→ 対象StateとUndo Stackが一致する場合だけ一括復元
```

## Phase 3 mutation scope

対応:

- `LIGHT_CREATE` / `LIGHT_UPDATE`
- `CAMERA_CREATE` / `CAMERA_UPDATE`
- `REFLECTION_PROBE_CREATE` / `REFLECTION_PROBE_UPDATE`
- `VOLUME_CREATE` / `VOLUME_UPDATE`
- 既存Volume Profileの`sharedProfile`割当
- Light Type、Color、Intensity、Range、Spot Angle、Shadow
- Camera Projection、FOV、Clip、Culling Mask、Clear、HDR、MSAA
- Reflection Probe Mode、Refresh、Importance、Intensity、Box、Size、Resolution
- Volume Global、Priority、Blend Distance、Weight、Enabled
- Transform、Name、Enabled

未対応:

- Delete / Area Light
- Camera Stack / Target Texture / Pipeline固有Additional Camera Data
- Reflection Probe Bake
- Volume Profile内部Overrideの作成・変更
- Material / Renderer Feature Mutation
- Scene / Asset Save
- Lighting Bake
- Capture / Visual Refine

Unity C#側で自然言語から設定値を推測しません。UnityAgentまたはMCP Clientが明示値を構造化し、Prepare Toolで正確な差分へ変換します。

## Safety contract

- Phase 2 Direction Plan必須
- Exact Preview必須
- 一時Approval Token必須
- Expected Revision一致必須
- Preview時Baselineを適用直前に再照合
- 同一Plan内のOperation ID重複を拒否
- 同一Componentへの複数Updateを拒否
- Volume APIをPrepare時にProperty / Fieldの両形状で検証
- 全操作を一つのUndo Groupへ集約
- 例外時はUndo Group単位でRollback
- Transaction ID、Revision、対象State、最新Undo GroupをUndo前に再確認
- `saveMode = NONE`のみ
- 自動保存禁止
- Bake禁止
- Silent Fallback禁止

InspectionとPlanningではScene、Persistent Asset、Undo Groupを変更しません。Mutationでは対象SceneをDirtyにしますが、保存は利用者の明示操作へ残します。

## Project environment policy

特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph、PlatformをPackage全体へ固定しません。

1. 対象Projectから検出した事実
2. 今回の依頼で明示されたTargetと制約
3. Project固有Profile
4. UnityAgentの既定Preference

この順序で解決します。`UNVERIFIED`を`UNSUPPORTED`として扱わず、未実装Backendへ黙ってFallbackしません。

Render Pipelines CoreはPackage本体の必須Dependencyにせず、導入先ProjectでVolume APIが利用可能な場合だけCapabilityを有効化します。

## Verified environment

- Unity: `6000.0.75f1`
- Host: GitHub Actions Ubuntu 24.04
- Verification Project: Built-in + Render Pipelines Core `17.0.4`
- Package Resolve: PASS
- Editor Compile: PASS
- 11 Tool Discovery: PASS
- Direct Handler Invocation: PASS
- EditMode: `46 / 46 PASS`

検証実績は`Tests/Compatibility/verification-matrix.yaml`を正本とします。この実績は一つのEditor環境に対するEvidenceであり、すべてのUnity Version、Pipeline、Player、実機対応を意味しません。

## Next phase

Phase 4では、Mutationと分離した明示承認境界として次を追加します。

```text
Save
Dependency限定Bake
Capture
Visual Evaluation
Refine Loop
```

汎用的な任意`SerializedProperty`書換えToolは追加しません。
