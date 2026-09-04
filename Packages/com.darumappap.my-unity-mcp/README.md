# MyUnityMCP

Unity EditorをMCP Clientから安全にInspection、Planning、Mutation、Save、Bake、Capture、Evaluate、Refineし、UnityAgentMCPから複数Domain WorkflowをOrchestrationするEditor-only Packageです。

## v1.1.1

- Unity `6000.0`以上
- Editor only
- **77 Tool Editor Operational Surface**
  - Graphics 32
  - Agent 10
  - WorldCreator 3
  - Profiler 8
  - Addressables 4
  - UI 5
  - Animation 5
  - Audio 5
  - Cinematic 5
- 全Tool `AutoRegister = false`
- UnityAgentMCPはControl PlaneでありUnity APIを直接Mutationしません
- WorldCreatorもUnity APIを直接Mutationせず、Agent経由でRead-only Graphics Preflightへ委譲します
- AddressablesはOptional Packageで、未導入時は自動導入せず`UNSUPPORTED`を返します
- Addressables Content Build、Build Domain、MovieCreator runtime、LiveCreator runtimeはv1.1.1 Surfaceから除外しています

## Verification

v1.1.0 Unity `6000.7.0a2` Direct Editor Evidenceをbaselineとして保持し、v1.1.1 Release PRでUnity Editor CIを再実行してからstable tagを公開します。

Addressables Positive Backend Matrix、External Transport Disconnect/Reconnect、Target Deviceは未検証範囲として明示します。

導入は[Installation](Documentation~/installation.md)、全Tool一覧は[Tool Reference](Documentation~/tool-reference.md)、現在の77 Tool構成とEvidenceは[Production Surface](Documentation~/production-surface.md)を参照してください。
