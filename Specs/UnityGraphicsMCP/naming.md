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
