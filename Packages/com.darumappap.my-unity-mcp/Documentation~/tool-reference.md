# Tool Reference

全Toolは`AutoRegister = false`です。`requestId`は追跡用で、多くのToolでは省略可能です。Mutation系ID、Revision、Tokenは直前Responseをそのまま使用します。

Current mainのProduction Tool Surfaceは **45 Tool = 32 Graphics + 10 Agent + 3 WorldCreator** です。`v1.0.0` Tagは32 Graphics Toolのimmutable baselineです。

## Graphics Domain

| Group | Tool | Purpose | Side effect |
|---|---|---|---|
| Inspection | `graphics.inspect_project` | Project環境、Pipeline、Package、Build TargetをRead-only取得 | なし |
| Inspection | `graphics.inspect_scene` | Scene SnapshotとPaging結果を取得 | なし |
| Inspection | `graphics.validate_scene` | Graphics規則によるFindingを返す | なし |
| Planning | `graphics.compile_direction` | 構造化Visual IntentからDirection Planを作成 | なし |
| Planning | `graphics.preview_plan` | 作成／変更／Dirty／Bake候補を予告 | なし |
| Mutation | `graphics.prepare_light_plan` | Light操作をExact DiffとTokenへ変換 | なし |
| Mutation | `graphics.apply_plan` | 承認済みLight Transactionを適用 | Scene Dirty |
| Mutation | `graphics.undo_last_transaction` | 直近Light Transactionを条件付きUndo | Scene変更 |
| Mutation | `graphics.prepare_environment_plan` | Camera／Probe／Volume操作を準備 | なし |
| Mutation | `graphics.apply_environment_plan` | 承認済みEnvironment Transactionを適用 | Scene Dirty |
| Mutation | `graphics.undo_last_environment_transaction` | 直近Environment Transactionを条件付きUndo | Scene変更 |
| Save | `graphics.prepare_save_plan` | 既存Loaded SceneのSave Baselineを固定 | なし |
| Save | `graphics.apply_save_plan` | 承認済みSceneだけを保存 | Scene File書込 |
| Bake | `graphics.prepare_bake_plan` | Dirty DependencyとBackendを固定 | なし |
| Bake | `graphics.bake_dependencies` | 承認済みDependencyだけを同期Bake | Asset／Lighting出力 |
| Capture | `graphics.capture_evaluation` | 互換Color Captureを作成 | Library出力 |
| Refine | `graphics.refine_direction` | Human ReviewからDirectionを更新 | なし |
| Capture | `graphics.capture_evidence` | COLOR／DEPTH／OBJECT_ID Bundleを作成 | Library出力 |
| Review | `graphics.submit_visual_review` | Evidence DigestへHuman Decisionを固定 | Session記録 |
| Refine | `graphics.refine_from_visual_review` | Rejected Reviewを次Planへ変換 | なし |
| APV | `graphics.prepare_apv_bake_plan` | Baking Set／Scenario／Scene／Outputを固定 | なし |
| APV | `graphics.start_apv_bake` | 承認済みAPV Jobを開始 | Asset出力 |
| APV | `graphics.get_apv_bake_status` | APV Job状態とOutput差分を取得 | なし |
| APV | `graphics.cancel_apv_bake` | APV JobへCancellation要求 | Job制御 |
| Evaluation | `graphics.prepare_acceptance_profile` | 評価項目とBudgetを固定 | なし |
| Evaluation | `graphics.evaluate_capture` | 外部MeasurementでEvidenceを評価 | Session記録 |
| Refine | `graphics.refine_from_evaluation` | 失敗Evaluationを次Planへ変換 | なし |
| Execution | `graphics.get_execution_status` | Progress／Terminal Stateを取得 | なし |
| Execution | `graphics.cancel_execution` | Executionへ協調Cancellation要求 | 実行制御 |
| Execution | `graphics.get_execution_history` | 永続Execution Historyを取得 | なし |
| Execution | `graphics.get_error_catalog` | Error CodeとRetry手順を取得 | なし |
| Execution | `graphics.get_support_matrix` | 実行中PackageのSupport Contractを取得 | なし |

## UnityAgent Control Plane

UnityAgentMCPはUnity APIを直接Mutationしません。Workflowを検証／Compileして、Operational Domain MCPへStep単位で委譲します。Current mainでOperationalなDelegate Domainは`unity_graphics_mcp`です。

| Group | Tool | Purpose | Side effect |
|---|---|---|---|
| Agent | `agent.inspect_capabilities` | Domain、Tool Group、実行可否をRead-only取得 | なし |
| Agent | `agent.validate_workflow` | Step、依存関係、Domain／Tool宣言をRead-only検証 | なし |
| Agent | `agent.compile_graph` | WorkflowをRevision固定のExecution GraphへCompile | なし |
| Agent | `agent.preview_execution` | Step、Mutation Group、必要ApprovalをPreview | なし |
| Agent | `agent.submit_approval` | 必要Groupを明示承認し期限付きTokenを発行 | Session内承認状態 |
| Agent | `agent.start_execution` | Revision／Approvalを再検証して協調Executionを開始 | Domainへ委譲 |
| Agent | `agent.get_execution_status` | Agent Execution状態とStep Resultを取得 | なし |
| Agent | `agent.cancel_execution` | Running Executionを安全なStep境界でCancel | 実行制御 |
| Agent | `agent.get_execution_history` | 永続Agent Execution Historyを取得 | なし |
| Agent | `agent.get_error_catalog` | Agent Error CodeとRetryabilityを取得 | なし |

AgentのMutationを伴うWorkflowでは、`compile_graph` → `preview_execution` → `submit_approval` → `start_execution`の境界を省略できません。非Operational Domainは`agent.validate_workflow`で拒否されます。

## WorldCreator

WorldCreatorはVisual GoalをRead-only Graphics Preflightへ変換するCreator Layerです。Unity APIを直接Mutationせず、UnityAgentMCPを経由して既存Graphics Toolへ委譲します。

| Group | Tool | Purpose | Side effect |
|---|---|---|---|
| Creator | `world.compile_workflow` | Visual Goal、Scene Scope、Mood、Platform、禁止変更、Acceptance条件からRevision固定Preflight GraphをCompile | なし |
| Creator | `world.start_preflight` | Compile済みWorld GraphをAgent Executionとして開始 | Read-only Domain実行 |
| Creator | `world.create_review_handoff` | Preflight結果をHuman Review必須のHandoffへ変換 | なし |

Canonical Preflightは`graphics.inspect_project` → `graphics.inspect_scene` → `graphics.validate_scene`です。`world.create_review_handoff`は`HUMAN_REVIEW_REQUIRED`を返し、`automaticVisualAcceptance`は`false`です。Mutation／Save／BakeはWorldCreatorから直接実行せず、後続Graphics Domainの既存Approval Boundaryへ渡します。

## Common response

Graphics Domain Responseでは主に次を使用します。

- `status`: Tool結果
- `sessionId`／`revision`: Editor状態の同一性
- `data`: Tool固有結果
- `issues`: Domain Finding
- `error`: Code、Category、Retryability、Retry Action、Remediation
- `execution`: Execution ID、Trace ID、Duration、Progress

Agent／WorldCreator Responseでは`success`、`errorCode`、`message`、Graph／Execution ID、Step Result、Review Handoffを返します。

Parameterの正確なSchemaはBridgeが公開するMCP Tool Schemaを正本とします。
