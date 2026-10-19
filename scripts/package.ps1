<#
.SYNOPSIS
    Builds, bundles, and compiles the Phase Drop Windows Release installer.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$FfmpegDir = "",
    [string]$NsisPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$publishDir = Join-Path $repoRoot "artifacts\publish"
$distDir = Join-Path $repoRoot "dist"

Write-Host "=== Phase Drop Release Packaging Pipeline ===" -ForegroundColor Cyan
Write-Host "Version:       $Version"
Write-Host "Configuration: $Configuration"
Write-Host "Repository:    $repoRoot"
Write-Host "Publish Dir:   $publishDir"
Write-Host "Dist Dir:      $distDir`n"

# 1. Resolve .NET CLI
$dotnetCmd = "dotnet"
$foundDotnet = Get-Command $dotnetCmd -ErrorAction SilentlyContinue
if (-not $foundDotnet) {
    if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_ROOT) -and (Test-Path "$env:DOTNET_ROOT\dotnet.exe")) {
        $dotnetCmd = "$env:DOTNET_ROOT\dotnet.exe"
    } elseif (Test-Path "$env:ProgramFiles\dotnet\dotnet.exe") {
        $dotnetCmd = "$env:ProgramFiles\dotnet\dotnet.exe"
    } else {
        Write-Error "The .NET CLI (dotnet) was not found in PATH or standard installation directories. Please install .NET 10 SDK."
        exit 1
    }
} else {
    $dotnetCmd = $foundDotnet.Source
}
Write-Host "Using .NET CLI: $dotnetCmd"

# 2. Resolve NSIS Compiler (makensis.exe)
$nsisExe = $NsisPath
if ([string]::IsNullOrWhiteSpace($nsisExe) -or -not (Test-Path $nsisExe)) {
    if (-not [string]::IsNullOrWhiteSpace($env:NSIS_PATH) -and (Test-Path $env:NSIS_PATH)) {
        $nsisExe = $env:NSIS_PATH
    } elseif (-not [string]::IsNullOrWhiteSpace($env:NSIS_DIR) -and (Test-Path "$env:NSIS_DIR\makensis.exe")) {
        $nsisExe = "$env:NSIS_DIR\makensis.exe"
    }
}
if ([string]::IsNullOrWhiteSpace($nsisExe) -or -not (Test-Path $nsisExe)) {
    $nsisCmd = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($nsisCmd) {
        $nsisExe = $nsisCmd.Source
    } else {
        $candidates = @(
            "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
            "$env:ProgramFiles\NSIS\makensis.exe",
            "$env:ChocolateyInstall\bin\makensis.exe",
            "$env:LOCALAPPDATA\Programs\NSIS\makensis.exe"
        )
        foreach ($c in $candidates) {
            if (-not [string]::IsNullOrWhiteSpace($c) -and (Test-Path $c)) {
                $nsisExe = $c
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($nsisExe) -or -not (Test-Path $nsisExe)) {
    Write-Error "NSIS compiler (makensis.exe) was not found in PATH or standard directories. Please install NSIS 3.13+ via 'choco install nsis' or pass -NsisPath."
    exit 1
}
Write-Host "Using NSIS Compiler: $nsisExe"

# 3. Clean previous artifacts
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}
$targetInstaller = Join-Path $distDir "PhaseDrop-Setup-v$Version-x64.exe"
if (Test-Path $targetInstaller) {
    Remove-Item -Force $targetInstaller
}
if (Test-Path "$targetInstaller.sha256") {
    Remove-Item -Force "$targetInstaller.sha256"
}

# 4. Self-contained .NET 10 publish
Write-Host "`n[1/5] Publishing self-contained win-x64 application..." -ForegroundColor Yellow
$appCsproj = Join-Path $repoRoot "src\PhaseDrop.App\PhaseDrop.App.csproj"
$publishArgs = @(
    "publish",
    $appCsproj,
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=false",
    "-p:Version=$Version",
    "-o", $publishDir
)

& $dotnetCmd @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 5. Bundle Conversion Engines (FFmpeg & FFprobe)
Write-Host "`n[2/5] Bundling media processing engines..." -ForegroundColor Yellow
$enginesMediaDir = Join-Path $publishDir "engines\media"
$acquireScript = Join-Path $repoRoot "scripts\acquire-ffmpeg.ps1"

$acquireArgs = @{
    DestinationDir = $enginesMediaDir
}
if (-not [string]::IsNullOrWhiteSpace($FfmpegDir)) {
    $acquireArgs["SourceDir"] = $FfmpegDir
}

& $acquireScript @acquireArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Media engine acquisition failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 6. Copy Legal Notices, Documentation, and Assets
Write-Host "`n[3/5] Copying legal notices and branding assets..." -ForegroundColor Yellow
Copy-Item (Join-Path $repoRoot "LICENSE") (Join-Path $publishDir "LICENSE") -Force
Copy-Item (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md") (Join-Path $publishDir "THIRD-PARTY-NOTICES.md") -Force
Copy-Item (Join-Path $repoRoot "README.md") (Join-Path $publishDir "README.md") -Force

$assetsDir = Join-Path $publishDir "Assets"
New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
Copy-Item (Join-Path $repoRoot "assets\branding\icon.ico") (Join-Path $assetsDir "icon.ico") -Force
Copy-Item (Join-Path $repoRoot "assets\branding\logo.png") (Join-Path $assetsDir "logo.png") -Force

# 7. Compile NSIS Installer
Write-Host "`n[4/5] Compiling NSIS Installer..." -ForegroundColor Yellow
$nsiScript = Join-Path $repoRoot "packaging\nsis\phasedrop.nsi"
$nsisArgs = @(
    "/V3",
    "/DPRODUCT_VERSION=$Version",
    $nsiScript
)

& $nsisExe @nsisArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "NSIS compilation failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 8. Verify & Checksum
Write-Host "`n[5/5] Generating cryptographic verification checksums..." -ForegroundColor Yellow
$installerPath = Join-Path $distDir "PhaseDrop-Setup-v$Version-x64.exe"
if (-not (Test-Path $installerPath)) {
    Write-Error "Expected installer was not generated at: $installerPath"
    exit 1
}

$hash = Get-FileHash -Path $installerPath -Algorithm SHA256
$hashFile = "$installerPath.sha256"
# Standard sha256sum consumable format: '<64-hex>  <filename>'
"$($hash.Hash.ToLowerInvariant())  $(Split-Path $installerPath -Leaf)" | Out-File -FilePath $hashFile -Encoding ascii

$metaPublishPath = Join-Path $publishDir "engines\media\ffmpeg-metadata.json"
if (Test-Path $metaPublishPath) {
    Copy-Item $metaPublishPath (Join-Path $distDir "ffmpeg-metadata.json") -Force
}

$fileSizeMb = [Math]::Round(((Get-Item $installerPath).Length / 1MB), 2)

Write-Host "`n=== Packaging Complete ===" -ForegroundColor Green
Write-Host "Installer: $installerPath ($fileSizeMb MB)"
Write-Host "SHA-256:   $($hash.Hash)"
Write-Host "Checksum:  $hashFile"
