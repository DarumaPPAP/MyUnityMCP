# UnityGraphicsMCP Unity Editor C# Tool実装設計

- DocumentVersion: `1.0.0`
- DesignStatus: `Draft`
- ImplementationStatus: `Not Started`

## 1. 目的

対象Unity ProjectをRead-onlyで解析し、そのProjectで利用可能なGraphics BackendとCapabilityを解決した上で、必要なToolだけを公開するEditor-only C#基盤を設計する。

特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph設定、Target PlatformをMyUnityMCP全体へ固定しない。

## 2. Initial tool scope

最初に実装するToolは次の三つとする。

- `graphics.inspect_project`
- `graphics.inspect_scene`
- `graphics.validate_scene`

`graphics.inspect_frame`、Mutation、Bake、Captureは別OwnerとLifetimeを持つため後続Phaseへ分離する。

## 3. Environment resolution

### 3.1 Detected project facts

`graphics.inspect_project`は最低限次を取得する。

- Unity Version
- Render Pipeline Kind
- Render Pipeline Package Version
- Active Renderer
- Rendering Path
- RenderGraph Mode
- Active Build Target
- Installed Build Targets
- Graphics API
- Scripting Backend
- Related Package Presence
- Backend Capability Summary

### 3.2 Requested target

ユーザーまたはCreatorから渡されたTarget Platform、品質方針、禁止事項は、検出済みProject事実とは別のDTOへ保持する。

Projectが現在設定しているPlatformと、今回要求されたTargetが異なる場合は自動で同一視しない。

### 3.3 Information precedence

1. 対象Unity Projectから検出した事実
2. 今回明示されたTargetと制約
3. Project固有Profile
4. UnityAgentの既定Preference

下位情報で上位情報を上書きしない。

## 4. Capability status

```csharp
public enum E_MCP_CAPABILITY_STATUS
{
    AVAILABLE,
    UNAVAILABLE,
    UNSUPPORTED,
    UNVERIFIED,
    PACKAGE_NOT_INSTALLED,
    VERSION_NOT_SUPPORTED,
    PROJECT_CONFIGURATION_REQUIRED,
    BACKEND_NOT_IMPLEMENTED
}
```

- `UNVERIFIED`は検証実績がない状態であり、`UNSUPPORTED`ではない。
- 対象Pipelineを検出できてもBackend実装が存在しない場合は`BACKEND_NOT_IMPLEMENTED`を返す。
- 別Pipelineへ黙ってFallbackしない。

## 5. Physical architecture

```text
Unity MCP Bridge
    ↓
UnityAgentMcpTools
    ↓
UnityMcpEditorSession
    ↓
UnityGraphicsMcpInspection
    ↓
Detected Backend Capability
    ↓
Unity Editor API
```

### `UnityAgentMcpTools.cs`

Owner: MCP外部境界

Lifetime: 一つのTool Call

Responsibility:

- Tool登録
- Request Schema検証
- Main Thread Sessionへの委譲
- Result Schema変換

Split Reason:

- Unity MCP Bridge Packageへの外部依存を隔離するため

### `UnityMcpEditorSession.cs`

Owner: Unity Editor Session

Lifetime: Domain ReloadまたはEditor終了まで

Responsibility:

- Main Thread Queue
- Session ID
- Revision
- Snapshot
- Cancellation
- Compile / Domain Reload / PlayMode遷移時の中断

Split Reason:

- Tool Callより長い状態とEditor Lifecycleを所有するため

### `UnityGraphicsMcpInspection.cs`

Owner: UnityGraphicsMCP

Lifetime: 一つのInspection

Responsibility:

- Project環境検出
- Scene Graphics解析
- Capability解決
- Validation
- Pipeline固有読取処理への委譲

Split Reason:

- Graphics専門判断とUnity API読取を所有するため

## 6. Backend strategy

初期実装で特定Pipelineを製品要件として固定しない。

1. Pipeline非依存のProject Inspectionを先に実装する。
2. 対象ProjectからPipelineとVersionを検出する。
3. 利用可能な検証Projectに対応する最初の具象Backendを実装する。
4. その検証環境をCompatibility Matrixへ記録する。
5. 二つ目の実在Backendが追加されるまで共通Interfaceを抽出しない。

最初の具象Backendは開発時に利用可能な検証Projectで決まり、MyUnityMCP全体の固定対応条件にはしない。

## 7. Read-only contract

Inspectionでは次を禁止する。

- `Undo.RecordObject`
- `EditorUtility.SetDirty`
- `EditorSceneManager.MarkSceneDirty`
- `SerializedObject.ApplyModifiedProperties`
- `AssetDatabase.SaveAssets`
- `AssetDatabase.Refresh`
- `Renderer.material` / `Renderer.materials`
- `Volume.profile`
- AssetまたはSceneを生成・保存するAPI

Tool実行前後でLoaded SceneのDirty状態、対象AssetのDirty状態、Revision、Undo Groupを比較する。

違反を検出した場合は成功Resultを返さず、`READ_ONLY_CONTRACT_VIOLATION`として記録する。Dirtyを自動解除して隠さない。

## 8. Main Thread and lifecycle

- Unity API操作はEditor Main Threadで実行する。
- 初期版はSingle Flightとする。
- Assembly Reload、Compile開始、Editor終了、PlayMode遷移開始で実行中Requestを中断する。
- Domain Reload後の古いSnapshotは`SESSION_EXPIRED`を返す。
- Scan中にRevisionが変わった場合は`STALE_DURING_SCAN`とする。

## 9. Snapshot and identity

Scene全体の巨大JSONを毎回返さず、Summary、Snapshot ID、Revision、Cursorを返す。

Object識別は次を優先する。

1. GlobalObjectId
2. Asset GUID + Local File ID
3. Scene GUID + GlobalObjectId
4. Session限定Instance ID

GameObject名だけを識別子にしない。

## 10. Project inspection result

ResultはDetected ProjectとRequested Targetを分離する。

```json
{
  "detectedProject": {
    "unityVersion": "...",
    "renderPipelineKind": "...",
    "renderingPath": "...",
    "activeBuildTarget": "..."
  },
  "requestedTarget": {
    "platforms": ["..."],
    "constraints": ["..."]
  },
  "capabilities": [],
  "verificationState": "UNVERIFIED"
}
```

## 11. Compatibility evidence

検証結果は`Tests/Compatibility/verification-matrix.yaml`へ記録する。

Matrixは次を表す。

- どのUnity VersionでCompileしたか
- どのPipeline / Rendering Pathで検証したか
- Editor / EditMode / Player / Target Deviceの各Gate
- 証拠Path

Matrix Entryが存在しない環境は`UNVERIFIED`であり、即座に`UNSUPPORTED`とはしない。

## 12. Initial production files

```text
Packages/com.darumappap.my-unity-mcp/
├─ Editor/
│  ├─ UnityAgentMcpTools.cs
│  ├─ UnityMcpEditorSession.cs
│  ├─ UnityGraphicsMcpInspection.cs
│  └─ MyUnityMcp.Editor.asmdef
└─ Tests/Editor/
   ├─ UnityGraphicsMcpInspectionTests.cs
   └─ MyUnityMcp.Editor.Tests.asmdef
```

Runtime Assembly、空Backend、Capabilityごとのasmdefは作らない。

## 13. Implementation gates

1. Unity MCP Bridge APIを対象Projectで確認する。
2. `graphics.inspect_project`をPipeline非依存で実装する。
3. Capability StatusとResult Schemaを実装する。
4. Main Thread / Domain Reload / Dirty Guard Testを通す。
5. 最初の具象Backendを実装する。
6. Compatibility Matrixへ実際の検証結果を記録する。
7. 実装済みToolだけManifestで公開する。
