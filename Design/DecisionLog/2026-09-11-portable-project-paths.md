# Decision: resolve project paths at the process boundary

Date: 2026-09-11

## Reference review

The attached migration pack remains the implementation authority. The following public repositories were reviewed as implementation references only:

- [`hatayama/unity-cli-loop`](https://github.com/hatayama/unity-cli-loop): adopt the project-root workflow, PATH-resolved global dispatcher, and per-project runner/version selection as useful portability patterns. Do not copy its dynamic-code or broad generic Unity operation surface into UnityArtistCLI.
- [`CoplayDev/unity-mcp`](https://github.com/CoplayDev/unity-mcp): use its package installation and client setup flow as UX inspiration. Do not reintroduce its MCP bridge or generic MCP tool surface; the migration target remains CLI-first and the legacy MCP surface stays outside production.

The official Unity CLI/Pipeline remains the required primary transport. The reference implementations must not silently replace that transport or weaken the explicit project-targeting and evidence requirements.

## Context

The CLI contract correctly requires an explicit `--project-path` whenever an
Editor may be ambiguous, but the external verification instructions had begun
showing a developer-specific absolute Windows path. That is unsuitable for a
checkout on another machine, for CI, and for a real project outside the
fixture repository.

## Decision

Keep explicit project targeting as a safety requirement, while standardizing
the caller-facing forms as follows:

- `--project-path .` when the shell is inside the selected Unity project.
- A repository-relative path for checked-in fixtures.
- An optional `UNITY_ARTIST_PROJECT_PATH` value for automation.
- Resolve the path only inside the verification process and pass the stable
  `.` argument after entering the project directory.
- Resolve `unity-artist` from PATH first, then from repository-local Release or
  distribution artifacts; never commit a developer home path as evidence.

The `scripts/verify-external-cli.ps1` helper implements this policy for the
read-only external checks. The CLI itself continues to require an explicit
project argument; no implicit Editor selection or hidden global default is
introduced.

## Consequences

Commands are portable across Windows accounts and checkout locations, while
multiple running Editors remain safely disambiguated. Evidence records logical
fixture paths and executable names rather than local installation paths.
