<#
.SYNOPSIS
    Validates the Phase Drop release package, installer integrity, payload, and bundled engines.
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$distDir = Join-Path $repoRoot "dist"
$installerPath = Join-Path $distDir "PhaseDrop-Setup-v$Version-x64.exe"
$hashFile = "$installerPath.sha256"

Write-Host "=== Validating Phase Drop Release Package (v$Version) ===" -ForegroundColor Cyan

# 1. Check Installer File Existence
if (-not (Test-Path $installerPath)) {
    Write-Error "Validation FAILED: Installer file does not exist at $installerPath"
    exit 1
}

$fileInfo = Get-Item $installerPath
$sizeMb = [Math]::Round(($fileInfo.Length / 1MB), 2)
Write-Host "Installer Found: $installerPath ($sizeMb MB)" -ForegroundColor Green

# 2. Verify Valid Windows PE Binary Header (MZ at byte 0, PE signature)
$fileStream = [System.IO.File]::OpenRead($installerPath)
$header = New-Object byte[] 2
$bytesRead = $fileStream.Read($header, 0, 2)
$fileStream.Close()

if ($bytesRead -lt 2 -or $header[0] -ne 0x4D -or $header[1] -ne 0x5A) {
    Write-Error "Validation FAILED: Installer file is not a valid Windows executable (missing 'MZ' header)."
    exit 1
}
Write-Host "PE Binary Signature Verified ('MZ' magic bytes present)." -ForegroundColor Green

# 3. Check Plausible Installer Size (> 50 MB required for self-contained runtime + engines)
if ($fileInfo.Length -lt 50000000) {
    Write-Error "Validation FAILED: Installer size ($sizeMb MB) is too small. Expected bundled .NET 10 runtime and media engines."
    exit 1
}

# 4. Check SHA-256 File and Hash Verification
if (-not (Test-Path $hashFile)) {
    Write-Error "Validation FAILED: Checksum file does not exist at $hashFile"
    exit 1
}

$expectedHash = (Get-Content $hashFile).Split(" ", [StringSplitOptions]::RemoveEmptyEntries)[0].Trim()
$actualHash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash

if ($expectedHash -ne $actualHash) {
    Write-Error "Validation FAILED: Checksum mismatch!`nExpected: $expectedHash`nActual:   $actualHash"
    exit 1
}
Write-Host "SHA-256 Checksum Verified: $actualHash" -ForegroundColor Green

# 5. Check Published Payload Files & Engines
$publishDir = Join-Path $repoRoot "artifacts\publish"
$requiredFiles = @(
    "PhaseDrop.exe",
    "PhaseDrop.dll",
    "PhaseDrop.Core.dll",
    "PhaseDrop.Platform.Windows.dll",
    "PhaseDrop.Engines.Images.dll",
    "PhaseDrop.Engines.Media.dll",
    "PhaseDrop.Engines.Pdf.dll",
    "PhaseDrop.Engines.Archives.dll",
    "PhaseDrop.Engines.Documents.dll",
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
    "Assets\icon.ico",
    "engines\media\ffmpeg.exe",
    "engines\media\ffprobe.exe"
)

foreach ($req in $requiredFiles) {
    $fullPath = Join-Path $publishDir $req
    if (-not (Test-Path $fullPath)) {
        Write-Error "Validation FAILED: Required payload file missing from published layout: $req"
        exit 1
    }
}
Write-Host "All $( $requiredFiles.Count ) required runtime payload files and bundled engines verified." -ForegroundColor Green

# 6. Verify Execution of Bundled Engines in Payload
$bundledFfmpeg = Join-Path $publishDir "engines\media\ffmpeg.exe"
$bundledFfprobe = Join-Path $publishDir "engines\media\ffprobe.exe"

$ffmpegOutput = & $bundledFfmpeg -version
if ($LASTEXITCODE -ne 0 -or -not $ffmpegOutput) {
    Write-Error "Validation FAILED: Bundled ffmpeg.exe execution check failed (exit code: $LASTEXITCODE)."
    exit 1
}
$ffmpegVer = $ffmpegOutput[0]
Write-Host "Bundled FFmpeg Verified: $ffmpegVer" -ForegroundColor Green

$ffprobeOutput = & $bundledFfprobe -version
if ($LASTEXITCODE -ne 0 -or -not $ffprobeOutput) {
    Write-Error "Validation FAILED: Bundled ffprobe.exe execution check failed (exit code: $LASTEXITCODE)."
    exit 1
}
$ffprobeVer = $ffprobeOutput[0]
Write-Host "Bundled FFprobe Verified: $ffprobeVer" -ForegroundColor Green

Write-Host "`nRelease Package Validation PASSED successfully!" -ForegroundColor Green
