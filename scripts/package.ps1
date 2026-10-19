<#
.SYNOPSIS
    Builds, bundles, and compiles the Phase Drop Windows Release installer.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path "$PSScriptRoot\.."
$dotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$nsisPath = "D:\Softwares\NSIS\makensis.exe"
$publishDir = "$repoRoot\artifacts\publish"
$distDir = "$repoRoot\dist"

Write-Host "=== Phase Drop Release Packaging Pipeline ===" -ForegroundColor Cyan
Write-Host "Version:       $Version"
Write-Host "Configuration: $Configuration"
Write-Host "Publish Dir:   $publishDir"
Write-Host "Dist Dir:      $distDir`n"

# 1. Clean previous artifacts
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

# 2. Self-contained .NET 10 publish
Write-Host "[1/5] Publishing self-contained win-x64 application..." -ForegroundColor Yellow
$publishArgs = @(
    "publish",
    "$repoRoot\src\PhaseDrop.App\PhaseDrop.App.csproj",
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=false",
    "-p:Version=$Version",
    "-o", $publishDir
)

& $dotnetPath @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 3. Bundle Engines (FFmpeg & FFprobe)
Write-Host "[2/5] Bundling conversion engines..." -ForegroundColor Yellow
$enginesMediaDir = "$publishDir\engines\media"
New-Item -ItemType Directory -Path $enginesMediaDir -Force | Out-Null

$wingetFfmpegDir = "C:\Users\shubh\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.2-full_build\bin"
if (Test-Path "$wingetFfmpegDir\ffmpeg.exe") {
    Copy-Item "$wingetFfmpegDir\ffmpeg.exe" "$enginesMediaDir\ffmpeg.exe" -Force
    Copy-Item "$wingetFfmpegDir\ffprobe.exe" "$enginesMediaDir\ffprobe.exe" -Force
    Write-Host "Bundled FFmpeg and FFprobe from WinGet package." -ForegroundColor Green
} else {
    Write-Warning "Local FFmpeg binary not found at $wingetFfmpegDir. Searching system PATH..."
    $ffmpegCmd = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
    $ffprobeCmd = Get-Command ffprobe.exe -ErrorAction SilentlyContinue
    if ($ffmpegCmd -and $ffprobeCmd) {
        Copy-Item $ffmpegCmd.Source "$enginesMediaDir\ffmpeg.exe" -Force
        Copy-Item $ffprobeCmd.Source "$enginesMediaDir\ffprobe.exe" -Force
        Write-Host "Bundled FFmpeg and FFprobe from PATH." -ForegroundColor Green
    } else {
        Write-Warning "FFmpeg binaries could not be bundled. Installer will proceed with managed-only processing engines."
    }
}

# 4. Copy Notice and License assets
Write-Host "[3/5] Copying legal notices and assets..." -ForegroundColor Yellow
Copy-Item "$repoRoot\LICENSE" "$publishDir\LICENSE" -Force
Copy-Item "$repoRoot\THIRD-PARTY-NOTICES.md" "$publishDir\THIRD-PARTY-NOTICES.md" -Force
Copy-Item "$repoRoot\README.md" "$publishDir\README.md" -Force

$assetsDir = "$publishDir\Assets"
New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
Copy-Item "$repoRoot\assets\branding\icon.ico" "$assetsDir\icon.ico" -Force
Copy-Item "$repoRoot\assets\branding\logo.png" "$assetsDir\logo.png" -Force

# 5. Compile NSIS Installer
Write-Host "[4/5] Compiling NSIS Installer..." -ForegroundColor Yellow
if (-not (Test-Path $nsisPath)) {
    $nsisCmd = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($nsisCmd) {
        $nsisPath = $nsisCmd.Source
    } else {
        Write-Error "makensis.exe not found at $nsisPath or in PATH."
        exit 1
    }
}

$nsiScript = "$repoRoot\packaging\nsis\phasedrop.nsi"
& $nsisPath "/V3" $nsiScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "NSIS compilation failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 6. Verify & Checksum
Write-Host "[5/5] Generating cryptographic verification checksums..." -ForegroundColor Yellow
$installerPath = "$distDir\PhaseDrop-Setup-v$Version-x64.exe"
if (-not (Test-Path $installerPath)) {
    Write-Error "Expected installer not found at $installerPath"
    exit 1
}

$hash = Get-FileHash -Path $installerPath -Algorithm SHA256
$hashFile = "$installerPath.sha256"
"$($hash.Hash)  $(Split-Path $installerPath -Leaf)" | Out-File -FilePath $hashFile -Encoding utf8

$fileSizeMb = [Math]::Round(((Get-Item $installerPath).Length / 1MB), 2)

Write-Host "`n=== Packaging Complete ===" -ForegroundColor Green
Write-Host "Installer: $installerPath ($fileSizeMb MB)"
Write-Host "SHA-256:   $($hash.Hash)"
Write-Host "Checksum:  $hashFile"
