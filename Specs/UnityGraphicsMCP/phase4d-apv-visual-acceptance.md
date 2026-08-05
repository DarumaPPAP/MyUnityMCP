# Phase 4D — APV Bake Job and Visual Acceptance

## 1. 目的

Phase 4Dは、Graphics変更後の永続化、限定Bake、Capture Evidence、Visual Evaluation、不合格理由、次IterationのRefine Directionまでを一つの監査可能な閉ループとして成立させる。

```text
変更
  ↓
明示Save
  ↓
限定Bake / APV Bake Job
  ↓
COLOR・LINEAR_DEPTH・OBJECT_ID Capture
  ↓
Acceptance ProfileによるVisual Evaluation
  ↓
不合格理由・Critical Failure・Performance違反・Object ID
  ↓
構造化Refine Direction
```

Unity C#は画像の意味を自動判定しない。Visual Measurementは人間または外部Vision Evaluatorが明示入力し、最終AcceptanceはPhase 4CのHuman Review契約で確定する。

## 2. APV Tool

### `graphics.prepare_apv_bake_plan`

Read-onlyで次の前提を固定する。

- `ProbeVolumeBakingSet` Asset Path
- Lighting Scenario名
- 明示Scene集合
- URP / HDRP Pipeline Capability
- APV Reflection Backend Capability
- Output Asset Root
- Timeout
- Approval Token
- Plan Digest

明示Scene集合はBaking SetのScene集合と完全一致しなければならない。対象Sceneは`Assets/`配下の既存Loaded Sceneに限定する。Built-in RP、未対応SRP、Baking Set不在、Scenario不在、APV API不在はSilent Fallbackせず明示失敗する。

### `graphics.start_apv_bake`

承認済みPlanを一度だけ消費し、APV Bake Jobを開始する。

- `bakeMode = EXPLICIT_APV_BAKING_SET`
- Expected Revision必須
- Approval Token必須
- Baking Set Digest再検証
- Loaded Scene集合再検証
- Output Baseline SHA-256取得
- `ProbeReferenceVolume`へBaking SetとLighting Scenarioを明示設定
- `AdaptiveProbeVolumes.BakeAsync`をReflectionで呼び出す

### `graphics.get_apv_bake_status`

Job状態を返す。

- `RUNNING`
- `SUCCEEDED`
- `PARTIAL`
- `FAILED`
- `CANCEL_REQUESTED`
- `CANCELLED`

完了時にOutput Root内Assetの追加・変更・削除をSHA-256差分として記録する。Output差分が無い正常終了は成功扱いにせず`APV_BAKE_NO_OUTPUT_DIFF`とする。

### `graphics.cancel_apv_bake`

Cancellationを要求する。

- APV BackendにCancel APIが存在する場合はNative Cancelを実行
- `Lightmapping.isRunning`の場合は`Lightmapping.Cancel()`を実行
- Native Cancelがない場合も協調PollingでJobを監視
- 生成済みOutputがある場合は`PARTIAL`
- Outputが無い場合は`CANCELLED`
- Cancellation後もPartial Resultと失敗理由を失わない

## 3. APV Safety Contract

- PrepareとStartを分離する
- Planは10分TTL
- Planは一度だけ使用可能
- Revision変更時は開始拒否または実行中Cancellation
- Timeout時はCancellationへ移行
- 自動Saveなし
- Unity Undoなし
- 自動Rollback保証なし
- Output差分がある場合だけEditor Revisionを進める
- APV実行前のEditor Baking Set / Lighting Scenarioは可能な範囲で復元する
- 実装詳細に依存するAPIはReflection Capabilityとして検証する

## 4. Acceptance Profile

### `graphics.prepare_acceptance_profile`

次の評価契約をSession-local Profileとして固定する。

- Profile名
- 最低総合合格値
- 1～32件の評価項目
- 各項目のWeight
- 各項目の最低合格値
- Critical Failure閾値
- 必須 / 任意
- 推奨改善Action
- Reference Capture ID / Evidence Digest
- Performance Budget

Score範囲は0～100、Weightは正数とする。ReferenceはPhase 4C Capture RecordとEvidence Digestの完全一致を要求する。Unity側はReference Imageの意味比較を実行しない。

## 5. Performance Budget

Profileは次の上限を任意に保持できる。

- CPU Frame Time ms
- GPU Frame Time ms
- Memory MB
- Draw Calls

MeasurementはPlayer、実機、Profiler、外部計測などのSourceを明示する。必須BudgetにMeasurementが無い場合は`INCOMPLETE`、上限超過時は`FAILED`とする。

## 6. Visual Evaluation

### `graphics.evaluate_capture`

Phase 4C Capture EvidenceとAcceptance Profileへ外部Measurementを適用する。

各Measurement:

- Criterion ID
- Score
- Confidence
- Summary
- Evidence
- Affected Object ID

判定:

- `PASSED`: 必須Evidenceが揃い、Critical Failureなし、各最低値・総合最低値・Performance Budgetを満たす
- `FAILED`: Critical Failure、評価項目最低値未達、総合最低値未達、Performance Budget違反のいずれか
- `INCOMPLETE`: 必須Measurementまたは必須Performance Measurementが不足

`PASSED`は自動Profile条件を満たしたことだけを意味し、Human Visual Acceptanceではない。最終承認にはPhase 4Cの`graphics.submit_visual_review`と`VISUAL_ACCEPTED`が必要である。

## 7. Object ID関連付け

MeasurementのAffected Object IDをCapture Bundleの`OBJECT_ID_MAP`へ照合し、次を返す。

- Object ID
- Renderer GlobalObjectId
- Renderer Type
- Name
- Hierarchy Path
- Scene Path
- Mapping Status

Map不在、読込失敗、Object未登録はSilent SuccessにせずMapping Statusへ記録する。

## 8. Structured Refine Direction

### `graphics.refine_from_evaluation`

`FAILED`または`INCOMPLETE`だけを次のDirection Planへ変換する。

構造:

- Evaluation ID / Digest
- Capture ID / Evidence Digest
- Acceptance Profile ID
- Decision
- Weighted Score
- Failed Criteria
- Critical Failure
- Performance Failures
- Affected Objects
- Recommended Actions
- Required Recapture Channels
- Human Review Required
- Acceptance Confirmation Requirement

`PASSED`から不要なRefine Planは生成しない。

## 9. Closed-loop Completion Gate

EditMode E2E契約で次を成立させる。

1. Scene変更
2. `graphics.prepare_save_plan`
3. `graphics.apply_save_plan`
4. `graphics.prepare_apv_bake_plan`
5. `graphics.start_apv_bake`
6. APV Output差分確定
7. COLOR / LINEAR_DEPTH / OBJECT_ID Capture Record
8. `graphics.prepare_acceptance_profile`
9. `graphics.evaluate_capture`
10. 不合格理由生成
11. `graphics.refine_from_evaluation`
12. 次Direction Plan生成

CIでは実APV Bake時間とPipeline Package依存を偽装しない。Job State Machine、Output差分、Cancellation、Partial ResultはBackend Overrideで契約検証し、実APV Reflection BackendのCompileとCapability解決を別に検証する。

## 10. 非保証範囲

- 全Unity VersionでのAPV内部API互換性
- 全URP / HDRP Package Version
- Player Build中のBake
- Target Device上のBake
- Unity C#だけによる画像意味判定
- Human Reviewなしの最終Visual Acceptance
- Bake失敗後の完全自動Rollback
