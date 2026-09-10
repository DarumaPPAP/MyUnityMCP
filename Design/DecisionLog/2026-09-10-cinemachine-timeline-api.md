# Decision Log: Cinemachine and Timeline API resolution

Date: 2026-09-10

## Decision

Keep Cinemachine/Timeline mutation behind the existing bounded
`artist.cinematic` capability and resolve optional artifact types by
reflection. For Cinemachine 3/6, prefer the `Unity.Cinemachine` assembly and
retain the legacy `Cinemachine` and `Unity.Cinemachine.Runtime` candidates for
older supported projects.

Timeline markers are created through the public Timeline API sequence
`TimelineAsset.CreateMarkerTrack()` → `TimelineAsset.markerTrack` →
`TrackAsset.CreateMarker(Type, double)`. The product records Undo for both the
PlayableDirector and its TimelineAsset and does not save during an apply.

## Rationale

Cinemachine 6.6 exposes `Unity.Cinemachine.CinemachineTrack` from the
`Unity.Cinemachine` runtime assembly; the earlier assembly-qualified name
reported the capability as unavailable even though the package was installed.
The marker factory is owned by `TrackAsset`, not `TimelineAsset`; using the
public API on the marker track keeps the implementation compatible with the
installed Timeline package without hard-linking optional Cinemachine types.

## Evidence boundary

The Unity 6 URP fixture was driven through the official Unity CLI/Pipeline:
three Cinemachine tracks were inspected, three shot-camera bindings were
planned/previewed/approval-gated/applied, a Timeline marker was applied, and
the saved fixture was verified with `get_timeline`. This is product capability
evidence; cinematic visual-quality review remains a separate human-review
gate.
