# UnityArtistCLI Compatibility Evidence

`support-matrix.yaml` is the authoritative four-row release matrix. `production-editor-acceptance.yaml` records Direct Editor evidence; `production-validation-evidence.yaml` records host/static and E2E evidence; `release-verification.yaml` records the release audit; `cli-pipeline-gate-evidence.yaml` records the concrete 2022.3 first-candidate gate result.

The official Unity CLI + Unity Pipeline is the first candidate for Unity 2022.3 Built-in as well as Unity 6 Built-in/URP/HDRP. The current host has Unity 2022.3.22f1, Unity 6000.6.0f1, and Unity CLI 1.0.0-beta.8 installed. The 2022.3 Pipeline install command returned the concrete failure that the installed Pipeline package requires Unity 6.0 or later. The Unity 6 Built-in disposable fixture now has direct Editor/Pipeline and UnityAgent Provider E2E evidence in `unity6-builtin-e2e-evidence.yaml`; the Unity 6 URP/HDRP rows and the 2022.3 row remain blocked or failed at their separate gates.
