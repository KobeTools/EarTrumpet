#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the dev build of EarTrumpet and streams its log file to the console.

.DESCRIPTION
    Uses EarTrumpetDev.exe from %LOCALAPPDATA%\Programs\EarTrumpet-Dev.
    Rebuild first with .\scripts\build-and-install.ps1 -SkipLaunch if needed.
#>
[CmdletBinding()]
param(
    [switch] $Rebuild
)

$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\EarTrumpet-Dev'
$exePath = Join-Path $installDir 'EarTrumpetDev.exe'
$logPath = Join-Path $installDir 'eartrumpet-dev.log'

if ($Rebuild) {
    $buildScript = Join-Path $PSScriptRoot 'build-and-install.ps1'
    & $buildScript -SkipLaunch
}

if (-not (Test-Path $exePath)) {
    throw "Dev build not found at $exePath. Run .\scripts\build-and-install.ps1 first."
}

if (Test-Path $logPath) {
    Remove-Item $logPath -Force
}

Write-Host "Starting $exePath" -ForegroundColor Cyan
Write-Host "Log file: $logPath" -ForegroundColor Cyan
Write-Host "Hover the tray icon: tooltip should start with 'EarTrumpet Dev:'" -ForegroundColor Yellow
Write-Host "Expand the flyout (chevron) before dragging between devices." -ForegroundColor Yellow
Write-Host "Red circle cursor = not over a valid device section (see log for reason)." -ForegroundColor Yellow
Write-Host "After a crash, re-open the log file (exceptions are appended)." -ForegroundColor Yellow
Write-Host ""

$process = Start-Process -FilePath $exePath -PassThru

try {
    $deadline = (Get-Date).AddSeconds(8)
    while (-not (Test-Path $logPath) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }

    if (-not (Test-Path $logPath)) {
        Write-Warning "Log file not created yet. If the app exited immediately, another EarTrumpet may be blocking startup."
    }
    else {
        Get-Content $logPath -Wait -Tail 30
    }
}
finally {
    if (-not $process.HasExited) {
        Write-Host "`nEarTrumpet Dev is still running (PID $($process.Id)). Close it from the tray when finished." -ForegroundColor Green
    }
}
