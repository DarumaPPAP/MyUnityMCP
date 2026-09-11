# Decision Log: Remote Bootstrap Installer

Date: 2026-09-11  
Status: accepted for `0.0.1-beta`

## Context

The local `scripts/install.ps1` installer assumes a repository checkout and a .NET SDK. The requested user experience is a one-line PowerShell bootstrap comparable to the Unity CLI Loop installer:

```powershell
irm https://raw.githubusercontent.com/DarumaPPAP/UnityArtistCLI/main/scripts/install-remote.ps1 | iex
```

## Decisions

1. Keep `scripts/install.ps1` as the checkout-based developer installer and add `scripts/install-remote.ps1` as a separate bootstrap surface. This preserves the existing local workflow and avoids making a piped script depend on `$PSScriptRoot` or repository files.
2. Publish `UnityArtistCLI-host-windows-x64.zip` and its `.sha256` sidecar as assets of the immutable `v0.0.1-beta` GitHub Release. The bootstrap downloads only those release assets; it does not clone source, run `dotnet`, run Unity, or execute downloaded scripts.
3. Publish the Windows host self-contained. The one-line installation therefore does not require a preinstalled .NET SDK or runtime. The trade-off is a larger archive.
4. Install to `%LOCALAPPDATA%\UnityArtistCLI\Beta` by default, update only the current user's PATH, and verify the installed executable reports the requested product version before success is printed.
5. Default to the explicit `v0.0.1-beta` tag rather than a mutable `latest` selector. `UNITY_ARTIST_VERSION` and `UNITY_ARTIST_INSTALL_ROOT` provide explicit overrides for controlled environments.

## Verification boundary

PowerShell parsing, release/static contracts, Windows `win-x64` self-contained publish, and the resulting host's `version` response are verified locally. The real remote install remains dependent on the human-gated release workflow publishing the matching GitHub Release assets; no release was created automatically as part of this code change.
