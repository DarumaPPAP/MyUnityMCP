# UnityAgentMCP Runtime Contract

- Release status in v1.0: `Not included`
- Development branch status: `Implementation in progress, Unity CI unverified`
- Runtime role: `Product Control Plane`
- Codex development harness: `No`

## Boundary

UnityAgentMCPはCreator／Domain選択、Tool Group Activation、Policy、Approval、実行順序、結果統合を所有するProduct Runtimeです。

Unity APIを直接Mutationしません。実際の操作は、Catalogで`editor_operational`となっているDomain Toolへ委譲します。現在の最小垂直Sliceでは、既存`unity_graphics_mcp`のRead-only Toolだけを実行Delegateとして登録しています。

## Tools

すべて`AutoRegister = false`です。

- `agent.inspect_capabilities`
- `agent.validate_workflow`
- `agent.compile_graph`
- `agent.preview_execution`
- `agent.submit_approval`
- `agent.start_execution`
- `agent.get_execution_status`
- `agent.cancel_execution`
- `agent.get_execution_history`
- `agent.get_error_catalog`

## Execution contract

```text
Catalog load
→ Workflow validation
→ DAG compile
→ Side-effect preview
→ Tool-group approval
→ Revision revalidation
→ Domain delegation
→ Result aggregation
→ History persistence
```

## Safety

- Design-only Domainは`AGENT-DOMAIN-NOT-OPERATIONAL`で拒否する。
- 未宣言Tool／Tool Groupを拒否する。
- Cycle／欠落Dependencyを拒否する。
- Mutation／Save／Bake／Build／Content Buildは一時Approval Tokenを要求する。
- Approvalは10分で期限切れになる。
- Preview後のRevision変更を拒否する。
- Control Plane SourceからUnity Mutation APIを呼ばない。
- Delegate未登録ToolをSilent Fallbackしない。
- Domain Reload、Compile開始、Editor終了時のRunning ExecutionをInterruptedとして記録する。

## Persistence

Execution Historyは`Library/MyUnityMCP/AgentExecution/history.jsonl`へ保存します。Project AssetやRelease Packageへ実行履歴を混入させません。

## Verification gate

以下が揃うまでRelease Catalogで`editor_operational`へ昇格しません。

- Unity Editor Compile
- Agent Runtime Editor Tests
- Existing Graphics inspection delegation E2E
- Direct mutation regression
- Existing GraphicsMCP regression
