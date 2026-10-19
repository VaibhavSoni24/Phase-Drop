<#
.SYNOPSIS
    Deterministically acquires, verifies, and stages pinned FFmpeg and FFprobe binaries for Phase Drop.
.DESCRIPTION
    Pins explicit Gyan.dev FFmpeg Essentials distribution, verifies the archive SHA-256 checksum,
    extracts binaries, validates execution and required encoders/filters, stages them into the media
    engine layout, and records build metadata for licensing compliance.
#>
[CmdletBinding()]
param(
    [string]$DestinationDir = "",
    [string]$SourceArchive = "",
    [string]$SourceDir = "",
    [switch]$ForceDownload = $false,
    [switch]$AllowPathFallback = $false
)

$ErrorActionPreference = "Stop"

# Pinned distribution parameters (Gyan.dev FFmpeg 9.0.2 Essentials build)
$PINNED_VERSION       = "9.0.2"
$PINNED_PRIMARY_URL   = "https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-essentials_build.zip"
$PINNED_FALLBACK_URL  = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip"
$PINNED_ARCHIVE_SHA256 = "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba"
$PINNED_ROOT_DIR      = "ffmpeg-9.0.2-essentials_build"

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
if ([string]::IsNullOrWhiteSpace($DestinationDir)) {
    $DestinationDir = Join-Path $repoRoot "artifacts\publish\engines\media"
}
$DestinationDir = [System.IO.Path]::GetFullPath($DestinationDir)

Write-Host "=== Deterministic FFmpeg Acquisition & Validation Pipeline ===" -ForegroundColor Cyan
Write-Host "Pinned Version: $PINNED_VERSION"
Write-Host "Expected SHA256: $PINNED_ARCHIVE_SHA256"
Write-Host "Target Directory: $DestinationDir"

New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null

$ffmpegTarget = Join-Path $DestinationDir "ffmpeg.exe"
$ffprobeTarget = Join-Path $DestinationDir "ffprobe.exe"
$metadataTarget = Join-Path $DestinationDir "ffmpeg-metadata.json"

# Helper: Test and validate executable pair
function Test-FfmpegBinaries {
    param(
        [string]$FfmpegExe,
        [string]$FfprobeExe
    )

    if (-not (Test-Path $FfmpegExe) -or -not (Test-Path $FfprobeExe)) {
        return $null
    }

    Write-Host "Validating media engine execution..." -ForegroundColor Yellow
    $oldEap = $ErrorActionPreference
    try {
        $ErrorActionPreference = "SilentlyContinue"
        $ffmpegVerRaw = & $FfmpegExe -version 2>&1
        $ffprobeVerRaw = & $FfprobeExe -version 2>&1
        $encodersRaw = (& $FfmpegExe -hide_banner -encoders 2>&1 | Out-String)
        $filtersRaw = (& $FfmpegExe -hide_banner -filters 2>&1 | Out-String)
    }
    finally {
        $ErrorActionPreference = $oldEap
    }

    if ($LASTEXITCODE -ne 0 -or -not $ffmpegVerRaw) {
        Write-Error "ffmpeg.exe execution test failed with exit code $LASTEXITCODE"
        return $null
    }

    $ffmpegVerStr = ($ffmpegVerRaw | Out-String)
    $firstLine = ($ffmpegVerRaw | Select-Object -First 1).ToString().Trim()

    # Extract build configuration line
    $configMatch = [regex]::Match($ffmpegVerStr, "configuration:\s*(.*)")
    $buildConfig = if ($configMatch.Success) { $configMatch.Groups[1].Value.Trim() } else { "Unknown" }

    # Validate required encoders
    $requiredEncoders = @("libx264", "libvpx", "libmp3lame", "libopus", "libvorbis", "libwebp", "aac", "flac")
    foreach ($enc in $requiredEncoders) {
        if (-not ($encodersRaw -match "\b$enc\b")) {
            Write-Error "Required encoder '$enc' is missing from the selected FFmpeg distribution."
            return $null
        }
    }

    # Validate required filters
    $requiredFilters = @("scale", "palettegen", "paletteuse", "volume", "aresample", "setpts", "atempo")
    foreach ($flt in $requiredFilters) {
        if (-not ($filtersRaw -match "\b$flt\b")) {
            Write-Error "Required filter '$flt' is missing from the selected FFmpeg distribution."
            return $null
        }
    }

    Write-Host "Validated all required encoders and filters in $firstLine" -ForegroundColor Green

    return [PSCustomObject]@{
        VersionLine = $firstLine
        Configuration = $buildConfig
        FfmpegExe = $FfmpegExe
        FfprobeExe = $FfprobeExe
    }
}

# 1. Check if an explicit source directory was provided
$verifiedInfo = $null
if (-not [string]::IsNullOrWhiteSpace($SourceDir) -and (Test-Path $SourceDir)) {
    $candFfmpeg = Join-Path $SourceDir "ffmpeg.exe"
    $candFfprobe = Join-Path $SourceDir "ffprobe.exe"
    $candCheck = Test-FfmpegBinaries -FfmpegExe $candFfmpeg -FfprobeExe $candFfprobe
    if ($candCheck) {
        Write-Host "Using verified binaries from explicit SourceDir: $SourceDir" -ForegroundColor Green
        Copy-Item $candFfmpeg $ffmpegTarget -Force
        Copy-Item $candFfprobe $ffprobeTarget -Force
        $verifiedInfo = $candCheck
    }
}

# 2. Check if destination already has verified binaries and metadata
if (-not $verifiedInfo -and -not $ForceDownload -and (Test-Path $ffmpegTarget) -and (Test-Path $ffprobeTarget)) {
    $existingCheck = Test-FfmpegBinaries -FfmpegExe $ffmpegTarget -FfprobeExe $ffprobeTarget
    if ($existingCheck) {
        Write-Host "Existing binaries in destination are verified and match requirements." -ForegroundColor Green
        $verifiedInfo = $existingCheck
    }
}

# 3. Download and verify pinned archive if not yet verified
if (-not $verifiedInfo) {
    # Resolve local cache directory
    $cacheDir = Join-Path $repoRoot ".cache"
    if (-not (Test-Path $cacheDir)) {
        New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null
    }
    $cachedZip = Join-Path $cacheDir "ffmpeg-$PINNED_VERSION-essentials_build.zip"

    # Also check temp cache from earlier downloads
    $tempZipCandidate = Join-Path $env:TEMP "ffmpeg-test.zip"
    if (-not (Test-Path $cachedZip) -and (Test-Path $tempZipCandidate)) {
        Copy-Item $tempZipCandidate $cachedZip -Force
    }

    $archiveValid = $false
    if ((Test-Path $cachedZip) -and -not $ForceDownload) {
        Write-Host "Verifying cached archive: $cachedZip"
        $cachedHash = (Get-FileHash -Path $cachedZip -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($cachedHash -eq $PINNED_ARCHIVE_SHA256.ToLowerInvariant()) {
            Write-Host "Cached archive SHA-256 verified successfully." -ForegroundColor Green
            $archiveValid = $true
        } else {
            Write-Warning "Cached archive checksum mismatch ($cachedHash). Re-downloading."
            Remove-Item -Force $cachedZip
        }
    }

    if (-not $archiveValid) {
        $downloadTarget = "$cachedZip.download"
        if (Test-Path $downloadTarget) { Remove-Item -Force $downloadTarget }

        Write-Host "Downloading pinned FFmpeg archive from: $PINNED_PRIMARY_URL" -ForegroundColor Cyan
        try {
            Invoke-WebRequest -Uri $PINNED_PRIMARY_URL -OutFile $downloadTarget -UseBasicParsing
        }
        catch {
            Write-Warning "Primary download failed: $_. Attempting fallback mirror: $PINNED_FALLBACK_URL"
            Invoke-WebRequest -Uri $PINNED_FALLBACK_URL -OutFile $downloadTarget -UseBasicParsing
        }

        # Verify cryptographic hash
        Write-Host "Computing SHA-256 hash of downloaded archive..."
        $downloadedHash = (Get-FileHash -Path $downloadTarget -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($downloadedHash -ne $PINNED_ARCHIVE_SHA256.ToLowerInvariant()) {
            Remove-Item -Force $downloadTarget
            Write-Error @"
[FATAL] Cryptographic verification failed!
Expected SHA-256: $PINNED_ARCHIVE_SHA256
Computed SHA-256: $downloadedHash
The downloaded FFmpeg archive does not match the pinned release signature.
"@
            exit 1
        }

        Move-Item -Path $downloadTarget -Destination $cachedZip -Force
        Write-Host "Downloaded archive verified and cached: SHA-256 $downloadedHash" -ForegroundColor Green
    }

    # Extract into temporary directory
    $extractTempDir = Join-Path $env:TEMP "phasedrop-ffmpeg-extract-$PINNED_VERSION"
    if (Test-Path $extractTempDir) {
        Remove-Item -Recurse -Force $extractTempDir
    }
    New-Item -ItemType Directory -Path $extractTempDir -Force | Out-Null

    Write-Host "Extracting archive safely..." -ForegroundColor Yellow
    Expand-Archive -Path $cachedZip -DestinationPath $extractTempDir -Force

    $extractedFfmpeg = Join-Path $extractTempDir "$PINNED_ROOT_DIR\bin\ffmpeg.exe"
    $extractedFfprobe = Join-Path $extractTempDir "$PINNED_ROOT_DIR\bin\ffprobe.exe"

    if (-not (Test-Path $extractedFfmpeg) -or -not (Test-Path $extractedFfprobe)) {
        # Fallback search inside extraction
        $extractedFfmpeg = (Get-ChildItem -Path $extractTempDir -Filter "ffmpeg.exe" -Recurse | Select-Object -First 1).FullName
        $extractedFfprobe = (Get-ChildItem -Path $extractTempDir -Filter "ffprobe.exe" -Recurse | Select-Object -First 1).FullName
    }

    $verifiedInfo = Test-FfmpegBinaries -FfmpegExe $extractedFfmpeg -FfprobeExe $extractedFfprobe
    if (-not $verifiedInfo) {
        Write-Error "Extracted FFmpeg binaries failed validation checks."
        exit 1
    }

    Copy-Item $extractedFfmpeg $ffmpegTarget -Force
    Copy-Item $extractedFfprobe $ffprobeTarget -Force

    # Clean up extraction temp directory
    try { Remove-Item -Recurse -Force $extractTempDir } catch {}
}

# 4. Fail closed if binaries are still missing
if (-not (Test-Path $ffmpegTarget) -or -not (Test-Path $ffprobeTarget)) {
    Write-Error "Failed to acquire and stage verified FFmpeg/FFprobe binaries."
    exit 1
}

# 5. Compute staged binary hashes and record metadata
$ffmpegHash = (Get-FileHash -Path $ffmpegTarget -Algorithm SHA256).Hash
$ffprobeHash = (Get-FileHash -Path $ffprobeTarget -Algorithm SHA256).Hash
$timestamp = [DateTime]::UtcNow.ToString("o")

$metadata = [ordered]@{
    distribution = "Gyan.dev FFmpeg Essentials Build"
    version = $PINNED_VERSION
    upstreamSource = "https://github.com/GyanD/codexffmpeg/releases/tag/$PINNED_VERSION"
    downloadUrl = $PINNED_PRIMARY_URL
    archiveSha256 = $PINNED_ARCHIVE_SHA256
    ffmpegSha256 = $ffmpegHash
    ffprobeSha256 = $ffprobeHash
    buildConfiguration = $verifiedInfo.Configuration
    license = "GPL-3.0-or-later"
    verificationTimestampUtc = $timestamp
}

$metadataJson = $metadata | ConvertTo-Json -Depth 4
$metadataJson | Out-File -FilePath $metadataTarget -Encoding utf8

Write-Host "`n=== FFmpeg Verification & Staging Complete ===" -ForegroundColor Green
Write-Host "ffmpeg.exe:  $ffmpegTarget"
Write-Host "  SHA-256:   $ffmpegHash"
Write-Host "ffprobe.exe: $ffprobeTarget"
Write-Host "  SHA-256:   $ffprobeHash"
Write-Host "Metadata:    $metadataTarget"
Write-Host "License:     $($metadata.license)"
