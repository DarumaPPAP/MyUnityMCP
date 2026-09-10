# Migration from MyUnityMCP v1.1.1

This branch introduces UnityArtistCLI 2.0.0 as the current product surface. The published MyUnityMCP v1.1.1 tag remains immutable and its files are retained under `Legacy/MyUnityMCP-1.1.1/` for migration reference.

| Former responsibility | Current owner |
|---|---|
| Generic project/editor/test/build operations | Official Unity CLI Provider |
| Goal, policy, approval, routing, loop, fallback, evidence | UnityAgent existing Runtime chain |
| LookDev, mood, lighting, environment, camera | UnityArtistCLI |
| Timeline, Cinemachine Shot, bindings, markers, signals | UnityArtistCLI cinematic adapter |
| MCP bridge transport and 77-tool surface | Legacy only; production-disabled |

The new UnityArtistCLI package has no dependency on `com.coplaydev.unity-mcp`. Its commands are discovered through Unity Pipeline `[CliCommand]` methods and are invoked by the `unity-artist` host through the official `unity command` path.

Migration is intentionally additive at the Provider boundary: UnityAgent gets `unity_artist_cli` beside the existing descriptors, semantic qualifiers gate compatibility, and the old `myunitymcp` descriptor is marked `legacy: true` and `production_enabled: false`. No new Player Manager or second Registry is introduced.

Existing projects should:

1. Install the official Unity CLI and verify `unity --version`.
2. Attempt `com.unity.pipeline` through `unity pipeline install --project-path <project>` as the first Editor transport. On Unity 2022.3 Built-in, if the concrete Unity 6-or-later compatibility gate is recorded, use the bounded `unity run` entrypoint documented in `Tests/Compatibility/unity2022-3-builtin-bounded-fallback-evidence.yaml`; do not silently choose another backend.
3. Add `com.darumappap.unity-artist` as the embedded/local package or Git package.
4. Run `unity-artist doctor --project-path <project> --format json --non-interactive`.
5. Replace old generic MCP calls with UnityAgent capabilities; use semantic `domain.workflow` qualifiers for Artist work.

Do not treat `Legacy/` assets as current production API. Do not edit the v1.1.1 release tag.
