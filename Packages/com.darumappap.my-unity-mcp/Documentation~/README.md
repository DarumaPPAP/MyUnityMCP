# My Unity MCP Package

## Current state

Phase 1のInspection、Phase 2のDirection Planning、Phase 3Aの承認制Light Mutationまで実装・Unity Editor CI検証済みです。

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

graphics.prepare_light_plan
→ 明示的なLIGHT_CREATE / LIGHT_UPDATEを検証
→ 正確なBefore / After、Diff Digest、Approval Tokenを発行

graphics.apply_plan
→ Plan ID、Expected Revision、Approval Tokenを再検証
→ 一つのUnity Undo Transactionとして適用

graphics.undo_last_transaction
→ 直近Transactionで外部変更がない場合だけ復元
```

## Phase 3A mutation scope

対応:

- `LIGHT_CREATE`
- `LIGHT_UPDATE`
- Directional / Point / Spot
- Name
- Color
- Intensity
- Range
- Spot Angle
- Shadow
- Position / Euler Angles
- Enabled

未対応:

- Light削除
- Area Light
- Volume / Reflection Probe / Camera Mutation
- Material / Renderer Feature Mutation
- Scene / Asset Save
- Lighting Bake
- Capture / Visual Refine

Unity C#側で自然言語からLightの数値を推測しません。UnityAgentまたはMCP Clientが明示値を構造化し、`prepare_light_plan`で正確な差分へ変換します。

## Safety contract

- Phase 2 Direction Plan必須
- Exact Preview必須
- 一時Approval Token必須
- Expected Revision一致必須
- Preview時Baselineを適用直前に再照合
- 全操作を一つのUndo Groupへ集約
- 例外時はUndo Group単位でRollback
- 直近Transaction以外の自動Undo禁止
- Transaction後に外部変更があればUndo拒否
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

## Verified environment

- Unity: `6000.0.75f1`
- Host: GitHub Actions Ubuntu 24.04
- Render Pipeline: Built-inの最小検証Project
- Package Resolve: PASS
- Editor Compile: PASS
- Bridge Discovery: PASS
- Direct Handler Invocation: PASS
- EditMode: `30 / 30 PASS`

検証実績は`Tests/Compatibility/verification-matrix.yaml`を正本とします。この実績は一つのEditor環境に対するEvidenceであり、すべてのUnity Version、Pipeline、Player、実機対応を意味しません。

## Next phase

Phase 3Bでは、同じTransaction Contractを維持したまま次のCapabilityを段階追加します。

```text
Volume
Reflection Probe
Camera
```

汎用的な任意`SerializedProperty`書換えToolは追加しません。
