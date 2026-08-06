# Acceptance Model

## Requirement mapping

各Requirementは最低1つのVerificationとEvidenceへ対応する。

```text
Requirement
  → Implementation
  → Verification
  → Evidence
  → Acceptance decision
```

## Machine acceptance

- Schema
- Compile
- Test
- Tool discovery
- Contract
- Revision／approval
- Hash／artifact
- Support matrix

## Human acceptance

- Visual quality
- Audio quality
- Cinematic continuity
- Live presentation
- Release approval
- High-risk API／platform decisions

Machine PASSはHuman acceptanceを代替しない。

## Unsupported acceptance

API／Package／Environment制約で実装不能な要求は、
Silent omissionではなく次をEvidence化する。

- Requested capability
- Official/current API evidence
- Supported alternative
- Risk of workaround
- Human decision
- Catalog status

「未対応を正しく拒否する」ことはPhaseの一部になり得るが、
PhaseそのものをDesign-onlyのまま完了扱いにはしない。
