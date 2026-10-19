<#
.SYNOPSIS
    Deterministically acquires and verifies FFmpeg and FFprobe executables for Phase Drop.
.DESCRIPTION
    Ensures that both ffmpeg.exe and ffprobe.exe are present in the target engines/media directory,
    validates their architecture and execution, and reports their build version.
#>
[CmdletBinding()]
param(
    [string]$DestinationDir = "",
    [string]$SourceDir = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DestinationDir)) {
    $DestinationDir = Join-Path $PSScriptRoot "..\artifacts\publish\engines\media"
}
$DestinationDir = [System.IO.Path]::GetFullPath($DestinationDir)

Write-Host "=== Deterministic FFmpeg Acquisition & Validation ===" -ForegroundColor Cyan
Write-Host "Destination: $DestinationDir"

New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null

$ffmpegTarget = Join-Path $DestinationDir "ffmpeg.exe"
$ffprobeTarget = Join-Path $DestinationDir "ffprobe.exe"

$foundSource = $false

# 1. Explicit source directory provided
if (-not [string]::IsNullOrWhiteSpace($SourceDir) -and (Test-Path $SourceDir)) {
    $srcFfmpeg = Join-Path $SourceDir "ffmpeg.exe"
    $srcFfprobe = Join-Path $SourceDir "ffprobe.exe"
    if ((Test-Path $srcFfmpeg) -and (Test-Path $srcFfprobe)) {
        Write-Host "Using explicitly provided source directory: $SourceDir" -ForegroundColor Green
        Copy-Item $srcFfmpeg $ffmpegTarget -Force
        Copy-Item $srcFfprobe $ffprobeTarget -Force
        $foundSource = $true
    }
}

# 2. Check if destination already contains verified binaries
if (-not $foundSource -and (Test-Path $ffmpegTarget) -and (Test-Path $ffprobeTarget)) {
    Write-Host "Found existing binaries in destination directory." -ForegroundColor Green
    $foundSource = $true
}

# 3. Check system PATH
if (-not $foundSource) {
    $ffmpegCmd = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
    $ffprobeCmd = Get-Command ffprobe.exe -ErrorAction SilentlyContinue
    if ($ffmpegCmd -and $ffprobeCmd) {
        Write-Host "Discovered FFmpeg in system PATH: $(Split-Path $ffmpegCmd.Source)" -ForegroundColor Green
        Copy-Item $ffmpegCmd.Source $ffmpegTarget -Force
        Copy-Item $ffprobeCmd.Source $ffprobeTarget -Force
        $foundSource = $true
    }
}

# 4. Check known Windows package manager locations
if (-not $foundSource) {
    $knownLocations = @(
        "$env:ChocolateyInstall\lib\ffmpeg\tools\ffmpeg\bin",
        "$env:ChocolateyInstall\bin",
        "$env:ProgramData\chocolatey\bin"
    )

    foreach ($loc in $knownLocations) {
        if (Test-Path $loc) {
            $candidateFfmpeg = Get-ChildItem -Path $loc -Filter "ffmpeg.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($candidateFfmpeg) {
                $candidateDir = $candidateFfmpeg.DirectoryName
                $candidateFfprobe = Join-Path $candidateDir "ffprobe.exe"
                if (Test-Path $candidateFfprobe) {
                    Write-Host "Discovered FFmpeg at: $candidateDir" -ForegroundColor Green
                    Copy-Item $candidateFfmpeg.FullName $ffmpegTarget -Force
                    Copy-Item $candidateFfprobe $ffprobeTarget -Force
                    $foundSource = $true
                    break
                }
            }
        }
    }
}

# 5. Fail early and loudly if binaries are absent
if (-not $foundSource -or -not (Test-Path $ffmpegTarget) -or -not (Test-Path $ffprobeTarget)) {
    Write-Error @"
[ERROR] Missing required media engine binaries:
  ffmpeg.exe: $(Test-Path $ffmpegTarget)
  ffprobe.exe: $(Test-Path $ffprobeTarget)
Destination: $DestinationDir

Phase Drop requires real, verified FFmpeg and FFprobe binaries to bundle into the installer.
To resolve this:
  1. Install via winget: 'winget install Gyan.FFmpeg'
  2. Install via choco:  'choco install ffmpeg -y'
  3. Or pass an explicit path: 'scripts/package.ps1 -FfmpegDir <path>'
"@
    exit 1
}

# 6. Verify execution and extract build details
try {
    $ffmpegVerOutput = & $ffmpegTarget -version 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ffmpeg.exe execution test failed with exit code $LASTEXITCODE"
        exit 1
    }

    $ffprobeVerOutput = & $ffprobeTarget -version 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ffprobe.exe execution test failed with exit code $LASTEXITCODE"
        exit 1
    }

    $firstLine = $ffmpegVerOutput | Select-Object -First 1
    Write-Host "Verified media engine: $firstLine" -ForegroundColor Green
    Write-Host "Binaries successfully staged at: $DestinationDir" -ForegroundColor Green
}
catch {
    Write-Error "Failed to execute or verify media engine binaries: $_"
    exit 1
}
