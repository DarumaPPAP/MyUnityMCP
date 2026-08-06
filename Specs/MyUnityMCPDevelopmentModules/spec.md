# MyUnityMCP Development Modules Runtime Contract

## Status

この仕様は`feature/graph-engineering-master`上の開発候補を定義します。
Unity Editor Compile、全Editor Test、外部MCP Client E2E、必要なHuman Reviewが揃うまで、Release Runtimeとして実行可能とは表現しません。

```text
Release operational:
- unity_graphics_mcp

Development implementation, Unity CI unverified:
- unity_agent_mcp
- world_creator
- unity_profiler_mcp
- unity_build_mcp
- unity_addressables_mcp
- unity_ui_mcp
- unity_animation_mcp
- unity_audio_mcp
- unity_cinematic_mcp
- movie_creator
- live_creator
```

## Shared execution contract

Mutation可能なDomainは次の順序を必須とします。

```text
Inspect
→ Prepare exact preview
→ Return plan ID and one-time approval token
→ Human approval
→ Revalidate Editor revision
→ Revalidate target object or asset
→ Apply with Undo where supported
→ Mark dirty
→ Advance MyUnityMCP revision
→ Return exact result
```

Shared constraints:

- Plan lifetime: 10 minutes
- Plan consumption: one time only
- Missing／expired／reused approval is rejected
- Revision race is rejected
- Automatic Save is prohibited
- Automatic Full Bake is prohibited
- Silent Fallback is prohibited
- Generic SerializedProperty mutation is prohibited
- Development module Reflection is prohibited
- All MCP tools remain`AutoRegister = false`

## Security modes

### PERSONAL

個人Project内でMachine情報、Object名、Project Path、Captureを扱える。ただしCredential、Token、Unity Project ID、組織・顧客情報、社内Issue番号は収集・出力しない。

### TEAM

Machine情報、Project Path、Object名、Screenshot、運用情報を出力しない。Credential、Unity Project ID、組織・顧客情報、社内Issue番号は収集項目自体へ含めない。

### RESTRICTED

既定値。Object名以外のMachine／Path／Screenshot／運用情報を除去する。

### CI

Count、Status、Error Codeなど再現可能な最小情報だけを残す。

## UnityAgentMCP

Role: Product Control Plane。

- Catalog-driven Domain selection
- Tool and Tool Group validation
- DAG compile and cycle rejection
- Side-effect preview
- Tool-group approval
- Revision revalidation
- Registered Domain Tool delegation
- Execution result aggregation
- Interruption and history persistence

Unity APIを直接Mutationしません。`editor_operational`以外のDomainを実行しません。

## WorldCreator

Role: Visual Goalから制作Workflowを作るCreator。

Initial vertical slice:

- Graphics project inspection
- Graphics scene inspection
- Graphics validation
- Direction／Mutation／Save／Bake／CaptureへのHandoff
- Human Review Handoff

Creator自身はUnity APIを変更しません。

## ProfilerMCP

- Editor／Project environment inspection
- Explicit ProfilerRecorder counter inspection
- Warmup and sample frame capture
- Cancellation／interruption
- Median／p95／max summary
- Same-environment baseline comparison

Editor Captureは対象実機性能として表現しません。Environment Fingerprintが異なるCaptureを比較しません。

## BuildMCP

- Active Build Target and Build Settings Scene inspection
- BuildPlayerOptions exact preview
- Approval-gated BuildPipeline.BuildPlayer
- BuildReport summary
- Session build history

Outputは`Builds/MyUnityMCP/`配下に限定します。実行中Buildの強制CancelはPublic APIで安全に実装できないため`BACKEND_NOT_IMPLEMENTED`です。

## AddressablesMCP

Optional adapterです。`com.unity.addressables`がないProjectでもCompileを壊しません。

Package／Settingsなし:

- `UNSUPPORTED`
- SettingsやGroupを自動生成しない

Packageあり:

- Settings／Profile／Builder／Group inspection
- Existing GroupへのEntry mutation preview
- Approval-gated CreateOrMoveEntry／Address／Label更新
- Separate approval-gated Content Build
- Automatic Saveなし

## UIMCP

- Canvas／RectTransform／UIDocument inspection
- Anchor／finite value／UIDocument validation
- Approval-gated RectTransform property update
- Screen Space Overlay描画を再実装しない

## AnimationMCP

- Animator／AnimatorController／Parameter／Layer／Clip inspection
- Duplicate Parameter／Animation Event validation
- Approval-gated AnimatorController Parameter addition

初期Mutation ScopeからState Machine、Transition、Curve、Clip Eventの書換えを除外します。

## AudioMCP

- AudioSource／AudioClip／AudioListener／Mixer routing inspection
- Value and listener validation
- Approval-gated Volume／Pitch／Spatial Blend／Loop／Mute／Play On Awake update

AudioClip差替えとAudioMixer Asset生成は行いません。

## CinematicMCP

Core Playables基盤:

- PlayableDirector inspection
- Playable Asset outputs and Generic Binding validation
- Approval-gated Initial Time／Update Mode／Wrap Mode／Play On Awake update

Timeline Track生成、Clip生成、Binding mutation、Cinemachine Shot mutationはOptional Adapterが検証されるまで実行しません。

## MovieCreator

- Shot List validation
- Shot start／duration／end compile
- Domain step and Human Gate projection
- Shot-level visual review handoff

Cinematic Domainが`editor_operational`になるまで`executionReady=false`です。自動Visual Acceptanceは禁止です。

## LiveCreator

- Cue timing and order validation
- Domain／Tool declaration validation
- Recovery Cue validation
- Operator Gate and Abort Policy
- Operator handoff

Unattended executionとAutomatic Go Liveは禁止です。

## Verification gate

Development modulesをRelease候補へ昇格する条件:

1. Architecture Lint PASS
2. Python Graph／Harness／Delivery tests PASS
3. Unity 6000.0.75f1 Editor Compile PASS
4. EditMode Test 158件以上、失敗・Skip・Inconclusiveなし
5. Candidate Tool Discovery 91件
6. Existing GraphicsMCP regression PASS
7. External MCP Client E2E
8. Optional Package absent/present validation
9. Security Mode validation
10. Fault Injection evidence
11. Human Visual／Operator Review where applicable
12. New Version and artifact-only Delivery PR approval
