# Source of Truth

優先順位:

1. ユーザーが明示したPolicy
2. `MASTER_GOAL.md`
3. `WORKFLOW.md`
4. Current executable Graph／State
5. Existing operational code and tests
6. Package Manifest／Catalog
7. Specs／Workflows／Docs
8. Current external official API documentation
9. General conventions

Conflictがある場合、黙って片方を採用しない。
Decision logへ記録し、Safety／Public contractに関わる場合はHuman Gateへ送る。

## Repository knowledge

- Short `AGENTS.md`: map
- `docs/`: human/agent-readable source of truth
- `graph/`: machine-readable control
- `state/`: resumable progress
- `schemas/`: machine validation
- `scripts/`: enforcement
- `tests/`: executable evidence
