<#
.SYNOPSIS
    Runs automated unit and integration tests across the Phase Drop solution.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$env:PATH = "C:\Program Files\dotnet;$env:PATH"

Write-Host "=== Running Phase Drop Tests ($Configuration) ===" -ForegroundColor Cyan
dotnet test PhaseDrop.sln -c $Configuration --no-build --nologo --verbosity normal

if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed with exit code $LASTEXITCODE"
} else {
    Write-Host "All tests passed successfully!" -ForegroundColor Green
}
