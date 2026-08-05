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
Light / Camera / Probe / Volume / Timeline / Cinemachine ...
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
Phase 0  Architecture / Catalog                         DONE
Phase 1  Project / Scene Inspection                     DONE
Phase 2  Direction Planning                             DONE
Phase 3  Approval-gated Graphics Mutation / Undo        DONE
Phase 4  Save / Bake / Capture / Visual Refine          PENDING
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
graphics.prepare_environment_plan
graphics.apply_environment_plan
graphics.undo_last_environment_transaction
```

全Toolは`AutoRegister = false`で、明示Activationされた場合だけ公開します。

## Phase 3 flow

Light:

```text
inspect
→ compile_direction
→ preview_plan
→ prepare_light_plan
→ Exact Diff確認・承認
→ apply_plan
→ undo_last_transaction
```

Camera / Reflection Probe / Volume:

```text
inspect
→ compile_direction
→ preview_plan
→ prepare_environment_plan
→ Exact Diff確認・承認
→ apply_environment_plan
→ undo_last_environment_transaction
```

Applyは次をすべて満たす場合だけ実行します。

- Direction Planが現在Sessionに存在する
- Expected Revisionが現在値と一致する
- Approval Tokenが一致する
- Preview時Baselineが適用直前状態と一致する
- 同一Plan内でOperation IDとUpdate対象が重複しない
- 対象Unity APIをPrepare時に読み書き可能と確認できる
- `saveMode = NONE`

変更は一つのUnity Undo Groupへ集約し、例外時はTransaction全体をRollbackします。Undo時は対象State、Revision、Transaction ID、最新Undo Groupを再確認します。自動保存とBakeは行いません。

## Phase 3 mutation scope

対応:

- Light Create / Update
- Camera Create / Update
- Reflection Probe Create / Update
- Volume Create / Update
- 既存Volume Profileの`sharedProfile`割当
- Property / Field形状差を吸収したVolume APIアクセス
- Atomic Transaction / Rollback / guarded Undo

未対応:

- Delete / Area Light
- Camera Stack / Target Texture / URP・HDRP Additional Camera Data
- Reflection Probe Bake
- Volume Profile内部Overrideの作成・変更
- Material / Renderer Feature Mutation
- Save / Bake / Capture

任意の`SerializedProperty`を書き換える汎用Toolは提供しません。

## Verification

Unity `6000.0.75f1`のGitHub Actions環境で、Package Resolve、Editor Compile、11 Tool Discovery、直接Handler Invocation、Inspection、Planning、Light／Camera／Reflection Probe／Volume Mutation、Atomic Undo、安全拒否を含むEditMode Testを実行しています。

Phase 1～3の総合結果は`46 / 46 PASS`です。正確なWorkflow RunとArtifactは`Tests/Compatibility/verification-matrix.yaml`を正本とします。

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
  MyUnityMCPVerification/
Tests/
  Compatibility/
```

## Next phase

Phase 4では、Mutationとは別の明示承認境界としてSave、Dependency限定Bake、Capture、Visual Evaluation、Refine Loopを追加します。
