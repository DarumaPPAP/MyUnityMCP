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

### 4. Keep confirmed and planned facts separate

- 正式Release /正式APIで確認: `CONFIRMED`
- Unity公式Planned breaking changes等、正式版で変更可能: `PLANNED`

`PLANNED`を根拠に、自動破壊変更、互換コード削除、minimum Unity引き上げを行わないでください。

### 5. Update the compatibility source of truth in the same change

Unity-version-sensitiveな変更を行ったPRでは、影響が無いと判断した場合もCompatibility確認を実施します。

影響がある場合は同一PRで必ず更新:

- `UnityApiCompatibility.cs`
- `UnityApiCompatibilityTests.cs`
- 必要なら`Specs/Compatibility/unity-api-compatibility.md`

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

- Unity 6.2+でEntityIdを利用できる箇所はEntityIdを第一候補にする。
- EntityIdをDictionary / Set keyとしてそのまま扱う。
- EntityId→intへのcast、符号依存、Sessionを跨ぐ永続ID扱いをしない。
- Unity 6.0 / 6.1サポートが必要ならLegacy経路をCompatibility boundaryへ隔離する。
- Project Assetの永続識別が目的ならGUID / local file id / GlobalObjectId等、用途に合ったIDを選ぶ。

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

## Stop conditions

次の場合は実装を確定扱いにしません。

- Planned情報しかなく正式仕様が未確定
- Package Versionが不明なのにPackage APIの削除を断定している
- Baseで解決できるのにVersion Patchを追加しようとしている
- 既存4 Bucket以外を承認なしで追加しようとしている
- Compile成功だけでPlayer / Device互換まで証明したとしている

## Output expectation

作業完了時は、変更内容を次の観点で説明します。

1. Baseへ吸収したもの
2. 6000.4 Bucketの変更
3. 6000.5 Bucketの変更
4. 6000.7 Roll-upの変更
5. Confirmed / Plannedの扱い
6. 実行した検証と未検証範囲
