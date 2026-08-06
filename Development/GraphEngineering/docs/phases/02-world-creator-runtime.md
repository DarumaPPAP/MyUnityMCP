# Phase 2 — WorldCreator Runtime


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

Visual Goalを既存GraphicsMCPのInspect／Plan／Apply／Capture／Reviewへ変換する最初のCreator Runtimeを完成させる。

## Initial scope

- Existing scene lighting
- Camera
- Reflection probe
- Environment
- Conditional volume
- Bake plan
- Capture／evaluation／human review／refine

## Non-goals

- Model generation
- Terrain generation
- Shader source generation
- Gameplay
- External asset download
- Build／Addressables

## Inputs

- visual_goal
- scene_scope
- environment_type
- desired_mood
- target_platforms
- render_pipeline
- prohibited_changes
- acceptance_profile
- optional visual reference

## Invariant

WorldCreatorはUnity APIを直接呼ばず、UnityAgentMCPへIntegrated Planを渡す。

## Acceptance scenario

Fresh sample scene:
Inspect → direction → preview → approval → apply → optional save/bake → capture → machine evaluation → human review handoff.

## Required evidence

- `world_creator_e2e`
- `human_review_handoff`
- `creator_no_direct_mutation`

## Done

最初のCreator integrated workflowが実行可能になり、Design-only statusを実証に基づき更新する。
