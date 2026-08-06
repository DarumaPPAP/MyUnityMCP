# Phase 11 — LiveCreator


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

Existing stage／characters／music timingからCharacter-firstなLive Sequenceを統合する。

## Required domains

- UnityAgentMCP
- GraphicsMCP
- CinematicMCP
- AnimationMCP
- AudioMCP

Conditional:

- ProfilerMCP
- BuildMCP

## Timing source

初期版は自動Beat MLを必須にしない。

- Timeline marker
- Cue sheet
- BPM + offset
- Human section/timecode
- Existing animation event

実装していない解析を「同期済み」と表現しない。

## Invariants

- Character visibility
- Face／subject framing
- Backlight／VFX silhouette
- Occlusion
- Camera cut density
- Explicit platform budget
- No performance claim without measurement

## Required evidence

- `live_creator_e2e`
- `timing_validation`
- `character_visibility_review`

## Done

Cue/timecode based sample live sequenceとHuman live review handoff。
