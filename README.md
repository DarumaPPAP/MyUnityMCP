# MyUnityMCP

MyUnityMCPは、Unity EditorをMCP Clientから安全に操作するためのEditor拡張Packageです。Project／SceneのInspection、構造化Planning、明示承認付きMutation／Save／Bake、Capture Evidence、Visual Evaluation、Refine、長時間実行の履歴・Timeout・Cancellationを提供します。

## v1.0 scope

- Unity Editor専用
- Unity `6000.0`以上
- CI検証環境: Unity `6000.0.75f1`
- MCP Tool: 32
- Toolはすべて既定で非公開（`AutoRegister = false`）
- Mutation、Save、Bakeはそれぞれ別の承認境界
- Player／実機上でのTool実行は非対応

## Quick Start

1. Unity Package ManagerからMCP for Unity Bridgeを導入します。
2. このRepositoryのPackageをGit URL、`.tgz`、またはEmbedded Packageとして導入します。
3. Unityで `Window > MCP for Unity` を開き、検出されたMCP Clientを設定します。
4. Client側では必要なToolだけを許可します。
5. 最初のCallは `graphics.inspect_project`、続いて `graphics.inspect_scene` を実行します。

詳細は[Quick Start](Packages/com.darumappap.my-unity-mcp/Documentation~/quick-start.md)と[Installation](Packages/com.darumappap.my-unity-mcp/Documentation~/installation.md)を参照してください。

## Safety model

```text
Inspect → Snapshot → Prepare Plan → Human/Client Approval → Apply
       → Prepare Save → Save
       → Prepare Bake → Bake
       → Capture → Evaluate → Human Review → Refine
```

- Read-only ToolはScene、Asset、Undo Groupを変更しません。
- ApplyはPlan ID、Expected Revision、Approval Token、Baselineを再検証します。
- SaveとBakeはMutationへ暗黙統合しません。
- 自動Save、自動Full Bake、任意SerializedProperty書換え、Silent Fallbackは禁止です。
- Visual EvaluationのPASSはHuman Acceptanceを代替しません。
- Domain Reload、Compile、Play Mode移行、Scene構成変更、Client切断、Unity再起動はExecution Historyへ構造化して残します。

## Documentation

- [Tool Reference](Packages/com.darumappap.my-unity-mcp/Documentation~/tool-reference.md)
- [Status / Error Codes](Packages/com.darumappap.my-unity-mcp/Documentation~/status-and-error-codes.md)
- [Safety Model](Packages/com.darumappap.my-unity-mcp/Documentation~/safety-model.md)
- [Bake Constraints](Packages/com.darumappap.my-unity-mcp/Documentation~/bake-constraints.md)
- [Pipeline Support](Packages/com.darumappap.my-unity-mcp/Documentation~/pipeline-support.md)
- [MCP Client Configuration](Packages/com.darumappap.my-unity-mcp/Documentation~/mcp-client-configuration.md)
- [Sample Workflow](Packages/com.darumappap.my-unity-mcp/Documentation~/sample-workflow.md)
- [Troubleshooting](Packages/com.darumappap.my-unity-mcp/Documentation~/troubleshooting.md)
- [Upgrade Guide](Packages/com.darumappap.my-unity-mcp/Documentation~/upgrade-guide.md)
- [Known Issues](Packages/com.darumappap.my-unity-mcp/Documentation~/known-issues.md)
- [Support Matrix](Specs/UnityGraphicsMCP/support-matrix.md)

## Distribution

- UPM Package: `Packages/com.darumappap.my-unity-mcp`
- Package Sample: `Samples~/Getting Started`
- Standalone Sample Project: `SampleProjects/MyUnityMCPGettingStarted`
- MCP Client Templates: `Templates/McpClients`
- Acceptance Profile Example: `Templates/AcceptanceProfiles`
- CI Template: `Templates/CI`

Tag `v1.0.0`のRelease WorkflowはPackage `.tgz`、Sample Project ZIP、Template ZIP、SHA-256一覧を生成します。

## Verification

Release Gateは次を検証します。

- Package Resolve／Editor Compile
- 32 Tool Discovery
- 125以上のEditor Contract Test
- 新規Sample ProjectでのPackage Compile
- Getting Started Sample Workflow
- Version／Manifest／Changelog／Support Matrix整合
- 必須文書／配布物／Known Issuesの存在

最新実績は`Tests/Compatibility/release-verification.yaml`、対応範囲は`Tests/Compatibility/support-matrix.yaml`を正本とします。

## Design-only assets

`UnityAgentMCP`、`LiveCreator`、`MovieCreator`、Graphics以外のDomain MCPは設計資産です。v1.0で実行可能なのは`unity_graphics_mcp`のUnity Editor Tool群であり、未実装Domainを実行可能とは表現しません。

## License

MIT License。詳細は[LICENSE](LICENSE)を参照してください。
