# MyUnityMCP Development Tool Reference

## Status rule

この文書のDevelopment Toolは`feature/graph-engineering-master`上の候補です。
Unity Editor CIとE2E Evidenceが揃うまで、Release v1.0.0で利用可能とは扱いません。

すべて`AutoRegister = false`であり、Client Allowlistによる明示Activationが必要です。

## Verified release tools

`unity_graphics_mcp`: 32 tools、Unity 6000.0.75f1 Editor CI検証済み。

詳細は`Docs/TOOL_REFERENCE.md`を参照してください。

## UnityAgentMCP — 10

| Tool | Responsibility | Side effect |
|---|---|---|
| `agent.inspect_capabilities` | CatalogとDomain実行可否 | なし |
| `agent.validate_workflow` | Tool／Group／Dependency／DAG検証 | なし |
| `agent.compile_graph` | Product Runtime Graph生成 | なし |
| `agent.preview_execution` | StepとApproval Group表示 | なし |
| `agent.submit_approval` | 一時Approval Token発行 | Approval stateのみ |
| `agent.start_execution` | 登録済みDomain Toolへ委譲 | Domainに依存 |
| `agent.get_execution_status` | Execution状態 | なし |
| `agent.cancel_execution` | 協調Cancellation要求 | Execution stateのみ |
| `agent.get_execution_history` | Library内履歴 | なし |
| `agent.get_error_catalog` | Structured Error一覧 | なし |

## WorldCreator — 3

- `world.compile_workflow`
- `world.start_preflight`
- `world.create_review_handoff`

CreatorはGraphicsのRead-only PreflightをAgentへ委譲し、人間Review用Handoffを作ります。Unity APIを直接変更しません。

## ProfilerMCP — 8

- `profiler.inspect_environment`
- `profiler.inspect_counters`
- `profiler.prepare_capture`
- `profiler.start_capture`
- `profiler.get_capture_status`
- `profiler.cancel_capture`
- `profiler.summarize_capture`
- `profiler.compare_baseline`

Editor Captureは対象実機の性能値ではありません。異なるEnvironment Fingerprintを比較しません。

## BuildMCP — 6

- `build.inspect_environment`
- `build.prepare_player`
- `build.start_player`
- `build.get_history`
- `build.cancel_player`
- `build.get_support_matrix`

Build出力は`Builds/MyUnityMCP/`配下だけです。`build.cancel_player`は安全なPublic APIがないため、実行中Buildに対して`BACKEND_NOT_IMPLEMENTED`を返します。

## AddressablesMCP — 6

- `addressables.inspect`
- `addressables.prepare_entry`
- `addressables.apply_entry`
- `addressables.prepare_content_build`
- `addressables.build_content`
- `addressables.get_support_matrix`

`com.unity.addressables`がない場合は`UNSUPPORTED`です。Settings／Groupの自動生成、自動Save、自動Content Buildは行いません。

## UIMCP — 5

- `ui.inspect`
- `ui.validate`
- `ui.prepare_rect_transform`
- `ui.apply_rect_transform`
- `ui.get_support_matrix`

RectTransform Mutationだけを初期Scopeにし、Approval、Revision、Undoを必須とします。

## AnimationMCP — 5

- `animation.inspect`
- `animation.validate`
- `animation.prepare_parameter`
- `animation.apply_parameter`
- `animation.get_support_matrix`

初期MutationはAnimatorController Parameter追加だけです。State、Transition、Curve、Clip Eventは変更しません。

## AudioMCP — 5

- `audio.inspect`
- `audio.validate`
- `audio.prepare_source`
- `audio.apply_source`
- `audio.get_support_matrix`

Volume、Pitch、Spatial Blend、Loop、Mute、Play On Awakeだけを更新します。ClipとMixer Assetは変更しません。

## CinematicMCP — 5

- `cinematic.inspect`
- `cinematic.validate`
- `cinematic.prepare_director`
- `cinematic.apply_director`
- `cinematic.get_support_matrix`

Core PlayableDirectorだけを対象とします。Timeline Track／Clip生成、Binding変更、Cinemachine Shot変更は未検証です。

## MovieCreator — 3

- `movie.compile_production`
- `movie.preview_production`
- `movie.create_review_handoff`

Shot ListをProduction Graphへ変換します。CinematicMCPが検証されるまで`executionReady=false`です。

## LiveCreator — 3

- `live.compile_show`
- `live.preview_show`
- `live.create_operator_handoff`

無人実行とAutomatic Go Liveは禁止です。Operator GateとRecovery Cueを必須とします。

## Candidate total

```text
Verified Graphics: 32
Development candidates: 59
Total discovered candidate tools: 91
```

## Common error semantics

- `INVALID_REQUEST`: Input／範囲／依存が不正
- `UNSUPPORTED`: Package／Platform／Backendが存在しない
- `UNVERIFIED`: 実装はあるが対象環境Evidenceが不足
- `BACKEND_NOT_IMPLEMENTED`: 安全なPublic API実装がない
- `STALE_REVISION`: Preview後にEditor状態が変更
- `APPROVAL_REQUIRED`: Approval不足／不一致
- `APPROVAL_EXPIRED`: PlanまたはToken期限切れ
- `NOT_FOUND`: Object／Asset／Plan／Execution欠落
- `PARTIAL`: 一部結果はあるが全体成功ではない
- `FAILED`: 実行失敗

## Promotion gate

Releaseへ昇格するには、Unity Compile、EditMode 158件以上、91 Tool Discovery、外部MCP Client E2E、Optional Backend Matrix、Security Mode、Fault Injection、Human Review、Version承認が必要です。
