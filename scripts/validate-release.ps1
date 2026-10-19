<#
.SYNOPSIS
    Validates the Phase Drop release package, installer integrity, and bundled files.
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\.."
$distDir = "$repoRoot\dist"
$installerPath = "$distDir\PhaseDrop-Setup-v$Version-x64.exe"
$hashFile = "$installerPath.sha256"

Write-Host "=== Validating Phase Drop Release Package ===" -ForegroundColor Cyan

# 1. Check Installer File
if (-not (Test-Path $installerPath)) {
    Write-Error "Validation FAILED: Installer file does not exist at $installerPath"
    exit 1
}

$fileInfo = Get-Item $installerPath
$sizeMb = [Math]::Round(($fileInfo.Length / 1MB), 2)
Write-Host "Installer Found: $installerPath ($sizeMb MB)" -ForegroundColor Green

if ($fileInfo.Length -lt 5000000) { # Less than 5MB indicates missing payload
    Write-Error "Validation FAILED: Installer file size ($sizeMb MB) is suspiciously small. Expected bundled runtime and engines."
    exit 1
}

# 2. Check SHA-256 File
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

# 3. Check Published Assets
$publishDir = "$repoRoot\artifacts\publish"
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
    "Assets\icon.ico"
)

foreach ($req in $requiredFiles) {
    $fullPath = "$publishDir\$req"
    if (-not (Test-Path $fullPath)) {
        Write-Error "Validation FAILED: Required file missing from payload: $req"
        exit 1
    }
}

Write-Host "All $( $requiredFiles.Count ) required runtime payload files verified." -ForegroundColor Green
Write-Host "`nRelease Package Validation PASSED successfully!" -ForegroundColor Green
