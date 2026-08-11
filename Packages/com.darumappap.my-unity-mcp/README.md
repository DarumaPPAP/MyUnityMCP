# MyUnityMCP

Unity EditorのGraphics制作をMCP Clientから安全にInspection、Planning、Mutation、Save、Bake、Capture、Evaluate、Refineし、UnityAgentMCPから複数Tool Workflowを安全にOrchestrationするPackageです。

- Unity `6000.0`以上
- Editor only
- Current main: 42 Tool（32 Graphics + 10 Agent）、すべて`AutoRegister = false`
- Stable `v1.0.0` baseline: 32 Graphics Tool
- UnityAgentMCPはControl PlaneでありUnity APIを直接Mutationしません
- MCP for Unity Bridgeが必要

Version／Tag Publicationは`VERSION`変更を伴う別Release操作です。Current mainのCapability Source Promotionはimmutableな`v1.0.0` Tagを変更しません。

導入は[Installation](Documentation~/installation.md)、Tool一覧は[Tool Reference](Documentation~/tool-reference.md)、最初の実行は[Quick Start](Documentation~/quick-start.md)を参照してください。