"""Static contract checks for the one-line remote Windows installer."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
INSTALLER = ROOT / "scripts" / "install-remote.ps1"
RELEASE_WORKFLOW = ROOT / ".github" / "workflows" / "release-tag.yml"


def main() -> None:
    installer = INSTALLER.read_text(encoding="utf-8")
    workflow = RELEASE_WORKFLOW.read_text(encoding="utf-8")

    required_installer_tokens = (
        '"DarumaPPAP/UnityArtistCLI"',
        '"v0.0.1-beta"',
        '"UnityArtistCLI-host-windows-x64.zip"',
        '"https://github.com/$repository/releases/download/$requestedVersion"',
        "Get-FileHash",
        "Expand-Archive",
        "ConvertFrom-Json",
        '"UnityArtistCLI\\Beta"',
        '"Path", "User"',
        "version --format json --non-interactive",
        "UNITY_ARTIST_VERSION",
        "UNITY_ARTIST_INSTALL_ROOT",
    )
    for token in required_installer_tokens:
        if token not in installer:
            raise AssertionError(f"remote installer is missing required token: {token}")

    forbidden_installer_tokens = (
        "Start-Process -Verb RunAs",
        "Invoke-Expression",
        "dotnet publish",
        "McpForUnityTool",
    )
    for token in forbidden_installer_tokens:
        if token in installer:
            raise AssertionError(f"remote installer must not contain: {token}")

    required_workflow_tokens = (
        "dotnet publish src/UnityArtist.Cli/UnityArtist.Cli.csproj --configuration Release --runtime win-x64 --self-contained true",
        "UnityArtistCLI-host-windows-x64.zip",
        "UnityArtistCLI-host-windows-x64.zip.sha256",
        "gh release create",
        "--verify-tag",
    )
    for token in required_workflow_tokens:
        if token not in workflow:
            raise AssertionError(f"release workflow is missing required token: {token}")

    print("remote installer contract: passed")


if __name__ == "__main__":
    main()
