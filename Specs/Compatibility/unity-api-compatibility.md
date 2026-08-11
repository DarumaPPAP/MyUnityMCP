# Unity API Compatibility Policy

## Purpose

MyUnityMCPのUnity C# API追従を、Unity minor versionごとのPatch File乱立ではなく、次の4つの保守Bucketへ集約します。

```text
BASE
  ↓
UNITY_6000_4
  ↓
UNITY_6000_5
  ↓
UNITY_6000_7  ← Unity 6.6由来の変更もRoll-up
```

Source of truthとなる実装は`Packages/com.darumappap.my-unity-mcp/Editor/UnityApiCompatibility.cs`です。

## Core policy

1. 新APIが最低対応Unityでも利用できる場合はVersion Patchへ置かず、まずBaseを現行形へ更新します。
2. Patch Bucketは「保守単位」であり、「APIの初回利用可能Version」と同義ではありません。
3. 例として`Object.GetEntityId()`はUnity 6.2から利用可能ですが、EntityId移行群は大規模変更が集中する`UNITY_6000_4` Bucketで一括保守します。
4. Unity 6.6専用Bucketは作成しません。6.6由来の変更は`UNITY_6000_7` Roll-up Bucketへ配置し、各Ruleの`preferredFrom` / `warningFrom` / `errorFrom` / `removedFrom` / `behaviorChangeFrom`で実際の適用Versionを保持します。
5. 正式リリースで確認済みの変更とPlanned breaking changeを同じ強度で扱いません。`CONFIRMED`と`PLANNED`を維持します。
6. Package APIはUnity Editor本体と別Versionで変化するため、Entities、Netcode、Input System、OpenXR、AR Plug-in等はPackage Versionも併せて確認します。

## Base modernization

Baseは「古いコードを延命する層」ではなく、サポート範囲内で利用できる現行APIへ可能な限り先行移行する層です。

代表例:

- `Component.renderer` / `GameObject.renderer`等 → `GetComponent<T>()`またはキャッシュ参照
- `GameObject.active` → 意図に応じて`activeSelf` / `activeInHierarchy`
- `UxmlFactory` / `UxmlTraits` → `UxmlElement` / `UxmlAttribute`
- URP新規RendererFeature → Compatibility Modeではなく`RecordRenderGraph`

## UNITY_6000_4

主な境界:

- int InstanceIDから`EntityId`への移行
- `EditorUtility.PingObject(EntityId)`等のEditor API
- Hierarchy / Project Window callbackのEntityId化
- `SerializedProperty.objectReferenceEntityIdValue`
- `EntityId.GetRawData()`から`ToULong()`への移行
- URP Compatibility Mode削除

EntityIdは「6.4になってから移行開始」ではありません。利用可能になった時点から先行移行し、6.4のwarning、6.5のerrorへ備えます。

## UNITY_6000_5

主な境界:

- Component / GameObject Legacy shortcutの削除
- `Entities.ForEach` / `Job.WithCode`削除
- Deprecated Aspects削除
- ModelImporter旧Property削除
- Legacy XR整理

Baseで先行除去できるものを6.5 Patchまで残さないことを優先します。

## UNITY_6000_7 roll-up

Unity 6.6専用Patchを増やさず、6.6〜6.7の開発基盤・Rendering・XR/ECS変更をここへ集約します。

6.6由来の代表例:

- `UNITY_64`
- `DEVELOPMENT_BUILD`の用途分離
- `UxmlFactory` / `UxmlTraits`削除
- Dynamic Batching削除
- Unity.Hierarchy obsolete APIのError化

6.7由来の代表例:

- Input System存在判定 → `ENABLE_INPUT_SYSTEM`
- AR Module削除
- RenderGraph Y-flip helper挙動変更
- RenderGraph Blit destination slice既定値変更
- NetcodeConfig Singleton生成方式変更
- 大量のLegacy API Error化

`PLANNED` Ruleは正式版確認前に自動的な破壊的Mutationを行う根拠として使用しません。

## Required maintenance flow

MyUnityMCPのC#、asmdef、RendererFeature、Build、UI Toolkit、ECS、XR/AR、Package依存を変更する場合は、同一PRで次を実施します。

1. `skills/myunitymcp-unity-api-compatibility/SKILL.md`を読む。
2. 変更をBaseへ吸収できるか最初に判定する。
3. Version固有差分だけ既存4 Bucketへ追加する。
4. 新しいminor version用Bucketを安易に増やさない。
5. `UnityApiCompatibilityTests.cs`を更新する。
6. `Tests/Compatibility/verify-unity-api-compatibility.py`を通す。
7. `CONFIRMED` / `PLANNED`の根拠を再確認する。

## Evidence basis

初期Rule Setは、2026-08-06基準の「Unity 6.4〜6.7 C# API Migration Roadmap」とUnity公式Release Notes / Upgrade情報 / Unity DiscussionsのPlanned breaking changesを基に構成しています。

6.4 / 6.5は正式情報を中心に扱い、6.6 / 6.7は予定変更を含むためSource Statusを分離します。
