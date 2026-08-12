# MyUnityMCP

Unity EditorのGraphics制作をMCP Clientから安全にInspection、Planning、Mutation、Save、Bake、Capture、Evaluate、Refineし、UnityAgentMCPから複数Tool Workflowを安全にOrchestrationし、WorldCreatorからVisual GoalのRead-only PreflightとHuman Review Handoffを作成するPackageです。

- Unity `6000.0`以上
- Editor only
- Production main: 45 Tool（32 Graphics + 10 Agent + 3 WorldCreator）、すべて`AutoRegister = false`
- `delivery/stage2-8-integration`: 79 Tool Integration Candidate（Production 45 + Candidate 34）
- Candidate 34: Profiler 8 + Addressables 6 + UI 5 + Animation 5 + Audio 5 + Cinematic 5
- Build Domainは今回のIntegration Candidateから撤去済み
- Stage 2〜8 CandidateはImplementation Complete / Validation Resetで、まだProduction Operationalではありません
- Stable `v1.0.0` baseline: 32 Graphics Tool
- UnityAgentMCPはControl PlaneでありUnity APIを直接Mutationしません
- WorldCreatorもUnity APIを直接Mutationせず、Agent経由でGraphics Read-only Preflightへ委譲します
- AddressablesはOptional Packageで、未導入時は`UNSUPPORTED`を返します
- MCP for Unity Bridgeが必要

Version／Tag Publicationは`VERSION`変更を伴う別Release操作です。Integration Candidateの実装はimmutableな`v1.0.0` TagやProduction 45 Tool Evidenceを変更しません。

導入は[Installation](Documentation~/installation.md)、Production Tool一覧は[Tool Reference](Documentation~/tool-reference.md)、Stage 2〜8 Integration Candidateは[Stage 2-8 Integration Wave](Documentation~/stage2-8-integration.md)を参照してください。
