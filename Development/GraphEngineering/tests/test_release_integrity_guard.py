import importlib.util
from pathlib import Path
import sys
import unittest

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
        self.assertFalse(result["tag_moved"])

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

    def test_failed_release_evidence_fails(self):
        with self.assertRaises(guard.ReleaseIntegrityError):
            guard.validate_snapshot(
                snapshot(release_evidence_status="not_passed"),
                published_tag_policy="immutable",
            )


if __name__ == "__main__":
    unittest.main()
