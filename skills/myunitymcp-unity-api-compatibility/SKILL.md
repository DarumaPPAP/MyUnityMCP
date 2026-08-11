---
name: myunitymcp-unity-api-compatibility
description: Maintains MyUnityMCP Unity C# API compatibility using the BASE + Unity 6.4 + Unity 6.5 + Unity 6.7 roll-up policy. Use whenever changing MyUnityMCP C#, asmdef, rendering, build, UI Toolkit, ECS, XR/AR, package dependencies, or Unity-version-sensitive behavior.
---

# MyUnityMCP Unity API Compatibility

## Goal

MyUnityMCPをUnity API変更へ追従させる際に、minor versionごとのCompatibility Fileを増殖させず、現行APIへ先行移行しながら次の4 Bucketだけを保守します。

```text
BASE
UNITY_6000_4
UNITY_6000_5
UNITY_6000_7  # 6.6変更もここへRoll-up
```

実装正本: `Packages/com.darumappap.my-unity-mcp/Editor/UnityApiCompatibility.cs`

契約: `Specs/Compatibility/unity-api-compatibility.md`

## Mandatory trigger

次のどれかを変更する作業では、このSkillを必ず適用してください。

- `Packages/com.darumappap.my-unity-mcp/**/*.cs`
- Package内へAsset / C# / Markdown / Testを追加・削除する変更
- `.asmdef` / Version Define / scripting define symbol
- ScriptableRenderPass / RendererFeature / RenderGraph
- UnityEditor API / SerializedProperty / Hierarchy / Project Window
- Build Pipeline / BuildOptions / PlayerSettings
- UI Toolkit / UXML
- Entities / Netcode
- Input System / OpenXR / ARCore / ARKit / XR Management
- Unity Package依存またはPackage Version判定
- `Tests/Compatibility/**`
- Unity minimum/support/verified versionの変更

## Workflow

### 1. Detect the target environment

最初に対象Projectまたは変更対象から次を確認します。

- Unity Editor Version
- Render PipelineとPackage Version
- 関連Package Version
- Build Target
- 変更するAPI名

Editor VersionだけでPackage APIの対応可否を断定しません。

### 2. Prefer Base modernization

変更対象の新APIが現在の最低対応Unityでも利用できるなら、Version Patchを追加せずBaseを更新します。

代表例:

- Component / GameObject Legacy shortcut → `GetComponent<T>()`
- `GameObject.active` → `activeSelf` / `activeInHierarchy`
- `UxmlFactory` / `UxmlTraits` → `UxmlElement` / `UxmlAttribute`
- URP新規Pass → `RecordRenderGraph`
- UnityのInstanceID値が不要な一時比較 → Object参照またはMyUnityMCP Session Token

「deprecatedになったVersionまで古いAPIを残す」は禁止です。

### 3. Put real version differences into an existing bucket

Baseへ吸収できない場合だけ次へ分類します。

#### UNITY_6000_4

- EntityId / int InstanceID migration
- EntityId対応Editor API
- Hierarchy / Project Window callback
- SerializedProperty EntityId
- URP Compatibility Mode removal

`Object.GetEntityId()`のように6.4より前から利用できるAPIでも、関連変更をまとめる保守単位として6000.4 Bucketへ置いて構いません。実際の適用開始VersionはRuleのLifecycle fieldで保持します。

#### UNITY_6000_5

- Legacy Component / GameObject API hard removal
- Entities.ForEach / Job.WithCode
- Deprecated Aspects
- Importer旧API
- Legacy XR

Baseで事前除去できる項目をここへ先送りしないでください。

#### UNITY_6000_7 roll-up

Unity 6.6専用Bucketは作りません。

6.6由来の変更も6000.7 Bucketへ入れ、`preferredFrom` / `warningFrom` / `errorFrom` / `removedFrom` / `behaviorChangeFrom`には実際のVersionを書きます。

主な対象:

- UNITY_64
- DEVELOPMENT_BUILD用途分離
- UxmlFactory / UxmlTraits removal
- Dynamic Batching removal
- Unity.Hierarchy obsolete API
- Input System built-in化
- AR Module removal
- RenderGraph Y-flip
- RenderGraph Blit destination slice
- NetcodeConfig
- 大量Legacy API Error化
- `SceneHandle` と `int` の暗黙変換廃止
- `Object.GetInstanceID()` / `EditorUtility.InstanceIDToObject(int)` のError Obsolete化

### 4. Keep confirmed and planned facts separate

- 正式Release /正式API / 実Editor Compilerで確認: `CONFIRMED`
- Unity公式Planned breaking changes等、正式版で変更可能: `PLANNED`

`PLANNED`を根拠に、自動破壊変更、互換コード削除、minimum Unity引き上げを行わないでください。

Alpha / Betaでも、実際の対象Editor Compilerが具体的なCS0619とreplacementを返した変更は、そのEditor Versionに対する実測Evidenceとして記録できます。ただし将来の正式版でも同じ仕様になるとは断定せず、検証Versionを残してください。

### 5. Update the compatibility source of truth in the same change

Unity-version-sensitiveな変更を行ったPRでは、影響が無いと判断した場合もCompatibility確認を実施します。

影響がある場合は同一PRで必ず更新:

- `UnityApiCompatibility.cs`
- `UnityApiCompatibilityTests.cs`
- 必要なら`Specs/Compatibility/unity-api-compatibility.md`
- 新しい実機/Editor検証Evidenceがある場合はSkillのKnown verified migrationも更新

新しいRuleには最低限これを持たせます。

- stable `ruleId`
- `patchBucket`
- `category`
- legacy API / behavior
- replacement / required action
- Lifecycle Version
- `CONFIRMED` or `PLANNED`
- 移行上の注意

### 6. Do not create patch-per-version debt

新しい`UNITY_6000_6`や`UNITY_6000_8` Bucketを反射的に追加しないでください。

新Bucket追加条件:

1. Baseへ吸収できない。
2. 既存BucketへRoll-upすると意味が壊れる。
3. 大規模な破壊変更が集中している。
4. Test / CI / Documentationの独立境界にする価値がある。
5. 人間が追加方針を承認している。

条件を満たさなければ直前または次の大きなBoundaryへRoll-upします。

## Object identity rule

`GetInstanceID()`を見つけても機械的にint→ulongへ変換しません。

- 永続識別はGUID / local file id / `GlobalObjectId`等、用途に合ったIDを使う。
- 同一Editor Session内の一時Transaction / Dictionary / Set比較だけなら、Unity IDを外へ露出せずObject参照またはMyUnityMCP Session Tokenを優先する。
- Unity 6.2+でEntityIdそのものが必要な箇所だけEntityIdを使う。
- EntityIdを使う場合はDictionary / Set keyとしてそのまま扱い、intへ戻さない。
- EntityId→intへのcast、符号依存、Sessionを跨ぐ永続ID扱いをしない。
- Unity 6.0 / 6.1サポートのためだけに新しい`GetInstanceID()` fallbackを追加しない。Session Tokenで解決できない場合にのみCompatibility boundaryを検討する。

## Scene identity rule

Unity 6.7系では`SceneHandle`と`int`の暗黙変換へ依存しません。

- `Scene.handle`を`int` field / Dictionary keyへ直接代入しない。
- raw handleが本当に必要なら、対応Versionでは`SceneHandle.GetRawData()`を使用する。
- MyUnityMCP内部の一時Scene識別は`UnityGraphicsMcpIdentityCompatibility.GetSceneToken(scene)`を優先する。
- Scene Tokenは同一Editor Session内だけ有効。保存Asset、Release metadata、別Sessionへ永続化しない。
- Sceneの永続的な識別が必要ならAsset path / GUID等を使用する。

## Immutable package asset rule

Git/UPM経由のPackageへAssetを追加するときは、対応する`.meta`を同じ変更で必ず追加してください。

特に以下は漏らさない:

- `Editor/*.cs`
- `Tests/**/*.cs`
- Package rootの`README.md` / `CHANGELOG.md` / `LICENSE.md`
- UnityがPackage AssetとしてImportする追加ファイル

`.meta`が無いPackage Assetはimmutable package folderで無視され、型そのものがCompile対象から消える可能性があります。新規Package file追加後は「file本体 + `.meta`」を1セットとしてレビューします。

## Known verified migrations

### Unity 6000.7 manual verification — 2026-08-11

`v1.0.2-test.1`をUnity 6.7系Editorへ導入した実測で次を確認しました。

- `SceneHandle.implicit operator int(SceneHandle)` → Error Obsolete。`GetRawData()`へ移行指示。
- `SceneHandle.implicit operator SceneHandle(int)` → Error Obsolete。`FromRawData(ulong)`へ移行指示。
- `Object.GetInstanceID()` → Error Obsolete。`GetEntityId()`へ移行指示。
- `EditorUtility.InstanceIDToObject(int)` → Error Obsolete。`EntityIdToObject`へ移行指示。
- Package Assetの`.meta`不足により`UnityApiCompatibility`系C#がimmutable Package内で無視された。

MyUnityMCPではこれをそのままint/ulong変換へ置換せず、既存のSession-only ID用途を`UnityGraphicsMcpIdentityCompatibility`へ隔離してBase modernizationします。

## Verification gate

変更後は最低限次を確認します。

```text
python3 Tests/Compatibility/verify-unity-api-compatibility.py
```

Unity Editorが利用可能ならEditMode Testも実行します。

確認項目:

- BucketがBASE / 6000.4 / 6000.5 / 6000.7の4つだけ
- 6.6変更が6000.7 Roll-upでLifecycleを保持
- `CONFIRMED` / `PLANNED`混同なし
- Version parserが`6000.7.0a2`等を解釈可能
- 新しいUnity API変更にTestがある
- minimum versionを勝手に引き上げていない
- Package Version依存をEditor Versionだけで判断していない
- Package新規Assetに`.meta`が存在する
- 新規`GetInstanceID()` / `InstanceIDToObject(int)`依存を増やしていない
- SceneHandleをintへ暗黙変換していない

## Stop conditions

次の場合は実装を確定扱いにしません。

- Planned情報しかなく正式仕様が未確定
- Package Versionが不明なのにPackage APIの削除を断定している
- Baseで解決できるのにVersion Patchを追加しようとしている
- 既存4 Bucket以外を承認なしで追加しようとしている
- Compile成功だけでPlayer / Device互換まで証明したとしている
- Unity Packageへ新規Fileを追加したのに`.meta`が無い

## Output expectation

作業完了時は、変更内容を次の観点で説明します。

1. Baseへ吸収したもの
2. 6000.4 Bucketの変更
3. 6000.5 Bucketの変更
4. 6000.7 Roll-upの変更
5. Confirmed / Plannedの扱い
6. 実行した検証と未検証範囲
