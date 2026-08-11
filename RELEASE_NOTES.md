# MyUnityMCP v1.0.2-test.1 Test Release Notes

MyUnityMCP v1.0.2-test.1は、Unity 6.7での直接確認を目的としたPre-releaseです。正式Releaseではありません。

## Test Target

- Primary manual verification: Unity 6.7
- Package: `com.darumappap.my-unity-mcp`
- Base CI reference: Unity `6000.0.75f1`
- Unity 6.7 automated Editor verification: pending

## Included

- Unity API Compatibility Registry
  - `BASE`
  - `UNITY_6000_4`
  - `UNITY_6000_5`
  - `UNITY_6000_7`
- Unity 6.6由来変更の6.7 Roll-up管理
- Compatibility lifecycleの`CONFIRMED` / `PLANNED`分離
- Package Versionを含むCompatibility Context
- MyUnityMCP変更時にCompatibility更新を要求するSkill
- Compatibility Contract / Editor Matrix CI

## Unity 6.7 Manual Check

次を優先して確認してください。

1. Package Managerから正常に導入できる
2. Script Compile Errorが発生しない
3. MCP Toolが32個Discoveryされる
4. `graphics.inspect_project`が応答する
5. `apiCompatibility`に6.7 Bucketが出力される
6. Inspect / Plan系のRead-only Toolが正常動作する
7. Mutation系は従来どおりApproval Boundaryを維持する

## Install

Git URL:

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.0.2-test.1
```

GitHub Release AssetとしてPackage `.tgz`、Sample Project、Templates、SHA256SUMSも生成します。

## Verification Status

- Base Editor CI: passed
- Unity 6.7: manual_test_pending
- This release: PRE-RELEASE / UNVERIFIED ON UNITY 6.7

Unity 6.7で問題が確認された場合は、このPre-releaseを正式版へ昇格せず修正版`test.2`以降で再検証します。