# Phase 8 — AudioMCP


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

Scene Audio構成と既存Mixer利用を検査し、Public APIで安全に可能なAudioSource routing/settingsを変更する。

## Initial operational scope

- AudioListener
- AudioSource／clip
- Existing mixer group assignment
- Volume／pitch／spatial blend／rolloff
- Loop／play on awake／priority
- Missing clip／group
- Multiple listener
- Existing snapshot／exposed parameter metadata

## Important API boundary

Mixer group creation、effect chain、send/return topologyなど、
安定Public Editor APIで保証できないAuthoringはOperationalと表現しない。
Internal API／Reflection／Generic SerializedPropertyはHuman decisionなしに採用しない。

## Tests

- Multiple listener
- Missing clip／group
- 2D／3D source
- Routing／undo
- Unsupported topology request
- Play mode exit／domain reload

## Required evidence

- `audio_scene_tests`
- `routing_mutation_tests`
- `unsupported_topology_test`

## Done

Public API範囲が実装され、非対応Topologyを正しく拒否する。
