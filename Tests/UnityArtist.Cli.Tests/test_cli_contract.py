from __future__ import annotations

import json
import subprocess
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "src" / "UnityArtist.Cli" / "UnityArtist.Cli.csproj"
FIXTURES = Path(__file__).resolve().parent / "fixtures"


def run_cli(*arguments: str) -> tuple[int, dict]:
    completed = subprocess.run(
        ["dotnet", "run", "--project", str(PROJECT), "--no-restore", "--", *arguments],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    try:
        payload = json.loads(completed.stdout)
    except json.JSONDecodeError as exc:
        raise AssertionError(
            f"CLI did not return JSON (exit={completed.returncode}): {completed.stdout!r} {completed.stderr!r}"
        ) from exc
    return completed.returncode, payload


class UnityArtistCliContractTests(unittest.TestCase):
    def test_version_is_machine_readable(self):
        exit_code, payload = run_cli("version", "--format", "json", "--non-interactive")
        self.assertEqual(exit_code, 0)
        self.assertEqual(payload["product"], "UnityArtistCLI")
        self.assertEqual(payload["status"], "passed")
        self.assertEqual(payload["data"]["version"], "2.0.0")

    def test_help_exposes_artist_surface_and_not_generic_crud(self):
        exit_code, payload = run_cli("help", "--format", "json", "--non-interactive")
        self.assertEqual(exit_code, 0)
        commands = set(payload["data"]["commands"])
        self.assertTrue({"inspect", "plan", "preview", "apply", "capture", "evaluate", "refine", "cinematic", "history"}.issubset(commands))
        self.assertNotIn("create-gameobject", commands)
        self.assertNotIn("get-hierarchy", commands)

    def test_help_flag_is_accepted_by_the_standalone_executable(self):
        exit_code, payload = run_cli("--help", "--format", "json", "--non-interactive")
        self.assertEqual(exit_code, 0)
        self.assertEqual(payload["command"], "help")
        self.assertEqual(payload["status"], "passed")

    def test_release_matrix_is_exactly_four_rows(self):
        exit_code, payload = run_cli("capabilities", "--format", "json", "--non-interactive")
        self.assertEqual(exit_code, 0)
        matrix = payload["data"]["releaseMatrix"]
        self.assertEqual(len(matrix), 4)
        self.assertEqual(
            {(row["unityVersion"], row["renderPipeline"]) for row in matrix},
            {
                ("2022.3 LTS", "builtin"),
                ("Unity 6.x+", "builtin"),
                ("Unity 6.x+", "urp"),
                ("Unity 6.x+", "hdrp"),
            },
        )

    def test_2022_3_urp_is_rejected_before_mutation(self):
        project = FIXTURES / "2022.3-urp"
        exit_code, payload = run_cli(
            "install", "--project-path", str(project), "--format", "json", "--non-interactive"
        )
        self.assertEqual(exit_code, 3)
        self.assertEqual(payload["status"], "blocked")
        self.assertEqual(payload["errors"][0]["code"], "UNSUPPORTED_RENDER_PIPELINE_VERSION")

    def test_2022_3_builtin_uses_official_cli_pipeline_first(self):
        project = FIXTURES / "2022.3-builtin"
        exit_code, payload = run_cli(
            "doctor", "--project-path", str(project), "--format", "json", "--non-interactive"
        )
        self.assertIn(exit_code, {0, 3})
        self.assertEqual(payload["data"]["support"]["transport"], "official_unity_cli_pipeline")
        self.assertEqual(payload["data"]["support"]["compatibilityBackend"], "builtin_editor_api")

    def test_apply_requires_plan_approval_and_revision_before_transport(self):
        project = FIXTURES / "2022.3-builtin"
        exit_code, payload = run_cli(
            "apply", "--project-path", str(project), "--format", "json", "--non-interactive"
        )
        self.assertEqual(exit_code, 3)
        self.assertEqual(payload["errors"][0]["code"], "PLAN_ID_REQUIRED")

    def test_cinematic_apply_uses_the_same_approval_gate(self):
        project = FIXTURES / "2022.3-builtin"
        exit_code, payload = run_cli(
            "cinematic", "--operation", "apply", "--project-path", str(project),
            "--format", "json", "--non-interactive"
        )
        self.assertEqual(exit_code, 3)
        self.assertEqual(payload["errors"][0]["code"], "PLAN_ID_REQUIRED")

    def test_invalid_format_is_a_typed_usage_failure(self):
        exit_code, payload = run_cli("version", "--format", "xml", "--non-interactive")
        self.assertEqual(exit_code, 3)
        self.assertEqual(payload["errors"][0]["code"], "INVALID_FORMAT")


if __name__ == "__main__":
    unittest.main()
