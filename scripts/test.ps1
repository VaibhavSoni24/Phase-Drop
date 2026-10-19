<#
.SYNOPSIS
    Runs automated unit and integration tests across the Phase Drop solution.
#>
param(
    [string]$Configuration = "Release",
    [switch]$NoBuild = $false
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

Write-Host "=== Running Phase Drop Tests ($Configuration) ===" -ForegroundColor Cyan
Write-Host "Using .NET CLI: $dotnetCmd" -ForegroundColor Gray

$testArgs = @("test", "PhaseDrop.sln", "-c", $Configuration, "--nologo", "--verbosity", "normal")
if ($NoBuild) {
    $testArgs += "--no-build"
}

& $dotnetCmd @testArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
} else {
    Write-Host "All tests passed successfully!" -ForegroundColor Green
}
