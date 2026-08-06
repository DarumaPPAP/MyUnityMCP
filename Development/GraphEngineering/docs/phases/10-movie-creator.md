# Phase 10 — MovieCreator


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

Narrative GoalとEmotional IntentからGraphics／Cinematic中心のShot Sequenceを統合し、
Continuity ReviewとHuman Visual Reviewまで実行する。

## Required domains

- UnityAgentMCP
- GraphicsMCP
- CinematicMCP

Conditional:

- AnimationMCP
- AudioMCP
- ProfilerMCP

## Graph

Narrative intent
→ parallel read-only inspection
→ shot/blocking plan + visual direction
→ integrated preview
→ human approval
→ ordered domain mutation
→ separate save/bake
→ sequence capture
→ technical/continuity review
→ human beauty review
→ refine

## Invariants

- Shared shot ID
- Duration／frame rate normalization
- Camera／lighting continuity
- Actor continuity
- Rollback boundary
- No direct mutation
- No automatic beauty pass

## Required evidence

- `movie_creator_e2e`
- `continuity_review`
- `human_visual_handoff`

## Done

Existing workflow contractをRuntimeが解釈し、sample movieがE2E完了する。
