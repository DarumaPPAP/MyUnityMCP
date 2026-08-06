# Codex Implementation Graph

## Node types

- `llm_loop`: Codex reasoning and implementation
- `code_gate`: deterministic script／test／schema
- `human_gate`: explicit approval
- `integration_gate`: cross-domain validation
- `terminal_gate`: project completion

## Topology

```text
Bootstrap Development Harness
  ↓
Phase 0 Release Integrity
  ↓
Phase 1 UnityAgentMCP Runtime
  ↓
Phase 2 WorldCreator Runtime
  ↓
Domain Spine
  ├─ Phase 3 ProfilerMCP
  ├─ Phase 4 BuildMCP
  ├─ Phase 5 AddressablesMCP
  ├─ Phase 6 UIMCP
  ├─ Phase 7 AnimationMCP
  └─ Phase 8 AudioMCP
        ↓
Phase 9 CinematicMCP
  ↓
Phase 10 MovieCreator
  ↓
Phase 11 LiveCreator
  ↓
Phase 12 Production Hardening
  ↓
Project Completion Gate
  ↓
Human Final Release Approval
  ↓
Project Complete
```

Domain SpineはDependency上並列可能だが、既定Concurrencyは1。
理由はCatalog／Manifest／shared test infrastructureの競合を避けるため。
Isolated worktreeとIntegration queueが実証された後だけ並列化する。

## Failure edges

- Compile/Test fail → Current phase loop
- Contract mismatch → Context／spec correction
- Harness insufficiency → Bootstrap harness
- Human decision → Human gate
- External package/platform unavailable → Blocked state
- Cross-domain integration fail → First responsible phase
