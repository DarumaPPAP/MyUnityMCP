# Decision Log: Unity CLI / Pipeline gate

Date: 2026-09-10
Goal: `unity_artist_cli_cutover_v2`
Decision: keep Official Unity CLI + Unity Pipeline as the formal primary transport.

The installed Unity CLI was `1.0.0-beta.8` and could enumerate the installed
2022.3.22f1 Editor. The CLI reported six available official Pipeline versions:
`0.6.0-exp.1`, `0.5.0-exp.1`, `0.4.0-exp.1`, `0.3.1-exp.1`, `0.3.0-exp.1`, and
`0.2.0-exp.2`. Each version was tested with `unity pipeline install` against the
disposable 2022.3 Built-in fixture. Every candidate returned exit code 1 with
`COMMAND_FAILED`; the official Pipeline package reported that Unity 6.0 or later
is required. The follow-up connected command discovery was therefore blocked
because no compatible licensed Editor/Pipeline instance was available.

This is the concrete compatibility-gate failure required by the specification. It
does not authorize a silent backend switch. The `unity-cli-loop` reference was
evaluated and not selected because its documented `execute-dynamic-code` surface
does not satisfy UnityArtistCLI's no-arbitrary-eval Artist contract. The evidence is
fixed in `Tests/Compatibility/cli-pipeline-gate-evidence.yaml`.

The same official CLI was then verified against the disposable Unity `6000.6.0f1`
Built-in fixture with proxy settings disabled. `unity pipeline install` installed
`com.unity.pipeline` `0.6.0-exp.1`; the live server advertised all nine
UnityArtistCLI commands. The existing UnityAgent ToolBroker → Resolver → Dispatcher
→ `unity_artist_cli` Provider path was exercised for inspect, plan, and approval/
revision-gated apply. The result mapper was hardened to retain nested Editor
Evidence and redacted plan provenance. The complete fixture record is fixed in
`Tests/Compatibility/unity6-builtin-e2e-evidence.yaml`.

The four-row release matrix remains unchanged. This is an exhaustive probe of the
currently listed official versions, not a partial smoke result. A future bounded
non-MCP fallback requires an explicit contract, a real 2022.3 fixture, equivalent
approval/scope/evidence behavior, and a separate decision.

That separate decision was made on 2026-09-11 after the bounded fixture and its
equivalent lifecycle were implemented and verified; see
`2026-09-11-unity2022-3-bounded-fallback.md`.
