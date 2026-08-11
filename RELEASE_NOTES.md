# MyUnityMCP v1.0.2-test.2 Test Release Notes

MyUnityMCP v1.0.2-test.2は、`v1.0.2-test.1`をUnity 6.7へ直接導入した際に検出されたCompatibility Errorを修正した再検証用Pre-releaseです。正式Releaseではありません。

## Test Target

- Primary manual verification: Unity 6.7
- Package: `com.darumappap.my-unity-mcp`
- Base CI: Unity `6000.0.75f1` PASS
- Compatibility CI: Unity `6000.4.12f1` PASS / `6000.5.5f1` PASS
- Unity 6.7 automated Editor verification: GameCI image未提供のためManual Test

## Fixed from test.1

- `SceneHandle.implicit operator int/uint` / `int -> SceneHandle`依存を除去
- Sceneの一時識別をMyUnityMCP Session Tokenへ移行
- `Object.GetInstanceID()` / `EditorUtility.InstanceIDToObject(int)`依存をSession Tokenへ分離
- `ApiCompatibility.cs` / `PackageInspection.cs`等の不足`.meta`を追加
- immutable Package内でCompatibility C#が無視され、型が見つからなくなる問題を修正
- Scene Handleの文字列化で発生したUnity 6.7型不一致を修正

## Compatibility Lifecycle Learned from CI

- Unity `6000.4.12f1`: SceneHandleとint/uintの暗黙変換はWarning
- Unity `6000.5.5f1`: 同変換はError
- そのためSceneHandle Ruleを`UNITY_6000_4` Bucketで管理し、`warningFrom=6000.4` / `errorFrom=6000.5`として記録
- Unity 6.0互換は維持し、6.4以降では`GetRawData()`、内部TransactionではSession Tokenを使用

## Unity 6.7 Manual Check

次を優先して確認してください。

1. Package Managerから正常に導入できる
2. immutable Packageの`.meta`不足Warningが出ない
3. `SceneHandle` implicit conversionのCompile Errorが出ない
4. `GetInstanceID` / `InstanceIDToObject`のCompile Errorが出ない
5. MCP Toolが32個Discoveryされる
6. `graphics.inspect_project`が応答する
7. `apiCompatibility`に6.7 Bucketが出力される
8. Inspect / Plan系のRead-only Toolが正常動作する
9. Mutation系は従来どおりApproval Boundaryを維持する

## Install

Git URL:

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.0.2-test.2
```

GitHub Release AssetとしてPackage `.tgz`、Sample Project、Templates、SHA256SUMSも生成します。

## Verification Status

- Base `6000.0.75f1`: passed
- Unity `6000.4.12f1`: passed
- Unity `6000.5.5f1`: passed
- Fresh Project / Sample Workflow / Release Contract: passed
- Unity 6.7: manual_test_pending
- This release: PRE-RELEASE / UNVERIFIED ON UNITY 6.7

Unity 6.7で問題が残る場合は、正式版へ昇格せず次のtest releaseで継続修正します。