$ErrorActionPreference = "Stop"
$RepositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $RepositoryRoot
python tools/graph-viewer/server.py
