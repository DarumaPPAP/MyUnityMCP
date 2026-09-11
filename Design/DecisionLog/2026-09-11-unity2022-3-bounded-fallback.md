# Decision Log: Unity 2022.3 bounded Artist fallback

Date: 2026-09-11 (JST)
Goal: `unity_artist_cli_cutover_v2`

## Decision

Select `official_unity_cli_bounded_batch_fallback` for the observed Unity
2022.3 LTS + Built-in compatibility case, but only after the Official Unity
CLI/Pipeline first-candidate gate has failed concretely. Keep Official
Unity CLI + Unity Pipeline as the formal primary transport for the release
matrix and for any future 2022.3 package/CLI combination that passes that gate.

## Evidence that permits the selection

The exhaustive probe against Unity `2022.3.22f1` with Unity CLI
`1.0.0-beta.8` tested every currently available official Pipeline version.
Each candidate returned the concrete message that the Pipeline package
requires Unity 6.0 or later. The result is recorded in
`Tests/Compatibility/cli-pipeline-gate-evidence.yaml`.

## Bounded implementation

The host invokes `unity run` with a fixed
`DarumaPPAP.UnityArtist.UnityArtistBatchCommands.Dispatch` entrypoint. The
request is a base64-encoded structured JSON DTO in
`UNITY_ARTIST_BATCH_REQUEST`; the provider returns one JSON response file in
`UNITY_ARTIST_BATCH_RESPONSE`. The bridge is restricted to Unity 2022.3
Built-in, an exact project-relative scene, and the allowlisted Artist
commands. It reuses the existing `ArtistSession` and its Inspect → Plan →
Exact Diff → Expected Revision → Approval → Apply → Undo / Evidence lifecycle.
Batch processes persist only plan/capture/history metadata under
`Library/UnityArtist/BatchSession.json`; approval tokens are not persisted and
Artist Apply never saves the scene.

Dynamic code execution, raw Unity YAML mutation, MCP transport, generic Unity
CRUD, arbitrary evaluation, and automatic scene saving are explicitly
forbidden. The fallback is therefore a transport adapter for the existing
Artist provider, not a second Player Framework.

## Verification

The disposable fixture passed compile-error inspection, inspect/plan/preview,
approval rejection, exact-diff apply with Undo and no save, 1920×1080 capture,
human evaluation, linked refine/apply, and cross-process history. The formal
record is `Tests/Compatibility/unity2022-3-builtin-bounded-fallback-evidence.yaml`.
