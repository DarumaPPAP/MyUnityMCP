# UnityArtistCLI Unity API Compatibility

UnityArtistCLI maintains one compatibility boundary for the four supported release rows. It does not create a patch source fork for every Unity patch version.

## Buckets

The maintenance buckets are `BASE`, `UNITY_6000_4`, `UNITY_6000_5`, and `UNITY_6000_7`. Unity 6.6 changes roll into the `UNITY_6000_7` bucket. Confirmed API facts and planned breaking changes are separate in the compatibility record.

The current implementation is `Packages/com.darumappap.unity-artist/Editor/Compatibility/ArtistCompatibility.cs`; its EditMode coverage is `Packages/com.darumappap.unity-artist/Tests/Editor/ArtistCompatibilityTests.cs`. These files change together for compatibility-sensitive work and both carry Unity `.meta` files.

## Adapter policy

- Built-in uses the common `BASE` Editor API adapter on Unity 2022.3 LTS and Unity 6.x+.
- URP and HDRP use native Unity 6 adapters only.
- 2022.3 URP/HDRP are unsupported and must be rejected before mutation.
- Unity 2023 and URP 14–16 are outside the formal release matrix.
- Package API availability is checked from both Unity version and package version; Editor version alone is not evidence.
- No generic version-specific fallback, serialized raw mutation, or arbitrary evaluation is permitted.

## Gate evidence

The official Unity CLI is the first candidate, including the Unity 2022.3 Built-in case. A compatibility row is eligible only when the real fixture demonstrates: CLI identify/open/manage, Pipeline package install/availability, connected command discovery, bounded command execution, and structured safety-scope evidence. If a Gate fails, record the concrete failure before considering another backend.
