# MyUnityMCP

Unity EditorのGraphics制作をMCP Clientから安全にInspection、Planning、Mutation、Save、Bake、Capture、Evaluate、Refineし、UnityAgentMCPから複数Tool Workflowを安全にOrchestrationし、WorldCreatorからVisual GoalのRead-only PreflightとHuman Review Handoffを作成するPackageです。

- Unity `6000.0`以上
- Editor only
- Current main target: 45 Tool（32 Graphics + 10 Agent + 3 WorldCreator）、すべて`AutoRegister = false`
- Stable `v1.0.0` baseline: 32 Graphics Tool
- UnityAgentMCPはControl PlaneでありUnity APIを直接Mutationしません
- WorldCreatorもUnity APIを直接Mutationせず、Agent経由でGraphics Read-only Preflightへ委譲します
- WorldCreatorのReview HandoffはHuman Review必須で、自動Visual Acceptanceを行いません
- MCP for Unity Bridgeが必要

Version／Tag Publicationは`VERSION`変更を伴う別Release操作です。Current mainのCapability Source Promotionはimmutableな`v1.0.0` Tagを変更しません。

導入は[Installation](Documentation~/installation.md)、Tool一覧は[Tool Reference](Documentation~/tool-reference.md)、最初の実行は[Quick Start](Documentation~/quick-start.md)を参照してください。
