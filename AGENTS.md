# UnityArtistCLI Repository Policy

## Authority

The attached migration goal pack is the implementation authority for this cutover. Current product truth is held by `VERSION`, `Packages/com.darumappap.unity-artist/package.json`, `Catalog/`, `Specs/`, `Tests/`, and the UnityArtistCLI CLI/package contracts. Historical MyUnityMCP material is under `Legacy/MyUnityMCP-1.1.1/` and Git history.

Do not reintroduce the old MCP bridge, `McpForUnityTool`, a `.mcp.json` plugin manifest, a second Player/Provider registry, or generic Unity CRUD into the new product.

## Architecture

UnityAgent remains Architect / Commander / Loop Owner. It owns semantic intent, Policy, Approval, Mutation Scope, routing, loop control, fallback, and Evidence. The existing Runtime chain is mandatory:

```text
CapabilityRequest → Runtime Guard → ToolBroker → Resolver
→ Environment Snapshot → Provider Registry → Dispatcher
→ Provider Adapter → ProviderResult → Evidence Normalizer → Persistence
```

UnityArtistCLI is a specialist Provider. The canonical provider id is `unity_artist_cli`; `Player` is not a new runtime abstraction. Semantic requests use `capability: domain.workflow` with qualifiers such as `domain: visual_art|cinematic` and a workflow value. Provider identity must never be hard-coded into Orchestration routes.

## Safety

Inspect and Plan are read-only. Mutations require an exact diff, current revision, explicit UnityAgent approval, expected revision, and a bounded allowlist. Apply uses Unity Undo and does not auto-save. Capture is evidence, not visual acceptance; Evaluate records a human decision; Refine links a new plan to that decision. Unknown, unsupported, stale, malformed, timeout, and unavailable states fail closed.

The UnityArtistCLI adapter must use typed argv, an explicit project path, bounded timeout/cancellation, structured JSON only, and an allowlisted command map. It must not evaluate arbitrary code, mutate serialized assets generically, infer aesthetic decisions, or silently switch transports. The only current fallback is the fixed `official_unity_cli_bounded_batch_fallback` for Unity 2022.3 LTS + Built-in, and it is selectable only after the concrete Official Unity CLI/Pipeline gate failure is recorded.

Project targeting must remain explicit, but production documentation and committed evidence must not contain a developer-machine absolute path. Prefer `--project-path .` from a project root, repository-relative fixture paths, or the task-specific `UNITY_ARTIST_PROJECT_PATH` input. Resolve paths only at the process boundary; record logical fixture paths and executable names in evidence.

## Official Unity CLI first

The official Unity CLI is the first transport for every supported matrix row, including Unity 2022.3 Built-in. The Pipeline package must be installed and reachable before editor command execution. A fallback is permitted only after a concrete compatibility Gate Failure is recorded with the Unity version, render pipeline, CLI/Pipeline observation, and failure class.

The bounded 2022.3 fallback uses `unity run` with the fixed `DarumaPPAP.UnityArtist.UnityArtistBatchCommands.Dispatch` entrypoint and the shared `ArtistSession`; it accepts structured JSON only, rejects dynamic code/raw YAML/MCP/generic CRUD, reopens an exact scene, persists only redacted plan/capture/history metadata, and never auto-saves an Artist mutation. It is not a second Player Framework or a generic backend.

Formal support is limited to:

- Unity 2022.3 LTS + Built-in
- Unity 6.x+ + Built-in
- Unity 6.x+ + URP
- Unity 6.x+ + HDRP

2022.3 URP/HDRP, Unity 2023, and URP 14–16 are rejected before mutation. API compatibility uses only `BASE`, `UNITY_6000_4`, `UNITY_6000_5`, and `UNITY_6000_7`; 6.6 changes roll into the 6.7 bucket.

## Codex plugins

This repository contains the skill-only `unity-artist` plugin at `.agents/plugins/unity-artist/`. It has no `.mcp.json`. The `unity-agent` plugin and marketplace authority live in the UnityAgent repository at `.agents/plugins/marketplace.json`.

## Verification

Run the relevant validators after every contract change. The minimum cutover set is:

```powershell
dotnet build src/UnityArtist.Cli/UnityArtist.Cli.csproj
python Tests/Release/verify_unity_artist_contract.py
python Tests/Compatibility/verify-unity-api-compatibility.py
```

`python Tests/Release/verify_portable_paths.py` is the guard against reintroducing account- or machine-specific paths into the production surface.

For C#/asmdef/Unity API changes, keep the package compatibility implementation and EditMode tests in the same change. Direct Editor, License, Pipeline, and visual E2E evidence must be labeled separately from static or host validation.

Compatibility-sensitive changes must apply `skills/unity-artist-unity-api-compatibility/SKILL.md`.

## Migration history

Never rewrite or move the published MyUnityMCP v1.1.1 Tag. Old package files and client templates remain available under `Legacy/MyUnityMCP-1.1.1/` as migration references only; they are not the current production surface.
