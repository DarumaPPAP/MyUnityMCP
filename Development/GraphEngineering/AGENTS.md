# Graph Engineering Agent Rules

1. Never commit, push, merge, retag, or change releases on `main` from this modernization work.
2. Treat `main` as the Production Source of Truth for UnityGraphicsMCP, repository layout, naming, safety, capability contracts, compatibility, release contracts, and package `.meta` policy.
3. Keep Production Catalog and Production MCP Manifest main-identical.
4. Put candidate metadata, state, evidence, and promotion information under `Development/GraphEngineering`.
5. Candidate Unity source belongs under `Packages/com.darumappap.my-unity-mcp/Editor/Development` while it is exercised in this integration branch.
6. Do not scatter Unity-version preprocessor conditionals through Domain runtime. Use the canonical Compatibility Registry and add an adapter only for a real version/API difference that BASE cannot absorb.
7. Do not add maintenance buckets beyond BASE, UNITY_6000_4, UNITY_6000_5, UNITY_6000_7 without explicit human approval.
8. UnityAgentMCP is a control plane. It delegates to Domain MCPs and must not mutate Unity directly. Creators also delegate through Agent/Domain boundaries.
9. Preserve approval tokens, expected revision, one-time plans, no automatic save/full bake, no silent fallback, no automatic visual acceptance, and no automatic execution resume.
10. Do not reuse historical evidence as evidence for a new source revision.
11. A missing Editor image or package environment is `unavailable` or `not_verified`, never PASS.
12. Graph Engineering never merges wholesale into main. Promotion is capability-scoped through `delivery/<capability>` from latest main.
