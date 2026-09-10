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

Use the host CLI from the repository root:

```text
unity artist doctor --project-path <project> --format json --non-interactive
unity artist install --project-path <project> --format json --non-interactive
```

The package uses the four release cases documented in `Specs/UnityArtistCLI/spec.md` and rejects unsupported version/pipeline combinations before mutation.
