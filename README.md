# MyUnityMCP

UnityAgentの判断規則と連携し、目的別Creator、専門Domain MCP、Unity操作Capability Moduleを必要時だけ有効化するUnity制作基盤です。

## Architecture

```text
UnityAgent
ユーザーの規約・感性・Unity知識
        ↓
UnityAgentMCP
Creator / Domain MCPの選択、権限、実行順序
        ↓
Creator Workflow
LiveCreator / MovieCreator / WorldCreator
        ↓
Domain MCP
Graphics / Cinematic / UI / Addressables / Profiler ...
        ↓
Capability Module
Light / Lightmap / Probe / Timeline / Cinemachine ...
        ↓
Unity Editor API
```

## Ownership

このRepositoryはUnityAgentMCP、Creator Workflow、Domain MCP、Capability Module、Catalog、Manifest、UPM Package、MCP固有仕様とTestを所有します。

MCPが生成・変更するScene、Prefab、Material、Timeline、Volume Profile等は対象Unity Projectが所有します。UnityAgentはユーザー固有の規約、Visual Direction、Route、Context、Knowledgeを所有します。

## Project environment resolution

Unity Version、Render Pipeline、Rendering Path、RenderGraph、Target PlatformをRepository全体の固定前提にしません。

```text
対象Unity ProjectをInspect
→ 検出したProject事実を確定
→ 今回指定されたTargetと比較
→ 利用可能なBackend / Capabilityだけを選択
→ 未対応・未検証・未設定を区別して返す
```

優先順位:

1. 対象Unity Projectから検出した事実
2. 今回明示されたTargetと制約
3. Project固有Profile
4. UnityAgentの既定Preference

## Current status

```text
Phase 0  Architecture / Catalog               DONE
Phase 1  Project / Scene Inspection           DONE
Phase 2  Direction Planning                   DONE
Phase 3A Approval-gated Light Mutation / Undo DONE
Phase 3B Volume / Reflection Probe / Camera   PENDING
Phase 4  Bake / Capture / Visual Refine       PENDING
```

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

全Toolは`AutoRegister = false`で、明示Activationされた場合だけ公開します。

## Phase 3A flow

```text
inspect
→ compile_direction
→ preview_plan
→ prepare_light_plan
→ 人間または上位AgentがExact Diffを確認
→ apply_plan
→ undo_last_transaction
```

`prepare_light_plan`は明示的な`LIGHT_CREATE` / `LIGHT_UPDATE`を検証し、Before / After、Diff Digest、一時Approval Tokenを発行します。

`apply_plan`は次をすべて満たす場合だけLightを変更します。

- Direction Planが現在Sessionに存在する
- Expected Revisionが現在値と一致する
- Approval Tokenが一致する
- Preview時Baselineが適用直前状態と一致する
- `saveMode = NONE`

変更は一つのUnity Undo Groupへ集約され、例外時はRollbackします。自動保存とBakeは行いません。直近Transaction後に外部変更が検出された場合、`undo_last_transaction`は安全のため拒否します。

## Phase 3A mutation scope

対応:

- Light Create / Update
- Directional / Point / Spot
- Name、Color、Intensity、Range、Spot Angle
- Shadow、Transform、Enabled

未対応:

- Delete / Area Light
- Volume / Reflection Probe / Camera Mutation
- Material / Renderer Feature Mutation
- Save / Bake / Capture

任意の`SerializedProperty`を書き換える汎用Toolは提供しません。

## Verification

Unity `6000.0.75f1`のGitHub Actions環境で、Package Resolve、Editor Compile、Bridge Discovery、直接Handler Invocation、Inspection、Planning、Light Mutation、Atomic Undo、安全拒否を含むEditMode Testを実行しています。

現在のPhase 3A Test結果は`30 / 30 PASS`です。正確なWorkflow RunとArtifactは`Tests/Compatibility/verification-matrix.yaml`を正本とします。

このEvidenceは一つのEditor環境に対する実績であり、すべてのUnity Version、Pipeline、Player、Target Device対応を意味しません。

## Repository map

```text
Catalog/
Specs/
  UnityAgentMCP/
  UnityGraphicsMCP/
Workflows/
Packages/
  com.darumappap.my-unity-mcp/
    Editor/
    Tests/Editor/
TestProjects/
  MyUnityMCPPhase1/
Tests/
  Compatibility/
```

## Next phase

Phase 3Bでは、Phase 3AのTransaction Contractを変更せず、Volume、Reflection Probe、CameraをCapability単位で追加します。
