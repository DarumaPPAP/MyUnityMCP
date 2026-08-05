# UnityGraphicsMCP v1.0 Contract

## Scope

UnityGraphicsMCPはUnity Editor専用のGraphics Domain MCPです。対象Projectの事実をInspectし、構造化Planを作成し、承認された限定OperationだけをUnity APIへ適用します。

## Operational capabilities

- Project／Scene InspectionとValidation
- Direction PlanningとRead-only Preview
- Light、Camera、Reflection Probe、VolumeのCreate／Update／Guarded Undo
- 一つの既存Loaded Sceneに対する明示Save
- Dirty Dependency Setに基づく限定Lightmap／Reflection Probe Bake
- URP／HDRPの明示Baking Set／Lighting Scenario APV Bake Job
- COLOR／LINEAR_DEPTH／OBJECT_ID Capture Evidence
- Acceptance Profile、外部Measurement、Human Review、Structured Refine
- Timeout、Cancellation、Progress、Structured Log、Execution History、Tool Call Trace、Lifecycle Recovery

## Non-goals

- Player RuntimeまたはTarget Device上でのTool実行
- 任意SerializedProperty Mutation
- Delete、Area Light、Camera Stack、Pipeline固有Additional Camera Data
- Volume Profile内部Overrideの生成・変更
- Material／Renderer Featureの汎用Mutation
- 自動Save、自動Full Bake、自動Visual Acceptance
- Unity C#内での画像意味解析

## Safety boundaries

Inspect、Plan、Preview、PrepareはRead-onlyです。Apply、Save、Bakeは互いに独立した承認境界を持ちます。Plan ID、Expected Revision、Approval Token、Baselineが一致しない場合はMutationを開始しません。

Save／Bake後の永続Assetに対するUnity Undoや自動Rollbackは保証しません。Bakeは全対象をPreflightし、Silent Fallbackを行いません。

## Tool exposure

全32 Toolは`AutoRegister = false`です。Package導入だけで外部Clientへ公開されません。BridgeまたはClient側のAllowlistで必要なToolだけを有効化します。

## Compatibility

対応状況は`support-matrix.md`を正本とします。CI成功は検証環境におけるEvidenceであり、すべてのUnity Patch、SRP Package、Player、Target Deviceを保証しません。

## Evidence

現在のRelease Evidenceは`Tests/Compatibility/release-verification.yaml`、過去のCI Evidenceは`Tests/Compatibility/README.md`から参照します。
