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

- v1.1.0 Unity `6000.7.0a2` Direct Editor Evidenceをbaselineとして保持
- v1.1.1 Release CandidateをUnity `6000.0.75f1`でEditMode Contract / Compile / NUnit / Production Tool Discoveryまで再検証しPASS
- Unity `6000.4.12f1` / `6000.5.5f1` Compatibility Matrixを再検証しPASS
- Release static contracts / repository hygiene / source-of-truth / Agent Runtime / Safety validatorsをPASS
- Current Unity 6000.7 automated canaryはGameCI image unavailableのため`not_verified`。v1.1.0の`6000.7.0a2` Direct Editor Evidenceを代替の新規PASSとは扱いません

Target Device、Addressables Positive Backend Matrix、External MCP Transport Disconnect/Reconnectは引き続き未検証範囲です。

## Upgrade

v1.1.0からはPackage参照を`v1.1.1`へ更新してください。

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.1.1
```

公開済みTagはimmutableです。
