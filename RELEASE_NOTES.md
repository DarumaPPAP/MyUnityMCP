# MyUnityMCP v1.0.1 Release Notes

MyUnityMCP v1.0.1は、v1.0.0の実行機能・Safety Contractを維持したまま、Repository構造とRelease運用を整理するPatch Releaseです。

## Highlights

- 実行可能な製品資産とDesign-only資産を分離
- UnityAgentMCP、LiveCreator、MovieCreatorなど未実装設計を`Design/`へ集約
- Operational Catalogを`unity_graphics_mcp`中心に整理
- Release ContractのTool Discoveryを再帰検索へ変更し、Editorコードの責務別フォルダ化に対応
- Release GateのDistribution Preview名／ZIP名を`VERSION`から動的生成
- Package、Manifest、Catalog、Support Matrix、Installation Guideをv1.0.1へ同期

## Runtime Compatibility

- MCP Tool: 32（変更なし）
- Unity Editor `6000.0`以上
- CI検証環境: Unity `6000.0.75f1`
- Mutation／Save／Bakeの承認境界に変更なし
- Player Runtime／実機上でのTool実行は対象外

## Upgrade from v1.0.0

Package参照を`v1.0.1`へ更新してください。Tool Schema、Safety Boundary、保存済みProject AssetのMigrationは不要です。

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.0.1
```

`v1.0.0` Tagはimmutableのまま保持され、v1.0.1は新しいRelease Commitから発行されます。

## Known Issues

詳細は`Packages/com.darumappap.my-unity-mcp/Documentation~/known-issues.md`を参照してください。
