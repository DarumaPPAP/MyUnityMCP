# Phase Loop

## Unit

1 Phaseまたは1つの最小垂直Slice。

## Loop

```text
Preflight
  → Inspect current facts
  → Form falsifiable hypothesis / implementation plan
  → Implement task-owned change
  → Run deterministic validation
  → Observe result
     ├─ Pass + evidence complete → Checkpoint Gate
     ├─ Fail with new information → Update context and adjust
     ├─ Same failure repeats → Recovery/Escalation
     └─ Human judgment needed → Human Gate
```

## Model responsibilities

- 原因解釈
- 設計案比較
- Scope判断
- Code／Test作成
- Failureから次のHypothesisを作る

## Code/Harness responsibilities

- Command execution
- Permission
- State update
- Attempt counting
- Evidence capture
- Schema validation
- Test result判定
- Completion predicate

## Stop conditions

- Checkpoint Gate pass
- Explicit human handoff
- External blocker
- Budget exhausted
- Context cannot be made sufficient
- Repeated equivalent failure
- Safety violation

無制限に「完成するまで繰り返す」とだけ書かない。
