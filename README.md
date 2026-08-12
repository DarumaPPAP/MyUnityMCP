# MyUnityMCP

MyUnityMCPは、Unity EditorをMCP Clientから安全に操作するためのEditor拡張Packageです。Project／SceneのInspection、構造化Planning、明示承認付きMutation／Save／Bake、Capture Evidence、Visual Evaluation、Refine、長時間実行の履歴・Timeout・Cancellationに加え、複数Toolを安全に組み立てるUnityAgentMCP Control Planeと、Visual GoalをRead-only Preflightへ変換するWorldCreatorを提供します。

## Production baseline

Current `main`は **45 Tool = 32 Graphics + 10 Agent + 3 WorldCreator** のProduction Surfaceです。WorldCreatorまで実Unity Editorで`integration_verified_manual`として確定しています。

- Unity Editor専用
- Unity `6000.0`以上
- Stable `v1.0.0`: 32 Graphics Tool
- Current Production Source: 45 Tool
- Toolはすべて`AutoRegister = false`
- UnityAgentMCPはControl PlaneでありUnity APIを直接Mutationしません
- WorldCreatorはRead-only PreflightとHuman Review Handoffを担当します

## Stage 2-8 Integration Wave

`delivery/stage2-8-integration`では、Production 45 Toolを変更せずに6 Candidate Domain / 34 Toolを追加し、**79 Toolを同一Candidateとしてまとめて検証する**構成です。Build Domainは今回のCandidateから撤去しています。

| Stage | Capability | Candidate Tools | Combined Target |
|---|---|---:|---:|
| 2 | Profiler | +8 | 53 |
| 3 | Build | 0 / Retired | 53 |
| 4 | Addressables | +6 | 59 |
| 5 | UI | +5 | 64 |
| 6 | Animation | +5 | 69 |
| 7 | Audio | +5 | 74 |
| 8 | Cinematic | +5 | 79 |

現行Candidateは **Implementation Complete / Validation Reset** です。`integration_candidate`は`editor_operational`と同義ではなく、Validation完了前にProduction PASSとして扱いません。途中で`main`へMergeせず、79 Tool Validation Wave完了後にPromotion判断します。

詳細は[Stage 2-8 Integration Wave](Packages/com.darumappap.my-unity-mcp/Documentation~/stage2-8-integration.md)と[Implementation Status](Development/GraphEngineering/stage2-8-implementation-status.yaml)を参照してください。

## Repository Layout

```text
MyUnityMCP/
├─ Packages/        # 実行可能なUPM Package
├─ Catalog/         # Production / Integration Capability Contract
├─ Specs/           # Production仕様
├─ Tests/           # Release / Compatibility / Editor Contract
├─ Development/     # Graph Engineering / Integration Wave status
├─ TestProjects/    # Unity Editor検証Project
├─ SampleProjects/  # Standalone Sample Project
├─ Templates/       # MCP Client / CI / Acceptance Profile配布物
├─ Design/          # 未実装Capabilityの設計資産
└─ .github/         # Repository automation
```

## Safety model

```text
Direct Domain:
Inspect → Prepare → Exact Diff → Approval when required → Apply

Agent Control Plane:
Inspect Capabilities → Validate Workflow → Compile Graph → Preview
                    → Explicit Approval when required → Delegate
                    → Status / Cancel / History

WorldCreator:
Visual Goal → Read-only Preflight → Human Review Handoff
```

Stage 2〜8のShared Domain ContractではExpected Revision、期限付きOne-time Plan、Approval Token、Mutation Scopeを共通化しています。自動Save、自動Full Bake、Generic SerializedProperty Mutation、Silent Fallbackは禁止です。AddressablesはOptional Dependencyで、Package未導入時に自動導入・Settings生成へFallbackしません。

## Quick Start

1. Unity Package ManagerからMCP for Unity Bridgeを導入します。
2. MyUnityMCPをGit URL、`.tgz`、またはEmbedded Packageとして導入します。
3. Unityで `Window > MCP for Unity` を開きます。
4. MCP Client側では必要なToolだけを許可します。
5. Production Read-only確認は`graphics.inspect_project`から開始します。

Integration Waveの79 Tool CandidateはProduction用途ではなく、Validation用Branchとして使用してください。

## Documentation

- [Installation](Packages/com.darumappap.my-unity-mcp/Documentation~/installation.md)
- [Quick Start](Packages/com.darumappap.my-unity-mcp/Documentation~/quick-start.md)
- [Tool Reference](Packages/com.darumappap.my-unity-mcp/Documentation~/tool-reference.md)
- [Stage 2-8 Integration Wave](Packages/com.darumappap.my-unity-mcp/Documentation~/stage2-8-integration.md)
- [Safety Model](Packages/com.darumappap.my-unity-mcp/Documentation~/safety-model.md)
- [Troubleshooting](Packages/com.darumappap.my-unity-mcp/Documentation~/troubleshooting.md)
- [Known Issues](Packages/com.darumappap.my-unity-mcp/Documentation~/known-issues.md)

## Verification state

Production 45 Tool Evidenceは既存のStage 0 / WorldCreator Evidenceを正本として維持します。Build撤去でCandidate Surfaceが変わったため、旧85 Tool途中結果はhistorical evidenceとして保持しつつ、現行79 Toolを対象にValidationを再開します。

Validation前に次を主張しません。

- Stage 2〜8 Production PASS
- 79/79 Tool Discovery PASS
- Automated CI PASS
- Target Device PASS

## Distribution

- Stable baseline Tag: `v1.0.0`
- UPM Package: `Packages/com.darumappap.my-unity-mcp`
- Standalone Sample Project: `SampleProjects/MyUnityMCPGettingStarted`
- MCP Client Templates: `Templates/McpClients`

Version／Tag PublicationはCapability Source Promotionとは分離しています。公開済みTagはimmutableです。

## License

MIT License。詳細は[LICENSE](LICENSE)を参照してください。
