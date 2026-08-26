# UnityAgentMCP Operational Contract

## Status

- Product state: `Editor Operational`
- Kind: Control Plane
- Direct Unity mutation: prohibited
- Tool count: 10
- Player runtime execution: unsupported

v1.1.0では以下のOperational Domainへ委譲できます。

- `unity_graphics_mcp`
- `unity_profiler_mcp`
- `unity_addressables_mcp`
- `unity_ui_mcp`
- `unity_animation_mcp`
- `unity_audio_mcp`
- `unity_cinematic_mcp`

WorldCreatorはCreator LayerとしてAgent Control Planeを利用します。

## Responsibility

UnityAgentMCPはWorkflow-level Coordinationだけを担当します。Requested Stepを検証し、Dependency GraphをCompileし、Execution ScopeをPreviewし、Approval / Revision Boundaryを適用して、各Executable StepをOperational Domain MCPへ委譲します。

UnityAgentMCPはUnity Object Mutation Logicを所有せず、Domain MCPの代わりに任意Unity APIを書き換えません。

## Tool Surface

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

全Tool `AutoRegister = false`です。

## Workflow Contract

```text
inspect capabilities
→ validate workflow
→ compile graph against current Editor revision
→ preview exact ordered steps and approval groups
→ submit explicit approval when required
→ start cooperative execution
→ delegate one safe step at a time
→ status / cancel / history
```

Workflow Stepは`stepId`、`domainId`、`toolName`、`toolGroup`、`dependsOn`、Tool Parametersを宣言します。

ValidationはDuplicate / Missing Step ID、Missing Dependency、Cycle、Undeclared Tool / Group、Non-operational Domain、Direct Control Plane Mutationを拒否します。

## Runtime Catalog v5

Runtime Catalogの永続化Schemaはv5です。各Operational Domainの`tools`は、Tool名、Canonical Group、Policyを持つObject配列です。Domain内の旧`toolGroups`配列は使用しません。Policyの許可値は、Effectが`none`、`scene_mutation`、`asset_mutation`、`save`、`bake`、`capture_control`、Revision Policyが`must_remain`または`may_advance`、Retry Policyが`none`です。`directUnityMutationAllowed`は常にfalseです。

RuntimeはCatalog検証後にOrdinalのTool Indexを構築し、重複Tool、欠落Tool、未知Tool、Policy不備、未登録DelegateをLoad Failureとして扱います。WorkflowはDomain存在、Domain内のTool存在、Domain由来Group、ToolのCanonical Group一致の順に検証します。未知Groupは`AGENT-TOOL-GROUP-MISSING`、既知だがCanonical Groupと異なるGroupは`AGENT-TOOL-GROUP-MISMATCH`です。

`agent.inspect_capabilities`はPublic Compatibility Projectionです。内部Tool ObjectやPolicyを返さず、従来どおり`toolGroups`と`tools`を文字列配列としてDomain順、Tool順、Groupの初出順を維持して返します。`creators[].tools`もCatalog上は文字列配列のままです。ApprovalはRuntime内のTool名リストではなく、Catalog Tool DefinitionのPolicyだけを参照します。

## Revision and Approval Safety

- Graph Compileは`Session.Revision`へ固定
- Preview / StartはEditor Revision変更後の古いGraphを拒否
- Mutation-capable Groupは明示Approval必須
- Approval Tokenは期限付きかつCompiled Graph / Group Scope限定
- Read-only Delegateが予期せずRevisionを変更した場合はExecutionを中断
- Successful Mutation後はExpected Revisionを更新して次Stepへ進む

## Cooperative Execution

- Timeout: 1–3600 seconds、default 60
- Safe Step BoundaryでCancellation
- Client Disconnect interruption
- Structured Terminal State
- Persistent Execution History
- Reload / Restart / Disconnect後のAutomatic Resumeは禁止

## Delegation Boundary

Agentは`Packages/com.darumappap.my-unity-mcp/Editor/Operational/Agent/UnityAgentMcpCatalog.json`で`editor_operational`と宣言され、Repository Catalog / Capability Contractと整合するDomainだけを実行します。

v1.1.0ではGraphics、Profiler、Addressables、UI、Animation、Audio、CinematicがOperational Delegateです。各Domain固有のRevision / Plan / Approval / Scope ContractはAgent経由でも省略できません。

Addressables Packageが存在しない場合はDomainの明示`UNSUPPORTED`をそのまま伝播し、Package導入やSettings/Group生成へSilent Fallbackしません。

新しいDomainを追加する場合、Production Catalog、Agent Runtime Catalog、Capability Contract、Tests、Documentation、Safety Evidenceを同一Deliveryで更新します。

## Result Integrity

Delegated FailureをSuccessとして表現しません。

- first-step failure → `FAILED`
- failure after earlier successful steps → `PARTIAL`
- revision / timeout / disconnect interruption → `INTERRUPTED`
- cancellation → `CANCELLED`
- all steps completed successfully → `SUCCEEDED`

`unavailable` / `UNSUPPORTED` / failureはPASSへ変換しません。

## Evidence

v1.1.0 Operational RoutingはUnity `6000.7.0a2` Direct Editor Evidenceを正本とします。

- Exact 77 Tool Discovery
- Duplicate 0
- Agent Domain Routing PASS
- Addressables `UNSUPPORTED` delegated failure propagation PASS
- Cross-domain Workflow PASS
- Timeout / Cancel / Domain Reload callbacks PASS
- Previous Production 45 Regression PASS

GitHub ActionsがRunner Step開始前に利用不能だった場合は`not_verified`として保持し、Direct Editor Evidenceを否定するCode Failureとして扱いません。
