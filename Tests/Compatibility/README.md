# UnityArtistCLI Compatibility Evidence

`support-matrix.yaml` is the authoritative four-row release matrix. `production-editor-acceptance.yaml` records Direct Editor evidence; `production-validation-evidence.yaml` records host/static and E2E evidence; `release-verification.yaml` records the release audit; `cli-pipeline-gate-evidence.yaml` records the concrete 2022.3 first-candidate gate result.

The official Unity CLI + Unity Pipeline is the first candidate for Unity 2022.3 Built-in as well as Unity 6 Built-in/URP/HDRP. The current host has Unity 2022.3.22f1 and Unity CLI 1.0.0-beta.8 installed. The 2022.3 Pipeline install command returned the concrete failure that the installed Pipeline package requires Unity 6.0 or later, and no connected licensed Editor/Pipeline instance was available for the remaining checks. Direct Editor and visual E2E are therefore explicitly `blocked_by_environment` rather than promoted to PASS.
