# MyUnityMCP v1.1.1

MyUnityMCP `1.1.1`は、77 Tool Production Surfaceを維持したまま、UnityAgent Control Planeの実行基盤とRepository / Release Contractを重点的に安定化するPatch Releaseです。

## Highlights

- UnityAgent Runtime Catalogをv5 Tool Object形式へ移行
- Catalog / Approval / Graph Compile / Execution / History / Trace / Result Normalizationを責務別Serviceへ分離
- Delegated Resultのfalse-success防止を強化
- Execution History migrationの互換ギャップを修正
- Agent Runtime Catalog / Safety Contract専用Validatorを追加
- Compatibility Source-of-Truthをcanonical registryへ統一
- Historical Evidence / obsolete Sample Surface /旧Support MatrixをProduction mainから除去
- Repository Hygiene Gateを追加
- Release Publication WorkflowをRelease Gateと同じStatic Contractセットへ同期

## Production Surface

Production Surfaceはv1.1.0と同じ **77 Tool** です。

- Graphics 32
- Agent 10
- WorldCreator 3
- Profiler 8
- Addressables 4
- UI 5
- Animation 5
- Audio 5
- Cinematic 5

Build Domain、Addressables Content Build、MovieCreator runtime、LiveCreator runtimeは引き続きSurface外です。

## Verification

v1.1.0のUnity `6000.7.0a2` Direct Editor Evidenceをbaselineとして保持します。

v1.1.1はRelease PRでUnity `6000.0.75f1`のEditMode Contract、Compile、Production Tool Discovery、Agent Runtime / Safety Contractを再検証し、PASS後にのみstable tagを公開します。

Target Device、Addressables Positive Backend Matrix、External MCP Transport Disconnect/Reconnectは引き続き未検証範囲です。

## Upgrade

v1.1.0からはPackage参照を`v1.1.1`へ更新してください。

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.1.1
```

公開済みTagはimmutableです。
