# Graph Engineering Modernization

`Development/GraphEngineering` is the Development Integration Lab for MyUnityMCP.

## Source of truth

- Production baseline: `main` at `12add57a3a8be1c7d5d01055c91ea2a60bb782e8`
- Rescued Graph source baseline: `feature/graph-engineering-master` at `f859e22ba06f58acbb4787987d6a373254a2b127`
- Integration branch: `feature/graph-engineering-master`
- Production UnityGraphicsMCP remains the protected canonical source. Graph-specific work must not rewrite canonical Graphics roots.

## Repository model

```text
main (production source of truth)
  -> Graph Engineering Development Integration Lab
  -> Editor/Development candidate modules
  -> scoped validation evidence
  -> feature/graph-engineering-master integration
  -> delivery/<capability> from latest main
  -> capability-scoped promotion
```

Production Catalog and `MCP_MANIFEST.yaml` remain protected from Development metadata contamination.

## Candidate order

1. UnityAgentMCP
2. ProfilerMCP
3. BuildMCP
4. AddressablesMCP
5. UIMCP
6. AnimationMCP
7. AudioMCP
8. CinematicMCP
9. WorldCreator
10. MovieCreator
11. LiveCreator

The priority is compile, architecture, compatibility, safety, and testability before feature expansion.

## Compatibility

The canonical compatibility registry is `Packages/com.darumappap.my-unity-mcp/Editor/Compatibility/ApiCompatibility.cs` from main.
Maintenance buckets are `BASE`, `UNITY_6000_4`, `UNITY_6000_5`, and `UNITY_6000_7`; 6.6 changes roll into the 6000.7 maintenance bucket.

BASE modernization is complete at source level. The temporary migration bridges were removed; Development candidates target canonical main internals directly.

Addressables remains optional. Its 91-tool frontend stays in the base Development assembly, while the typed Addressables Public API backend is isolated in a package-gated Editor assembly so projects without `com.unity.addressables` still compile and discover the same tool surface.

## Verification semantics

Evidence keeps technical results and operator acceptance separate.

- `pass` / `integration_verified` evidence identifies its source revision and validation scope.
- `unavailable`, `not_verified`, and runner-side failures are never silently rewritten as automated PASS.
- Operator acceptance may close a project gate when explicitly approved, while the underlying technical limitation remains recorded.
- Scoped historical evidence may carry forward only when its validated paths are unchanged.

The final Graph integration cycle combines:

- successful Run #52 automated EditMode contracts on Unity 6000.0.75f1, 6000.4.12f1 and 6000.5.5f1 for unchanged Development modules;
- manual Unity 6000.7.0a2 Git-package installation, compile, package recognition and 91/91 tool discovery;
- manual Addressables package-absent/package-present validation for the optional backend split;
- external Unity project UPM installation validation;
- scoped fault-injection carry-forward evidence;
- explicit repository-owner acceptance for the External MCP limitation and final delivery-promotion preparation.

`roadmap-state.json` is `ready_for_delivery_promotion` with no remaining Graph integration blockers.

## Promotion rule

**Do not wholesale merge `feature/graph-engineering-master` into `main`.**

For production promotion:

1. Start `delivery/<capability>` from the latest `main`.
2. Move only that capability and its explicitly required shared dependencies.
3. Re-run the capability-specific compile/contracts against the delivery branch where available.
4. Review the diff against `main`.
5. Merge the delivery PR into `main` only after human approval.

This preserves `main` as the Production Source of Truth while allowing Graph Engineering to remain the integration laboratory.
