# UnityArtistCLI final acceptance audit

Date: 2026-09-11 (JST)
Audited revision: `b4e5b75` (`migration/unity-artist-cli-v2`)
Authority: `01_GOAL_AND_DEFINITION_OF_DONE.md`, `08_ACCEPTANCE_AND_TEST_PLAN.md`, `09_CODEX_MASTER_PROMPT.md`, `12_UNITY_VERSION_PIPELINE_SUPPORT.md`, and the repository contracts.

## Result

Overall state: **BLOCKED**  
Completion state: `partial_verified`  
Production-ready: `false`

The CLI-first cutover and the live Artist flows are implemented and verified for Unity 6 Built-in, URP, and HDRP. The release cannot be reported as complete because the required Unity 2022.3 official Pipeline gate is concretely incompatible on this host.

## Definition of Done audit

| Area | Status | Evidence / reason |
|---|---|---|
| A. CLI-first architecture and cutover | PASS | UnityAgent keeps the existing Capability → Provider Registry → Resolver → Dispatcher → Provider Adapter → Evidence chain; the production package has no MCP runtime dependency and legacy MyUnityMCP remains migration-only. |
| B. Artist capability boundary | PASS | UnityArtistCLI is limited to Artist inspection, LookDev, lighting, environment, camera, cinematic/Timeline, capture, evaluation, and refinement; generic CRUD and arbitrary C# execution are not exposed. |
| C. Safety and evidence lifecycle | PASS | JSON contracts, support preflight, expected-revision checks, opaque approval token, exact diffs, Undo registration, and separate `save_all` are covered by CLI and release validators. |
| D. Codex plugin / Marketplace contract | PASS | UnityAgent provider, routing/catalog entries, plugin manifest, and marketplace-facing contract validators pass. Publication/CI remains pending the authorized GitHub push. |
| E. Unity 6 Built-in matrix row | PASS | Direct fixture and UnityAgent → UnityArtistCLI → Unity/Pipeline E2E evidence pass in `unity6-builtin-e2e-evidence.yaml`. |
| F. Unity 6 URP matrix row | PASS | Primary visual fixture, native URP `ColorAdjustments` Volume transaction, capture/evaluate/refine, and 3-shot Cinemachine Timeline evidence pass in `unity6-urp-primary-visual-evidence.yaml` and `unity6-urp-cinematic-evidence.yaml`. |
| G. Unity 2022.3 LTS + Built-in row | BLOCKED | Official CLI/Pipeline was tested on Unity `2022.3.22f1` with CLI `1.0.0-beta.8`; Pipeline install returned that the installed package requires Unity 6.0+, so no compliant execution path was available. No fallback was selected. See `cli-pipeline-gate-evidence.yaml`. |
| H. Unity 6 HDRP matrix row | PASS | Full Official Pipeline-authored HDRP fixture, native `Fog` Volume transaction, capture/evaluate/refine loop, and 3-shot Cinemachine Timeline evidence pass in `unity6-hdrp-primary-visual-evidence.yaml` and `unity6-hdrp-cinematic-evidence.yaml`. |
| I. Tests, validation, documentation, and decision log | PASS | CLI build, CLI unit tests (9), MyUnityMCP release validators, UnityAgent canonical validation, live Unity recompile, URP/HDRP primary verifiers, and URP/HDRP Cinemachine verifiers pass; URP/HDRP Volume and Timeline decisions are recorded. |

## Mandatory URP scenario audit

Status: **PASS**

The official Unity CLI/Pipeline-authored URP fixture contains the required subject and goal proxies, courtyard geometry, directional light, camera, reflection probe, Volume/profile, materials, fog direction, and compressed three-shot Timeline. The observed lifecycle includes inspect → capture → plan → preview → approval guard → apply → save → evaluate/refine. The final capture is 1920×1080 and the final human review decision is `accepted`.

The native URP Volume adapter verified and persisted a `ColorAdjustments` component with `postExposure=-0.6` and `contrast=15`, with exact diff, Undo registration, and no implicit save. The direct capture renderer did not produce a distinct pixel hash for the post-processing camera toggle; therefore the Volume claim is supported by native profile inspection and transaction evidence, while the visual review is attributed to the authored scene, materials, fog, lighting, and camera composition rather than an inferred pixel delta.

## Mandatory HDRP scenario audit

Status: **PASS**

The official Unity CLI/Pipeline-authored HDRP fixture contains the required subject and goal proxies, courtyard geometry, directional light, camera, reflection probe, native HDRP Volume/Fog profile, HDRP materials, and compressed three-shot Timeline. The observed lifecycle includes inspect → capture → plan → preview → approval guard → apply → save → evaluate/refine. The initial capture was explicitly reviewed as `needs_refine`; the post-refinement 1920×1080 capture was explicitly accepted.

The native HDRP adapter verified and applied `HDRP.Fog.meanFreePath` from `400` to `28.5714`, with exact diff, Undo registration, and no implicit save. The separate cinematic evidence verifies all three Main Camera Brain bindings, all three virtual-camera references, clip timing, and the marker track.

Editor observability recorded zero C# compile errors. Unity 6 HDRP also emitted eight known package-resource `Host type is not matching any asset type` messages during import/render; they did not prevent the Official Pipeline connection, Artist command execution, or successful 1920×1080 captures, and are classified explicitly in the HDRP evidence rather than hidden.

## Remaining blockers

1. Re-run the official Unity CLI/Pipeline gate for Unity 2022.3 when a Pipeline package/CLI combination that actually supports 2022.3 is available, without silently selecting a fallback backend.
2. Publish the local migration commits, wait for GitHub CI, and update the pull request evidence once the destination authorization gate permits the push. Merging is intentionally not performed without an explicit merge request.

This audit deliberately reports `BLOCKED` rather than `complete` or `implemented_unverified`.
