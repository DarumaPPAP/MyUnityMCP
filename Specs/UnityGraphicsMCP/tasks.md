# UnityGraphicsMCP Tasks

- TaskPlanVersion: `2.0.0`
- CurrentPhase: `Phase 0`
- ImplementationStatus: `Not Started`

## Phase 0: Governance and repository bootstrap

### UGMCP-000-001 Repository policy

- `AGENTS.md`を追加する。
- UnityAgent、MyUnityMCP、対象Unity Projectの所有境界を定義する。

Acceptance:

- MCP本体の正本がMyUnityMCPである。
- Project固有生成AssetをMyUnityMCPへ保存しない。

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

### UGMCP-000-004 Graphics specification

- `spec.md`
- `plan.md`
- `tasks.md`

Acceptance:

- URP-first範囲が明示される。
- Built-in / HDRP空実装を要求しない。
- NamespaceとEnum命名がUnityAgent規約に一致する。

### UGMCP-000-005 Creator workflows

- LiveCreator
- MovieCreator

Acceptance:

- CreatorはDomain MCPを要求するだけでUnity APIを直接操作しない。
- Workflowは`status: specified`であり、実装済みと表現しない。

### UGMCP-000-006 Package skeleton

- `package.json`
- `MCP_MANIFEST.yaml`
- Documentation

Acceptance:

- C# Tool未実装が明記される。
- Packageを導入してもUnity機能を変更しない。

### UGMCP-000-007 Routing tests

Acceptance:

- Live制作、Graphics Inspection、Shader-only、Mutation、Bake、Source Read Guardを検証するCaseがある。

## Phase 1: Read-only package implementation

### UGMCP-010-001 Unity MCP API confirmation

- 使用するUnity MCP Bridge Versionを確定する。
- Tool登録API、Schema、Image Result、Main Thread Contractを公式Sourceで確認する。

Stop:

- Package VersionまたはAPIが未確定。

### UGMCP-010-002 Package assembly

- Editor-only asmdefを必要最小限で作成する。
- Runtime Assemblyは作らない。

Acceptance:

- Unity 6000.3でCompileできる。
- MCP Bridge未導入時のPackage依存方針が明確である。

### UGMCP-010-003 Inspect project

Tool:

- `graphics.inspect_project`

Output:

- Unity Version
- Pipeline / Package Version
- Renderer
- Platform
- Capability Summary

Acceptance:

- Project / AssetをDirtyにしない。

### UGMCP-010-004 Inspect scene

Tool:

- `graphics.inspect_scene`

Scope:

- Camera
- Light
- Lightmap
- Light Probe
- APV
- Reflection Probe
- Material Summary
- Decal
- Particle
- Volume
- RendererFeature
- Timeline / Cinemachine read-only state

Acceptance:

- SceneをDirtyにしない。
- 大きな結果はSnapshot IDで参照する。

### UGMCP-010-005 Validate scene

Tool:

- `graphics.validate_scene`

Acceptance:

- Severity、Evidence、Confidence、Affected Object IDを返す。
- 未確認事項をFailureとして捏造しない。

### UGMCP-010-006 EditMode tests

- Read-only Dirty Guard
- Missing Object
- Duplicate Name
- Unsupported Capability
- Domain Reload

## Phase 2: Planning

### UGMCP-020-001 Visual Intent

- 参考画像と自然言語をPipeline非依存Intentへ変換する。

### UGMCP-020-002 Compile direction

Tool:

- `graphics.compile_direction`

Acceptance:

- Lighting / GI / Reflection / Atmosphere / Look / Platform Planを返す。
- 推奨値にRange、Reason、Dependency、Confidenceを含める。

### UGMCP-020-003 Preview plan

Tool:

- `graphics.preview_plan`

Acceptance:

- Created / Modified / Dirty / Bake Required / Unsupported / Fallbackを返す。
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
- APV
- Reflection Probe
- Capture

### UGMCP-040-002 Bake dependencies

Tool:

- `graphics.bake_dependencies`

Acceptance:

- 別承認を要求する。
- 無条件の全Bakeを行わない。

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

優先順位:

1. UnityCinematicMCP
2. LiveCreator / MovieCreator実行化
3. UnityProfilerMCP
4. UnityUIMCP
5. UnityAddressablesMCP
6. UnityBuildMCP

空Packageや空Backendを先に追加しない。
