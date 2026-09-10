# UnityArtistCLI Unity Package

This package registers the Unity Pipeline command adapter for UnityArtistCLI. It is Editor-only and intentionally focused on visual-art and cinematic workflows.

Supported command registrations:

- `artist.inspect`
- `artist.plan`
- `artist.preview`
- `artist.apply`
- `artist.capture`
- `artist.evaluate`
- `artist.refine`
- `artist.cinematic`
- `artist.history`

The package never exposes generic GameObject CRUD, arbitrary C# evaluation, automatic Save, automatic full Bake, or silent fallback. `artist.apply` requires an opaque UnityAgent approval token and an expected revision, registers Undo, and leaves saving to a separate approved operation.

For Unity 6 URP LookDev, an intent may set `setVolumeLookDev: true` together with `volumePostExposure` and `volumeContrast`. The adapter resolves the scene's Volume and profile through the Unity Editor API, creates or reuses URP `ColorAdjustments`, records Undo, returns an exact diff, and fails closed outside URP. It does not save implicitly; call the separately approved `save_all` operation after reviewing the evidence.

Example intent:

```json
{"workflow":"lookdev","targetName":"ArtistCourtyardVolume","setVolumeLookDev":true,"volumePostExposure":-0.6,"volumeContrast":15}
```

The URP Volume operation is a bounded Artist capability, not a generic component/property editor. Built-in and HDRP use their own supported adapters and reject this URP-specific intent before mutation.

Use the host CLI from the repository root:

```text
unity artist doctor --project-path <project> --format json --non-interactive
unity artist install --project-path <project> --format json --non-interactive
```

The package uses the four release cases documented in `Specs/UnityArtistCLI/spec.md` and rejects unsupported version/pipeline combinations before mutation.
