# Phase 4C Capture Evidence Contract

## Scope

Phase 4Cは、既存のColor-only `graphics.capture_evaluation`を互換維持したまま、評価用Captureを再現可能なEvidence Bundleへ拡張し、Human Visual Acceptanceを明示的に確定する。

追加Tool:

- `graphics.capture_evidence`
- `graphics.submit_visual_review`
- `graphics.refine_from_visual_review`

全Toolは`AutoRegister = false`であり、明示有効化なしに外部公開しない。

## Capture Evidence Bundle

`graphics.capture_evidence`は指定CameraとEditor Revisionを固定し、次のChannelを選択して`Library/MyUnityMCP/Captures`へBundle単位で出力する。

- `COLOR`: Active Render PipelineによるCamera ColorをPNG RGBA8で保存
- `LINEAR_DEPTH`: Camera Near/Farで正規化したLinear Eye DepthをFloat EXRで保存
- `OBJECT_ID`: Renderer単位の決定的24-bit IDをPNGへ保存
- `OBJECT_ID_MAP`: ID、GlobalObjectId、Hierarchy Path、Scene Path、Renderer Type、SubMesh CountをJSONで保存
- `MANIFEST`: Camera Baseline、Artifact Hash、Coverage、Depth Semantics、Review状態をJSONで保存

各ArtifactはSHA-256とByte Lengthを持ち、Camera Baseline、解像度、Renderer Coverage、Artifact集合から`evidenceDigest`を生成する。Visual ReviewはこのDigestと完全一致するCaptureだけを参照できる。

## Atomic output

Bundleは一時Directoryへ生成し、全ArtifactとManifestの生成成功後に最終Directoryへ移動する。

次の場合は一時・最終DirectoryとSession Recordを破棄する。

- Capture中の例外
- Editor Revision変更
- Scene／Asset DirtyまたはUndo状態の変化
- Camera TargetTexture／Active RenderTexture／Scene Dirty状態の復元失敗
- Artifact欠落

## Temporary Editor state

Capture後は必ず次を復元する。

- `Camera.targetTexture`
- `RenderTexture.active`
- Capture開始時のScene Dirty状態
- Scene／Asset Dirty集合
- Undo Group

Capture Artifactの`Library`書き込みはProject Asset Mutationとして扱わないが、Scene、Asset、Undoを変更してはならない。

## Object ID coverage

Object IDとLinear Depthの共通Renderer Backendは、次を対象とする。

- Loaded Sceneに存在するActiveかつEnabledな`Renderer`
- Camera Culling Mask内
- Camera Frustum内
- `ShadowsOnly`ではないRenderer

RendererはScene Path、Hierarchy Path、Renderer Type、Instance IDの順で並べ、1から始まる24-bit IDを割り当てる。Blackは背景である。

次はSilentに対応済みと扱わず、ManifestへCoverage Limitとして記録する。

- Terrain
- DecalProjector
- `Graphics.DrawMesh`等のProcedural Draw
- Particle等、Renderer Component以外から生成される描画
- Alpha Clip、Original Material Pass固有の頂点変形を完全再現しないOverride Capture

## Linear Depth semantics

Linear DepthはShaderでView Space Depthを取得し、次式で0～1へ正規化する。

`(eyeDepth - nearClipPlane) / (farClipPlane - nearClipPlane)`

- 0: Near Plane
- 1: Far Planeまたは背景

Color Captureとは別のRenderer Override Passであり、透明合成後の最終Depthではない。

## Human Visual Review

`graphics.submit_visual_review`は、Capture ID、Editor Revision、Evidence Digest、Reviewerを必須とし、同一Captureに対して一度だけReviewを確定する。

Decision:

- `ACCEPTED`
- `REJECTED`
- `NEEDS_ADJUSTMENT`

`ACCEPTED`には次が必要である。

- Human Observationを一つ以上指定
- `acceptanceConfirmation = VISUAL_ACCEPTED`
- `requestedAdjustments`を指定しない

Unity C#側は画像の意味解析を行わず、Capture直後に自動で`visualAccepted = true`へしない。

## Refinement

`graphics.refine_from_visual_review`は、確定済み`REJECTED`または`NEEDS_ADJUSTMENT` Reviewだけを参照して次IterationのDirection PlanをRead-onlyで作成する。

`ACCEPTED` ReviewからRefine Planは作成しない。

新Planには次を固定する。

- Capture ID / Evidence Digest
- Visual Review ID / Review Digest
- Decision / Reviewer
- Human Observations
- Requested Adjustments

Mutation、Save、Bakeは実行しない。

## Session and stale policy

CaptureとReviewはEditor Session内だけで保持し、30分TTLを持つ。次で失効する。

- Play Mode遷移
- Compile開始
- Domain Reload
- Editor終了
- Editor Revision変更

Capture最大8件、Review最大16件を保持し、上限到達時は最古Recordから除去する。

## Null Graphics Device

BatchMode等で`GraphicsDeviceType.Null`の場合、描画Artifactを偽装せず`UNVERIFIED`を返す。Channel Validation、Revision Guard、Digest、Review、Refine契約はEditMode Testで独立検証する。
