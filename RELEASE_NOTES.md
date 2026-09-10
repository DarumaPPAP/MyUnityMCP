# UnityArtistCLI 2.0.0

UnityArtistCLI 2.0.0 moves the current product from the old MCP-first MyUnityMCP surface to an official Unity CLI + Unity Pipeline Artist specialist.

## Production surface

- `unity-artist` host CLI with structured human/JSON/NDJSON output
- LookDev, visual direction, lighting, environment, camera, capture, evaluation and refinement
- Timeline, Cinemachine Shot, track/clip/binding, marker/signal, activation/control/animation planning
- Explicit project binding, exact diff, expected revision, approval, Undo and Evidence lifecycle
- UnityAgent Provider id `unity_artist_cli` through the existing Runtime chain

## Support

- Unity 2022.3 LTS + Built-in
- Unity 6.x+ + Built-in
- Unity 6.x+ + URP
- Unity 6.x+ + HDRP

2022.3 URP/HDRP, Unity 2023 and URP 14–16 are unsupported before mutation. Unity 2022.3 uses official Unity CLI + Pipeline as the first candidate.

## Verification status

Host CLI build, structured envelopes, compatibility preflight and static contracts are covered. Direct Editor/Pipeline connection and visual E2E require an available licensed Editor and are recorded as `blocked_by_environment` when unavailable.

MyUnityMCP v1.1.1 and its immutable tag remain available as migration history under `Legacy/MyUnityMCP-1.1.1/`.
