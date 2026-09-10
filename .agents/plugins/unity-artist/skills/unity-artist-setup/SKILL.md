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

Do not auto-install an Editor, bypass a Unity license, or silently switch transports. For Unity 2022.3 Built-in, verify official CLI + Pipeline first; only the concrete Unity 6-or-later Pipeline Gate Failure permits the documented fixed `unity run` batch fallback at `DarumaPPAP.UnityArtist.UnityArtistBatchCommands.Dispatch`. Do not use dynamic code, raw YAML, MCP, or generic CRUD through that fallback. Unsupported pipelines remain blocked before mutation.
