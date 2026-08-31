# MyUnityMCP v1.1.0

MyUnityMCP `1.1.0`は、Unity Editor向けProduction Surfaceを**77 Tool**へ拡張するMinor Releaseです。

## Highlights

- **77 Tool Editor Operational Surface**
  - Graphics 32
  - Agent 10
  - WorldCreator 3
  - Profiler 8
  - Addressables 4
  - UI 5
  - Animation 5
  - Audio 5
  - Cinematic 5
- UnityAgentMCP Control Planeが全Operational Domainへ安全にRouting
- Profiler Capture / Baseline Comparison
- Addressables Entry Inspection / Prepare / Apply（Optional Package）
- RectTransform中心のUI Inspection / Scoped Mutation
- AnimatorController Parameter中心のAnimation Inspection / Scoped Mutation
- AudioSource中心のAudio Inspection / Scoped Mutation
- PlayableDirector中心のCinematic Inspection / Scoped Mutation
- Direct Unity Editor ValidationをPrimary Promotion / Release Evidenceへ変更

## Safety

v1.1.0でも以下の境界を維持します。

- PrepareはRead-only
- MutationはExpected Revision + One-time Plan + Approval Token
- Automatic Save禁止
- Automatic Full Bake禁止
- Generic SerializedProperty Mutation禁止
- Silent Fallback禁止
- Automatic Visual Acceptance禁止
- UnityAgent / WorldCreatorによるDirect Unity Mutation禁止
- Addressables Package未導入時の自動Package導入・Settings/Group生成禁止
- Addressables Content Buildはv1.1.0 Surface外

## Direct Unity Editor Verification

Unity `6000.7.0a2`で以下を実観測済みです。

- Compile Error 0
- Exact 77/77 Tool Discovery
- Duplicate Tool 0
- Read-only Domain Smoke PASS
- Stale Revision rejection PASS
- Missing / Wrong Approval rejection PASS
- One-time Plan reuse rejection PASS
- Profiler Capture PASS
- UI / Animation / Audio / Cinematic Scoped Mutation E2E PASS
- Addressables Package未導入時の`UNSUPPORTED`境界 PASS
- Agent Routing PASS
- Delegated Failure Propagation PASS（false successなし）
- Cross-domain Workflow PASS
- Timeout / Cancel / Domain Reload callbacks PASS
- Previous Production 45 Regression PASS

## Supplemental / Not Verified

GitHub ActionsはSupplemental Evidenceです。Runner Step開始前に利用不能だった実行は`not_verified`として保持し、PASSにもCode Failureにも読み替えません。

v1.1.0で未検証の範囲:

- Automated CI
- Package Editor Test Runner
- Addressables Positive Backend Matrix
- External MCP Transport Disconnect/Reconnect
- Player / Target Device Tool Execution
- 全Unity 6000.x Patch / 全Render Pipeline Package Versionの組み合わせ

これらはEditor-only v1.1.0 ReleaseのBlocking Gateにはしません。Addressables Packageが存在しない環境は明示`UNSUPPORTED`が正式な境界です。

## Current Scope

- Unity Editor専用
- Minimum Unity: `6000.0`
- MCP for Unity Bridge dependency: `10.1.2`
- All tools: `AutoRegister = false`
- Build Domain: excluded
- Addressables Content Build: excluded
- MovieCreator runtime: excluded
- LiveCreator runtime: excluded

## Upgrade

v1.0.xからはPackage参照を`v1.1.0`へ更新してください。公開済みTagはimmutableであり、既存v1.0.x Tagは変更しません。

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.1.0
```
