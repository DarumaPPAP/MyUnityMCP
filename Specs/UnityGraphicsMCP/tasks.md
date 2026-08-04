# UnityGraphicsMCP Tasks

- TaskPlanVersion: `2.2.0`
- CurrentPhase: `Phase 1 Verification`
- ImplementationStatus: `Read-only Source Complete / Unity Unverified`

## Status legend

- `DONE`: Sourceまたは仕様の完了条件を満たした。
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

Environment EntryはUnity検証を実行するまで空とする。

## Phase 1A: Bridge and project environment inspection

### UGMCP-010-001 Unity MCP API confirmation — DONE

確認基準:

- Package: `com.coplaydev.unity-mcp`
- Version: `10.1.2`
- Assembly: `MCPForUnity.Editor`
- Tool Attribute: `McpForUnityToolAttribute`
- Parameter Attribute: `ToolParameterAttribute`
- Command Entry: `HandleCommand(JObject)`
- Responses: `SuccessResponse` / `ErrorResponse`

未確認:

- 対象ProjectでのPackage解決
- Tool Discovery実行
- MCP ClientからのInvocation

### UGMCP-010-002 Package assembly — SOURCE_DONE

追加済み:

- `Editor/MyUnityMcp.Editor.asmdef`
- `Tests/Editor/MyUnityMcp.Editor.Tests.asmdef`
- MCP Bridge / Newtonsoft / Test Framework依存

Acceptance残件:

- 対象Unity ProjectでCompile
- Package dependency解決

### UGMCP-010-003 Project environment resolution — SOURCE_DONE

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

Acceptance残件:

- Unity Compile
- 実Projectでの出力確認
- Version差異確認

### UGMCP-010-004 Capability and backend selection — SOURCE_DONE

現在のPhase 1はPipeline Package型へ直接依存せず、Project検出とSerialized Capability Inspectionを行う。

- 別PipelineへSilent Fallbackしない。
- 不明値は`UNKNOWN`または未検証状態として返す。
- 二つ目の実在Backendがないため共通Backend Interfaceは作成しない。

### UGMCP-010-005 Editor session — SOURCE_DONE

実装済み:

- Session ID
- Revision
- In-memory Snapshot
- Snapshot TTL / Count上限
- Cursor Paging
- Hierarchy / Project / Undo / Scene EventによるRevision更新
- Compile / Domain Reload / Editor終了時の無効化
- Read-only Dirty Guard

制限:

- MCP BridgeがMain ThreadでCommandを実行する前提。
- Worker Thread Queueは、実際に非Main Thread Callが確認された場合に追加する。

## Phase 1B: Scene inspection and validation

### UGMCP-011-001 Read-only scene inspection — SOURCE_DONE

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

安全方針:

- `Renderer.sharedMaterials`のみ使用。
- `Volume.sharedProfile`相当のみRead-only取得。
- Serialized Propertyへ書き込まない。
- SnapshotへUnityEngine.Objectを保持しない。

### UGMCP-011-002 Validate scene — SOURCE_DONE

Tool:

- `graphics.validate_scene`

実装済みRule:

- Missing Shared Material
- Missing Shader
- Lightmap Index範囲外
- Enabled VolumeのShared Profileなし
- Lightmapあり / LightingDataAsset未確認のHeuristic
- URP Renderer Data解決失敗

返却契約:

- Rule ID
- Invariant / Policy / Heuristic
- Severity
- Confidence
- Affected Object ID
- Evidence

### UGMCP-011-003 EditMode tests — SOURCE_DONE

追加済みTest Source:

- `inspect_project`のDirty非変更
- Camera / Light Inspection
- Renderer Material非インスタンス化
- Lightmap Index範囲外検出
- Snapshot Cursor範囲外拒否

実行状態:

- Not Run

### UGMCP-011-004 Verification matrix update — PENDING

対象Unity Projectで次を別Gateとして記録する。

- Editor Compile
- Bridge Tool Discovery
- EditMode
- Player
- Target Device

## Phase 1 completion gate

以下がすべて通るまでPhase 1をOperational Completeとしない。

1. Package dependency解決
2. Unity Editor Compile
3. 3 ToolのBridge Discovery
4. `graphics.inspect_project`実行
5. `graphics.inspect_scene`実行
6. `graphics.validate_scene`実行
7. EditMode Test成功
8. Read-only Dirty Guard成功
9. Compatibility Matrix Environment Entry追加

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
