# UnityArtistCLI 2.0.0 Specification

## Product boundary

UnityArtistCLI is the Artist specialist Provider. It handles visual intent inspection and planning, mood/lookdev, lighting, environment, sky/fog/reflection/GI, camera/depth/continuity, Cinemachine/TL cinematic planning, capture, human evaluation, and linked refinement. It does not become a general Unity editor API.

The host executable is `unity-artist`; the command UX is `unity artist`; the UPM package is `com.darumappap.unity-artist`; the C# namespace is `DarumaPPAP.UnityArtist`.

## Transport and layering

```text
UnityAgent CapabilityRequest
  → Runtime Guard / Policy / Approval / Mutation Scope
  → Provider Registry / Resolver / Dispatcher
  → unity_artist_cli Provider Adapter
  → unity-artist
  → official Unity CLI command
  → Unity Pipeline [CliCommand]
  → Unity Editor API
  → structured ProviderResult / Evidence
```

The adapter accepts typed argv only, always supplies an explicit project path, uses bounded cancellation, parses JSON only, and uses an allowlisted command map. It never calls arbitrary eval or chooses aesthetic intent.

For Unity 2022.3 LTS + Built-in, the host always probes the Official Unity CLI/Pipeline install first. Only the recorded Unity 6.0-or-later Pipeline compatibility failure selects the bounded `official_unity_cli_bounded_batch_fallback`: `unity run` invokes the fixed `DarumaPPAP.UnityArtist.UnityArtistBatchCommands.Dispatch` method with base64-encoded structured JSON and a single JSON response file. The bridge reuses `ArtistSession`, allowlists the Artist commands, loads an exact scene, and does not auto-save or persist approval tokens. It is not a second Player Framework, MCP transport, dynamic-code executor, or generic Unity CRUD surface.

## Command contract

Required commands are `help`, `version`, `doctor`, `capabilities`, `install`, `inspect`, `plan`, `preview`, `apply`, `capture`, `evaluate`, `refine`, `history`; `cinematic` is the explicit Timeline/Cinemachine specialist extension. Operational commands support `--project-path`, `--format human|json|ndjson`, `--non-interactive`, and `--verbose`. `unity artist help` and standalone `unity-artist --help` are the supported help entrypoints; the Unity CLI beta's global `unity artist --help` form is host-intercepted before plugin dispatch and is tracked as an external compatibility limitation.

`version --format json` returns product, CLI version, package id, transport, and compatibility backend. `doctor` observes CLI, project, package, Pipeline, safe-mode, render-pipeline, Timeline, Cinemachine, and approval readiness. `capabilities` returns the formal matrix and observed capabilities.

## Visual safety lifecycle

```text
Inspect → Visual Intent → Exact Plan/Diff → Expected Revision
→ UnityAgent Approval → Apply → Undo/Evidence
→ Capture → Human Evaluate → Refine
```

Inspect/Plan/Preview are read-only. Apply requires `plan-id`, `expected-revision`, and an opaque UnityAgent approval token. It does not save the project. Capture produces a bundle reference and camera/look/lighting/plan metadata; capture is not acceptance. Evaluate requires a human decision: `accepted`, `rejected`, or `needs_refine`.

## Artist and cinematic surface

The semantic surface includes LookDev and visual direction, Lighting, Environment, Camera, Visual Capture, Visual Evaluation, Refine, Timeline, Cinemachine Shot, Activation, Signal/Marker, Control, Animation, Track/Clip/Binding inspection and bounded mutation. Generic Project/Scene Save/Test/Build/Play/Stop/Console operations are delegated to the official Unity CLI or existing UnityAgent providers.

## Support matrix

| Unity version | Pipeline | Adapter | Status |
|---|---|---|---|
| 2022.3 LTS | Built-in | `builtin_editor_api` | primary |
| Unity 6.x+ | Built-in | `builtin_editor_api` | primary |
| Unity 6.x+ | URP | `urp_native_api` | primary |
| Unity 6.x+ | HDRP | `hdrp_native_api` | primary |

The 2022.3 row is verified with the official Unity CLI + Unity Pipeline first, followed by the bounded batch fallback after the observed concrete compatibility failure. 2022.3 URP/HDRP, Unity 2023, and URP 14–16 are rejected before mutation with a typed unsupported result.

## Error and terminal states

The host uses structured codes including `PROJECT_PATH_REQUIRED`, `UNITY_CLI_UNAVAILABLE`, `PIPELINE_INSTALL_FAILED`, `CAPABILITY_UNAVAILABLE`, `UNSUPPORTED_UNITY_VERSION`, `UNSUPPORTED_RENDER_PIPELINE_VERSION`, `STALE_REVISION`, `APPROVAL_REQUIRED`, `PLAN_ID_REQUIRED`, `CAMERA_NOT_FOUND`, `INVALID_REVIEW_DECISION`, `TIMEOUT`, and `ARTIST_PIPELINE_COMMAND_FAILED`.

Evidence terminal states are `verified`, `partial_verified`, and `blocked_by_environment`; `implemented_unverified` is not a completion state. The 2022.3 bounded fixture uses `verified_for_fixture` inside its evidence record and is rolled up to the release audit separately from the remaining host-level limitations.
