# Release Candidate Gate

## Role

Release Candidate Gateは、Sourceが存在することではなく、製品としてDelivery可能なEvidenceが揃ったことを機械判定します。

```text
Development/GraphEngineering/scripts/release_candidate_gate.py
```

## Required gates

- Architecture Lint
- Python Harness Test
- Unity Editor CI
- External MCP Client E2E
- Addressables Packageなし構成
- Addressables Packageあり構成
- Security Mode
- Fault Injection
- World Human Visual Review
- Movie Human Visual Review
- Live Operator Review
- Release Version承認
- Artifact-only Delivery Validation

各Gateは`pass`／`passed`／`approved`だけでは不十分で、Evidence Pathも必要です。

## Roadmap conditions

- `phase_12_production_hardening`が`complete`
- `project_completion_gate`はまだ`complete`ではない
- `terminal_goal_satisfied`は`false`

Release Candidate GateはProject Completion Gateの前に実行します。

## Delivery conditions

Delivery Manifestは次を満たす必要があります。

- Base Branch: `main`
- Include Pathが1件以上
- `Development/GraphEngineering/**`を含まない
- `GRAPH_ENGINEERING.md`を含まない

Graph Engineering Branchをそのまま`main`へPRしません。

## Current expected result

現時点では次が未達です。

- Unity Editor CI Evidence
- External MCP Client E2E
- Addressables PackageありMatrix
- Desktop／Platform Build Evidence
- Human Review
- Release Version承認
- Delivery Branch Validation

したがってGateがFAILするのが正常です。

## Sequence

```text
Source implementation
→ Static validation
→ Unity Editor CI
→ External MCP E2E
→ Optional backend matrix
→ Human review
→ Version approval
→ latest mainからdelivery/*作成
→ Artifact-only validation
→ Release Candidate Gate
→ Project Completion Gate
→ Human Final Release Approval
```
