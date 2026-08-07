import importlib.util
from pathlib import Path
import sys
import unittest
from unittest import mock

MODULE_PATH = Path(__file__).resolve().parents[1] / "scripts" / "release_integrity_guard.py"
SPEC = importlib.util.spec_from_file_location("release_integrity_guard", MODULE_PATH)
assert SPEC and SPEC.loader
guard = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = guard
SPEC.loader.exec_module(guard)


def snapshot(**overrides):
    values = {
        "version": "1.0.0",
        "package_version": "1.0.0",
        "manifest_version": "1.0.0",
        "support_version": "1.0.0",
        "changelog_has_version": True,
        "workflow_has_identity": True,
        "workflow_preserves_published_tag": True,
        "tag_exists": True,
        "tag_commit": "tag-sha",
        "main_commit": "tag-sha",
        "release_evidence_status": "passed",
    }
    values.update(overrides)
    return guard.ReleaseSnapshot(**values)


class ReleaseIntegrityGuardTests(unittest.TestCase):
    def test_aligned_release_passes(self):
        result = guard.validate_snapshot(snapshot(), published_tag_policy="immutable")
        self.assertTrue(result["passed"])
        self.assertEqual(result["release_state"], "aligned")
        self.assertFalse(result["tag_moved"])

    def test_immutable_published_tag_may_trail_main(self):
        result = guard.validate_snapshot(
            snapshot(main_commit="new-main-sha"),
            published_tag_policy="immutable",
        )
        self.assertEqual(result["release_state"], "published_tag_immutable_main_ahead")
        self.assertEqual(
            result["next_release_action"],
            "create_patch_release_for_future_product_changes",
        )
        self.assertEqual(result["rerun_source"], "tag_commit")
        self.assertFalse(result["tag_moved"])

    def test_ci_checkout_resolves_origin_main_when_local_main_is_absent(self):
        with mock.patch.object(
            guard,
            "_git_optional",
            side_effect=["", "origin-main-sha"],
        ) as resolver:
            result = guard._resolve_main_commit(Path("."))

        self.assertEqual(result, "origin-main-sha")
        self.assertEqual(resolver.call_count, 2)
        self.assertEqual(resolver.call_args_list[0].args[2], "main^{commit}")
        self.assertEqual(resolver.call_args_list[1].args[2], "origin/main^{commit}")

    def test_main_resolution_fails_when_no_allowed_main_ref_exists(self):
        with mock.patch.object(guard, "_git_optional", return_value=""):
            with self.assertRaises(guard.ReleaseIntegrityError):
                guard._resolve_main_commit(Path("."))

    def test_missing_tag_fails(self):
        with self.assertRaises(guard.ReleaseIntegrityError):
            guard.validate_snapshot(
                snapshot(tag_exists=False, tag_commit=""),
                published_tag_policy="immutable",
            )

    def test_version_mismatch_fails(self):
        with self.assertRaises(guard.ReleaseIntegrityError):
            guard.validate_snapshot(
                snapshot(package_version="1.0.1"),
                published_tag_policy="immutable",
            )

    def test_identity_config_missing_fails(self):
        with self.assertRaises(guard.ReleaseIntegrityError):
            guard.validate_snapshot(
                snapshot(workflow_has_identity=False),
                published_tag_policy="immutable",
            )

    def test_mutable_rerun_workflow_fails(self):
        with self.assertRaises(guard.ReleaseIntegrityError):
            guard.validate_snapshot(
                snapshot(workflow_preserves_published_tag=False),
                published_tag_policy="immutable",
            )

    def test_failed_release_evidence_fails(self):
        with self.assertRaises(guard.ReleaseIntegrityError):
            guard.validate_snapshot(
                snapshot(release_evidence_status="not_passed"),
                published_tag_policy="immutable",
            )


if __name__ == "__main__":
    unittest.main()
