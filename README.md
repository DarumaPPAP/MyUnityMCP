# MyUnityMCP

MyUnityMCPは、Unity EditorをMCP Clientから安全に操作するためのEditor拡張Packageです。Project／Scene Inspection、構造化Planning、承認付きMutation／Save／Bake、Capture Evidence、Visual Evaluation／Refine、Profiler、Addressables Entry管理、UI、Animation、Audio、Cinematic、および複数Toolを統括するUnityAgentMCP Control PlaneとWorldCreatorを提供します。

## v1.1.1 Production Surface

Current `main` / v1.1.1 Release Candidateは **77 Tool** のEditor Operational Surfaceです。

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

Build Domain、Addressables Content Build、MovieCreator runtime、LiveCreator runtimeはv1.1.1 Surfaceに含めません。

- Unity Editor専用
- Unity `6000.0`以上
- Toolはすべて`AutoRegister = false`
- UnityAgentMCPはControl PlaneでありUnity APIを直接Mutationしません
- WorldCreatorはRead-only PreflightとHuman Review Handoffを担当します
- AddressablesはOptional Packageです。未導入時は自動導入せず`UNSUPPORTED`を返します

## Verification

v1.1.0のUnity `6000.7.0a2` Direct Editor Evidenceをbaselineとして保持し、v1.1.1 Release PRではUnity `6000.0.75f1` EditMode / Compile / Production Tool Discovery / Agent Contractを再検証してから公開します。

GitHub ActionsのEditor Runnerが実行不能な場合はPASSに読み替えません。Addressables Positive Backend Matrix、External Transport Disconnect/Reconnect、Target Deviceは未検証範囲として明示します。

## Safety Model

```text
Direct Domain:
Inspect → Prepare → Exact Diff → Revision → Approval → Apply

Agent Control Plane:
Inspect Capabilities → Validate Workflow → Compile Graph → Preview
                    → Explicit Approval → Delegate
                    → Status / Cancel / History

WorldCreator:
Visual Goal → Read-only Preflight → Human Review Handoff
```

自動Save、自動Full Bake、Generic SerializedProperty Mutation、Silent Fallback、自動Visual Acceptanceは禁止です。

## Quick Start

1. Unity Package ManagerからMCP for Unity Bridgeを導入します。
2. MyUnityMCPをGit URL、`.tgz`、またはEmbedded Packageとして導入します。
3. Unityで `Window > MCP for Unity` を開きます。
4. MCP Client側では必要なToolだけを許可します。
5. 最初の確認は`graphics.inspect_project`または`agent.inspect_capabilities`から開始します。

## Repository Layout

```text
MyUnityMCP/
├─ Packages/        # 実行可能なUPM Package
├─ Catalog/         # Operational Capability / Production Surface Contract
├─ Specs/           # 現行製品仕様
├─ Tests/           # Release / Compatibility / Editor Evidence
├─ Templates/       # MCP Client / CI / Acceptance Profile配布物
├─ Design/          # 未実装Capabilityの設計資産
└─ .github/         # Repository automation
```

Graph EngineeringのGoal / Workflow / Run Recordなどの開発制御資産は、この製品RepositoryのProduction `main`には含めません。

## Documentation

- [Installation](Packages/com.darumappap.my-unity-mcp/Documentation~/installation.md)
- [Quick Start](Packages/com.darumappap.my-unity-mcp/Documentation~/quick-start.md)
- [Tool Reference](Packages/com.darumappap.my-unity-mcp/Documentation~/tool-reference.md)
- [Production Surface](Packages/com.darumappap.my-unity-mcp/Documentation~/production-surface.md)
- [Safety Model](Packages/com.darumappap.my-unity-mcp/Documentation~/safety-model.md)
- [Troubleshooting](Packages/com.darumappap.my-unity-mcp/Documentation~/troubleshooting.md)
- [Known Issues](Packages/com.darumappap.my-unity-mcp/Documentation~/known-issues.md)

## Distribution

- Latest published stable: `v1.1.0`
- UPM Package: `Packages/com.darumappap.my-unity-mcp`
- MCP Client Templates: `Templates/McpClients`

公開済みTagはimmutableです。新しい製品内容は新Version / 新Tagで公開します。

## License

MIT License。詳細は[LICENSE](LICENSE)を参照してください。
