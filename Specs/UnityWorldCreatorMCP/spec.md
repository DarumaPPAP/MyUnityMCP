# UnityWorldCreatorMCP Specification

## Purpose

UnityWorldCreatorMCPは、Visual GoalをUnityAgentMCP上のRead-only Graphics Preflightへ変換し、その結果を人間が制作判断できるReview Handoffへ接続するCreator Layerです。

WorldCreator自身はUnity APIを直接Mutationしません。`unity_graphics_mcp`へUnityAgentMCP経由で委譲します。

## Production Tool Surface

| Tool | Responsibility | Direct Unity mutation |
|---|---|---|
| `world.compile_workflow` | Visual Goalと制約からRevision固定のRead-only Preflight GraphをCompile | なし |
| `world.start_preflight` | Compile済みGraphをUnityAgentMCP Executionとして開始 | なし |
| `world.create_review_handoff` | Execution結果をHuman Review必須のHandoffへ変換 | なし |

## Canonical Flow

```text
world.compile_workflow
  -> agent graph
     -> graphics.inspect_project
     -> graphics.inspect_scene
     -> graphics.validate_scene
  -> world.start_preflight
  -> world.create_review_handoff
  -> HUMAN_REVIEW_REQUIRED
```

## Safety Contract

- `expectedRevision`は`world.compile_workflow`で必須
- PreflightはRead-only Graphics Toolだけで構成
- WorldCreatorはDirect Unity Mutation APIを呼ばない
- Mutation、Save、Bakeは後続Domain Planと既存Approval Boundaryへ委譲
- `automaticVisualAcceptance`は常に`false`
- Review Handoffは`HUMAN_REVIEW_REQUIRED`
- Stale RevisionではWorkflow Compileを拒否

## Output Intent

`world.compile_workflow`はVisual Goal、Scene Scope、Environment Type、Mood、Target Platform、Prohibited Changes、Acceptance Criteriaを`visualIntent`として保持します。

Preflight成功後の`nextStages`は制作の自動実行指示ではありません。Graphics Planning、Human Approval、Domain Mutation、任意Save/Bake Approval、Capture Evidence、Human Visual Reviewへ進むための明示的Handoff候補です。

## Operational Dependencies

- `unity_agent_mcp`: editor_operational
- `unity_graphics_mcp`: editor_operational

v1.1.0ではProfiler、Addressables、UI、Animation、Audio、CinematicもOperationalですが、WorldCreator canonical preflightの必須Dependencyにはしません。MovieCreator / LiveCreator runtimeもWorldCreatorの必須条件ではありません。

## Verification Gate

- WorldCreator 3 Toolがv1.1.0 Exact 77 Tool Discoveryへ含まれること
- Read-only Preflightが3/3 Stepで成功すること
- Stale Revisionを拒否すること
- Human Review Requiredを維持すること
- Creator Sourceが直接Unity Mutation APIを呼ばないこと
- Automated CIが利用不能な場合は`not_verified`を維持し、Direct Editor Evidenceと混同しないこと
