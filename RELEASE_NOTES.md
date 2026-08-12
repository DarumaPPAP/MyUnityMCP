# MyUnityMCP v1.1.0 Release Candidate Notes

MyUnityMCP `1.1.0`は、32 Graphics ToolにUnityAgentMCP Control Plane 10 Toolを追加した**次期Source Version**です。現時点ではGitHub Releaseとして未公開であり、この文書はRelease Candidateの状態を記録します。

## Highlights

- Production Tool Surface: **42 = 32 Graphics + 10 Agent**
- UnityAgentMCP Control PlaneをOperational Capabilityへ昇格
- `Inspect Capabilities → Validate Workflow → Compile Graph → Preview → Approval → Domain Delegate → Status / Cancel / History`を提供
- UnityAgentはUnity APIを直接Mutationせず、現在は`unity_graphics_mcp`へ処理を委譲
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

未完了Evidence:

- **Current exact 42 Tool Production CI**: GitHub Actions JobがRunner Step開始前にFailureしているため`not_verified`
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
- Production Operational: `unity_graphics_mcp`, `unity_agent_mcp`
- Production Tools: 42
- Player / Target Device上でのTool実行は対象外
- Built-in PipelineのAPV Bakeは非対応
- URP / HDRPのAPV BakeはProject固有Baking Set / Backend条件に依存

## Next production capability

Graph Engineeringの残りDevelopment候補は49 Toolです。次のCapability-scoped Delivery候補は`WorldCreator`とし、必ず最新`main`から`delivery/world-creator`を作成して昇格します。
