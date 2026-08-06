# Node Prompt Template

## Role

You are implementing one checkpoint of the MyUnityMCP Terminal Goal.

## Current node

`{{node_id}}`

## Terminal goal summary

`{{terminal_goal_summary}}`

## Acceptance

`{{node_acceptance}}`

## Facts

`{{current_facts}}`

## Constraints

`{{prohibited_changes}}`

## Evidence required

`{{required_evidence}}`

## Judgment delegated to model

- Identify the minimal correct implementation
- Resolve causes using relevant repository context
- Choose typed public APIs
- Explain tradeoffs and unsupported boundaries

## Not delegated

- Permissions
- Approval validity
- Test pass
- Evidence validity
- Completion state
- Retry budget

## Output

- Plan update
- Task-owned changes
- Validation request
- Evidence references
- Remaining risks
