# Phase 12 — Production Hardening


## Engineering-layer contract

### Prompt

現在PhaseでModelへ委ねる判断、Non-goals、Output schemaをNode Promptへ限定する。
権限、Test合格、完了判定はPromptで強制した扱いにしない。

### Context

このPhase spec、関連実装、関連Tests、該当Catalog／Manifest、Current failures、
直前DecisionだけをContext Bundleへ入れる。

### Harness

Phase固有Tool、Approval、Evidence、Failure injectionを機械検証する。

### Loop

Inspect → Plan → Implement → Validate → Observe → Adjust。
同じFailureの反復、自動Side-effect Retry、Evidenceなしの完了を禁止する。

### Graph

依存Node完了後のみ開始し、Done Gate通過後に次Edgeへ進む。
Phase完了はProject完了ではない。


## Goal

全RuntimeをRelease Candidateへ引き上げる改善／監査Graph。

## Workstreams

- Unity／pipeline／package／OS compatibility matrix
- External MCP client E2E
- Real graphics device capture
- APV／Bake evidence
- Build／player evidence where available
- Personal／Team／Restricted／CI security modes
- Observability
- Fault injection
- Upgrade／migration
- Release engineering
- Documentation gardening
- Entropy／architecture lint

## Team mode privacy

認証情報、Unity Project ID、組織情報、顧客名、社内Issue、
Screenshot、運用情報を収集項目へ入れない。

## Fault injection

- Domain reload
- Compile
- Scene change
- Play mode
- Package absent
- Client disconnect
- Timeout／cancel
- Revision race
- Disk issue
- Build／Addressables／capture failure

## Required evidence

- `compatibility_matrix`
- `external_client_e2e`
- `security_modes`
- `release_candidate`

## Done

Critical safety issueがなく、Release Candidate、upgrade path、failure recovery evidenceが揃う。
