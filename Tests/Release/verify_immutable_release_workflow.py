#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
workflow = (ROOT / ".github/workflows/release-tag.yml").read_text(encoding="utf-8")
verification = (ROOT / "Tests/Compatibility/release-verification.yaml").read_text(encoding="utf-8")

required_workflow_fragments = [
    'source_commit=$(git rev-parse HEAD)',
    'source_commit=$(git rev-parse "$tag^{commit}")',
    'git checkout --detach "$source_commit"',
    'python3 Tests/Release/verify_release_contract.py --mode tag --tag "$tag"',
    'release_mode="verify_existing"',
]
for fragment in required_workflow_fragments:
    if fragment not in workflow:
        raise SystemExit(f"Missing immutable release workflow fragment: {fragment}")

trigger_section = workflow.split("permissions:", 1)[0]
if "workflow_dispatch:" not in trigger_section:
    raise SystemExit("Explicit workflow_dispatch release trigger is required.")
if "issue_comment:" not in trigger_section:
    raise SystemExit("Explicit approved issue_comment release trigger is required.")
if "\n  push:" in trigger_section:
    raise SystemExit("Release publication must not be triggered implicitly by a VERSION push.")
if "github.event_name == 'push'" in workflow:
    raise SystemExit("Release job must not accept push events as publication approval.")

forbidden_fragments = [
    "git tag -f",
    "git push --force",
    "git push -f",
    "git tag -d",
    'git push origin ":refs/tags/',
]
for fragment in forbidden_fragments:
    if fragment in workflow:
        raise SystemExit(f"Forbidden published-tag mutation found: {fragment}")

existing_index = workflow.index('if git rev-parse "$tag"')
existing_source_index = workflow.index('source_commit=$(git rev-parse "$tag^{commit}")')
else_index = workflow.index("\n          else\n", existing_index)
create_index = workflow.index('git tag -a "$tag"', else_index)
checkout_index = workflow.index('git checkout --detach "$source_commit"')
build_index = workflow.index("- name: Build release distribution from immutable source")

if not (existing_index < existing_source_index < else_index < create_index < checkout_index < build_index):
    raise SystemExit("Immutable source resolution order is invalid.")

required_policy_fragments = [
    "published_tag_policy: immutable",
    "existing_tag_rerun_source: tagged_commit",
    "published_tag_move_allowed: false",
    "future_product_change_requires_new_version: true",
]
for fragment in required_policy_fragments:
    if fragment not in verification:
        raise SystemExit(f"Missing release evidence policy: {fragment}")

print("Immutable release workflow and explicit publication trigger policy PASS")
