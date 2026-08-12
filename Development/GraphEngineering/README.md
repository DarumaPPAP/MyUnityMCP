# Graph Engineering Modernization

`Development/GraphEngineering` is the Development Integration Lab for MyUnityMCP.

## Source of truth

- Production baseline: `main` at `85912aa37c3b8d69bb71a4075cea75d4e93cf7aa`
- Production surface: **42 tools = 32 Graphics + 10 UnityAgent**
- Previous Graph integration baseline: `feature/graph-engineering-master` at `75118eaabda9ab269f47b899a21aa2e286bfcf45`
- Integration branch: `feature/graph-engineering-master`
- Remaining Development candidate surface: **49 tools**
- Final combined target: **91 tools**

Current `main` is the protected Production Source of Truth. Graph-specific work must not rewrite canonical Graphics or promoted UnityAgent production roots.

## Repository model

```text
main (42-tool production source of truth)
  -> Graph Engineering Development Integration Lab
  -> 49 remaining Development candidate tools
  -> scoped validation evidence
  -> feature/graph-engineering-master integration
  -> delivery/<capability> from latest main
  -> capability-scoped promotion
  -> final 91-tool product surface
```

Production Catalog and `MCP_MANIFEST.yaml` remain protected from unpromoted Development metadata contamination.

## Promotion state

UnityAgentMCP has already been promoted to Production and is no longer counted as a Development candidate.

Remaining recommended delivery order:

1. WorldCreator
2. ProfilerMCP
3. BuildMCP
4. AddressablesMCP
5. UIMCP
6. AnimationMCP
7. AudioMCP
8. CinematicMCP
9. MovieCreator
10. LiveCreator

WorldCreator is first because it can already route through the operational UnityAgent + Graphics stack without waiting for another Domain. MovieCreator follows CinematicMCP, and LiveCreator remains last because it composes operational Domain tools into operator-gated cue execution.

## Compatibility

The canonical compatibility registry is `Packages/com.darumappap.my-unity-mcp/Editor/Compatibility/ApiCompatibility.cs` from main.
Maintenance buckets are `BASE`, `UNITY_6000_4`, `UNITY_6000_5`, and `UNITY_6000_7`; 6.6 changes roll into the 6000.7 maintenance bucket.

BASE modernization is complete at source level. Development candidates target canonical main internals directly.

Addressables remains optional. Its typed Public API backend is isolated in a package-gated Editor assembly so projects without `com.unity.addressables` can retain structured unsupported behavior instead of failing compilation.

## Verification semantics

Evidence keeps technical results and operator acceptance separate.

- `pass` / `integration_verified` evidence identifies its source revision and validation scope.
- `unavailable`, `not_verified`, and runner-side failures are never silently rewritten as automated PASS.
- Operator acceptance may close a project gate when explicitly approved, while the underlying technical limitation remains recorded.
- Scoped historical evidence may carry forward only when its validated paths are unchanged.
- Production UnityAgent promotion does not convert the old 91-tool Graph discovery evidence into fresh 42-tool Production CI evidence.

The Graph integration evidence currently includes:

- Run #52 automated EditMode contracts on Unity 6000.0.75f1, 6000.4.12f1 and 6000.5.5f1 for unchanged paths;
- manual Unity 6000.7.0a2 Git-package installation, compile, package recognition and 91/91 discovery;
- manual Addressables package-absent/package-present validation;
- external Unity project UPM installation validation;
- scoped fault-injection carry-forward evidence;
- explicit repository-owner acceptance for the earlier External MCP limitation.

`roadmap-state.json` remains `ready_for_delivery_promotion` with no Graph integration blockers. Full protocol-level External MCP E2E is still a later Production Hardening requirement for the final product.

## Promotion rule

**Do not wholesale merge `feature/graph-engineering-master` into `main`.**

For production promotion:

1. Start `delivery/<capability>` from the latest `main`.
2. Move only that capability and explicitly required shared dependencies.
3. Re-run capability-specific compile/contracts against the delivery branch where available.
4. Review the diff against `main`.
5. Merge the delivery PR into `main` only after human approval.

This preserves `main` as the Production Source of Truth while allowing Graph Engineering to remain the integration laboratory.
