# Evidence and Completion

## Evidence is not prose alone

「実装しました」「Testが通りました」というAssistant messageはEvidenceではない。

Accepted evidence:

- Test runner report
- Command／exit code
- File hash
- Git diff／commit
- Unity structured log
- Tool discovery result
- Catalog consistency report
- Approval record
- Human review decision

## Phase completion

Phase specの`required_evidence`をすべて満たす。
HarnessがEvidence SchemaとPredicateを検証する。

## Project completion

- Graph上の全必須Nodeがcomplete
- Completion Gateがpass
- Human final approvalがrecorded
- Stateの`terminal_goal_satisfied=true`

Assistantが任意に設定してはならない。
