# Safety Model

## Read-only boundary

Inspection、Planning、Preview、PrepareはScene Dirty、Persistent Asset Dirty、Undo Groupを変更してはいけません。違反を検出すると`READ_ONLY_CONTRACT_VIOLATION`で結果を破棄します。

## Approval boundary

Mutation、Save、Bakeは以下を直前に再検証します。

- 現在Sessionに属するPlan ID
- Expected Revision
- 一時Approval Token
- Preview時Baseline／Digest
- 対象Scene／Component／Dependencyの存在
- Mode文字列（`NONE`、`EXPLICIT_SCENE`、`EXPLICIT_DEPENDENCIES`等）

## Transaction boundary

Scene Component Mutationは一つのUnity Undo Groupに集約し、途中例外ではTransaction全体をRollbackします。Save／Bakeで生成された永続FileはUndo／自動Rollback保証の対象外です。

## Lifecycle boundary

Domain Reload、Compile、Play Mode、Scene Close、Multi Scene構成変更、Client切断、Unity終了ではActive Executionを中断します。Unity再起動後に自動再開しません。

## Visual acceptance boundary

自動EvaluationとHuman Acceptanceは別です。`evaluate_capture`が`PASSED`でも、最終AcceptanceにはEvidence Digestに紐づくHuman Reviewが必要です。
