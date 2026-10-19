<#
.SYNOPSIS
    Validates the Phase Drop release package, installer integrity, payload, and bundled engines.
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$SkipSmokeTest = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$distDir = Join-Path $repoRoot "dist"
$installerPath = Join-Path $distDir "PhaseDrop-Setup-v$Version-x64.exe"
$hashFile = "$installerPath.sha256"

Write-Host "=== Validating Phase Drop Release Package (v$Version) ===" -ForegroundColor Cyan

function Test-PeBinary {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) {
        return $false
    }

    $stream = $null
    try {
        $stream = [System.IO.File]::OpenRead($FilePath)
        if ($stream.Length -lt 64) { return $false }
        $reader = New-Object System.IO.BinaryReader($stream)

        # 1. DOS Header: 'MZ' (0x5A4D)
        $magic = $reader.ReadUInt16()
        if ($magic -ne 0x5A4D) { return $false }

        # 2. Offset to PE Header at 0x3C
        $stream.Seek(0x3C, [System.IO.SeekOrigin]::Begin) | Out-Null
        $peOffset = $reader.ReadInt32()
        if ($peOffset -le 0 -or $peOffset -ge ($stream.Length - 4)) { return $false }

        # 3. PE Signature: 'PE\0\0' (0x00004550)
        $stream.Seek($peOffset, [System.IO.SeekOrigin]::Begin) | Out-Null
        $peSig = $reader.ReadUInt32()
        if ($peSig -ne 0x00004550) { return $false }

        # 4. Machine Architecture (2 bytes after PE signature)
        $machine = $reader.ReadUInt16()
        # 0x8664 = IMAGE_FILE_MACHINE_AMD64, 0x014C = IMAGE_FILE_MACHINE_I386
        if ($machine -ne 0x8664 -and $machine -ne 0x014C) { return $false }

        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($stream) { $stream.Close() }
    }
}

# 1. Check Installer File Existence
if (-not (Test-Path $installerPath)) {
    Write-Error "Validation FAILED: Installer file does not exist at $installerPath"
    exit 1
}

$fileInfo = Get-Item $installerPath
$sizeMb = [Math]::Round(($fileInfo.Length / 1MB), 2)
Write-Host "Installer Found: $installerPath ($sizeMb MB)" -ForegroundColor Green

# 2. Complete Windows PE Binary Verification
if (-not (Test-PeBinary -FilePath $installerPath)) {
    Write-Error "Validation FAILED: Installer file is not a valid Windows PE binary (missing valid DOS/PE header or invalid machine signature)."
    exit 1
}
Write-Host "PE Binary Signature Verified (Valid DOS stub, 'PE\0\0' signature, and valid machine header)." -ForegroundColor Green

# 3. Check Plausible Installer Size (> 50 MB required for self-contained runtime + engines)
if ($fileInfo.Length -lt 50000000) {
    Write-Error "Validation FAILED: Installer size ($sizeMb MB) is too small. Expected bundled .NET 10 runtime and media engines."
    exit 1
}

# 4. Check Project Version Consistency
$csprojPath = Join-Path $repoRoot "src\PhaseDrop.App\PhaseDrop.App.csproj"
if (-not (Test-Path $csprojPath)) {
    Write-Error "Validation FAILED: PhaseDrop.App.csproj not found at $csprojPath"
    exit 1
}
$csprojContent = [xml](Get-Content $csprojPath)
$projectVersion = $csprojContent.Project.PropertyGroup.Version | Select-Object -First 1
if ($projectVersion -ne $Version) {
    Write-Error "Validation FAILED: Version mismatch! Project file defines '$projectVersion', but validating '$Version'."
    exit 1
}
Write-Host "Application Project Version Verified: $projectVersion" -ForegroundColor Green

# 5. Check SHA-256 Sidecar Format and Hash Verification
if (-not (Test-Path $hashFile)) {
    Write-Error "Validation FAILED: Checksum file does not exist at $hashFile"
    exit 1
}

$sidecarRaw = (Get-Content $hashFile -Raw).Trim()
# Standard sha256sum consumable format: '<64-hex>  <filename>' or '<64-hex> *<filename>'
$sidecarMatch = [regex]::Match($sidecarRaw, "^([a-fA-F0-9]{64})\s+[\*]?([^\r\n]+)$")
if (-not $sidecarMatch.Success) {
    Write-Error "Validation FAILED: Checksum sidecar is not in standard sha256sum format.`nContent: $sidecarRaw"
    exit 1
}

$expectedHash = $sidecarMatch.Groups[1].Value.ToLowerInvariant()
$sidecarFilename = $sidecarMatch.Groups[2].Value.Trim()
$installerLeaf = Split-Path $installerPath -Leaf

if ($sidecarFilename -ne $installerLeaf) {
    Write-Error "Validation FAILED: Checksum sidecar referenced filename '$sidecarFilename', expected '$installerLeaf'."
    exit 1
}

$actualHash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expectedHash -ne $actualHash) {
    Write-Error "Validation FAILED: Checksum mismatch!`nExpected: $expectedHash`nActual:   $actualHash"
    exit 1
}
Write-Host "SHA-256 Checksum Sidecar Verified ($actualHash, standard sha256sum format)." -ForegroundColor Green

# 6. Check Published Payload Files & Engines
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
    "engines\media\ffprobe.exe",
    "engines\media\ffmpeg-metadata.json"
)

foreach ($req in $requiredFiles) {
    $fullPath = Join-Path $publishDir $req
    if (-not (Test-Path $fullPath)) {
        Write-Error "Validation FAILED: Required payload file missing from published layout: $req"
        exit 1
    }
}
Write-Host "All $( $requiredFiles.Count ) required runtime payload files and bundled engines verified in staging." -ForegroundColor Green

# 7. Verify Execution of Bundled Engines in Payload
$bundledFfmpeg = Join-Path $publishDir "engines\media\ffmpeg.exe"
$bundledFfprobe = Join-Path $publishDir "engines\media\ffprobe.exe"
$bundledMeta = Join-Path $publishDir "engines\media\ffmpeg-metadata.json"

$oldEap = $ErrorActionPreference
try {
    $ErrorActionPreference = "SilentlyContinue"
    $ffmpegOutput = & $bundledFfmpeg -version 2>&1
    $ffprobeOutput = & $bundledFfprobe -version 2>&1
}
finally {
    $ErrorActionPreference = $oldEap
}

if ($LASTEXITCODE -ne 0 -or -not $ffmpegOutput) {
    Write-Error "Validation FAILED: Bundled ffmpeg.exe execution check failed (exit code: $LASTEXITCODE)."
    exit 1
}
$ffmpegVer = ($ffmpegOutput | Select-Object -First 1).ToString().Trim()
Write-Host "Bundled FFmpeg Verified: $ffmpegVer" -ForegroundColor Green

if ($LASTEXITCODE -ne 0 -or -not $ffprobeOutput) {
    Write-Error "Validation FAILED: Bundled ffprobe.exe execution check failed (exit code: $LASTEXITCODE)."
    exit 1
}
$ffprobeVer = ($ffprobeOutput | Select-Object -First 1).ToString().Trim()
Write-Host "Bundled FFprobe Verified: $ffprobeVer" -ForegroundColor Green

# 8. Validate Media Engine Release Metadata Integrity
$metaContent = Get-Content $bundledMeta -Raw | ConvertFrom-Json
if (-not $metaContent.version -or -not $metaContent.archiveSha256 -or -not $metaContent.ffmpegSha256 -or -not $metaContent.ffprobeSha256) {
    Write-Error "Validation FAILED: ffmpeg-metadata.json is missing required fields."
    exit 1
}

$actualFfmpegHash = (Get-FileHash -Path $bundledFfmpeg -Algorithm SHA256).Hash
if ($actualFfmpegHash -ne $metaContent.ffmpegSha256) {
    Write-Error "Validation FAILED: Staged ffmpeg.exe SHA-256 does not match metadata record!`nActual: $actualFfmpegHash`nMetadata: $($metaContent.ffmpegSha256)"
    exit 1
}

$actualFfprobeHash = (Get-FileHash -Path $bundledFfprobe -Algorithm SHA256).Hash
if ($actualFfprobeHash -ne $metaContent.ffprobeSha256) {
    Write-Error "Validation FAILED: Staged ffprobe.exe SHA-256 does not match metadata record!`nActual: $actualFfprobeHash`nMetadata: $($metaContent.ffprobeSha256)"
    exit 1
}
Write-Host "FFmpeg & FFprobe Metadata & Binary Hash Consistency Verified." -ForegroundColor Green

# 9. Real Installer Extraction and Smoke Test
if (-not $SkipSmokeTest) {
    Write-Host "`nRunning Installer Silent Smoke Test (Installation & Extraction Verification)..." -ForegroundColor Yellow
    $smokeGuid = [System.Guid]::NewGuid().ToString("N").Substring(0, 8)
    $smokeDir = Join-Path $env:TEMP "PhaseDrop_SmokeTest_$smokeGuid"

    try {
        if (Test-Path $smokeDir) {
            Remove-Item -Recurse -Force $smokeDir
        }
        New-Item -ItemType Directory -Path $smokeDir -Force | Out-Null

        # Run NSIS installer silently to disposable directory. Note: /D must be the last argument without quotes.
        $installProc = Start-Process -FilePath $installerPath -ArgumentList "/S", "/D=$smokeDir" -Wait -PassThru
        if ($installProc.ExitCode -ne 0) {
            Write-Error "Validation FAILED: Installer silent installation returned non-zero exit code: $($installProc.ExitCode)"
            exit 1
        }

        # Verify extracted application binary
        $extractedApp = Join-Path $smokeDir "PhaseDrop.exe"
        if (-not (Test-Path $extractedApp)) {
            Write-Error "Validation FAILED: Installer payload failed to extract PhaseDrop.exe to $smokeDir"
            exit 1
        }
        if (-not (Test-PeBinary -FilePath $extractedApp)) {
            Write-Error "Validation FAILED: Extracted PhaseDrop.exe failed PE binary verification."
            exit 1
        }
        Write-Host "Smoke Test: Extracted PhaseDrop.exe verified." -ForegroundColor Green

        # Verify extracted engines
        $extractedFfmpeg = Join-Path $smokeDir "engines\media\ffmpeg.exe"
        $extractedFfprobe = Join-Path $smokeDir "engines\media\ffprobe.exe"
        if (-not (Test-Path $extractedFfmpeg) -or -not (Test-Path $extractedFfprobe)) {
            Write-Error "Validation FAILED: Extracted payload is missing ffmpeg.exe or ffprobe.exe."
            exit 1
        }

        $exFfmpegOut = & $extractedFfmpeg -version 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Validation FAILED: Extracted ffmpeg.exe failed execution check."
            exit 1
        }
        Write-Host "Smoke Test: Extracted media engines execute cleanly." -ForegroundColor Green

        # Verify uninstaller presence and execute silent uninstaller
        $uninstaller = Join-Path $smokeDir "Uninstall.exe"
        if (-not (Test-Path $uninstaller)) {
            Write-Error "Validation FAILED: Installer failed to generate Uninstall.exe."
            exit 1
        }
        if (-not (Test-PeBinary -FilePath $uninstaller)) {
            Write-Error "Validation FAILED: Generated Uninstall.exe is not a valid PE binary."
            exit 1
        }

        # NSIS uninstaller: /S _?=dir ensures synchronous silent uninstall in-place
        $uninstProc = Start-Process -FilePath $uninstaller -ArgumentList "/S", "_?=$smokeDir" -Wait -PassThru
        if ($uninstProc.ExitCode -ne 0) {
            Write-Error "Validation FAILED: Uninstaller returned non-zero exit code: $($uninstProc.ExitCode)"
            exit 1
        }

        # Verify application file cleanup
        if (Test-Path $extractedApp) {
            Write-Error "Validation FAILED: Uninstallation did not remove PhaseDrop.exe."
            exit 1
        }
        Write-Host "Smoke Test: Uninstaller executed and removed application binaries cleanly." -ForegroundColor Green
    }
    finally {
        # Clean up temporary smoke directory
        if (Test-Path $smokeDir) {
            Remove-Item -Recurse -Force $smokeDir -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "`nRelease Package Validation PASSED successfully!" -ForegroundColor Green
