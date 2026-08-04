# My Unity MCP Package

## Current state

このPackageは現在、Architecture、Catalog、Manifest、Workflow、Task定義だけを持つPhase 0です。

Unity EditorへToolを登録するC#実装、Unity MCP Bridge依存、asmdef、EditMode Testは未実装です。

`MCP_MANIFEST.yaml`内のToolはすべて`status: planned`であり、利用可能なToolとして公開してはなりません。

## First implementation target

- Unity 6000.3
- URP 17+
- Forward
- RenderGraph
- Nintendo Switch優先
- Editor-only
- Read-only Inspectionから開始

## Required implementation order

1. Unity MCP Bridge Versionと公式Tool登録APIを確定する。
2. Editor-only Assembly境界を作成する。
3. `graphics.inspect_project`を実装する。
4. `graphics.inspect_scene`を実装する。
5. Dirty Guardを含むEditMode Testを追加する。
6. 実装済みToolだけManifestのStatusを変更する。

## Safety

- Read-only ToolはSceneやAssetをDirtyにしない。
- Mutation、Bake、SaveをRead-only Toolへ含めない。
- Unity Editor未検証のコードを動作済みと表現しない。
