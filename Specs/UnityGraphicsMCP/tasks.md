# UnityGraphicsMCP Tasks

- TaskPlanVersion: `2.3.0`
- CurrentPhase: `Phase 2 Planning`
- ImplementationStatus: `Phase 1 Read-only Operational Complete`

## Status legend

- `DONE`: Source、Unity Compile、必要な実行検証を含む完了条件を満たした。
- `SOURCE_DONE`: Sourceは存在するがUnity Compileまたは実行検証前。
- `PENDING`: 未着手または前提Gate待ち。
- `BLOCKED`: 必須依存または承認待ち。

## Phase 0: Governance and repository bootstrap

### UGMCP-000-001 Repository policy — DONE

- UnityAgent、MyUnityMCP、対象Unity Projectの所有境界を定義。
- 特定Project環境をMyUnityMCP全体へ固定しない。

### UGMCP-000-002 Catalog — DONE

- MCP / Creator / Capability Catalogを定義。
- 未選択ManifestとSourceの非読込を規定。

### UGMCP-000-003 Control Plane specification — DONE

- Creator、Domain MCP、Capability Moduleの責務を分離。
- Tool Group GateとProject事実の優先順位を定義。

### UGMCP-000-004 Graphics specification — DONE

- `spec.md`
- `plan.md`
- `editor-tool-design.md`
- `tasks.md`

### UGMCP-000-005 Creator workflows — DONE

- LiveCreator / MovieCreatorを`specified_not_executable`で定義。

### UGMCP-000-006 Package skeleton — DONE

- `package.json`
- `MCP_MANIFEST.yaml`
- Documentation

### UGMCP-000-007 Routing tests — DONE

### UGMCP-000-008 Compatibility matrix — DONE

- Phase 1 Unity CI実績をEnvironment Entryとして記録。
- 一つの検証実績をPackage全体の固定対応条件として扱わない。

## Phase 1A: Bridge and project environment inspection

### UGMCP-010-001 Unity MCP API confirmation — DONE

確認・検証済み:

- Package: `com.coplaydev.unity-mcp`
- 宣言API基準: `10.1.2`
- Unity検証Commit: `9f84072c38906e3ca903f14f6a8edc1a1c9012c3`
- Assembly: `MCPForUnity.Editor`
- Tool Attribute: `McpForUnityToolAttribute`
- Parameter Attribute: `ToolParameterAttribute`
- Command Entry: `HandleCommand(JObject)`
- Responses: `SuccessResponse` / `ErrorResponse`
- Command Registry Discovery: PASS
- Direct Handler Invocation: PASS

外部MCP ClientからのNetwork接続はPhase 1 CI対象外で、未検証。

### UGMCP-010-002 Package assembly — DONE

- Editor Assembly Compile: PASS
- EditMode Test Assembly Compile: PASS
- Bridge / Newtonsoft / Test Framework依存解決: PASS
- Unity Package metadata / GUID固定: DONE

### UGMCP-010-003 Project environment resolution — DONE

Tool:

- `graphics.inspect_project`

実装済み出力:

- Unity Version
- Active Build Target
- Installed Build Targets
- Graphics API
- Scripting Backend
- Pipeline Kind / Asset / Package Version
- Renderer Data / Feature Count
- Rendering PathのRead-only推定
- RenderGraph ModeのRead-only推定
- Loaded Scene
- Relevant Package
- Detected Project Facts
- Requested Target / Constraint

Detected ProjectとRequested Targetの分離をEditMode Testで確認済み。

### UGMCP-010-004 Capability and backend selection — DONE

- Pipeline Package型へ直接固定依存しないRead-only検出。
- 別PipelineへのSilent Fallback禁止。
- `UNVERIFIED`と`UNSUPPORTED`を分離。
- 二つ目の実在Backendがないため共通Backend Interfaceを作らない。

### UGMCP-010-005 Editor session — DONE

- Session ID
- Revision
- In-memory Snapshot
- Snapshot TTL / Count上限
- Cursor Paging
- Hierarchy / Project / Undo / Scene EventによるRevision更新
- Compile / Domain Reload / Play Mode遷移 / Editor終了時の無効化
- Read-only Dirty Guard

MCP BridgeのMain Thread Command DispatchをSourceとUnity実行で確認済み。Worker Thread Queueは実際の必要性が確認されるまで追加しない。

## Phase 1B: Scene inspection and validation

### UGMCP-011-001 Read-only scene inspection — DONE

Tool:

- `graphics.inspect_scene`

実装済みSection:

- Camera
- Light
- Lightmap / LightingDataAsset
- Light Probe / Light Probe Proxy Volume
- Reflection Probe
- Renderer / Shared Material / Shader Summary
- Volume Capability
- Decal Capability
- Probe Volume Capability
- Particle System
- VFX Graph Capability
- PlayableDirector
- Cinemachine Capability
- Renderer Feature

安全検証:

- Scene Dirty非変更: PASS
- Persistent Asset Dirty非変更: PASS
- Renderer Material非インスタンス化: PASS
- Snapshot Cursor範囲外拒否: PASS

### UGMCP-011-002 Validate scene — DONE

Tool:

- `graphics.validate_scene`

実装済みRule:

- Missing Shared Material
- Missing Shader
- Lightmap Index範囲外
- Enabled VolumeのShared Profileなし
- Lightmapあり / LightingDataAsset未確認のHeuristic
- URP Renderer Data解決失敗

Lightmap Index範囲外RuleをEditMode Testで確認済み。

### UGMCP-011-003 EditMode tests — DONE

Unity `6000.0.75f1`のGitHub Actions環境で実行。

- Total: 9
- Passed: 9
- Failed: 0
- Skipped: 0
- Inconclusive: 0

検証内容:

- 3 ToolのBridge Discovery
- Default Disable契約
- Bridge Handler Invocation
- Scene Dirty Guard
- Persistent Asset Dirty Guard
- Detected Project / Requested Target分離
- Camera / Light Inspection
- Renderer Material非インスタンス化
- Snapshot Cursor検証
- Lightmap Validation

### UGMCP-011-004 Verification matrix update — DONE

Evidence:

- Workflow Run: `30909837287`
- Job: `91993536157`
- Artifact: `MyUnityMCP-Phase1-Unity-Evidence`
- Artifact ID: `8892693112`

PlayerとTarget DeviceはEditor-only Phase 1の完了条件外であり、未実行と明示する。

## Phase 1 completion gate — PASSED

1. Package dependency解決 — PASS
2. Unity Editor Compile — PASS
3. 3 ToolのBridge Discovery — PASS
4. `graphics.inspect_project` Bridge Invocation — PASS
5. `graphics.inspect_scene`実行 — PASS
6. `graphics.validate_scene`実行 — PASS
7. EditMode Test成功 — 9 / 9 PASS
8. Read-only Dirty Guard成功 — PASS
9. Compatibility Matrix Environment Entry追加 — DONE

## Phase 2: Planning — PENDING

### UGMCP-020-001 Visual Intent

参考画像と自然言語をPipeline非依存Intentへ変換する。

### UGMCP-020-002 Compile direction

Tool: `graphics.compile_direction`

- Lighting / GI / Reflection / Atmosphere / Look / Platform Plan
- Range / Reason / Dependency / Confidence / Verification Level

### UGMCP-020-003 Preview plan

Tool: `graphics.preview_plan`

Unity状態を変更せず、Created / Modified / Dirty / Bake Required / Unsupported / Unverifiedを返す。

## Phase 3: Mutation — PENDING

- Transaction Contract
- `graphics.apply_plan`
- `graphics.undo_transaction`
- Automatic Save禁止
- Bakeを同時実行しない

## Phase 4: Bake and capture — PENDING

- Dirty Dependency
- `graphics.bake_dependencies`
- `graphics.capture_evaluation`
- `graphics.refine_direction`
- Human ReviewなしのVisual Acceptance禁止

## Phase 5: Domain expansion — PENDING

優先順位は実際の利用Goalと不足Capabilityから決める。

候補:

- UnityCinematicMCP
- LiveCreator / MovieCreator実行化
- UnityProfilerMCP
- UnityUIMCP
- UnityAddressablesMCP
- UnityBuildMCP

空Packageや空Backendを先に追加しない。
