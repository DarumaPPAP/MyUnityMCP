# MyUnityMCP Production Surface

MyUnityMCP v1.1.0のProduction Editor Surfaceは **77 Tool** です。

## Tool composition

| Capability | Tools | Status |
|---|---:|---|
| Graphics | 32 | Editor Operational |
| Agent | 10 | Editor Operational |
| WorldCreator | 3 | Editor Operational |
| Profiler | 8 | Editor Operational |
| Addressables | 4 | Editor Operational / Optional Backend |
| UI | 5 | Editor Operational |
| Animation | 5 | Editor Operational |
| Audio | 5 | Editor Operational |
| Cinematic | 5 | Editor Operational |
| **Total** | **77** | **Editor Operational** |

Build Domain、Addressables Content Build、MovieCreator runtime、LiveCreator runtimeはCurrent Surfaceに含めません。

## Operational rules

- 全Toolは`AutoRegister = false`。
- MutationはExpected Revision、one-time Plan、Approval Tokenを必須とします。
- Automatic Save、Automatic Full Bake、Generic SerializedProperty Mutation、Silent Fallback、Automatic Visual Acceptanceは禁止です。
- UnityAgentMCPはControl Planeであり、Unity APIを直接Mutationしません。
- Addressables Package未導入時は自動導入せず`UNSUPPORTED`を返します。

## Verification authority

Primary EvidenceはDirect Unity Editor Validationです。

Unity `6000.7.0a2`で以下を確認済みです。

- Compile Error 0
- Exact 77 Tool Discovery / Duplicate 0
- Extended Domain Read-only Smoke
- Stale Revision / Approval / One-time Plan Safety
- Profiler Capture
- UI / Animation / Audio / Cinematic Scoped Mutation E2E
- Addressables Package-absent `UNSUPPORTED` Boundary
- Agent Operational Routing / Delegated Failure Propagation
- Cross-domain Workflow
- Timeout / Cancel / Domain Reload callbacks
- Previous Production 45 Regression

GitHub ActionsがRunner Step開始前に停止した実行は`not_verified`として保持し、PASSまたはCode Failureへ読み替えません。

## Production contracts

- `Catalog/production-surface-contract.yaml`
- `Catalog/mcp-catalog.yaml`
- `Tests/Compatibility/production-editor-acceptance.yaml`
- `Tests/Compatibility/production-validation-evidence.yaml`
- `Tests/Compatibility/support-matrix.yaml`
- `Tests/Compatibility/release-verification.yaml`

開発用のGraph Engineering実行記録、開発branch、作業Run状態はMyUnityMCPのProduction Surfaceには含めません。
