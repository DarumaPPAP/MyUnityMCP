---
name: lookdev-refine
description: Run UnityArtistCLI visual intent, LookDev, lighting, environment, camera, capture, evaluate and refine workflows.
---

# LookDev and visual refinement

Keep semantic intent in UnityAgent. Use `domain.workflow` with qualifiers such as `domain: visual_art` and `workflow: lookdev_refine`, `lighting`, `environment`, or `camera`.

Follow this order:

1. Inspect the exact project, scene, pipeline, cameras, lights, environment and revision.
2. Create a read-only plan with structured visual intent and an exact diff.
3. Request UnityAgent approval; do not manufacture or echo approval tokens.
4. Apply only with the expected revision and approved scope; preserve Undo and do not auto-save.
5. Capture evidence, record a human review decision, and create a linked refinement plan only when needed.

Do not expose generic GameObject CRUD, arbitrary evaluation, silent fallback, or automatic visual acceptance. A capture is evidence, not a claim that the look is accepted.
