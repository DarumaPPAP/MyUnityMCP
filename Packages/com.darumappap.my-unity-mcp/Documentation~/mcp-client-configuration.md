# MCP Client Configuration

最も安全な設定方法は、Unityの`Window > MCP for Unity`からClient自動設定を実行することです。Bridgeが生成した接続情報を正本にしてください。

`Templates/McpClients`にはHTTP Endpointを使うClient向けの例があります。`<UNITY_MCP_HTTP_URL>`はBridge UIに表示された実値へ置換します。

## Recommended allowlist

Read-only開始時:

```json
{
  "allowedTools": [
    "graphics.get_support_matrix",
    "graphics.get_error_catalog",
    "graphics.inspect_project",
    "graphics.inspect_scene",
    "graphics.validate_scene",
    "graphics.compile_direction",
    "graphics.preview_plan"
  ]
}
```

Mutation／Save／Bake Toolを常時許可しない運用を推奨します。Taskに必要な時間だけ追加し、完了後にAllowlistから外します。

## Security

- Project Path、Repository URL、認証情報をPromptへ埋め込まない。
- Remote ClientからUnity Editorへ接続する場合は、Bridge側のNetwork公開範囲と認証を確認する。
- `AutoRegister = false`は「Toolが存在しない」意味ではなく「明示Activationが必要」という意味です。
