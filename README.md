# UnityArtistCLI

UnityArtistCLI は、Unity Editor の世界観・雰囲気・LookDev・Lighting・Environment・Camera・Cinematic・Timeline・Visual Evaluation/Refine を、公式 Unity CLI と Unity Pipeline 経由で実行する CLI-first 製品です。

製品名は `UnityArtistCLI`、実行ファイルは `unity-artist`、UX は `unity artist <command>`、UPM Package は `com.darumappap.unity-artist`、C# namespace は `DarumaPPAP.UnityArtist` です。

## Current contract

```text
UnityAgent (Architect / Commander / Loop Owner)
  CapabilityRequest → Policy / Approval / Scope
  → Provider Registry → Resolver → Dispatcher
  → unity_artist_cli Provider Adapter
  → UnityArtistCLI → official Unity CLI / Unity Pipeline
  → ProviderResult → Evidence Normalizer → Persistence
```

UnityAgent に別の Player Framework や Registry は追加しません。Player は Provider の概念上の呼称であり、実装上の canonical name は `unity_artist_cli` です。

## Commands

```text
unity artist help
unity-artist --help
unity artist version --format json
unity artist doctor --project-path <project> --format json --non-interactive
unity artist capabilities --project-path <project> --format json
unity artist install --project-path <project> --format json --non-interactive
unity artist inspect|plan|preview|apply|capture|evaluate|refine|cinematic|history ...
```

`unity artist help` is the plugin help command and `unity-artist --help` is the standalone executable form. The installed Unity CLI beta currently intercepts `unity artist --help` as its own global help flag before plugin dispatch; this is recorded as an external CLI compatibility limitation rather than being presented as a successful Artist help invocation.

Operational commands require an explicit project path and support `human`, `json`, and `ndjson` output. Mutation follows `Inspect → Plan → Exact Diff → Expected Revision → UnityAgent Approval → Apply → Evidence`; Apply never saves automatically and registers Unity Undo.

UnityArtistCLI does not expose generic GameObject/hierarchy CRUD, compile/test/build/play/stop/log operations, arbitrary evaluation, generic Addressables/UI/Audio control, or a second Control Plane. Those concerns stay with the official Unity CLI or the existing UnityAgent Provider chain.

## Release matrix

| Unity | Pipeline | Tier | Transport |
|---|---|---|---|
| 2022.3 LTS | Built-in | primary | official Unity CLI + Unity Pipeline |
| Unity 6.x+ | Built-in | primary | official Unity CLI + Unity Pipeline |
| Unity 6.x+ | URP | primary | official Unity CLI + Unity Pipeline |
| Unity 6.x+ | HDRP | primary | official Unity CLI + Unity Pipeline |

2022.3 URP/HDRP、Unity 2023、URP 14–16 は正式対応外です。2022.3 も最初に公式 CLI + Pipeline の実接続を検証し、具体的な Gate Failure がない限り別 Backend へ切り替えません。

## Verification

```powershell
dotnet build src/UnityArtist.Cli/UnityArtist.Cli.csproj
python Tests/Release/verify_unity_artist_contract.py
python Tests/Compatibility/verify-unity-api-compatibility.py
```

実 Editor / License / Pipeline 接続がない環境では、静的契約・CLI parser・unsupported preflight までを検証し、Direct Editor と E2E は `blocked_by_environment` として記録します。未観測を成功に昇格させません。

## Migration

旧 MyUnityMCP v1.1.1 の Package と Client Template は `Legacy/MyUnityMCP-1.1.1/` に履歴付きで保持します。v1.1.1 Tag は変更せず、新しい production surface に MCP transport や `McpForUnityTool` を再導入しません。詳細は [MIGRATION_FROM_MYUNITYMCP.md](MIGRATION_FROM_MYUNITYMCP.md) を参照してください。

## Layout

```text
src/UnityArtist.Cli/                         # unity-artist host CLI
Packages/com.darumappap.unity-artist/        # Unity Pipeline command package
Legacy/MyUnityMCP-1.1.1/Package/              # legacy package source, not production
Tests/Compatibility/                         # matrix and compatibility gates
Tests/Release/                               # production contract validators
.agents/plugins/unity-artist/                # skill-only Codex plugin
Legacy/MyUnityMCP-1.1.1/                     # immutable migration reference
```

The UnityAgent repository owns the shared marketplace authority and its `unity-agent` plugin. The UnityArtistCLI repository owns only the `unity-artist` plugin.

MIT License. See [LICENSE](LICENSE).
