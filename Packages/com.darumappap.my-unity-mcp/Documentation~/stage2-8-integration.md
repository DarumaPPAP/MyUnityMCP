# Stage 2-8 Integration Wave

`delivery/stage2-8-integration`は、Production 45 Tool baselineの上に6 Domain / 34 Toolを追加し、**79 Toolを同一候補Commitでまとめて検証するためのIntegration Branch**です。

この文書はImplementation Contractであり、Validation Evidenceではありません。

## Baseline

- Production main: 45 Tool
  - Graphics: 32
  - UnityAgentMCP: 10
  - WorldCreator: 3
- Integration Candidate: +34 Tool
- Combined Validation Target: 79 Tool
- Validation完了前の`main`へのMerge: 禁止
- Validation完了前の`editor_operational`昇格: 禁止

## Stage 2 — Profiler (+8)

- `profiler.inspect_environment`
- `profiler.inspect_counters`
- `profiler.prepare_capture`
- `profiler.start_capture`
- `profiler.get_capture_status`
- `profiler.cancel_capture`
- `profiler.summarize_capture`
- `profiler.compare_baseline`

Editor ProfilerRecorderを使ったCaptureとSummaryを提供します。Editor結果をTarget Device性能として扱いません。Baseline比較はEnvironment一致が必要です。

## Stage 3 — Build (Retired / 0)

Build Domainは今回のMCP Candidateから撤去しました。`build.*` Tool、Agent routing、Build専用Contract/Testは現行79 Tool Surfaceに含めません。

## Stage 4 — Addressables (+6)

- `addressables.inspect`
- `addressables.prepare_entry`
- `addressables.apply_entry`
- `addressables.prepare_content_build`
- `addressables.build_content`
- `addressables.get_support_matrix`

`com.unity.addressables`はOptional Dependencyです。Packageが無い場合はFrontendが`UNSUPPORTED`を返し、Packageがある場合だけ`versionDefines`でTyped BackendをCompileします。Settings / Groupの自動作成、自動Saveは禁止です。

## Stage 5 — UI (+5)

- `ui.inspect`
- `ui.validate`
- `ui.prepare_rect_transform`
- `ui.apply_rect_transform`
- `ui.get_support_matrix`

UGUI / UI Toolkit構成をInspectionし、Mutationは既存`RectTransform`の承認済みExact Diffだけに限定します。

## Stage 6 — Animation (+5)

- `animation.inspect`
- `animation.validate`
- `animation.prepare_parameter`
- `animation.apply_parameter`
- `animation.get_support_matrix`

既存AnimatorControllerへのParameter追加だけをMutation Scopeとします。State Machine / Transition / AnimationCurve / Clip Eventの書換えは対象外です。

## Stage 7 — Audio (+5)

- `audio.inspect`
- `audio.validate`
- `audio.prepare_source`
- `audio.apply_source`
- `audio.get_support_matrix`

既存AudioSourceのVolume / Pitch / Spatial Blend / Loop / Mute / Play On Awakeだけを変更対象とします。AudioClip差替えやAudioMixer Asset生成は行いません。

## Stage 8 — Cinematic (+5)

- `cinematic.inspect`
- `cinematic.validate`
- `cinematic.prepare_director`
- `cinematic.apply_director`
- `cinematic.get_support_matrix`

Core PlayablesのPlayableDirectorを対象に、Initial Time / Update Mode / Wrap Mode / Play On Awakeだけを変更します。Playable Asset、Binding、Timeline Track / Clip、Cinemachine Shotの生成・変更は対象外です。

## Shared Safety Contract

現行Candidate Domainは`UnityDomainMcpCommon`を共有し、次を統一します。

- PrepareはRead-only
- MutationはCurrent Revision一致必須
- Mutation / Content Buildは対象に応じてApproval必須
- Planは一度だけConsume可能
- Expired Plan / Stale Revision / Token mismatchを拒否
- 自動Save禁止
- 自動Full Bake禁止
- Generic SerializedProperty Mutation禁止
- Silent Fallback禁止

## UnityAgent Integration

Candidate DomainはAgent Catalog上`integration_candidate`として登録します。これはProductionの`editor_operational`とは別状態です。

Integration BranchではValidationのため、UnityAgentMCPが`integration_candidate`へRouteできます。DelegateはMCP Tool Attributeから構築するRegistryを使用し、Catalogに宣言されたToolだけがWorkflow Validationを通過します。

Agent自身はUnity APIを直接Mutationしません。Domain側のRevision / Plan / Approval境界も省略しません。Build DomainはAgent Catalogから除外されています。

## Validation Wave

Build撤去により候補Surfaceが変わったため、旧85 Tool Validation結果をそのまま79 Tool Candidateの完了Evidenceには流用しません。現行候補では次から再確認します。

1. Package Resolve / Unity Compile
2. Exact 79 Tool Discovery
3. 6 Candidate DomainのRead-only path
4. Approval / Stale Revision / One-time Plan
5. Mutation Scope / Recovery
6. Addressables external side-effect boundary
7. Agent → 各Candidate Domain Routing
8. Timeout / Cancel / Domain Reload / Failure Propagation
9. Cross-domain Workflow
10. Production 45 Tool Regression

Validation完了前は、現行CandidateをProduction PASSとして扱いません。
