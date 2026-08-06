# Active ExecPlan — MyUnityMCP Phase 1〜12 Development Modules

## Goal

Phase 1〜12の製品Source、Tool Contract、Safety Guard、Tests、Documentation、Compatibility Matrix、External E2E Harness、Release Candidate Gateを`feature/graph-engineering-master`へ構築する。

## Current state

- Bootstrap Harness: complete
- Phase 0 Release Integrity: complete
- Phase 1 UnityAgent Runtime: running, Unity CI pending
- Phase 2〜11: source implementation present, validation blocked by Phase 1 and Unity CI
- Phase 12: hardening artifacts present, release candidate gates pending
- Terminal Goal: false

## Implemented source

### Control Plane

- `Packages/com.darumappap.my-unity-mcp/Editor/UnityAgentMcpCatalog.json`
- `UnityAgentMcpTools.cs`
- `UnityAgentMcpRuntime.cs`

### Shared safety

- `UnityDomainMcpCommon.cs`
- `UnityMcpSecurityPolicy.cs`

### Creator runtimes

- `UnityWorldCreatorMcp.cs`
- `UnityMovieCreatorMcp.cs`
- `UnityLiveCreatorMcp.cs`

### Domain runtimes

- `UnityProfilerMcp.cs`
- `UnityBuildMcp.cs`
- `UnityAddressablesMcp.cs`
- `UnityUiMcp.cs`
- `UnityAnimationMcpTools.cs`
- `UnityAnimationMcpRuntime.cs`
- `UnityAudioMcp.cs`
- `UnityCinematicMcp.cs`

## Implemented tests

- UnityAgent runtime contracts: 10
- Cross-domain safety contracts: 8 minimum, 9 without Addressables
- Creator contracts: 5
- Security mode contracts: 5
- Fault injection contracts: 5
- Existing verified Graphics contracts: 125
- Expected Unity EditMode minimum: 158
- Expected without Addressables: 159

## Implemented machine gates

- Release Integrity Guard
- Artifact-only Delivery Guard
- Architecture Lint
- Graph／State Harness validation
- Unity Editor CI evidence writer
- External MCP HTTP client E2E Harness
- Release Candidate Gate

## Validation pending

1. GitHub Actions or local Unity 6000.0.75f1 compile
2. EditMode Test 158件以上
3. Candidate Tool Discovery 91件
4. Existing GraphicsMCP regression
5. External MCP Client E2E
6. Addressables package-present matrix
7. Desktop player Build evidence
8. Platform-holder environment evidence where required
9. World／Movie Human Visual Review
10. Live Operator Review
11. Release version approval
12. Artifact-only delivery branch validation

## Failure policy

Compile Error、Test失敗、Tool Discovery不一致があれば、最初の具体的なErrorをCurrent Observationとして記録し、Phase 1を完了扱いにしない。

Optional Packageなし、Public API不足、Platform holder環境不足は`UNSUPPORTED`、`BACKEND_NOT_IMPLEMENTED`、`UNVERIFIED`へ分離する。

## Delivery policy

Graph Engineering Branchを`main`へMergeしない。
検証・人間承認後、最新`main`から`delivery/<capability>`を作り、Manifestに列挙した製品成果物だけを移植する。

PR作成とMergeは別の明示承認を必要とする。
