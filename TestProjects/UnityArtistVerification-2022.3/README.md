# Unity 2022.3 Official Pipeline Gate Fixture

This minimal Built-in project is the disposable fixture used by the Unity CLI/Pipeline compatibility gate. It intentionally contains no UnityArtistCLI scene authoring because every currently listed Official Unity Pipeline version fails before Editor connection on Unity `2022.3.22f1` with the explicit requirement for Unity 6.0 or later.

The observed results are recorded in `Tests/Compatibility/cli-pipeline-gate-evidence.yaml`. Do not replace this gate with a fallback backend without a separately approved contract and evidence plan.
