# Codex Development Harness Requirements

## Definition

CodexとMyUnityMCP Repository／Unity Editor／GitHubの間を媒介し、
Context、Tool、権限、State、Observation、Validation、Evidence、Completionを扱う実行基盤。

`AGENTS.md`やMaster PromptだけではHarnessにならない。

## Required components

### 1. Repository map

- Short AGENTS.md
- Structured docs index
- Phase specs
- Active ExecPlan
- Machine-readable Graph／State

### 2. Environment preflight

- Current branch／revision
- Dirty worktree
- Unity executable availability
- Required package availability
- GitHub authentication when needed
- Disk write roots
- Secret presence without value logging

### 3. Tool boundary

- Read tools
- Repository edit tools
- Unity batch test tools
- Git／GitHub tools
- Release tools
- Platform-specific tools

Toolごとにread/write、approval、timeout、evidenceを定義する。

### 4. Permission enforcement

Promptの「禁止」だけに依存しない。

- Codex sandbox／workspace permission
- Branch protection
- CI required checks
- Write root validation
- No secret echo
- Human-only release/tag/merge gate
- MCP Tool allowlist

### 5. State persistence

- Current node
- Completed nodes
- Attempt ID
- Source revision
- Active plan
- Decisions
- Evidence references
- Blockers
- Human approvals
- Next eligible nodes

Conversation memoryをStateの正本にしない。

### 6. Validation

- Graph／State schema
- Docs cross-links
- Catalog／Manifest consistency
- Unity compile
- Tests
- Read-only purity
- Approval boundary
- Failure injection
- Existing Graphics regression
- Release integrity

### 7. Evidence

- Commands and exit status
- Test reports
- Diff summary
- Structured findings
- Artifact hashes
- Approval record
- Unsupported evidence
- Environment metadata

### 8. Completion enforcement

Phaseの完了は、必要EvidenceとDone predicateがすべて合格した場合だけ。
Project完了はCompletion Gateだけが決定する。

### 9. Recovery

- Context exhaustion
- Codex turn termination
- Unity domain reload
- Compile interruption
- CI failure
- Human review request
- External environment unavailable

再開はStateから行い、自動的に副作用Nodeを再実行しない。
