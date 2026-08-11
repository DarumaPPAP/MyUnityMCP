# Graph Engineering Modernization

`Development/GraphEngineering` is the Development Integration Lab for MyUnityMCP.

## Source of truth

- Production baseline: `main` at `12add57a3a8be1c7d5d01055c91ea2a60bb782e8`
- Rescued Graph source: `feature/graph-engineering-master` at `f859e22ba06f58acbb4787987d6a373254a2b127`
- Working branch: `refactor/graph-engineering-modernization`
- Production UnityGraphicsMCP is read-only canonical source. Graph-specific code must not modify canonical Graphics roots.

## Repository model

```text
main (production source of truth)
  -> Graph Engineering canonical production baseline
  -> Editor/Development (candidate source overlay)
  -> Development/GraphEngineering (catalog, manifest, state, evidence, migration data)
  -> validated capability artifact
  -> delivery/<capability> from latest main
```

Production Catalog and `MCP_MANIFEST.yaml` remain main-identical. Candidate metadata lives only below this directory.

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

A temporary internal `SessionMigrationBridge` exists only to keep rescued candidates buildable after the main naming modernization. It is a promotion blocker and must be removed by BASE modernization; it is not a Unity-version patch.

## Verification semantics

Old Graph evidence is historical only. A new HEAD must be verified again. `unavailable`, `not_verified`, `blocked`, and `awaiting_approval` must never be promoted to PASS.

Run locally:

```bash
python3 Development/GraphEngineering/scripts/verify_canonical_graphics.py
python3 Development/GraphEngineering/scripts/verify_development_separation.py
python3 Development/GraphEngineering/scripts/verify_promotion_gate.py
```

The promotion gate is expected to remain blocked until the migration bridge, fresh multi-version Editor verification, package matrix, External MCP E2E, Fresh Project verification, and human approval are complete.
