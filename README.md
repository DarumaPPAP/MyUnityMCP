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

Project paths are explicit for safe Editor targeting, but they do not need to be machine-specific absolute paths. Run from the Unity project root with `--project-path .`, or pass a repository-relative path such as `--project-path .\TestProjects\UnityArtistVerification-URP`. For repeatable external verification, the repository-provided script resolves the project and local Release host without changing the system PATH:

```powershell
# From this repository root; use the current directory when already inside a Unity project.
.\scripts\verify-external-cli.ps1 -ProjectPath .\TestProjects\UnityArtistVerification-URP

# From a real project root, no absolute user path is required.
.\path\to\UnityArtistCLI\scripts\verify-external-cli.ps1 -ProjectPath .
```

The script accepts `UNITY_ARTIST_PROJECT_PATH` and `UNITY_ARTIST_CLI_PATH` when a caller needs configuration outside the current directory. It resolves those values only at the process boundary; committed commands and evidence use logical fixture paths, not a developer's home directory.

UnityArtistCLI does not expose generic GameObject/hierarchy CRUD, compile/test/build/play/stop/log operations, arbitrary evaluation, generic Addressables/UI/Audio control, or a second Control Plane. Those concerns stay with the official Unity CLI or the existing UnityAgent Provider chain.

## Release matrix

| Unity | Pipeline | Tier | Transport |
|---|---|---|---|
| 2022.3 LTS | Built-in | primary | official Unity CLI + Unity Pipeline |
| Unity 6.x+ | Built-in | primary | official Unity CLI + Unity Pipeline |
| Unity 6.x+ | URP | primary | official Unity CLI + Unity Pipeline |
| Unity 6.x+ | HDRP | primary | official Unity CLI + Unity Pipeline |

2022.3 URP/HDRP、Unity 2023、URP 14–16 は正式対応外です。2022.3 Built-in も最初に公式 CLI + Pipeline の実接続を検証します。今回のホストでは全列挙版が Unity 6.0 要件で具体的に失敗したため、その証跡後に限り、固定 `unity run` バッチ入口 `DarumaPPAP.UnityArtist.UnityArtistBatchCommands.Dispatch` を限定フォールバックとして使用します。これは shared `ArtistSession` を再利用し、動的コード・MCP・汎用CRUD・自動保存を許可しません。

## Verification

```powershell
dotnet build src/UnityArtist.Cli/UnityArtist.Cli.csproj
python Tests/Release/verify_unity_artist_contract.py
python Tests/Compatibility/verify-unity-api-compatibility.py
python -m unittest Tests/Minimal/test_minimal_smoke_contract.py
python Tests/Compatibility/verify-primary-urp-evidence.py
python Tests/Compatibility/verify-cinematic-evidence.py
python Tests/Compatibility/verify-hdrp-primary-evidence.py
python Tests/Compatibility/verify-hdrp-cinematic-evidence.py
```

実 Editor / License / Pipeline 接続がない環境では、静的契約・CLI parser・unsupported preflight までを検証し、Direct Editor と E2E は `blocked_by_environment` として記録します。未観測を成功に昇格させません。現在は Unity 6 Built-in/URP/HDRP の live evidence と、Unity 2022.3 Built-in の「公式 Pipeline 全列挙版の gate failure → 固定バッチ fallback」evidence を個別の compatibility contract で検証しています。

接続済みのUnity 6 Editorに対する最小ライブ検証は、`python scripts/run_minimal_live_smoke.py --project-path .\TestProjects\UnityArtistVerification` で実行できます。これは一つのCubeとMain Cameraだけを使い、Artistの計画・承認・適用・PNG capture・評価・Refine・履歴を短時間で検証します。結果は `Tests/Compatibility/unity6-builtin-minimal-smoke-evidence.yaml` に記録します。Editorの対象指定は常に呼び出し側で行い、固定のユーザー別絶対パスを前提にしません。

## Migration

旧 MyUnityMCP v1.1.1 の Package と Client Template は `Legacy/MyUnityMCP-1.1.1/` に履歴付きで保持します。v1.1.1 Tag は変更せず、新しい production surface に MCP transport や `McpForUnityTool` を再導入しません。詳細は [MIGRATION_FROM_MYUNITYMCP.md](MIGRATION_FROM_MYUNITYMCP.md) を参照してください。

## Layout

```text
src/UnityArtist.Cli/                         # unity-artist host CLI
Packages/com.darumappap.unity-artist/        # UnityArtist Editor API + optional Pipeline registrations
Legacy/MyUnityMCP-1.1.1/Package/              # legacy package source, not production
Tests/Compatibility/                         # matrix and compatibility gates
Tests/Release/                               # production contract validators
.agents/plugins/unity-artist/                # skill-only Codex plugin
Legacy/MyUnityMCP-1.1.1/                     # immutable migration reference
```

The UnityAgent repository owns the shared marketplace authority and its `unity-agent` plugin. The UnityArtistCLI repository owns only the `unity-artist` plugin.

MIT License. See [LICENSE](LICENSE).
