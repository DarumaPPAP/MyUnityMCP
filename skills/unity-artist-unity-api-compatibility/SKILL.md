---
name: unity-artist-unity-api-compatibility
description: Maintain UnityArtistCLI compatibility across the four supported Unity/pipeline rows.
---

# UnityArtistCLI Unity API Compatibility

Use this skill for C#, asmdef, rendering, Timeline, package, or Unity-version changes.

1. Read `Specs/Compatibility/unity-api-compatibility.md` and `Tests/Compatibility/support-matrix.yaml`.
2. Keep exactly the maintenance buckets `BASE`, `UNITY_6000_4`, `UNITY_6000_5`, and `UNITY_6000_7`; never add a 6000.6 bucket. Roll 6.6 changes into 6000.7.
3. Keep confirmed and planned facts separate. Do not infer Package API availability from Editor version alone.
4. Keep `ArtistCompatibility.cs` and `ArtistCompatibilityTests.cs` in the same change.
5. Add a `.meta` file for every Unity package asset. This is the Immutable package asset rule.
6. Do not add `GetInstanceID`, raw SceneHandle/int identity conversion, legacy component shortcuts, or an unsupported SRP API. This is the Scene identity rule and applies even when the target is an older Editor.
7. Validate the formal matrix before any mutation. Unsupported version/pipeline combinations return a typed failure.

The official Unity CLI plus Unity Pipeline is the first transport for Unity 2022.3 Built-in and Unity 6 Built-in/URP/HDRP. A fallback requires concrete connected-fixture evidence of a CLI/Pipeline Gate Failure.
