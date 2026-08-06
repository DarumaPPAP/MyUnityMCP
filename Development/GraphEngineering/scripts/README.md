# Reference Harness

This is a minimal repository-state Harness.

Commands:

```bash
python scripts/roadmap_harness.py validate
python scripts/roadmap_harness.py status
python scripts/roadmap_harness.py next
python scripts/roadmap_harness.py start <node> --revision <sha> --iteration-budget <n>
python scripts/roadmap_harness.py record-evidence <node> <key> <artifact>
python scripts/roadmap_harness.py complete <node>
python scripts/roadmap_harness.py block <node> --reason <reason>
python scripts/roadmap_harness.py completion-check
python scripts/roadmap_harness.py viewer
```

It does not:

- Invoke Codex
- Run Unity
- Merge
- Tag
- Publish
- Install packages
- Handle secrets

Codex must integrate equivalent checks into the Repository’s real CI and Unity tooling.

## Graph Dashboard

```bash
python scripts/roadmap_harness.py viewer
```

This delegates to the read-only server in `tools/graph-viewer/`.
