---
name: unity-artist-setup
description: Install and diagnose the UnityArtistCLI host and its Unity Pipeline package.
---

# UnityArtistCLI setup

Use the official Unity CLI as the first transport. Check `unity --version`, then `unity artist version --format json`. The host executable must be available as `unity-artist` on PATH or through the repository release artifact.

For a project, use an explicit path and machine-readable output. Prefer `--project-path .` from the project root or a repository-relative path; never invent or copy a developer-specific absolute path into a command:

```text
unity artist install --project-path <project> --format json --non-interactive
unity artist doctor --project-path <project> --format json --non-interactive
unity artist capabilities --project-path <project> --format json --non-interactive
```

For repeatable verification from this repository, use `scripts/verify-external-cli.ps1 -ProjectPath .\TestProjects\UnityArtistVerification-URP`. The script resolves the local host executable and passes `--project-path .` after entering the selected project, so the same command works on another Windows account or checkout location. `UNITY_ARTIST_PROJECT_PATH` and `UNITY_ARTIST_CLI_PATH` are optional configuration inputs for automation.

Do not auto-install an Editor, bypass a Unity license, or silently switch transports. For Unity 2022.3 Built-in, verify official CLI + Pipeline first; only the concrete Unity 6-or-later Pipeline Gate Failure permits the documented fixed `unity run` batch fallback at `DarumaPPAP.UnityArtist.UnityArtistBatchCommands.Dispatch`. Do not use dynamic code, raw YAML, MCP, or generic CRUD through that fallback. Unsupported pipelines remain blocked before mutation.
