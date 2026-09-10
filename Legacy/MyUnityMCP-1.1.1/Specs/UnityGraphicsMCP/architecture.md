# UnityGraphicsMCP Architecture

```text
MCP Client
  ↓ explicit allowlist
MCP for Unity Bridge
  ↓ main-thread command dispatch
UnityGraphicsMCP Tool Bridge
  ↓ execution scope / structured result
Inspection → Planning → Prepared Transaction
  ↓ approval + revision + baseline
Mutation / Save / Bake / Capture
  ↓ evidence
Evaluation / Human Review / Refine
```

## State ownership

- Session、Snapshot、Plan、Approval Token: Editor Process内の一時State
- Execution History／Trace: `Library/MyUnityMCP/Execution`
- Capture Evidence: `Library/MyUnityMCP`配下
- Scene／Asset変更: 導入先Unity Project
- Package Source／Specification: MyUnityMCP Repository

## Lifetime

Domain Reload、Compile、Play Mode遷移、Scene Set変更、Unity再起動では一時IDを再利用しません。未完了Executionは中断として履歴化し、自動再開しません。

## Dependency direction

`MCP Client → Bridge → Tool Bridge → Domain Operation → Unity Editor API`の一方向です。Unity Project固有DataをPackageへ逆流させません。
