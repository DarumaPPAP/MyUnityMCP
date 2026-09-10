# Decision: keep a minimal live smoke path

Date: 2026-09-10

## Decision

Maintain a disposable Unity 6 Built-in fixture and a short live smoke runner as
the first verification path for UnityArtistCLI. The test uses one default scene,
one Cube, and Main Camera, and drives all Editor authoring through Official
Unity CLI/Pipeline commands.

## Rationale

The release matrix still requires separate URP/HDRP and Unity 2022.3 evidence,
but a full cinematic fixture is too slow and too stateful for diagnosing the
core migration contract. The minimal path isolates transport, support
detection, plan/preview, approval and revision safety, evidence, capture, human
evaluation, refinement, and history.

## Boundary

This smoke test is not release-matrix evidence and does not replace the
UnityAgent Provider E2E or visual quality review. It is a fast regression gate
that should be run before any longer matrix test.
