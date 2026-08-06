# Context Engineering Policy

## Goal

現在Nodeの判断に必要な情報だけを、出典と鮮度を保ってCodexへ渡す。

## Always include

- Terminal Goalの短い要約
- Current Node
- Node Done条件
- Prohibited changes
- Repository revision／branch
- Current failures／blockers
- Required output schema

## Retrieve on demand

- 対象Phase spec
- 関連Product code
- 関連Tests
- Catalog／Manifestの該当Entry
- 直前のDecision log
- Package／Unity official docs
- Failure logs
- Diff

## Do not preload

- 全Phase仕様
- 全Repository source
- 全CI logs
- 古いPR discussion全文
- 重複した設計文書
- Current Nodeに無関係なDomain
- 秘密情報
- Project固有Screenshotや顧客情報

## Ordering

1. Goal／Acceptance
2. Current facts
3. Applicable policy
4. Target code
5. Failing evidence
6. Related references
7. Historical decisions

## Labels

Context Bundleは各情報を次で区別する。

- `[SOURCE]`
- `[POLICY]`
- `[OBSERVATION]`
- `[EXPECTED]`
- `[ACTUAL]`
- `[DECISION]`
- `[UNKNOWN]`
- `[PROHIBITED]`

## Freshness

- Current branch／commit／test resultは毎Run取得
- Unity／Package APIは実装時に公式Sourceで再確認
- Design-only statusをCurrent code確認なしにOperationalへ変えない
- 古いEvidenceはHistoricalとして扱う

## Compaction

Conversation summaryではなく、次をStateへ残す。

- Decision
- Changed files
- Test evidence
- Open findings
- Next node
- Required human input

Contextが肥大化したら、古いTool outputをEvidence fileへのPointerへ置換する。
