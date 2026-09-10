---
name: cinematic-evidence
description: Inspect and safely plan Unity Timeline, Cinemachine Shot, bindings, markers, signals, activation, control and animation evidence.
---

# Cinematic and Timeline evidence

Use `domain.workflow` with `domain: cinematic` and workflow `timeline` or `cinematic`. Inspect PlayableDirector, Timeline assets, tracks, clips, bindings, Cinemachine shots, Activation, Signal/Marker, Control and Animation objects before planning.

Every mutation must include an exact target, exact diff, current revision, UnityAgent approval, and an Undo path. Keep unrelated tracks and bindings unchanged. Emit timeline evidence and capture references; never claim cinematic quality without human review. Saving remains a separate approved operation.
