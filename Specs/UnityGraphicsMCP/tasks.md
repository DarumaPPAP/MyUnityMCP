# UnityGraphicsMCP Tasks

- TaskPlanVersion: `4.2.0`
- CurrentPhase: `Phase 4 Save / Bake / Capture`
- ImplementationStatus: `Phase 4C Capture Evidence Complete`

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

## Phase 4A: Save / Color Capture / Refine — DONE

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

### Color Capture / Refine

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

- Unity 6000.0互換層でLoaded Sceneの`isDirty`遷移を監視
- Scene Save後もDependencyをSession内保持
- Scene Close / Play Mode / Compile / Domain Reloadで失効
- `LIGHTMAP_SCENE`、`REFLECTION_PROBE`、`ADAPTIVE_PROBE_VOLUME`を区別
- 完了済みDependencyだけをSetから除去

### Bake Plan / Apply

- PrepareはRead-only
- Expected Revision / Dirty Set Serial
- Contributing Scene Handle / Path / Dirty / Content Digest
- Dependency Baseline / Backend / Exact Diff Digest
- 10分TTLの別Approval Token
- `bakeMode = EXPLICIT_DEPENDENCIES`のみ
- Apply直前にRevision / Token / Dirty Set / Loaded Scene / Baselineを再検証
- 全DependencyをPreflightしてから実行
- Scene限定Lightmap Bake
- 既存Cubemap Assetを持つ明示Baked Reflection Probe Bake
- 自動Saveなし
- 複数Loaded Sceneで全Scene BakeへのSilent Fallbackなし
- Unity Undo / 自動Rollback保証なし
- APVはBaking Set / Lighting Scenario契約未実装のため`BACKEND_NOT_IMPLEMENTED`

## Phase 4C: Capture Evidence and Visual Acceptance — DONE

Tool:

- `graphics.capture_evidence`
- `graphics.submit_visual_review`
- `graphics.refine_from_visual_review`

### Capture Evidence Bundle

- `COLOR` PNG
- 正規化Linear Eye Depth Float EXR
- Renderer単位の決定的24-bit Object ID PNG
- Object IDとGlobalObjectId / Hierarchy / Sceneの対応JSON
- Camera Baseline、Scene、Render Pipeline、Color Space、解像度をManifestへ固定
- Artifact SHA-256とBundle全体の`evidenceDigest`
- 一時Directoryから最終Directoryへの原子的確定
- Expected Revision必須
- Camera TargetTexture / Active RenderTextureを復元
- Scene / Project Asset DirtyおよびUndo Groupを変更しない
- Capture中にRevisionが変化した場合はBundleを破棄
- Null Graphics Deviceでは描画を偽装せず`UNVERIFIED`
- Unity組み込み`Default-Material`の一時Dirty FlagをProject Asset変更から分離

### Coverage Contract

- LoadedかつActiveなCamera Frustum内Rendererを対象
- Renderer上限を明示し、超過時は拒否またはManifestへ記録
- Terrain、DecalProjector、Procedural Draw、Material固有Alpha Clip、頂点変形等の未対応範囲をManifestへ記録
- Coverage不足をSilent Successとして扱わない

### Human Visual Acceptance

- Evidence Digest完全一致必須
- Human Reviewer必須
- Decisionは`ACCEPTED` / `REJECTED` / `NEEDS_ADJUSTMENT`
- `ACCEPTED`にはObservationと`VISUAL_ACCEPTED`確認文字列が必要
- 同一CaptureへのReviewは一度だけで改変不可
- `REJECTED`または`NEEDS_ADJUSTMENT`だけを次IterationのDirection PlanへRefine
- Unity C#側は画像の意味解析や自動Acceptanceを行わない

## Phase 4C verification gate — PASSED

1. Package dependency resolution — PASS
2. Unity 6000.0.75f1 Editor Compile — PASS
3. 20 Tool Bridge Discovery — PASS
4. Default Disable contract — PASS
5. Phase 1～4B Regression — PASS
6. Color Capture Evidence — PASS
7. Linear Depth Evidence — PASS
8. Deterministic Object ID Evidence / Mapping — PASS
9. Manifest / Artifact SHA-256 / Evidence Digest — PASS
10. Atomic Bundle Publish — PASS
11. Expected Revision / Capture State Guard — PASS
12. Scene / Project Asset Dirty / Undo Guard — PASS
13. Null Graphics Device `UNVERIFIED` — PASS
14. Human Review / Acceptance Confirmation Guard — PASS
15. Immutable Review — PASS
16. Review起点Refine — PASS
17. EditMode Test — `78 / 78 PASS`
18. Evidence Artifact Upload — PASS

最新Evidence:

- Verification ID: `MUMCP-PHASE4C-CI-20260805-001`
- Workflow Run: `30991161982`
- Job: `92257378283`
- Artifact: `MyUnityMCP-Phase4C-Unity-Evidence` (`8924347145`)
- Source: `Tests/Compatibility/phase4c-verification.yaml`

## Phase 4D candidates — PENDING

- APV Baking Set / Lighting Scenario Backend
- Reflection Probe新規Cubemap Asset生成Plan
- Async Bake Job / Progress / Cancel
- Bake Artifact Digest
- Visual Acceptance Profile / Score / Performance Budget
- End-to-End Modify → Save → Bake → Capture → Review → Refine Workflow Test

## Phase 5: Domain expansion — PENDING

- Deferred Renderer / Material Mutation
- UnityCinematicMCP
- LiveCreator / MovieCreator実行化
- UnityProfilerMCP
- UnityUIMCP
- UnityAddressablesMCP
- UnityBuildMCP

空Package、空Backend、利用実績のないInterfaceを先に追加しない。
