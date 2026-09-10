# Quick Start

## 1. Install

MCP for Unity Bridgeを先に導入し、その後MyUnityMCPを導入します。詳細は[Installation](installation.md)を参照してください。

## 2. Configure the client

Unityで`Window > MCP for Unity`を開き、検出されたClientを設定します。自動設定が使えない場合だけ[Template](../../../Templates/McpClients/README.md)を利用します。

## 3. Allow only required tools

最初は次のRead-only Toolだけを許可します。

```text
graphics.inspect_project
graphics.inspect_scene
graphics.validate_scene
graphics.compile_direction
graphics.preview_plan
graphics.get_support_matrix
graphics.get_error_catalog
```

## 4. First calls

1. `graphics.get_support_matrix`
2. `graphics.inspect_project`
3. `graphics.inspect_scene`
4. `graphics.validate_scene`
5. `graphics.compile_direction`
6. `graphics.preview_plan`

Mutationが必要になった時点で、対応するPrepare／Apply Toolを追加許可します。Applyが返すApproval Tokenを事前に作成・推測しないでください。

## 5. Import the sample

Package ManagerのSamplesから`Getting Started`をImportするか、`SampleProjects/MyUnityMCPGettingStarted`をUnityで開きます。
