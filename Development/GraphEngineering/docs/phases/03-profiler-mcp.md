# Phase 3 — ProfilerMCP


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

Unity Editor内の再現可能な計測EvidenceとBaseline比較を提供する。
Editor値をTarget Device性能と表現しない。

## Initial scope

- ProfilerRecorder
- Available counter inspection
- Warmup／sample plan
- CPU／GPU／memory／rendering counters when available
- Median／p95／max aggregation
- Baseline comparison
- Environment metadata
- Execution history

## Deferred

- Remote Player connection
- Nintendo Switch／PlayStation実機
- Automatic deep profiling
- Proprietary SDK
- ML profiling analysis

## Tools

- inspect environment
- list counters
- prepare capture
- start/status/cancel
- summarize
- compare baseline
- support matrix

## Tests

- Counter missing
- GPU unavailable
- Warmup/sample correctness
- Play mode exit
- Domain reload
- Cancel
- Incompatible baseline
- No false device claim

## Required evidence

- `profiler_capture_tests`
- `baseline_compare`
- `no_false_device_claim`

## Done

同一Environmentで再現可能なEditor captureと比較ができる。
