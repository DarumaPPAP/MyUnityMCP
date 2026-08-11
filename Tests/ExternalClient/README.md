# External MCP Client E2E

このDirectoryは、Unity Editor内の直接Handler呼出ではなく、外部MCP ClientとしてHTTP Transportへ接続するE2E Harnessです。

## Preconditions

1. 対象Unity ProjectをUnity Editorで開く。
2. MCP for UnityのHTTP Transportを開始する。
3. MyUnityMCP Development ToolをClient側Allowlistで有効化する。
4. `feature/graph-engineering-master`のSourceとUnity EditorのPackage Sourceを一致させる。

## Run

```powershell
py Tests\ExternalClient\run_mcp_http_e2e.py `
  --endpoint http://127.0.0.1:8090/mcp `
  --expected-tool graphics.inspect_project `
  --expected-tool agent.inspect_capabilities `
  --output Development\GraphEngineering\state\evidence\external-client\latest.json
```

実際のPort／PathはMCP for Unity Windowに表示されたHTTP URLを使用します。

## Protocol flow

```text
initialize
→ notifications/initialized
→ tools/list
→ required tool validation
→ graphics.inspect_project
→ agent.inspect_capabilities
→ evidence JSON
```

## Security

EvidenceはCI Modeとして生成します。

- EndpointのHost／PortをRedact
- Credentialを収集しない
- Project Pathを収集しない
- Screenshotを収集しない
- Tool Result本文をEvidenceへ保存しない
- Tool名、件数、成功／失敗だけを保存

## Completion gate

Phase 1、Phase 12、Project Completion Gateでは、次を満たしたEvidenceだけを使用します。

- Tool Discoveryに必要Toolが存在
- Read-only Tool Callが`isError=false`
- Source Revisionが対象Commitと一致
- Unity Editor Compile／EditMode Testが別EvidenceでPASS
- Security ModeがCI

Harness Scriptが存在するだけではE2E完了になりません。実行済みEvidenceが必要です。
