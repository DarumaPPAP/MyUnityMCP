# Graph Engineering Modernization

`Development/GraphEngineering` is the Development Integration Lab for MyUnityMCP.

## Source of truth

- Production baseline: `main` at `12add57a3a8be1c7d5d01055c91ea2a60bb782e8`
- Original rescued Graph source: `feature/graph-engineering-master` at `f859e22ba06f58acbb4787987d6a373254a2b127`
- Development Integration Lab: `feature/graph-engineering-master`
- Current final-validation work is reviewed through capability/fix PRs into the Development Integration Lab.
- Production UnityGraphicsMCP is the read-only canonical source. Graph-specific code must not modify canonical Graphics roots.

## Repository model

```text
main (production source of truth)
  -> feature/graph-engineering-master (Development Integration Lab)
      -> Editor/Development (candidate source overlay)
      -> Development/GraphEngineering (catalog, manifest, state, evidence, migration data)
      -> final validation
  -> validated capability artifact
  -> delivery/<capability> from latest main
  -> human-approved main promotion
```

Production Catalog and `MCP_MANIFEST.yaml` remain main-identical. Candidate metadata lives only below `Development/GraphEngineering`.

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

BASE modernization is complete at source level. The temporary `SessionMigrationBridge` and canonical-tool migration bridge were removed; Development candidates now target the canonical main internals directly.

Addressables remains optional. Its 91-tool frontend stays in the base Development assembly, while the typed Addressables Public API backend is isolated in a package-gated Editor assembly so projects without `com.unity.addressables` still compile and discover the same tool surface.

## Verification semantics

Evidence records a validated `source_revision`. PASS evidence from the exact revision is accepted directly. PASS evidence recorded by a later metadata-only commit may remain applicable only when it declares explicit `validated_paths`, the validated revision remains an ancestor, and those paths are byte-for-byte unchanged across the Git diff. Source changes invalidate that evidence.

`unavailable`, `not_verified`, `blocked`, and `awaiting_approval` must never be promoted to PASS. Unity 6000.7 automation remains `not_verified` when the required GameCI image is unavailable; human evidence must state exactly what was verified.

Run locally:

```bash
python3 Development/GraphEngineering/scripts/verify_canonical_graphics.py
python3 Development/GraphEngineering/scripts/verify_development_separation.py
python3 Development/GraphEngineering/scripts/state_reducer.py --check
python3 Development/GraphEngineering/scripts/verify_promotion_gate.py
```

The promotion gate remains blocked until fresh multi-version Editor verification for the final source revision, package-present/package-absent verification, actual External MCP Client E2E, Fresh Project verification, and human final approval are complete.
