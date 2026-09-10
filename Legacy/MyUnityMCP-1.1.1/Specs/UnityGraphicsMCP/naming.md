# UnityGraphicsMCP Naming Rules

## Rule

Production code、Editor Test、Workflow、運用中の仕様書およびEvidenceは、実装順やDelivery Phaseではなく責務・Capabilityで命名する。

## Prohibited

- `Phase1`、`Phase2`、`Phase3`、`Phase4`等を型名、Method名、File名、Workflow名へ含めること
- 一時的なRoadmap番号をPublic APIまたは内部Domain用語として固定すること
- 実装時期だけを表し、責務を説明しない名前

## Preferred capability names

- Save Evaluation
- Dependency Bake
- Capture Evidence
- Adaptive Probe Volume Bake
- Visual Acceptance
- APV Visual Acceptance Tools

Delivery Phaseは、過去のPR、Release Note、Task履歴など履歴を説明する文脈だけで使用できる。

## Internal C# naming

- Root namespaceは`UnityGraphicsMcp`を維持する。
- `UnityGraphicsMcp` namespace配下の型名へ`UnityGraphicsMcp` prefixを重ねない。
- 外部MCP Tool名の`graphics.*`は安定契約として変更しない。
- C#型名・File名の整理を理由にTool名、Request/Response schema、Approval Boundaryを変更しない。
- MCP Tool wrapperは`InspectProjectTool`のように責務名 + `Tool`で命名する。
- File名は主責務を表し、Domain実装は原則として主責務単位、Tool wrapperはDomain単位でまとめる。

## Editor layout

```text
Editor/
  Core/
  Compatibility/
  Inspection/
  Planning/
  Mutation/
  Save/
  Bake/
  Capture/
  Execution/
  Tools/
```

Testも同じDomain区分へ寄せる。1 Tool = 1 Fileのような過剰分割は行わない。
