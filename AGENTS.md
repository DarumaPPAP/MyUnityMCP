# MyUnityMCP Repository Policy

## 1. Repository role

MyUnityMCPは、UnityAgentが選択して利用するUnity専門MCP基盤の正本です。

このRepositoryは次を所有します。

- UnityAgentMCP Control Plane
- Creator Workflow
- Domain MCP
- Capability Module
- MCP Catalog
- MCP Manifest
- Tool Schema
- Unity Editor向けUPM Package
- MCP固有Test
- MCP固有仕様

UnityAgentはユーザー固有の設計思想、コーディング規約、Visual Direction、Route、Context Pack、Task Contract、Knowledge Contractを所有します。

MCPが生成または変更するScene、Prefab、Material、Shader、Timeline、Volume Profile、Lighting Data等は対象Unity Projectが所有します。このRepositoryへProject固有成果物を保存しません。

## 2. Logical architecture

```text
UnityAgent
    ↓ policy and preferences
UnityAgentMCP
    ↓ selects workflow and domain tools
Creator Workflow
    ↓ requests technical work
Domain MCP
    ↓ invokes bounded operations
Capability Module
    ↓
Unity Editor API
```

- UnityAgentMCPはControl Planeだけを所有し、GraphicsやTimelineの具体操作を実装しない。
- Creatorは完成目的と制作工程を所有する。
- Domain MCPは専門領域の技術判断とTool Groupを所有する。
- Capability Moduleは限定されたUnity API操作を所有する。
- 論理Moduleを理由なく別Server、別Process、別Port、別Package、別asmdefへ分割しない。

## 3. On-demand activation

通常時はCatalogの短いEntryだけを読みます。

1. Primary CreatorまたはPrimary Domain MCPを一つ選択する。
2. 選択されたManifestだけを読む。
3. 条件成立時だけConditional Domain MCPを最大2つ追加する。
4. 必要なTool Groupだけを公開する。
5. 通常利用ではPackage Sourceを読まない。

Tool Groupは次の順序で段階公開します。

```text
inspect → plan → mutate → bake → capture
```

- `inspect`と`plan`はRead-only。
- `mutate`は承認済みPlan、Revision、Diff、Undo、明示許可が必要。
- `bake`は別の明示許可とDirty Dependencyが必要。
- `capture`は一時的Editor状態を復元する。
- Automatic Saveは禁止。

## 4. C# constraints

- Root Namespaceがない場合、Feature単位の単一階層namespaceを使用する。
- `namespace Namespace.*`、`RootNamespace`、`CHANGE_ME`、先頭・末尾`.`を禁止する。
- 例: `namespace UnityAgentMcp`、`namespace UnityGraphicsMcp`。
- enum型は`E_UPPER_SNAKE_CASE`。
- struct型は必要時のみ`S_UPPER_SNAKE_CASE`、原則`readonly struct`。
- private fieldは`_camelCase`。
- constは`SCREAMING_SNAKE_CASE`。
- Runtimeから`UnityEditor`を参照しない。
- Editor機能はEditor FolderまたはEditor-only Assemblyへ隔離する。
- 1実装しかないInterfaceを将来予測だけで作らない。
- Controller、Manager、Service、Coordinator、Profile、AdapterをPattern完成のために作らない。
- 新規ファイルにはOwner、Lifetime、Consumers、Responsibility、Split Reasonを記録する。
- 小規模な内部Enum、Result、State、PassDataを無条件に別ファイルへ分離しない。
- asmdefはPackage依存またはEditor / Runtime境界が実在する場合だけ作る。

詳細なC#およびArchitecture規約は`DarumaPPAP/UnityAgent`の正本を参照します。

## 5. Render Pipeline boundary

- Visual IntentはPipeline非依存とする。
- Unityへ適用するNative設定はBuilt-in、URP、HDRPで分離する。
- PipelineとPlatformは別の解決軸とする。
- 最初の実装対象はUnity 6000.3、URP 17、Forward、RenderGraph。
- Built-inとHDRPは実装が存在するまでInterfaceや空Backendを作らない。
- すべてのPipeline設定を一つのnullable設定型へ押し込めない。

## 6. Mutation and evidence

- Read-only ToolはScene、Asset、Timeline、Material、ProfileをDirtyにしない。
- Scene、Prefab、MaterialをRaw YAMLで編集しない。
- Project Settings、Renderer Data、Render Pipeline Asset変更は明示承認を要求する。
- BakeをMutationへ暗黙的に含めない。
- Compile成功をRuntime、Visual、Performance、実機の成功と扱わない。
- Human Reviewなしに`VISUAL_ACCEPTED`としない。
- Editor結果だけでPlayerまたはNintendo Switchを保証しない。

## 7. Current status

現在はArchitecture、Catalog、Manifest、Workflow、Task定義を構築するPhase 0です。

Unity Editorへ接続するC# Tool実装は未着手です。仕様やManifestの存在を動作済みと表現しません。
