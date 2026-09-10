# Decision Log: Unity 6 URP native Volume LookDev adapter

Date: 2026-09-10

## Decision

Expose the bounded URP Volume operation through the existing
`artist.plan -> artist.preview -> approved artist.apply` contract. Resolve
`UnityEngine.Rendering.Universal.ColorAdjustments` by reflection so the
package remains loadable when the active project is Built-in or HDRP. The
operation changes only `postExposure` and `contrast`, records Undo for the
Volume/Profile/override, marks the scene and profile dirty, and never saves
automatically.

## Rationale

Unity 6 URP's creative lookdev surface is a Volume override rather than the
HDRP fog API or Built-in RenderSettings. Keeping the adapter semantic and
allowlisted gives UnityAgent a single visual intent contract while respecting
pipeline-specific implementation details. If the active project does not
provide a Volume profile or the ColorAdjustments API, planning fails closed
with structured evidence instead of silently falling back to generic mutation.

## Evidence

The Unity 6 URP fixture contains `ArtistCourtyardVolume` with
`Assets/Volumes/ArtistCourtyardProfile.asset`. The live run planned an absent
ColorAdjustments override, applied `postExposure=-0.6` and `contrast=15` with
approval and revision guards, saved through the official `save_all` command,
and confirmed one ColorAdjustments component using `get_serialized_fields`.
