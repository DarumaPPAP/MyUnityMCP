# UnityWorldCreatorMCP Specification

## Purpose

UnityWorldCreatorMCPは、Visual GoalをUnityAgentMCP上のRead-only Graphics Preflightへ変換し、その結果を人間が制作判断できるReview Handoffへ接続するCreator Layerです。

WorldCreator自身はUnity APIを直接Mutationしません。ProductionでOperationalな`unity_graphics_mcp`へUnityAgentMCP経由で委譲します。

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

- `expectedRevision`は`world.compile_workflow`で必須です。
- PreflightはRead-onlyのGraphics Toolだけで構成します。
- WorldCreatorは`Undo.RecordObject`、`EditorUtility.SetDirty`、`AssetDatabase.CreateAsset`、`EditorSceneManager.SaveScene`、`BuildPipeline.BuildPlayer`を直接呼びません。
- Mutation、Save、BakeはWorldCreator自身が実行せず、後続のDomain Planと既存Approval Boundaryへ委譲します。
- `automaticVisualAcceptance`は常に`false`です。
- Review Handoffは`HUMAN_REVIEW_REQUIRED`を返します。
- Stale RevisionではWorkflow Compileを拒否します。

## Output Intent

`world.compile_workflow`はVisual Goal、Scene Scope、Environment Type、Mood、Target Platform、Prohibited Changes、Acceptance Criteriaを`visualIntent`として保持します。

Preflight成功後の`nextStages`は制作の自動実行指示ではありません。Graphics Planning、Human Approval、Domain Mutation、任意Save/Bake Approval、Capture Evidence、Human Visual Reviewへ進むための明示的なHandoff候補です。

## Operational Dependencies

- `unity_agent_mcp`: editor_operational
- `unity_graphics_mcp`: editor_operational

Profiler、Build、Addressables、UI、Animation、Audio、Cinematic、MovieCreator、LiveCreatorのOperational化はWorldCreatorのProduction条件に含めません。

## Verification Gate

Production昇格には以下を要求します。

- 3 ToolがProduction Tool Discoveryへ含まれること
- Tool Countが`45 = 32 Graphics + 10 Agent + 3 WorldCreator`であること
- Read-only Preflightが3/3 Stepで成功すること
- Stale Revisionを拒否すること
- Human Review Requiredを維持すること
- Creator Sourceが直接Unity Mutation APIを呼ばないこと
- Automated CIが利用不能な場合は`not_verified`を維持し、手動Evidenceと混同しないこと
