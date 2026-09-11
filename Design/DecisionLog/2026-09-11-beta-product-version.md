# Decision: publish the migration as 0.0.1-beta

Date: 2026-09-11

## Decision

Per the user's explicit product decision, the UnityArtistCLI migration is now
the `0.0.1-beta` release. This version is used consistently by the executable,
.NET project, UPM package, Codex plugin manifests, catalogs, compatibility
evidence, release scripts, and validation contracts.

The installation channel directory remains `Beta`:

- Windows: `%LOCALAPPDATA%\\UnityArtistCLI\\Beta`
- Unix: `~/.local/lib/unity-artist/Beta`

The migration Goal pack was updated to use `0.0.1-beta` as its target product
version. The frozen MyUnityMCP `v1.1.1` history and legacy package anchor remain
unchanged.

## Consequences

`0.0.1-beta` is valid SemVer and can be consumed by .NET, UPM, and Codex
Plugin tooling. The CLI `version` command reports the same value, and no
version-specific backend or transport behavior changes as a result of this
release-label decision.
