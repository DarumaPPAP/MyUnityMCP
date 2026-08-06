# Graph Adoption Decision

## Decision

`最小限に分割`

## Baseline

各Phaseはまず1つのCodex Loopで実装する。
13個の常駐Agentへ分割しない。

## Why a graph is still necessary

- 13の長期Checkpoint
- 明確なDependency
- Human approval
- Release／Build／Package side effects
- RestartをまたぐState
- Failure時に責任Phaseへ戻る必要
- Product phaseとdevelopment harnessの分離

## Hypothesis status

複数Reviewer／並列Domain Agentが品質を改善するかは未測定。
初期GraphはPhase／Gate／Stateの制御だけを分割し、
追加Agent分割はBenchmark後に採用する。

## Adoption benchmark

単一Loopと追加Review／parallel構成を同じ代表Taskで比較する。

- Success
- Critical defect
- Quality
- Variance
- Token
- Time
- Handoff loss
- Retry scope
- Control complexity

改善が再現し追加コストを上回る場合だけ固定する。
