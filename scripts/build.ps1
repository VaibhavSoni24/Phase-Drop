<#
.SYNOPSIS
    Builds the Phase Drop solution in Release configuration.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$dotnetCmd = if (Get-Command "dotnet" -ErrorAction SilentlyContinue) {
    "dotnet"
} elseif (Test-Path "$env:ProgramFiles\dotnet\dotnet.exe") {
    "$env:ProgramFiles\dotnet\dotnet.exe"
} else {
    Write-Error "Prerequisite missing: .NET CLI ('dotnet') was not found in PATH or standard ProgramFiles directory."
    exit 1
}

Write-Host "=== Building Phase Drop ($Configuration) ===" -ForegroundColor Cyan
Write-Host "Using .NET CLI: $dotnetCmd" -ForegroundColor Gray
& $dotnetCmd build PhaseDrop.sln -c $Configuration --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
} else {
    Write-Host "Build succeeded!" -ForegroundColor Green
}
