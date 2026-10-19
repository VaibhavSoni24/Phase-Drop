<#
.SYNOPSIS
    Measures Phase Drop background process idle CPU, Working Set, and Private Bytes.
#>
param(
    [int]$SampleDurationSeconds = 30
)

$process = Get-Process -Name "PhaseDrop" -ErrorAction SilentlyContinue
if (-not $process) {
    Write-Warning "PhaseDrop is not currently running. Launching in background for measurement..."
    $exePath = Resolve-Path "src\PhaseDrop.App\bin\Release\net10.0-windows\PhaseDrop.exe" -ErrorAction SilentlyContinue
    if (-not $exePath) {
        $exePath = Resolve-Path "src\PhaseDrop.App\bin\Debug\net10.0-windows\PhaseDrop.exe" -ErrorAction SilentlyContinue
    }

    if (-not $exePath) {
        Write-Error "Could not find PhaseDrop executable. Run build.ps1 first."
        exit 1
    }

    $process = Start-Process -FilePath $exePath -ArgumentList "--background" -PassThru
    Start-Sleep -Seconds 3 # Let process settle
}

Write-Host "Measuring PhaseDrop (PID $($process.Id)) over $SampleDurationSeconds seconds..." -ForegroundColor Cyan

$cpuSamples = @()
$wsSamples = @()
$pbSamples = @()

$prevCpu = $process.TotalProcessorTime.TotalMilliseconds
$prevTime = [DateTime]::UtcNow

for ($i = 0; $i -lt $SampleDurationSeconds; $i++) {
    Start-Sleep -Seconds 1
    $process.Refresh()
    if ($process.HasExited) {
        Write-Error "PhaseDrop exited unexpectedly during measurement."
        exit 1
    }

    $nowTime = [DateTime]::UtcNow
    $curCpu = $process.TotalProcessorTime.TotalMilliseconds

    $timeDelta = ($nowTime - $prevTime).TotalMilliseconds
    $cpuDelta = $curCpu - $prevCpu
    $cpuUsagePercent = ($cpuDelta / ($timeDelta * [Environment]::ProcessorCount)) * 100.0

    $cpuSamples += $cpuUsagePercent
    $wsSamples += ($process.WorkingSet64 / 1MB)
    $pbSamples += ($process.PrivateMemorySize64 / 1MB)

    $prevCpu = $curCpu
    $prevTime = $nowTime
}

$avgCpu = ($cpuSamples | Measure-Object -Average).Average
$avgWs = ($wsSamples | Measure-Object -Average).Average
$avgPb = ($pbSamples | Measure-Object -Average).Average
$maxCpu = ($cpuSamples | Measure-Object -Maximum).Maximum

Write-Host "`n=== Idle Performance Report ===" -ForegroundColor Green
Write-Host "Sample Duration:      $SampleDurationSeconds seconds"
Write-Host "Logical Processors:   $([Environment]::ProcessorCount)"
Write-Host "Average Idle CPU:     $([Math]::Round($avgCpu, 3))%"
Write-Host "Peak CPU in Sample:   $([Math]::Round($maxCpu, 3))%"
Write-Host "Average Working Set:  $([Math]::Round($avgWs, 1)) MB"
Write-Host "Average Private Byte: $([Math]::Round($avgPb, 1)) MB"

if ($avgCpu -lt 0.1) {
    Write-Host "PASSED: Idle CPU budget (< 0.1%) satisfied!" -ForegroundColor Green
} else {
    Write-Warning "Average idle CPU ($([Math]::Round($avgCpu, 3))%) exceeded budget (< 0.1%)."
}
