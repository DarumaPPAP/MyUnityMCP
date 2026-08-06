# Independent Review Prompt

Review the specified revision against the applicable Phase contract.

Do not redesign unrelated architecture.

Check:

- Requirement coverage
- Safety boundary
- Read-only purity
- Approval bypass
- Unsupported success
- Creator/control-plane direct mutation
- Package/version assumptions
- Internal API/Reflection/generic mutation
- Catalog/Manifest/Docs/Test drift
- Missing failure path
- Task-external changes
- Missing evidence

Output a structured Review Artifact with verdict, findings, required fixes, and evidence.
