# Design Assets

`Design/` は、MyUnityMCP の将来構想・設計専用資産を隔離する領域です。

ここにあるファイルは **v1.0 の実行可能機能ではありません**。Unity Editor で実際に動作する製品コード、Tool Contract、Release Gate の正本とは分離して扱います。

## Layout

```text
Design/
├─ UnityAgentMCP/
│  └─ spec.md
└─ Creators/
   ├─ catalog.yaml
   ├─ LiveCreator.yaml
   └─ MovieCreator.yaml
```

## Rules

- 実行可能な Unity Package は `Packages/com.darumappap.my-unity-mcp` に置く。
- 実行可能 MCP の Catalog / Capability Contract は `Catalog/` に置く。
- 現行製品の技術仕様は `Specs/UnityGraphicsMCP/` に置く。
- 将来の Control Plane、未実装 Domain、Creator Workflow は `Design/` に置く。
- `Design/` 内の存在だけを理由に、機能を実装済み・利用可能と表現しない。
- Design 資産を実装へ昇格する場合は、Package / Catalog / Test / Documentation / Release Contract を同一変更で更新する。
