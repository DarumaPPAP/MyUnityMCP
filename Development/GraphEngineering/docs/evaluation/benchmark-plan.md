# Graph Benchmark Plan

## Purpose

追加Loop、Reviewer、並列Agentが単一Loopより有効かを測る。

## Representative tasks

各Phase:

- Normal
- Boundary／unsupported
- Failure／interruption

Creator:

- Simple goal
- Cross-domain conflict
- Human review fail → refine

## Compare

A. Single phase loop
B. Phase loop + independent review
C. Isolated parallel domain loops（Harness成熟後のみ）

## Metrics

- Success rate
- Critical defects
- Requirement coverage
- False support claims
- Run-to-run variance
- Token
- Time
- Handoff loss
- Retry scope
- Human intervention
- Control complexity

数値Thresholdは未指定。
Benchmark結果またはユーザー目標なしに固定しない。
