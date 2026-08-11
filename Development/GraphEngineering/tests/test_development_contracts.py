import pathlib
import subprocess
import sys
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[3]


class DevelopmentContractTests(unittest.TestCase):
    def test_all_candidate_contracts_and_security_policy_exist(self):
        result = subprocess.run(
            [sys.executable, "Development/GraphEngineering/scripts/verify_development_contracts.py"],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
