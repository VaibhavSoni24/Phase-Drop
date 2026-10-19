<#
.SYNOPSIS
    Builds the Phase Drop solution in Release configuration.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$env:PATH = "C:\Program Files\dotnet;$env:PATH"

Write-Host "=== Building Phase Drop ($Configuration) ===" -ForegroundColor Cyan
dotnet build PhaseDrop.sln -c $Configuration --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
} else {
    Write-Host "Build succeeded!" -ForegroundColor Green
}
