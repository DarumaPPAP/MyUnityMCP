# MyUnityMCP v1.1.0 Release Candidate Notes

MyUnityMCP `1.1.0`は、32 Graphics ToolにUnityAgentMCP Control Plane 10 ToolとWorldCreator 3 Toolを追加する**次期Source Version**です。現時点ではGitHub Releaseとして未公開であり、この文書はRelease Candidateの状態を記録します。

## Highlights

- Production Tool Surface target: **45 = 32 Graphics + 10 Agent + 3 WorldCreator**
- UnityAgentMCP Control PlaneをOperational Capabilityへ昇格
- `Inspect Capabilities → Validate Workflow → Compile Graph → Preview → Approval → Domain Delegate → Status / Cancel / History`を提供
- WorldCreatorは`Visual Goal → Read-only Graphics Preflight → Human Review Handoff`を提供
- WorldCreator canonical preflightは`graphics.inspect_project` → `graphics.inspect_scene` → `graphics.validate_scene`
- UnityAgent／WorldCreatorはUnity APIを直接Mutationせず、現在は`unity_graphics_mcp`へ処理を委譲
- Graphics側のApproval / Revision / Save / Bake / Capture / Human Review境界は維持
- Tool CountはManifestからRelease Contractへ導出し、Capability追加時の固定値依存を除去
- Release PublicationをSource Version変更から分離し、`workflow_dispatch`または明示Publish承認だけで起動

## Verification status

検証済みEvidence:

- Graphics Production baseline: Unity `6000.0.75f1` / 32 Tool / Editor Contracts
- Compatibility: Unity `6000.4.12f1` / `6000.5.5f1`
- Unity `6000.7.0a2`: Manual Package Import / Compile / Graphics Tool Discovery / Compatibility確認
- UnityAgent source: Graph Engineering Run #52でUnity `6000.0.75f1` / `6000.4.12f1` / `6000.5.5f1` Contract PASS
- Unity `6000.7.0a2` Graph Engineering Manual CanaryでAgentを含む91/91 Combined Discovery確認
- Stage 0 exact 42 Tool Production baseline: Unity `6000.7.0a2`実EditorでGraphics Read-only、Agent Orchestration、Approval、Light Mutation、Normal Undoまで`integration_verified_manual`

未完了Evidence:

- **Current exact 42 Tool Production CI**: GitHub Actions JobがRunner Step開始前にFailureしているため`not_verified`
- **WorldCreator 45 Tool Delivery**: Source / Manifest / Contract / DocsはDelivery Branchへ移植済み。実Editor Tool DiscoveryとWorldCreator E2EはPromotion前Gateとして未実施
- Full protocol-level External MCP E2E: final Production Hardeningで実施
- Player / Target Device Tool Execution: unsupported / not_verified

Runner未実行をTechnical PASSへ読み替えません。

## Release control

`VERSION` / Package / ManifestはSource Versionとして`1.1.0`へ揃えますが、**この変更だけではTagまたはGitHub Releaseを作成しません**。

Release Publicationは別の明示操作で行います。

```text
Source Version 1.1.0
        ↓
Release Candidate verification
        ↓
Explicit human publication approval
        ↓
workflow_dispatch / approved publish command
        ↓
v1.1.0 Tag + GitHub Release
```

公開済みTagはimmutableです。既存Tagの移動・削除・Force更新は行いません。

## Current scope

- Unity Editor専用
- Minimum Unity: `6000.0`
- Production Operational baseline: `unity_graphics_mcp`, `unity_agent_mcp`
- Current Delivery Candidate: `world_creator`
- Production Tools after WorldCreator promotion: 45
- Player / Target Device上でのTool実行は対象外
- Built-in PipelineのAPV Bakeは非対応
- URP / HDRPのAPV BakeはProject固有Baking Set / Backend条件に依存

## Next production capability

`delivery/world-creator`の実Editor Gateが完了した後、次のCapability-scoped Delivery候補は`Profiler`です。Graph Engineering branchを一括Mergeせず、必ずその時点の最新`main`から個別Delivery Branchを作成します。
