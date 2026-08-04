# UnityGraphicsMCP Tasks

- TaskPlanVersion: `4.0.0`
- CurrentPhase: `Phase 4 Save / Bake / Capture`
- ImplementationStatus: `Phase 3 Approval-gated Graphics Mutation Operational Complete`

## Status legend

- `DONE`: Source、Unity Compile、必要な実行検証を含む完了条件を満たした。
- `SOURCE_DONE`: Sourceは存在するがUnity Compileまたは実行検証前。
- `PENDING`: 未着手または前提Gate待ち。
- `BLOCKED`: 必須依存または承認待ち。

## Phase 0: Governance and repository bootstrap — DONE

- Repository ownership、Catalog、Manifest、Control Plane、Graphics仕様、Creator Workflow、Package skeleton、Routing Test、Compatibility Matrixを作成。
- 特定Project環境をMyUnityMCP全体へ固定しない。
- 未選択ManifestとSourceを常時読み込まない。

## Phase 1: Inspection and validation — DONE

実装済みTool:

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`

完了項目:

- MCP Bridge API確認
- Editor / Test Assembly
- Project Environment / Requested Target分離
- Capability / Backend Status解決
- Session ID / Revision
- Snapshot TTL / Count上限 / Cursor Paging
- Compile / Reload / Play Mode遷移時の失効
- Scene / Persistent Asset / Undo Read-only Guard
- Camera、Light、Lightmap、Probe、Renderer、Material、Volume等のScene解析
- Invariant / Policy / Heuristic Validation
- Bridge Discovery / Direct Handler Invocation

Phase 1単独Evidenceは`Tests/Compatibility/verification-matrix.yaml`へ記録する。

## Phase 2: Direction planning — DONE

### UGMCP-020-001 Structured Visual Intent — DONE

Unity C#側で自然言語や画像の意味理解を偽装しない。UnityAgentまたはMCP Clientが構造化したIntentを入力する。

### UGMCP-020-002 Compile direction — DONE

Tool:

- `graphics.compile_direction`

実装:

- Lighting
- GI
- Reflection
- Atmosphere
- Look
- Platform
- Range / Reason / Dependency / Confidence / Verification Level
- Session-local Plan ID
- Expected Revision
- 不足Intentの`PARTIAL`返却

### UGMCP-020-003 Preview plan — DONE

Tool:

- `graphics.preview_plan`

Unity状態を変更せず、Created / Modified / Dirty / Bake Required / Unsupported / Unverified候補を返す。

### Phase 2 safety — DONE

- Scene Dirty非変更
- Persistent Asset Dirty非変更
- Undo Group非変更
- Stale Revision拒否
- Project事実とRequested Targetを混同しない

## Phase 3: Approval-gated graphics mutation — DONE

### UGMCP-030-001 Exact Light Plan — DONE

Tool:

- `graphics.prepare_light_plan`

対応:

- `LIGHT_CREATE` / `LIGHT_UPDATE`
- Directional / Point / Spot
- Exact Before / After Preview
- Diff Digest
- 10分TTLの一時Approval Token
- 明示値のみ使用し、数値を推測しない

### UGMCP-030-002 Light Apply / Undo — DONE

Tool:

- `graphics.apply_plan`
- `graphics.undo_last_transaction`

実装:

- Plan / Revision / Token / Baseline再検証
- 一つのUnity Undo Groupへ集約
- 例外時Rollback
- Plan一回使用
- 外部変更後のUndo拒否
- 自動保存なし
- Bakeなし

### UGMCP-031-001 Exact Environment Plan — DONE

Tool:

- `graphics.prepare_environment_plan`

対応Operation:

- `CAMERA_CREATE` / `CAMERA_UPDATE`
- `REFLECTION_PROBE_CREATE` / `REFLECTION_PROBE_UPDATE`
- `VOLUME_CREATE` / `VOLUME_UPDATE`

実装:

- Camera、Reflection Probe、Volumeを同一Planへ混在可能
- Exact Before / Requested After
- Diff Digest / Approval Token
- Operation ID重複拒否
- 同一Componentへの複数Update拒否
- Volume APIのProperty / Field差を吸収
- 指定されたVolume MemberをPrepare時に読み書き可能か検証
- 既存`sharedProfile`参照の割当
- Profile内部Overrideの作成・変更は禁止

### UGMCP-031-002 Environment Apply / Undo — DONE

Tool:

- `graphics.apply_environment_plan`
- `graphics.undo_last_environment_transaction`

実装:

- Camera / Probe / VolumeのAtomic Transaction
- 途中例外時に全体Rollback
- Expected Revision / Approval / Baseline再検証
- Transaction ID / Revision / 対象StateをUndo前に再確認
- TransactionがUndo Stackの最新Groupでない場合は拒否
- 外部変更後のUndo拒否
- 自動保存なし
- Bakeなし

### UGMCP-031-003 Bridge and EditMode tests — DONE

検証対象:

- 11 ToolのBridge DiscoveryとDefault Disable契約
- Phase 1 / Phase 2 / Phase 3A Regression
- PrepareのRead-only保証
- Approval Tokenなしの拒否
- Stale Revision / Changed Baseline拒否
- Automatic Save Mode拒否
- Light Create / Update
- Camera Create / Update
- Reflection Probe Create / Update
- Volume Create / Update / sharedProfile
- Volume Property / Field API解決
- Operation ID重複拒否
- 同一Update対象重複拒否
- Camera / Probe / Volume Atomic Transaction
- Atomic Undo
- 外部対象変更後のUndo拒否
- 新しいUndo Group追加後のUndo拒否
- No Auto-save / No Bake

Unity `6000.0.75f1`のGitHub Actions環境で`46 / 46 PASS`。最終RunとArtifactはCompatibility Matrixを正本とする。

## Phase 3 completion gate — PASSED

1. Package dependency解決 — PASS
2. Unity Editor Compile — PASS
3. 11 ToolのBridge Discovery — PASS
4. Direct Handler Invocation — PASS
5. Exact Light / Environment Preview — PASS
6. Approval Token Guard — PASS
7. Stale Revision / Baseline Guard — PASS
8. Light Create / Update — PASS
9. Camera Create / Update — PASS
10. Reflection Probe Create / Update — PASS
11. Volume Create / Update / sharedProfile — PASS
12. Property / Field API Resolution — PASS
13. Duplicate Operation / Target Rejection — PASS
14. Exception Rollback Contract — PASS
15. Atomic Undo — PASS
16. External Change Undo Rejection — PASS
17. Newer Undo Group Rejection — PASS
18. Automatic Save禁止 — PASS
19. Bake非実行 — PASS
20. EditMode Test — 46 / 46 PASS

## Deferred graphics mutation scope

Phase 3へ無理に含めず、実際のGoalと必要性を確認して独立計画する。

- Material Asset / Scene Overrideの所有分離
- Shared Material参照維持
- Render Queue / Shader / Keywordの安全差分
- Renderer FeatureのPipeline別Capability Gate
- Variant / Build影響の明示

任意のUnity Objectを操作できる汎用`SerializedProperty` Mutation Toolは作らない。

## Phase 4: Save, bake and capture — PENDING

- 明示Save Plan / Approval
- Dirty Dependency Set
- `graphics.bake_dependencies`
- `graphics.capture_evaluation`
- `graphics.refine_direction`
- Bakeの別承認
- Capture時の一時Editor状態復元
- Human ReviewなしのVisual Acceptance禁止

## Phase 5: Domain expansion — PENDING

実際のGoalと不足Capabilityに応じて選択する。

- Deferred Renderer / Material Mutation
- UnityCinematicMCP
- LiveCreator / MovieCreator実行化
- UnityProfilerMCP
- UnityUIMCP
- UnityAddressablesMCP
- UnityBuildMCP

空Package、空Backend、利用実績のないInterfaceを先に追加しない。
