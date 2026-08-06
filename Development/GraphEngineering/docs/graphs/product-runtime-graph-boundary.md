# Product Runtime Graph Boundary

Phase 1で実装するUnityAgentMCP Runtimeは、完成後の製品Control Plane。

```text
MCP Client
  → UnityAgentMCP
    → Domain selection
    → Tool activation
    → Inspect／Plan
    → Approval
    → Domain Apply
    → Evidence aggregation
    → Human review／Refine
```

Codex Implementation GraphとはState、Evidence、Owner、Lifecycleが異なる。
Product RuntimeはCodex Roadmapを実行しない。
