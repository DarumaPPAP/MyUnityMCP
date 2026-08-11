# UnityAgentMCP Operational Contract

## Status

- Product state: `Editor Operational` on current main delivery candidate
- Kind: Control Plane
- Direct Unity mutation: prohibited
- Current operational delegate domain: `unity_graphics_mcp`
- Tool count: 10
- Player runtime execution: unsupported

`v1.0.0` Tag is the immutable pre-Agent release baseline. This specification describes the capability being promoted to current `main`; Version／Tag Publication is a separate release operation.

## Responsibility

UnityAgentMCP owns workflow-level coordination only. It validates requested steps, compiles a dependency graph, previews execution scope, enforces approval/revision boundaries, delegates each executable step to an operational Domain MCP, and records execution status/history.

UnityAgentMCP does **not** own Unity object mutation logic and must not call arbitrary Unity mutation APIs as a substitute for a Domain MCP.

## Tool surface

- `agent.inspect_capabilities`
- `agent.validate_workflow`
- `agent.compile_graph`
- `agent.preview_execution`
- `agent.submit_approval`
- `agent.start_execution`
- `agent.get_execution_status`
- `agent.cancel_execution`
- `agent.get_execution_history`
- `agent.get_error_catalog`

All tools use `AutoRegister = false` and require explicit client/bridge activation.

## Workflow contract

```text
inspect capabilities
→ validate workflow
→ compile graph against current Editor revision
→ preview exact ordered steps and approval groups
→ submit explicit approval when required
→ start cooperative execution
→ delegate one safe step at a time
→ status / cancel / history
```

A workflow step declares:

- stable `stepId`
- `domainId`
- exact MCP `toolName`
- `toolGroup`
- dependencies (`dependsOn`)
- tool parameters

Validation rejects duplicate/missing Step IDs, missing dependencies, dependency cycles, undeclared tools/groups, non-operational domains, and any Domain catalog entry that would permit direct Control Plane mutation.

## Revision and approval safety

- Graph compile is bound to `Session.Revision`.
- Preview/start reject a graph after Editor Revision changes.
- Mutation-capable groups require explicit approval before execution.
- Approval tokens are time-limited and scoped to the compiled graph/groups.
- A delegated read-only step that unexpectedly changes Editor Revision interrupts execution.
- A successful approved mutation updates the expected revision before the next step.

## Cooperative execution

Execution advances at safe step boundaries rather than recursively running an unbounded workflow.

Supported controls:

- timeout (1–3600 seconds, default 60)
- cancellation before the next delegated step
- client-disconnect interruption
- structured terminal state
- persistent execution history

Automatic resume after reload/restart/disconnect is prohibited.

## Delegation boundary

Current production Agent execution delegates only to registered `unity_graphics_mcp` handlers. Domains that are present only as design/candidate metadata remain non-operational and are rejected during workflow validation.

Promoting another Domain does not automatically make it executable through Agent. Its production catalog state, Agent runtime catalog entry, delegate registration, tests, documentation, and safety contract must be promoted together.

## Result integrity

A delegated failure must not be represented as success.

- first-step failure → `FAILED`
- failure after earlier successful steps → `PARTIAL`
- revision/timeout/disconnect interruption → `INTERRUPTED`
- cancellation → `CANCELLED`
- all steps completed successfully → `SUCCEEDED`

Structured errors expose an error code and retryability contract. `unavailable` or non-operational states are never converted to PASS.

## Evidence

Capability verification is based on:

- Graph Engineering Run #52 automated EditMode contracts for unchanged Agent source on Unity 6000.0.75f1 / 6000.4.12f1 / 6000.5.5f1;
- Unity 6000.7.0a2 manual Git-package compile/recognition and combined tool-discovery canary;
- `UnityAgentMcpRuntimeTests` shipped with the capability;
- production delivery diff review from latest main.

Current Actions infrastructure may fail before runner steps start. Such runner-side failures remain explicitly distinct from Unity/contract test failures.
