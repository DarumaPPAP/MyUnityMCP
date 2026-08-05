# UnityGraphicsMCP Tasks

- TaskPlanVersion: `4.1.0`
- CurrentPhase: `Phase 4 Save / Bake / Capture`
- ImplementationStatus: `Phase 4B Dirty Dependency Bake Complete`

## Status legend

- `DONE`: Source、Unity Compile、必要な実行検証を含む完了条件を満たした。
- `SOURCE_DONE`: Sourceは存在するがUnity Compileまたは実行検証前。
- `PENDING`: 未着手または前提Gate待ち。
- `BLOCKED`: 必須依存または承認待ち。

## Phase 0: Governance and repository bootstrap — DONE

Repository ownership、Catalog、Manifest、Control Plane、Graphics仕様、Creator Workflow、Package skeleton、Routing Test、Compatibility Matrixを作成した。

## Phase 1: Inspection and validation — DONE

Tool:

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`

完了項目:

- Project Environment / Requested Target分離
- Capability / Backend Status解決
- Session ID / Revision
- Snapshot TTL / Count上限 / Cursor Paging
- Compile / Reload / Play Mode遷移時の失効
- Scene / Persistent Asset / Undo Read-only Guard
- Camera、Light、Lightmap、Probe、Renderer、Material、Volume等のScene解析
- Invariant / Policy / Heuristic Validation

## Phase 2: Direction planning — DONE

Tool:

- `graphics.compile_direction`
- `graphics.preview_plan`

完了項目:

- Structured Visual Intent
- Project FactsとRequested Targetの分離
- Recommendation / Range / Reason / Dependency
- Confidence / Verification Level
- Session-local Plan ID / Expected Revision
- Mutation / Save / Bakeを行わないRead-only Preview

## Phase 3: Approval-gated graphics mutation — DONE

Tool:

- `graphics.prepare_light_plan`
- `graphics.apply_plan`
- `graphics.undo_last_transaction`
- `graphics.prepare_environment_plan`
- `graphics.apply_environment_plan`
- `graphics.undo_last_environment_transaction`

Operation:

- `LIGHT_CREATE` / `LIGHT_UPDATE`
- `CAMERA_CREATE` / `CAMERA_UPDATE`
- `REFLECTION_PROBE_CREATE` / `REFLECTION_PROBE_UPDATE`
- `VOLUME_CREATE` / `VOLUME_UPDATE`

安全契約:

- Exact Before / Requested After
- Diff Digest / 一時Approval Token
- Expected Revision / Baseline再検証
- Atomic Unity Undo Transaction
- 途中例外時Rollback
- 外部変更後または新しいUndo Group後のUndo拒否
- 自動Saveなし
- Bakeなし

## Phase 4A: Save / Capture / Refine — DONE

Tool:

- `graphics.prepare_save_plan`
- `graphics.apply_save_plan`
- `graphics.capture_evaluation`
- `graphics.refine_direction`

### Save

- 一つの既存Loaded Sceneだけを対象
- Dirty Sceneのみ
- Scene Handle / Path / Content Digest / Dirty Stateを固定
- Expected Revision / 一時Approval Token必須
- `saveMode = EXPLICIT_SCENE`のみ
- Save As、自動保存、全Scene保存、Asset一括保存なし
- 永続化後の自動Undo / Rollback保証なし

### Capture / Refine

- 指定CameraのColor PNGを`Library/MyUnityMCP/Captures`へ出力
- Camera TargetTexture、Active RenderTexture、Scene / Asset Dirty、Undo状態を復元
- Unity C#側で画像意味解析を行わない
- Human Reviewの明示入力だけを次Iterationへ反映
- Human ReviewなしにVisual Acceptedと判定しない

## Phase 4B: Dirty Dependency Bake — DONE

Tool:

- `graphics.prepare_bake_plan`
- `graphics.bake_dependencies`

### Dirty Dependency Set

- `EditorSceneManager.sceneDirtied`で保存済みLoaded Sceneを追跡
- Scene Save後もDependencyをSession内保持
- Scene Close / Play Mode / Compile / Domain Reloadで失効
- `LIGHTMAP_SCENE`、`REFLECTION_PROBE`、`ADAPTIVE_PROBE_VOLUME`を区別
- 完了済みDependencyだけをSetから除去

### Bake Plan

- PrepareはRead-only
- Expected Revision
- Dirty Set Serial
- 全Loaded Contributing SceneのHandle / Path / Dirty / Content Digest
- Dependency Baseline / Backend
- Exact Diff Digest
- 10分TTLの別Approval Token

### Bake Apply

- `bakeMode = EXPLICIT_DEPENDENCIES`のみ
- Apply直前にRevision / Token / Dirty Set / Loaded Scene / Baselineを再検証
- 全DependencyをPreflightしてから実行
- Scene限定Lightmap Bake
- 既存Cubemap Assetを持つ明示Baked Reflection Probe Bake
- 自動Saveなし
- 複数Loaded Sceneで全Scene BakeへのSilent Fallbackなし
- Unity Undo / 自動Rollback保証なし
- APVは検出するがBaking Set / Lighting Scenario契約未実装のため`BACKEND_NOT_IMPLEMENTED`

## Phase 4 verification gate — PASSED

1. Package dependency resolution — PASS
2. Unity Editor Compile — PASS
3. 17 Tool Bridge Discovery — PASS
4. Default Disable contract — PASS
5. Phase 1-3 Regression — PASS
6. Phase 4A Save / Capture / Refine contract — PASS
7. Phase 4B Prepare Read-only — PASS
8. Save後のDirty Dependency保持 — PASS
9. Bake Approval rejection — PASS
10. Bake Revision / Baseline / Dirty Set guard — PASS
11. Explicit Scene Backend invocation — PASS
12. Completed Dependency removal — PASS
13. APV Backend rejection — PASS
14. No Auto-save — PASS
15. No Silent Full Bake Fallback — PASS
16. EditMode Test — `63 / 63 PASS`

最終Run、Artifact、Verification IDは`Tests/Compatibility/verification-matrix.yaml`を正本とする。

## Phase 4C candidates — PENDING

- APV Baking Set / Lighting Scenario Backend
- Reflection Probe新規Cubemap Asset生成Plan
- Async Bake Job / Progress / Cancel
- Bake Artifact Digest
- Depth / Object ID Capture
- Visual Acceptance確定Workflow

## Phase 5: Domain expansion — PENDING

- Deferred Renderer / Material Mutation
- UnityCinematicMCP
- LiveCreator / MovieCreator実行化
- UnityProfilerMCP
- UnityUIMCP
- UnityAddressablesMCP
- UnityBuildMCP

空Package、空Backend、利用実績のないInterfaceを先に追加しない。
