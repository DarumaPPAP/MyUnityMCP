# Decision Log: Unity CLI / Pipeline gate

Date: 2026-09-10
Goal: `unity_artist_cli_cutover_v2`
Decision: keep Official Unity CLI + Unity Pipeline as the formal primary transport.

The installed Unity CLI was `1.0.0-beta.8` and could enumerate the installed
2022.3.22f1 Editor. Running `unity pipeline install` against the disposable
2022.3 Built-in fixture returned exit code 1 with `COMMAND_FAILED`; the official
Pipeline package reported that Unity 6.0 or later is required. The follow-up
connected command discovery was therefore blocked because no licensed connected
Editor/Pipeline instance was available.

This is the concrete compatibility-gate failure required by the specification. It
does not authorize a silent backend switch. The `unity-cli-loop` reference was
evaluated and not selected because its documented `execute-dynamic-code` surface
does not satisfy UnityArtistCLI's no-arbitrary-eval Artist contract. The evidence is
fixed in `Tests/Compatibility/cli-pipeline-gate-evidence.yaml`.

The four-row release matrix remains unchanged. A future bounded non-MCP fallback
requires an explicit contract, a real 2022.3 fixture, equivalent approval/scope/
evidence behavior, and a separate decision.
