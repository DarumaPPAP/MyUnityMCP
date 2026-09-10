# Decision Log: HDRP native Volume/Fog adapter

Date: 2026-09-10

## Decision

Use the HDRP-native `Volume` + `Fog.meanFreePath` contract for HDRP fog
intents. Keep the public UnityArtistCLI intent as `setFog`/`fogDensity` and
map density to `meanFreePath = clamp(1 / density, 1, 10000)`.

## Rationale

HDRP does not use the Built-in/URP `RenderSettings.fogDensity` path as its
authoritative visual control. Reporting HDRP as primary while mutating only
`RenderSettings` would produce a false-positive adapter. The adapter therefore
inspects the exact scene Volume and profile, emits an exact native diff, records
Undo, and leaves the scene unsaved after apply.

## Compatibility boundary

The implementation discovers HDRP types by reflection so the same editor
assembly remains compilable in Unity 2022.3, where HDRP assemblies may not be
present. Planning and applying fail closed with `HDRP_VOLUME_FOG_REQUIRED` when
the exact Volume/profile/Fog override is absent. This adapter evidence is a
reduced HDRP matrix gate; it does not replace the full URP cinematic acceptance
scenario or the unresolved Unity 2022.3 Pipeline gate.
