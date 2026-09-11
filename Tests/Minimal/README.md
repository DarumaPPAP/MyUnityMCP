# Minimal live smoke test

`scripts/run_minimal_live_smoke.py` is the shortest end-to-end check for the
UnityArtistCLI migration. It targets the disposable Unity 6 Built-in fixture and
uses only the Official Unity CLI/Pipeline for Editor interaction.

The test creates `Assets/MinimalSmoke.unity` with Unity's default Main Camera,
Directional Light, and one Cube, then verifies:

1. Artist support is detected through the Official Unity CLI/Pipeline transport.
2. `artist.plan` and `artist.preview` are read-only and expose an exact diff.
3. `artist.apply` blocks without an approval token and succeeds with one.
4. Apply evidence contains mutation, Undo, and explicit no-save facts.
5. `artist.capture` produces a non-empty, parseable PNG.
6. `artist.evaluate` and `artist.refine` complete the visual loop.
7. `artist.history` returns the session evidence.

The Editor must already be connected. Prepare it once from the repository root:

```powershell
unity pipeline install --project-path .\TestProjects\UnityArtistVerification --proxy-disable
unity open .\TestProjects\UnityArtistVerification --editor-version 6000.6.0f1 --non-interactive --no-banner --proxy-disable
```

Then run:

```powershell
python scripts/run_minimal_live_smoke.py
```

This is a smoke test, not release-matrix evidence. The four-row matrix and the
Unity 2022.3 first-candidate gate remain covered by their dedicated contracts.
