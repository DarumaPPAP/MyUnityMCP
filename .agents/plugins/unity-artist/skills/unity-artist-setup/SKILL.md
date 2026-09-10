---
name: unity-artist-setup
description: Install and diagnose the UnityArtistCLI host and its Unity Pipeline package.
---

# UnityArtistCLI setup

Use the official Unity CLI as the first transport. Check `unity --version`, then `unity artist version --format json`. The host executable must be available as `unity-artist` on PATH or through the repository release artifact.

For a project, use an explicit path and machine-readable output:

```text
unity artist install --project-path <project> --format json --non-interactive
unity artist doctor --project-path <project> --format json --non-interactive
unity artist capabilities --project-path <project> --format json --non-interactive
```

Do not auto-install an Editor, bypass a Unity license, create a fallback transport, or continue when the doctor reports an unsupported Unity version/pipeline. For Unity 2022.3, verify official CLI + Pipeline first; only a concrete connected Gate Failure permits considering another backend.
