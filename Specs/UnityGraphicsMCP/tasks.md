# UnityGraphicsMCP Tasks

- TaskPlanVersion: `5.0.0`
- CurrentPhase: `Phase 4 APV / Visual Acceptance Closed Loop`
- ImplementationStatus: `APV and Visual Acceptance APV and Visual Acceptance Complete`

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

## Save and Evaluation: Save / Color Capture / Refine — DONE

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

## Dependency Bake: Dirty Dependency Bake — DONE

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

## Capture Evidence: Capture Evidence and Human Visual Acceptance — DONE

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

## APV and Visual Acceptance: APV Bake Job and Acceptance Profile — DONE

Tool:

- `graphics.prepare_apv_bake_plan`
- `graphics.start_apv_bake`
- `graphics.get_apv_bake_status`
- `graphics.cancel_apv_bake`
- `graphics.prepare_acceptance_profile`
- `graphics.evaluate_capture`
- `graphics.refine_from_evaluation`

### APV Backend

- `ProbeVolumeBakingSet` Assetを明示指定
- Lighting Scenarioを明示指定
- 明示Scene集合とBaking Set Scene集合の完全一致検証
- Sceneは`Assets/`配下の既存Loaded Sceneへ限定
- URP / HDRP Pipeline Capability検証
- APV APIをReflection Capabilityとして検証
- Prepare / Start / Poll / Cancel Job契約
- 10分TTLの一時Approval Token
- `bakeMode = EXPLICIT_APV_BAKING_SET`
- Output Asset Rootの事前 / 事後SHA-256差分
- Timeoutと実行中Revision変更のCancellation
- Native Cancel + Cooperative Polling
- 生成済みOutputがある失敗 / Cancelは`PARTIAL`
- OutputがないCancelは`CANCELLED`
- 正常終了でOutput差分が無い場合は失敗
- 自動Save、Unity Undo、自動Rollback保証なし

### Acceptance Profile

- Profile名と最低総合合格値
- 1～32件の評価項目
- 項目別Weight / 最低合格値 / Critical Failure閾値
- 必須 / 任意Measurement
- Reference Capture ID / Evidence Digest固定
- CPU / GPU Frame Time、Memory、Draw Calls Performance Budget
- Unity側の画像意味解析なし
- 自動Profile合格とHuman Acceptanceを分離

### Visual Evaluation

- `PASSED` / `FAILED` / `INCOMPLETE`
- Critical Failureは総合Scoreより優先
- 必須Measurement不足は`INCOMPLETE`
- Performance Budget超過は`FAILED`
- Affected Object IDをCaptureのObject ID Mapへ解決
- Renderer GlobalObjectId / Type / Hierarchy / Sceneへ関連付け
- 不合格項目、Performance違反、問題Object、推奨Actionを構造化
- `FAILED` / `INCOMPLETE`だけを次Direction PlanへRefine
- `PASSED`でも最終AcceptanceにはCapture Evidence Human Reviewが必要

### Closed-loop Completion

EditMode E2E契約で次を成立させた。

1. Scene変更
2. 明示Save Plan / Apply
3. APV限定Bake Plan / Job
4. Output差分確定
5. COLOR / LINEAR_DEPTH / OBJECT_ID Capture Record
6. Acceptance Profile
7. Visual Evaluation
8. 不合格理由
9. Structured Refine Direction
10. 次Direction Plan

CIでは実APV Bakeを偽装せず、Job State MachineとOutput差分をBackend Overrideで契約検証し、Reflection BackendはUnity CompileとCapability解決で検証する。

## APV and Visual Acceptance verification gate — PASSED

1. Package dependency resolution — PASS
2. Unity 6000.0.75f1 Editor Compile — PASS
3. 27 Tool Bridge Discovery — PASS
4. Default Disable contract — PASS
5. Phase 1～4C Regression — PASS
6. APV Baking Set / Scenario / Scene Set Validation — PASS
7. Pipeline / Backend Capability — PASS
8. Approval / Revision / Plan Digest Guard — PASS
9. Async Job Status / Output Diff — PASS
10. Cancellation / Partial Result — PASS
11. Acceptance Profile Validation — PASS
12. Weight / Minimum / Critical Failure — PASS
13. Reference Capture Provenance — PASS
14. Performance Budget — PASS
15. Object ID Mapping — PASS
16. Structured Refine Direction — PASS
17. Closed-loop E2E Contract — PASS
18. EditMode Test — `98 / 98 PASS`
19. Evidence Artifact Upload — PASS

最新Evidence:

- Verification ID: `MUMCP-APV-VISUAL-ACCEPTANCE-CI-20260805-001`
- Workflow Run: `31005540655`
- Job: `92304475814`
- Artifact: `MyUnityMCP-APV-Visual-Acceptance-Evidence` (`8930337405`)
- Source: `Tests/Compatibility/apv-visual-acceptance-verification.yaml`

## Phase 5: Hardening and domain expansion — PENDING

- Actual URP / HDRP APV Sample Project verification
- Multiple Unity / SRP Version Matrix
- Player / Target Device Performance Evidence
- Reflection Probe新規Cubemap Asset生成Plan
- Progress percentage / structured operation history
- Deferred Renderer / Material Mutation
- UnityCinematicMCP
- LiveCreator / MovieCreator実行化
- UnityProfilerMCP
- UnityUIMCP
- UnityAddressablesMCP
- UnityBuildMCP

空Package、空Backend、利用実績のないInterfaceを先に追加しない。
