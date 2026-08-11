import pathlib
import subprocess
import sys
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[3]


class CandidateCompatibilityTests(unittest.TestCase):
    def test_candidate_sources_have_no_known_legacy_blockers(self):
        result = subprocess.run(
            [sys.executable, "Development/GraphEngineering/scripts/verify_candidate_compatibility.py"],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
