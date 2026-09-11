# UnityArtistCLI remote bootstrap installer.
#
# Intended usage from PowerShell:
#   irm https://raw.githubusercontent.com/DarumaPPAP/UnityArtistCLI/main/scripts/install-remote.ps1 | iex
#
# Configuration is supplied through environment variables so the script remains usable
# when it is piped into PowerShell. The release workflow publishes the archive and
# its SHA-256 sidecar; this script never builds source code or executes downloaded scripts.

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -lt 5) {
    throw "PowerShell 5.1 or newer is required."
}

if ($PSVersionTable.PSVersion.Major -eq 5 -and $PSVersionTable.PSVersion.Minor -eq 1) {
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    }
    catch {
        throw "Unable to enable TLS 1.2 for the download."
    }
}

$repository = "DarumaPPAP/UnityArtistCLI"
$requestedVersion = if ($env:UNITY_ARTIST_VERSION) { $env:UNITY_ARTIST_VERSION.Trim() } else { "v0.0.1-beta" }
if ($requestedVersion -notmatch '^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "UNITY_ARTIST_VERSION must be a release tag such as v0.0.1-beta."
}

$productVersion = $requestedVersion.Substring(1)
$assetName = "UnityArtistCLI-host-windows-x64.zip"
$assetBaseUrl = "https://github.com/$repository/releases/download/$requestedVersion"
$archiveUrl = "$assetBaseUrl/$assetName"
$checksumUrl = "$assetBaseUrl/$assetName.sha256"

if (-not $env:LOCALAPPDATA) {
    throw "LOCALAPPDATA is not set; run this installer in a Windows user session."
}

$installRoot = if ($env:UNITY_ARTIST_INSTALL_ROOT) {
    $env:UNITY_ARTIST_INSTALL_ROOT.Trim().Trim('"')
}
else {
    Join-Path $env:LOCALAPPDATA "UnityArtistCLI\Beta"
}

if ([string]::IsNullOrWhiteSpace($installRoot)) {
    throw "UNITY_ARTIST_INSTALL_ROOT cannot be empty."
}

if (-not [IO.Path]::IsPathRooted($installRoot)) {
    $installRoot = Join-Path (Get-Location).Path $installRoot
}
$installRoot = [IO.Path]::GetFullPath($installRoot).TrimEnd('\')
$installRootParent = [IO.Path]::GetPathRoot($installRoot).TrimEnd('\')
if ([string]::Equals($installRoot, $installRootParent, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to install into a filesystem root: $installRoot"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("unity-artist-install-" + [Guid]::NewGuid().ToString("N"))
$archivePath = Join-Path $tempRoot $assetName
$checksumPath = "$archivePath.sha256"
$payloadRoot = Join-Path $tempRoot "payload"

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null

    Write-Host "Downloading UnityArtistCLI $productVersion..."
    Invoke-WebRequest -UseBasicParsing -Uri $archiveUrl -OutFile $archivePath
    Invoke-WebRequest -UseBasicParsing -Uri $checksumUrl -OutFile $checksumPath

    $expectedHash = ((Get-Content -LiteralPath $checksumPath -Raw) -split '\s+')[0].Trim().ToLowerInvariant()
    if ($expectedHash -notmatch '^[0-9a-f]{64}$') {
        throw "The downloaded checksum file is invalid."
    }

    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "SHA-256 verification failed for $assetName."
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $payloadRoot -Force
    $candidateExecutables = @(Get-ChildItem -LiteralPath $payloadRoot -Filter "unity-artist.exe" -File -Recurse)
    if ($candidateExecutables.Count -ne 1) {
        throw "The release archive must contain exactly one unity-artist.exe."
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    foreach ($payloadItem in @(Get-ChildItem -LiteralPath $payloadRoot -Force)) {
        Copy-Item -LiteralPath $payloadItem.FullName -Destination (Join-Path $installRoot $payloadItem.Name) -Recurse -Force
    }

    $installedExecutable = Join-Path $installRoot "unity-artist.exe"
    if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) {
        throw "The installed archive did not produce $installedExecutable."
    }

    $env:Path = "$installRoot;$env:Path"
    $versionOutput = (& $installedExecutable version --format json --non-interactive | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Installed unity-artist version verification failed with exit code $LASTEXITCODE."
    }

    try {
        $versionPayload = $versionOutput | ConvertFrom-Json
    }
    catch {
        throw "Installed unity-artist returned invalid JSON during version verification."
    }

    $reportedVersion = [string]$versionPayload.data.version
    if ($reportedVersion -ne $productVersion) {
        throw "Installed version mismatch: expected $productVersion, received $reportedVersion."
    }

    $currentUserPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $pathParts = @()
    if ($currentUserPath) {
        $pathParts = @($currentUserPath -split ';' | Where-Object { $_ -and $_.Trim() })
    }
    $alreadyPresent = @($pathParts | Where-Object { [string]::Equals($_.TrimEnd('\'), $installRoot, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
    if (-not $alreadyPresent) {
        [Environment]::SetEnvironmentVariable("Path", (($pathParts + $installRoot) -join ';'), "User")
    }

    Write-Host "Installed unity-artist $reportedVersion to $installRoot"
    Write-Host "SHA-256 verified: $actualHash"
    Write-Host "Open a new PowerShell window, then run: unity-artist version --format json --non-interactive"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
