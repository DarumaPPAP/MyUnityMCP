# UnityGraphicsMCP 実装計画

- PlanVersion: `4.0.0`
- Status: `Phase 3 Implemented and Unity Verified`
- CurrentPhase: `Phase 4 Save / Bake / Capture`

## 1. Goal

UnityAgentMCP配下で必要時だけActivationされるGraphics Domain MCPとして、対象Unity Projectの事実をRead-onlyで取得し、Visual Directionを構造化Planへ変換し、明示承認された限定OperationだけをUnity Undo Transactionとして適用する。

特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph、Target PlatformをRepository全体の固定前提にしない。

## 2. Scope classification

分類は`Project Infrastructure`とする。

- Unity Editor外部Agentとの契約境界を持つ
- Scene、Asset、Graphics設定を横断する
- Inspection、Planning、Mutation、Save、Bake、Captureで安全境界が異なる
- Unity Version、Pipeline、PlatformのVariationを対象Projectから解決する

実在する複数Backendがない段階で抽象Interfaceや空Moduleを増やさない。

## 3. Ownership and lifetime

### UnityAgentMCP

- Owner: MyUnityMCP
- Lifetime: MCP接続Session
- Responsibility: Selection、Activation、権限、実行順序

### UnityGraphicsMCP

- Owner: MyUnityMCP
- Lifetime: 選択されたMCP Session
- Responsibility: Project Inspection、Direction Planning、Graphics Transaction

### Graphics Capability Operation

- Owner: UnityGraphicsMCP
- Lifetime: Prepare PlanからApply / Undoまで
- Responsibility: Light、Camera、Reflection Probe、Volumeの限定操作

### Generated / Modified Unity Data

- Owner: 対象Unity Project
- Lifetime: Project Asset / Scene
- Responsibility: 実際のScene、Component、Profile参照

## 4. Environment information model

次を混在させない。

1. Detected Project Facts
2. Explicit Requested Target
3. Project-specific Profile
4. UnityAgent Preference

優先順位:

```text
Detected Project Facts
→ Explicit Requested Target
→ Project-specific Profile
→ UnityAgent Preference
```

`UNVERIFIED`を`UNSUPPORTED`へ変換せず、未対応BackendへSilent Fallbackしない。

## 5. Implemented architecture

```text
inspect_project / inspect_scene / validate_scene
        ↓
compile_direction
        ↓
preview_plan
        ↓
prepare_light_plan または prepare_environment_plan
        ↓
Exact Diff + Approval Token + Expected Revision
        ↓
apply_plan または apply_environment_plan
        ↓
Guarded Undo
```

### Read-only boundary

Inspection、Direction Compile、Preview、Prepareは次を変更しない。

- Scene Dirty State
- Persistent Asset Dirty State
- Undo Group
- Material Instance

### Mutation boundary

Applyは次をすべて満たす場合だけ実行する。

- Direction Planが現在Sessionに存在する
- Expected Revisionが一致する
- Approval Tokenが一致する
- Preview Baselineが適用直前状態と一致する
- Operation IDとUpdate対象がPlan内で一意
- 指定Unity APIをPrepare時に読み書き可能と確認済み
- `saveMode = NONE`

複数Operationは一つのUndo Groupへ集約し、途中例外時は全体Rollbackする。

### Undo boundary

Undoは次を再確認する。

- Transaction ID
- Expected Revision
- 対象Componentの適用後State Digest
- TransactionがUndo Stackの最新Groupであること

外部変更や新しいUndo操作が存在する場合は拒否する。

## 6. Phase 3 implemented capabilities

### Light

- `LIGHT_CREATE`
- `LIGHT_UPDATE`
- Directional / Point / Spot
- Color、Intensity、Range、Spot Angle、Shadow、Transform、Enabled

### Camera

- `CAMERA_CREATE`
- `CAMERA_UPDATE`
- Projection、FOV、Orthographic Size、Clip Plane、Culling Mask、Clear、HDR、MSAA、Transform、Enabled

### Reflection Probe

- `REFLECTION_PROBE_CREATE`
- `REFLECTION_PROBE_UPDATE`
- Mode、Refresh、Time Slicing、Importance、Intensity、Box Projection、Size、Center、Blend、Resolution、Culling Mask

### Volume

- `VOLUME_CREATE`
- `VOLUME_UPDATE`
- Global、Priority、Blend Distance、Weight、Enabled
- 既存`sharedProfile`参照の割当
- Unity Version差による公開Property / Field形状を吸収

Volume Profile内部Overrideの作成・変更はPhase 3対象外とする。

## 7. Intentionally excluded from Phase 3

- Delete Operation
- Area Light
- Camera Stack / Target Texture
- URP / HDRP Additional Camera Data
- Reflection Probe Bake
- Volume Profile内部Override
- Material / Renderer Feature Mutation
- Scene / Asset Save
- Bake
- Capture / Visual Acceptance
- 任意`SerializedProperty` Mutation

## 8. File responsibilities

| Path | Responsibility |
|---|---|
| `Editor/UnityGraphicsMcpInspection.cs` | Project / Scene InspectionとValidation |
| `Editor/UnityGraphicsMcpPlanning.cs` | Direction CompileとPlan Preview |
| `Editor/UnityGraphicsMcpMutation.cs` | Light Prepare / Apply / Undo |
| `Editor/UnityGraphicsMcpEnvironmentMutation.cs` | Camera / Probe / Volume Prepare / Apply / Undo |
| `Editor/UnityGraphicsMcpSession.cs` | Session、Revision、Snapshot、Plan Lifetime |
| `Editor/UnityGraphicsMcpTools.cs` | MCP Bridge Entry |
| `Tests/Editor/` | Read-only、Planning、Mutation Contract Test |
| `Tests/Compatibility/verification-matrix.yaml` | 実測Evidence |

Feature-local DTO、Enum、Helperは最も近いPrimary Typeと同一ファイルへ保持し、責務分離理由がない小ファイルを量産しない。

## 9. Dependency direction

```text
UnityAgent Policy
    ↓
UnityAgentMCP
    ↓
Creator Workflow
    ↓
UnityGraphicsMCP
    ↓
限定Capability Operation
    ↓
Unity Editor API
```

逆方向依存を禁止する。

## 10. Verification gate

Phase 3完了条件:

- Package Resolve
- Unity Editor Compile
- 11 Tool Bridge Discovery
- Direct Handler Invocation
- Phase 1～2 Regression
- Light Create / Update / Undo
- Camera Create / Update / Undo
- Reflection Probe Create / Update / Undo
- Volume Create / Update / sharedProfile / Undo
- Approval / Revision / Baseline Guard
- Duplicate Operation / Update Target Rejection
- Property / Field API Resolution
- Atomic Transaction / Rollback
- External Change Undo Rejection
- Newer Undo Group Rejection
- No Auto-save / No Bake

実測結果と環境条件はCompatibility Matrixを正本とし、一環境の成功をPackage全体の対応保証へ拡張しない。

## 11. Phase 4 plan

Phase 4はMutationと別の承認境界として実装する。

1. Save Planと明示承認
2. Dirty Dependency Set
3. Dependency限定Bake
4. Capture時の一時Editor State管理と復元
5. Visual Evaluation
6. Human Reviewを含むRefine Loop

Save、Bake、CaptureをPhase 3 Applyへ暗黙統合しない。

## 12. Re-evaluation conditions

次の場合にArchitectureを再評価する。

- 二つ目のPipeline Backendが実際に追加される
- Domainごとに異なるPackage依存が発生する
- Material / Renderer Mutationの所有境界が確定する
- Save / Bake / Captureが独立Transaction Storeを要求する
- Physical Server分離が必要なSecurityまたはDeployment要件が発生する
