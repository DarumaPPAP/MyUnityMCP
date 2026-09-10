[CmdletBinding()]
param([string]$OutputRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) "dist\unity-artist-2.0.0"))

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\UnityArtist.Cli\UnityArtist.Cli.csproj"

dotnet publish $project --configuration Release --self-contained false --output $OutputRoot
Write-Host "Release artifact prepared at $OutputRoot"
