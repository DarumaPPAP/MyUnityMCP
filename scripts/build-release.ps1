[CmdletBinding()]
param([string]$OutputRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) "dist\unity-artist-0.0.1-beta"))

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\UnityArtist.Cli\UnityArtist.Cli.csproj"

dotnet publish $project --configuration Release --self-contained false --output $OutputRoot
Write-Host "Release artifact prepared at $OutputRoot"
