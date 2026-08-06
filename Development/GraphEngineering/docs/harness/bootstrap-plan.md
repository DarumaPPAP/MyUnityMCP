# Development Harness Bootstrap

これはProduct Phaseではなく、Codex Implementation Graphの前提Nodeです。

## Goal

RepositoryへPrompt／Context／Harness／Loop／Graphの責任分離を実際に導入します。

## Required implementation

- Short Agent map and docs index
- Repository-owned WORKFLOW contract
- Graph／State／Evidence schemas
- State CLI
- Context manifest builder
- Evidence validation
- Completion check
- CI job for docs／graph／state consistency
- Repository-owned read-only Graph Dashboard
- Active ExecPlan template
- Secret redaction checks
- Human gate representation
- Codex Turn interruption／resume representation

## Acceptance

- Fresh checkoutでHarness validationを実行できる
- Invalid graph／state／evidenceを拒否する
- Missing completion evidenceでPhaseをcompleteにできない
- Project completionをPhase completionから分離する
- Restart後にStateから再開できる
- 利用上限やTurn終了をProject完了扱いにしない
- HarnessはProduct Runtime codeへ依存しない

## Reference

`scripts/roadmap_harness.py`は最小参照実装です。
既存Repository Toolchainへ適合させ、CIへ統合します。
