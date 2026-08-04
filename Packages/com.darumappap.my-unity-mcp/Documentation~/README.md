# My Unity MCP Package

## Current state

このPackageは現在、Architecture、Catalog、Manifest、Workflow、Task定義だけを持つPhase 0です。

Unity EditorへToolを登録するC#実装、Unity MCP Bridge依存、asmdef、EditMode Testは未実装です。

`MCP_MANIFEST.yaml`内のToolはすべて`status: planned`であり、利用可能なToolとして公開してはなりません。

## First implementation target

特定のUnity Version、Render Pipeline、Rendering Path、RenderGraph、PlatformをPackage全体の初期対象として固定しません。

最初に実装する能力は次です。

- 対象Unity ProjectのRead-only環境検出
- Project Contextと要求Targetの分離
- Pipeline / Rendering Path / Platform別Capability解決
- 実装済みBackendだけの選択
- `UNSUPPORTED`と`UNVERIFIED`の区別
- Editor-only Read-only Inspection

実際に使用した検証環境は`Tests/Compatibility/verification-matrix.yaml`へ記録します。検証実績をPackage全体の対応条件とは扱いません。

## Required implementation order

1. Unity MCP Bridge Versionと公式Tool登録APIを確定する。
2. `graphics.inspect_project`でProject環境をRead-only取得する。
3. Capability StatusとBackend選択を実装する。
4. Editor-only Assembly境界を作成する。
5. `graphics.inspect_scene`を実装する。
6. Dirty Guardを含むEditMode Testを追加する。
7. 検証環境をCompatibility Matrixへ記録する。
8. 実装済みToolだけManifestのStatusを変更する。

## Safety

- Read-only ToolはSceneやAssetをDirtyにしない。
- Mutation、Bake、SaveをRead-only Toolへ含めない。
- Project Profileを検出済み事実として扱わない。
- 未実装Backendへ黙ってFallbackしない。
- Unity Editor未検証のコードを動作済みと表現しない。
