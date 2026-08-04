# UnityGraphicsMCP Tasks

- TaskPlanVersion: `2.1.0`
- CurrentPhase: `Phase 0`
- ImplementationStatus: `Not Started`

## Phase 0: Governance and repository bootstrap

### UGMCP-000-001 Repository policy

- `AGENTS.md`を追加する。
- UnityAgent、MyUnityMCP、対象Unity Projectの所有境界を定義する。
- 特定Project環境をMyUnityMCP全体へ固定しない。

Acceptance:

- MCP本体の正本がMyUnityMCPである。
- Project固有生成AssetをMyUnityMCPへ保存しない。
- Unity Version、Pipeline、Rendering Path、Platformは対象Projectから解決する。

### UGMCP-000-002 Catalog

- `mcp-catalog.yaml`
- `creator-catalog.yaml`
- `capability-catalog.yaml`

Acceptance:

- Primary CreatorまたはDomain MCPを一つ選択できる。
- Conditional Domain MCP上限が定義される。
- 未選択ManifestとSourceの非読込が明記される。

### UGMCP-000-003 Control Plane specification

- `Specs/UnityAgentMCP/spec.md`

Acceptance:

- Creator、Domain MCP、Capability Moduleの責務が分離される。
- Tool Group Gateが定義される。
- Project事実とRequested Targetの優先順位が定義される。

### UGMCP-000-004 Graphics specification

- `spec.md`
- `plan.md`
- `editor-tool-design.md`
- `tasks.md`

Acceptance:

- Project Environment Resolutionが明示される。
- 特定Unity Version、Pipeline、Rendering Path、PlatformがGlobal Contractではない。
- 空Backendを要求しない。
- NamespaceとEnum命名がUnityAgent規約に一致する。

### UGMCP-000-005 Creator workflows

- LiveCreator
- MovieCreator

Acceptance:

- CreatorはDomain MCPを要求するだけでUnity APIを直接操作しない。
- Workflowは`specified_not_executable`であり、実装済みと表現しない。

### UGMCP-000-006 Package skeleton

- `package.json`
- `MCP_MANIFEST.yaml`
- Documentation

Acceptance:

- C# Tool未実装が明記される。
- Packageを導入してもUnity機能を変更しない。
- Package Manifestに固定Project環境を持たない。

### UGMCP-000-007 Routing tests

Acceptance:

- Live制作、Graphics Inspection、Shader-only、Mutation、Bake、Source Read Guardを検証するCaseがある。

### UGMCP-000-008 Compatibility matrix

- `Tests/Compatibility/verification-matrix.yaml`

Acceptance:

- 検証実績とGlobal Support Contractが分離される。
- Empty Matrixを未検証として扱う。
- `UNVERIFIED`と`UNSUPPORTED`を区別する。

## Phase 1A: Bridge and project environment inspection

### UGMCP-010-001 Unity MCP API confirmation

- 使用するUnity MCP Bridge Versionを導入先Projectから確定する。
- Tool登録API、Schema、Image Result、Main Thread Contractを公式Sourceで確認する。

Stop:

- Package VersionまたはAPIが未確定。

### UGMCP-010-002 Package assembly

- Editor-only asmdefを必要最小限で作成する。
- Runtime Assemblyは作らない。
- 導入先ProjectのUnity VersionとPackage依存を検出してAssembly参照を確定する。

Acceptance:

- 導入先の検証ProjectでCompileできる。
- MCP Bridge未導入時のPackage依存方針が明確である。
- 一つの検証Projectの成功をPackage全体の対応保証としない。

### UGMCP-010-003 Project environment resolution

Tool:

- `graphics.inspect_project`

Output:

- Unity Version
- Pipeline Kind / Package Version
- Active Renderer / Rendering Path
- RenderGraph Mode
- Active / Installed Build Target
- Graphics API
- Scripting Backend
- Related Package Presence
- Capability Summary
- Detected Project Facts
- Requested Target

Acceptance:

- Project / AssetをDirtyにしない。
- ProfileやPreferenceで検出済みProject事実を上書きしない。
- Unknown Factを推測しない。
- `UNSUPPORTED`と`UNVERIFIED`を区別する。

### UGMCP-010-004 Capability and backend selection

Acceptance:

- Project Inspection後にBackendを選択する。
- 実装済みBackendだけを公開する。
- Backend未実装時は`BACKEND_NOT_IMPLEMENTED`を返す。
- 別Pipelineへ黙ってFallbackしない。

### UGMCP-010-005 Editor session

- Main Thread Queue
- Session ID
- Revision
- Snapshot
- Cancellation
- Domain Reload / Compile / PlayMode遷移中断

Acceptance:

- Worker ThreadからのTool CallをMain Threadへ移す。
- 古いSnapshotを`SESSION_EXPIRED`または`STALE_SNAPSHOT`として拒否する。

## Phase 1B: First concrete backend and scene inspection

### UGMCP-011-001 First concrete backend

- 利用可能な検証Projectで検出されたPipelineへ対応する。
- 最初のBackendを仕様上固定しない。

Acceptance:

- Backend固有Package依存がPipeline非依存Coreへ漏れない。
- 実際の検証環境をCompatibility Matrixへ記録する。
- 一つのBackendしかない段階で共通Pipeline Interfaceを作らない。

### UGMCP-011-002 Inspect scene

Tool:

- `graphics.inspect_scene`

Scope:

- Camera
- Light
- Lightmap
- Light Probe
- Pipeline対応時のProbe Volume
- Reflection Probe
- Material Summary
- Decal Capability
- Particle
- Volume / Post Process Capability
- Renderer Feature / Custom Pass Capability
- Timeline / Cinemachine read-only state

Acceptance:

- SceneをDirtyにしない。
- 大きな結果はSnapshot IDで参照する。
- 未対応CapabilityをFailureとして捏造しない。

### UGMCP-011-003 Validate scene

Tool:

- `graphics.validate_scene`

Acceptance:

- Severity、Evidence、Confidence、Affected Object IDを返す。
- Invariant、Policy、Heuristicを区別する。
- 未確認事項をFailureとして捏造しない。

### UGMCP-011-004 EditMode tests

- Read-only Dirty Guard
- Missing Object
- Duplicate Name
- Unsupported Capability
- Unverified Environment
- Project Profile Override Guard
- Domain Reload

### UGMCP-011-005 Verification matrix update

Acceptance:

- Editor Compile、EditMode、Player、Target Deviceを別Gateで記録する。
- 未実行GateをPassedと記録しない。

## Phase 2: Planning

### UGMCP-020-001 Visual Intent

- 参考画像と自然言語をPipeline非依存Intentへ変換する。

### UGMCP-020-002 Compile direction

Tool:

- `graphics.compile_direction`

Acceptance:

- Lighting / GI / Reflection / Atmosphere / Look / Platform Planを返す。
- 推奨値にRange、Reason、Dependency、Confidence、Verification Levelを含める。

### UGMCP-020-003 Preview plan

Tool:

- `graphics.preview_plan`

Acceptance:

- Created / Modified / Dirty / Bake Required / Unsupported / Unverified / Fallbackを返す。
- Unity状態を変更しない。

## Phase 3: Mutation

### UGMCP-030-001 Transaction contract

- Plan ID
- Expected Revision
- Diff
- Undo
- Save Mode

### UGMCP-030-002 Apply plan

Tool:

- `graphics.apply_plan`

Acceptance:

- 承認済みPlanだけを適用する。
- Automatic Saveしない。
- Bakeを同時実行しない。

### UGMCP-030-003 Undo transaction

Tool:

- `graphics.undo_transaction`

Acceptance:

- 対象Transactionだけを戻す。

## Phase 4: Bake and capture

### UGMCP-040-001 Dirty dependency

- Lightmap
- Light Probe
- Pipeline固有Probe Volume
- Reflection Probe
- Capture

### UGMCP-040-002 Bake dependencies

Tool:

- `graphics.bake_dependencies`

Acceptance:

- 別承認を要求する。
- 無条件の全Bakeを行わない。
- Pipeline APIが部分Bake非対応の場合は明示する。

### UGMCP-040-003 Capture evaluation

Tool:

- `graphics.capture_evaluation`

Acceptance:

- 一時状態を復元する。
- Capture生成をVisual Acceptanceにしない。

### UGMCP-040-004 Refine direction

Tool:

- `graphics.refine_direction`

Acceptance:

- 修正Planを返す。
- 自動適用しない。

## Phase 5: Domain expansion

優先順位は実際の利用Goalと不足Capabilityから決める。

候補:

- UnityCinematicMCP
- LiveCreator / MovieCreator実行化
- UnityProfilerMCP
- UnityUIMCP
- UnityAddressablesMCP
- UnityBuildMCP

空Packageや空Backendを先に追加しない。
