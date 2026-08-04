# UnityGraphicsMCP Tasks

- TaskPlanVersion: `3.0.0`
- CurrentPhase: `Phase 3B Capability Expansion`
- ImplementationStatus: `Phase 3A Light Mutation Operational Complete`

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

## Phase 3A: Light mutation transaction — DONE

### UGMCP-030-001 Exact Light Plan — DONE

Tool:

- `graphics.prepare_light_plan`

実装:

- Phase 2 Direction Plan必須
- `LIGHT_CREATE` / `LIGHT_UPDATE`
- Directional / Point / Spot
- 明示値のみ使用し、数値を推測しない
- Exact Before / After Preview
- Diff Digest
- 10分TTLの一時Approval Token
- Executable Plan最大8件
- Read-only Guard

### UGMCP-030-002 Apply plan — DONE

Tool:

- `graphics.apply_plan`

必須入力:

- Executable Plan ID
- Expected Revision
- Approval Token
- `saveMode = NONE`

実装:

- Plan / Revision / Token再検証
- Preview時Light Baselineを適用直前に再照合
- 一つのUnity Undo Groupへ集約
- Light Create / Update
- 対象SceneだけをDirty化
- 例外時`Undo.RevertAllDownToGroup`でRollback
- Plan一回使用
- 自動保存なし
- Bakeなし

### UGMCP-030-003 Undo latest transaction — DONE

Tool:

- `graphics.undo_last_transaction`

実装:

- 直近MyUnityMCP Transactionのみ
- Expected Revision一致必須
- Undo Stackの最新Group必須
- Transaction適用後状態の一致確認
- 外部Hierarchy / Project / Undo変更後は拒否
- Created Light削除とUpdated Light復元を実行後検証

### UGMCP-030-004 Bridge and EditMode tests — DONE

検証対象:

- 8 ToolのBridge DiscoveryとDefault Disable契約
- Phase 3A Handler Invocation
- PrepareのRead-only保証
- Approval Tokenなしの拒否
- Stale Revision拒否
- Automatic Save Mode拒否
- Light Create
- Light Update
- Atomic Undo
- Preview後Target変更の拒否
- Undo前外部変更の拒否
- No Auto-save / No Bake
- Phase 1 / Phase 2 Regression

Unity `6000.0.75f1`のGitHub Actions環境で`30 / 30 PASS`。最終RunとArtifactはCompatibility Matrixを正本とする。

## Phase 3A completion gate — PASSED

1. Package dependency解決 — PASS
2. Unity Editor Compile — PASS
3. 8 ToolのBridge Discovery — PASS
4. Direct Handler Invocation — PASS
5. Exact Light Preview — PASS
6. Approval Token Guard — PASS
7. Stale Revision Guard — PASS
8. Light Create / Update — PASS
9. Exception Rollback Contract — PASS
10. Atomic Undo — PASS
11. External Change Undo Rejection — PASS
12. Automatic Save禁止 — PASS
13. Bake非実行 — PASS
14. EditMode Test — 30 / 30 PASS

## Phase 3B: Capability expansion — PENDING

Phase 3AのTransaction Contractを変更せず、Capability単位で追加する。

候補順:

1. Volume
2. Reflection Probe
3. Camera

各Capabilityで必要な条件:

- 専用Operation Schema
- Exact Preview
- Approval Token
- Expected Revision
- Undo / Rollback
- No Auto-save
- Pipeline Capability Status
- EditMode Test

任意のUnity Objectを操作できる汎用`SerializedProperty` Mutation Toolは作らない。

## Phase 3C: Renderer and material mutation — PENDING

- Shared Material参照の維持
- Material AssetとScene Overrideの所有分離
- Render Queue / Shader / Keywordの安全差分
- Renderer FeatureのPipeline別Capability Gate
- Variant / Build影響の明示

## Phase 4: Bake and capture — PENDING

- Dirty Dependency Set
- `graphics.bake_dependencies`
- `graphics.capture_evaluation`
- `graphics.refine_direction`
- Bakeの別承認
- Capture時の一時Editor状態復元
- Human ReviewなしのVisual Acceptance禁止

## Phase 5: Domain expansion — PENDING

実際のGoalと不足Capabilityに応じて選択する。

- UnityCinematicMCP
- LiveCreator / MovieCreator実行化
- UnityProfilerMCP
- UnityUIMCP
- UnityAddressablesMCP
- UnityBuildMCP

空Package、空Backend、利用実績のないInterfaceを先に追加しない。
