# Design Assets

`Design/` は、MyUnityMCP の **将来構想・未実装設計** を隔離する領域です。

現在の実行可能製品、Operational Contract、Release Gate、昇格済み仕様の正本は `Packages/`、`Catalog/`、`Specs/`、`Tests/` に置きます。過去に昇格済みだったDesign baselineをcurrent `main`へ複製保持せず、Historical StateはGit historyとimmutable release tagsで参照します。

## Layout

```text
Design/
├─ README.md
├─ module-catalog.yaml
└─ Creators/
   ├─ catalog.yaml
   ├─ LiveCreator.yaml
   └─ MovieCreator.yaml
```

## Rules

- 実行可能な Unity Package は `Packages/com.darumappap.my-unity-mcp` に置く。
- 実行可能 MCP の Catalog / Capability Contract は `Catalog/` に置く。
- 現行製品の技術仕様は `Specs/` に置く。
- 将来のControl Plane、未実装Domain、未実装Creator Workflowは `Design/` に置く。
- 昇格済みCapabilityのcurrent specやhistorical copyを `Design/` と `Specs/` の二重Source of Truthにしない。
- `Design/` 内の存在だけを理由に、機能を実装済み・利用可能と表現しない。
- Design資産を実装へ昇格する場合は、Package / Catalog / Test / Documentation / Current Spec / Release Contractを同一変更で更新する。
- 昇格前の状態を後から確認する必要がある場合はGit historyまたはimmutable release tagを使用する。
