# MyUnityMCP v1.0.0 Release Notes

MyUnityMCP v1.0.0は、Unity EditorのGraphics制作をMCP Clientから安全にInspection、Planning、承認制Mutation / Save / Bake、Capture、Evaluate、Refineする正式Releaseです。

## Highlights

- 32 Unity Editor MCP Toolを提供
- `Inspect → Plan → Approval → Apply → Save / Bake → Capture → Evaluate / Refine`の安全な操作境界
- Unity API Compatibilityを`BASE + UNITY_6000_4 + UNITY_6000_5 + UNITY_6000_7`の4保守Bucketで管理
- Compatibility SkillをRepository Policyへ接続し、今後のMyUnityMCP変更時にAPI互換性・Registry・Testsを同時再評価
- Unity 6.7でError化した`SceneHandle`暗黙変換、`GetInstanceID()`、`InstanceIDToObject(int)`依存を除去
- Object / Sceneの一時識別をMyUnityMCP Session Tokenへ分離
- Editor実装を`Core / Compatibility / Inspection / Planning / Mutation / Save / Bake / Capture / Execution / Tools`へ責務別整理
- `UnityGraphicsMcp` namespaceは維持し、内部クラスの冗長な`UnityGraphicsMcp` prefixを削除
- Package Asset `.meta`契約とSemantic Naming GuardをCIへ追加

## Verification

- Unity `6000.0.75f1`: Compile / 32 Tool Discovery / 125以上のEditMode Contract PASS
- Unity `6000.4.12f1`: Compatibility EditMode / Compile Verify PASS
- Unity `6000.5.5f1`: Compatibility EditMode / Compile Verify PASS
- Fresh Project / Sample Workflow / Release Contract PASS
- Unity `6000.7.0a2`: Manual Package Import / Compile / 32 Tool Discovery / `graphics.inspect_project` PASS
- Unity 6.7で`apiCompatibility`が`BASE / UNITY_6000_4 / UNITY_6000_5 / UNITY_6000_7`を返すことを確認
- Manual環境のMCP for Unity: `10.1.3-beta.3`

## Install

Unity Package Managerの`Add package from git URL...`から以下を追加します。

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.0.0
```

GitHub Release AssetとしてPackage `.tgz`、Sample Project ZIP、Templates ZIP、`SHA256SUMS.txt`も生成します。

## Scope / Limitations

- Unity Editor専用
- Minimum Unity: `6000.0`
- Player / Target Device上でのTool実行は対象外・未検証
- Built-in PipelineのAPV Bakeは非対応
- URP / HDRPのAPV BakeはProject固有Baking Set / Backend条件に依存
- Unity 6.7 automated CanaryはGameCI image未提供のため、6.7 EvidenceはManual Editor Verificationを使用

## Release history

`v1.0.1`および`v1.0.2-test.*`はこの正式1.0.0を固める過程で使用したRepository / Compatibility検証Buildです。現在の完成状態をCanonical v1.0.0として扱います。
