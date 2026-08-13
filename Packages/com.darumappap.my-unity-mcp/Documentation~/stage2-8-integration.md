# 77 Tool Promotion Record

この文書は、v1.1.0でProduction 45 Tool baselineへ追加された6 Domain / 32 ToolのIntegration履歴と、77 Tool Editor Operational SurfaceへのPromotion結果を記録します。

現在の正本Statusは**Promotion Completed**です。旧`delivery/stage2-8-integration` Branchは履歴上のIntegration経路であり、現行Runtime Statusではありません。

## Promotion Result

- Previous Production baseline: 45 Tool
  - Graphics 32
  - Agent 10
  - WorldCreator 3
- Promoted: 32 Tool
  - Profiler 8
  - Addressables 4
  - UI 5
  - Animation 5
  - Audio 5
  - Cinematic 5
- v1.1.0 Production Surface: **77 Tool**
- Status: `editor_operational`
- Primary Evidence: Direct Unity Editor Validation
- Unity: `6000.7.0a2`

## Promoted Domains

### Profiler — 8

`inspect_environment` / `inspect_counters` / `prepare_capture` / `start_capture` / `get_capture_status` / `cancel_capture` / `summarize_capture` / `compare_baseline`

Editor Profiler ResultをTarget Device性能として扱いません。Baseline比較はEnvironment Identityの互換性を必要とします。

### Addressables — 4

`inspect` / `prepare_entry` / `apply_entry` / `get_support_matrix`

`com.unity.addressables`はOptional Dependencyです。Packageが無い場合は`UNSUPPORTED`を返します。Package自動導入、Settings / Group自動生成、自動Save、Content Buildは禁止です。

### UI — 5

`inspect` / `validate` / `prepare_rect_transform` / `apply_rect_transform` / `get_support_matrix`

Mutationは既存RectTransformの承認済みScopeへ限定します。

### Animation — 5

`inspect` / `validate` / `prepare_parameter` / `apply_parameter` / `get_support_matrix`

MutationはAnimatorController Parameterへ限定し、State Machine / Transition / Curve / Clip Event書換えは対象外です。

### Audio — 5

`inspect` / `validate` / `prepare_source` / `apply_source` / `get_support_matrix`

Mutationは対応AudioSource Propertyへ限定し、AudioClip Replacement / AudioMixer Asset生成は対象外です。

### Cinematic — 5

`inspect` / `validate` / `prepare_director` / `apply_director` / `get_support_matrix`

Mutationは対応PlayableDirector Settingsへ限定し、PlayableAsset / Binding / Timeline Track / Clip / Cinemachine Shot変更は対象外です。

## Shared Safety Contract

- PrepareはRead-only
- Expected Revision一致必須
- MutationはOne-time Plan + Approval Token必須
- Expired Plan / Stale Revision / Token mismatchを拒否
- 自動Save禁止
- 自動Full Bake禁止
- Generic SerializedProperty Mutation禁止
- Silent Fallback禁止
- Addressables Content Build禁止

## UnityAgent Routing

v1.1.0ではProfiler / Addressables / UI / Animation / Audio / CinematicはAgent Runtime Catalogでも`editor_operational`です。Agentは各DomainのRevision / Plan / Approval / Scope境界を省略しません。

Delegate FailureはSuccessへ変換せず、先行Step成功後の失敗は`PARTIAL`として伝播します。

## Direct Editor Evidence

Unity `6000.7.0a2`で以下を確認済みです。

1. Package Compile / Compile Error 0
2. Exact 77 Tool Discovery / Duplicate 0
3. Extended Domain Read-only Smoke
4. Stale Revision / Approval / One-time Plan rejection
5. Profiler Capture
6. UI / Animation / Audio / Cinematic Scoped Mutation E2E
7. Addressables Package未導入時の`UNSUPPORTED`境界
8. Agent Operational Routing / Failure Propagation
9. Timeout / Cancel / Domain Reload callback
10. Cross-domain Workflow
11. Previous Production 45 Regression

Evidenceの正本は`Tests/Compatibility/stage2-8-main-merge-acceptance.yaml`と`Tests/Compatibility/stage2-8-validation-progress.yaml`です。

## Not Verified / Non-blocking

- Package Editor Test Runner
- Fresh-project Sample Workflow
- Automated CI
- Addressables Positive Backend Matrix
- External Transport Disconnect/Reconnect
- Player / Target Device execution

Direct Editor ValidationはTarget Device Validationを意味しません。CI利用不能もPASSへ読み替えません。
