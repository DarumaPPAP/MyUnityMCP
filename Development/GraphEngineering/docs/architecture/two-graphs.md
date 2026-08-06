# Two Separate Graphs

## A. Codex Implementation Graph

目的:

CodexがMyUnityMCP RepositoryをPhase 0〜12まで実装する。

主なNode:

- Harness Bootstrap
- Repository Inspection
- Product Phase implementation
- Unity Compile/Test
- Independent validation
- Human approval
- Release Gate

State:

- Repository revision
- Current checkpoint
- Evidence
- Test results
- Decisions
- Blockers
- Human approvals

Owner:

Repository-level development Harness。

## B. MyUnityMCP Product Runtime Graph

目的:

完成したUnityAgentMCPが、MCP ClientのGoalをDomain MCP／Creatorへ分解・実行する。

主なNode:

- Intent compile
- Domain selection
- Tool activation
- Inspect
- Plan
- Human approval
- Apply
- Save／Bake／Build
- Capture／Profile
- Human review
- Refine

State:

- Unity Project／Scene revision
- Plan ID
- Approval token
- Dirty dependencies
- Execution history
- Evidence

Owner:

Phase 1で実装するUnityAgentMCP Runtime。

## Prohibited conflation

- UnityAgentMCP RuntimeをCodexの開発Harnessとして使わない
- CodexのPhase LoopをWorldCreator Runtimeへ入れない
- Codex Roadmap StateをUnity Project内Runtime Stateへ流さない
- Product Project dataをRepository docsへ自動転記しない
