# Recovery Loop

## Same failure detection

次が同じならEquivalent failureとして扱う。

- Error code
- Failing test
- Relevant stack frame
- Modified area
- Proposed fix class

同じ失敗に同じ修正を繰り返さない。

## Recovery actions

1. Context不足を確認
2. Assumptionを明示
3. Official API／installed sourceを確認
4. Minimal reproduction
5. Scope縮小
6. Harness／Tool不足を特定
7. Human escalation

## Interrupted side effects

Mutation／Save／Bake／Build／Tagは自動Retryしない。
まずState、revision、output、dirty assetsを再検査する。

## Handoff

- What failed
- Evidence
- Attempts
- Ruled-out hypotheses
- Current repository state
- Safe resume node
- Required human input
