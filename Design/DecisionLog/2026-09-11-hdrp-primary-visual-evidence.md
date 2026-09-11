# Decision: Unity 6 HDRP primary visual evidence

Date: 2026-09-11

## Context

The Unity 6 HDRP release row was previously only reduced-verified. The acceptance contract requires an authored primary fixture, a real visual capture, an explicit review/refinement loop, and bounded Cinemachine/Timeline evidence.

## Decision

Complete the HDRP fixture through the Official Unity CLI/Pipeline transport and use the existing UnityArtistCLI provider chain. The fixture uses native HDRP `Volume` + `Fog` and the `hdrp_native_api` adapter. The fog intent is represented as a bounded `HDRP.Fog.meanFreePath` change (`400` to `28.5714`), not as a Built-in or URP fallback.

The scene is authored and persisted only through official Pipeline commands. Unity YAML was not edited. Cinemachine 6.6 tracks and clips are created through the bounded `artist.cinematic` capability, with the Main Camera's `CinemachineBrain` as the binding and three explicit virtual-camera references.

## Evidence

- `Tests/Compatibility/unity6-hdrp-primary-visual-evidence.yaml`
- `Tests/Compatibility/unity6-hdrp-cinematic-evidence.yaml`
- `Tests/Compatibility/verify-hdrp-primary-evidence.py`
- `Tests/Compatibility/verify-hdrp-cinematic-evidence.py`

The fixture was observed on Unity `6000.6.0f1`, Official Pipeline `0.6.0-exp.1`, and the HDRP native backend. The initial capture was explicitly reviewed as `needs_refine`; the post-refinement capture was explicitly accepted.

## Consequence

The Unity 6 HDRP matrix row is primary-verified. The overall four-row release remains blocked only by the independent Unity 2022.3 Official Pipeline gate until an official compatible Pipeline release can be observed on a 2022.3 Editor.
