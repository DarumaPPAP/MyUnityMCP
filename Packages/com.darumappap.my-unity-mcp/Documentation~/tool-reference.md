# Tool Reference

全Toolは`AutoRegister = false`です。`requestId`は追跡用で、多くのToolでは省略可能です。Mutation系ID、Revision、Tokenは直前Responseをそのまま使用します。

v1.1.0 Production Surfaceは **77 Tool** です。

## Graphics Domain — 32

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
| APV | `graphics.prepare_apv_bake_plan` | Baking Set／Scenario／Scene／Outputを固定 | なし |
| APV | `graphics.start_apv_bake` | 承認済みAPV Jobを開始 | Asset出力 |
| APV | `graphics.get_apv_bake_status` | APV Job状態とOutput差分を取得 | なし |
| APV | `graphics.cancel_apv_bake` | APV JobへCancellation要求 | Job制御 |
| Capture | `graphics.capture_evaluation` | 互換Color Captureを作成 | Library出力 |
| Capture | `graphics.capture_evidence` | COLOR／DEPTH／OBJECT_ID Bundleを作成 | Library出力 |
| Refine | `graphics.refine_direction` | Human ReviewからDirectionを更新 | なし |
| Review | `graphics.submit_visual_review` | Evidence DigestへHuman Decisionを固定 | Session記録 |
| Refine | `graphics.refine_from_visual_review` | Rejected Reviewを次Planへ変換 | なし |
| Evaluation | `graphics.prepare_acceptance_profile` | 評価項目とBudgetを固定 | なし |
| Evaluation | `graphics.evaluate_capture` | 外部MeasurementでEvidenceを評価 | Session記録 |
| Refine | `graphics.refine_from_evaluation` | 失敗Evaluationを次Planへ変換 | なし |
| Execution | `graphics.get_execution_status` | Progress／Terminal Stateを取得 | なし |
| Execution | `graphics.cancel_execution` | Executionへ協調Cancellation要求 | 実行制御 |
| Execution | `graphics.get_execution_history` | 永続Execution Historyを取得 | なし |
| Execution | `graphics.get_error_catalog` | Error CodeとRetry手順を取得 | なし |
| Execution | `graphics.get_support_matrix` | 実行中PackageのSupport Contractを取得 | なし |

## UnityAgent Control Plane — 10

UnityAgentMCPはUnity APIを直接Mutationしません。Operational Domain MCPへStep単位で委譲します。

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

Mutationを伴うWorkflowでは`compile_graph` → `preview_execution` → `submit_approval` → `start_execution`を省略できません。

## WorldCreator — 3

- `world.compile_workflow`
- `world.start_preflight`
- `world.create_review_handoff`

Canonical Preflightは`graphics.inspect_project` → `graphics.inspect_scene` → `graphics.validate_scene`です。WorldCreatorはDirect Unity Mutationを行いません。

## Profiler Domain — 8

- `profiler.inspect_environment`
- `profiler.inspect_counters`
- `profiler.prepare_capture`
- `profiler.start_capture`
- `profiler.get_capture_status`
- `profiler.cancel_capture`
- `profiler.summarize_capture`
- `profiler.compare_baseline`

Profiler ResultはEditor Environment IdentityをEvidenceとして保持します。Target Device性能として扱うには別のDevice Evidenceが必要です。

## Addressables Domain — 4

- `addressables.inspect`
- `addressables.prepare_entry`
- `addressables.apply_entry`
- `addressables.get_support_matrix`

AddressablesはOptional Packageです。Package未導入時は`UNSUPPORTED`を返し、自動Package導入、Settings/Group生成、Content BuildへFallbackしません。

## UI Domain — 5

- `ui.inspect`
- `ui.validate`
- `ui.prepare_rect_transform`
- `ui.apply_rect_transform`
- `ui.get_support_matrix`

Mutation ScopeはRectTransformに限定され、Expected Revision、One-time Plan、Approval Tokenが必要です。

## Animation Domain — 5

- `animation.inspect`
- `animation.validate`
- `animation.prepare_parameter`
- `animation.apply_parameter`
- `animation.get_support_matrix`

Mutation ScopeはAnimatorController Parameterに限定します。State Machine / Transition / Curve / Clip Event書換えは対象外です。

## Audio Domain — 5

- `audio.inspect`
- `audio.validate`
- `audio.prepare_source`
- `audio.apply_source`
- `audio.get_support_matrix`

Mutation Scopeは対応AudioSource Propertyに限定します。AudioClip ReplacementやAudioMixer Asset生成は対象外です。

## Cinematic Domain — 5

- `cinematic.inspect`
- `cinematic.validate`
- `cinematic.prepare_director`
- `cinematic.apply_director`
- `cinematic.get_support_matrix`

Mutation Scopeは対応PlayableDirector Settingsに限定します。PlayableAsset Replacement、Timeline Track/Clip生成、Generic Binding、Cinemachine Shot Mutationは対象外です。

## Common Safety

Mutation系Domainは以下を共通境界とします。

- PrepareはRead-only
- Expected Revision必須
- One-time Plan必須
- Approval Token必須
- Exact Scope外Mutation禁止
- Automatic Save禁止
- Silent Fallback禁止

## Common Response

主に以下を返します。

- `status` / `success`: Tool結果
- `sessionId` / `revision`: Editor状態の同一性
- `data`: Tool固有結果
- `error` / `errorCode`: Error Code、Retryability、Remediation
- `execution`: Execution ID、Trace ID、Progress、Terminal State

Parameterの正確なSchemaはBridgeが公開するMCP Tool Schemaを正本とします。
