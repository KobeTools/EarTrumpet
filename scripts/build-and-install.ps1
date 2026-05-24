#Requires -Version 5.1
<#
.SYNOPSIS
    Builds EarTrumpet and installs it to a local dev folder, then starts it.

.DESCRIPTION
    Restores NuGet packages, builds EarTrumpet (x86), copies output to
    %LOCALAPPDATA%\Programs\EarTrumpet-Dev, stops any running instance from
    that folder, and launches the new build.

.PARAMETER Configuration
    MSBuild configuration (Debug or Release). Default: Debug.

.PARAMETER InstallDir
    Folder to install the built app. Default: %LOCALAPPDATA%\Programs\EarTrumpet-Dev

.PARAMETER SkipLaunch
    Install only; do not start EarTrumpet after copying files.

.PARAMETER InstallPrerequisites
    Install missing build prerequisites (.NET 4.6.2 Developer Pack, Windows 10 SDK) via winget.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\EarTrumpet-Dev'),

    [switch] $SkipLaunch,

    [switch] $InstallPrerequisites
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'EarTrumpet\EarTrumpet.csproj'
$packagesConfig = Join-Path $repoRoot 'EarTrumpet\packages.config'
$packagesDir = Join-Path $repoRoot 'packages'
$buildOutput = Join-Path $repoRoot "Build\$Configuration"
$toolsDir = Join-Path $PSScriptRoot '.tools'
$nugetExe = Join-Path $toolsDir 'nuget.exe'

function Write-Step([string] $Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Get-MsBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw 'vswhere.exe was not found. Install Visual Studio 2017 or newer with the .NET desktop development workload.'
    }

    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (-not $msbuild) {
        throw 'MSBuild was not found. Install Visual Studio with the MSBuild component.'
    }

    return $msbuild
}

function Test-DotNet462TargetingPack {
    $referenceAssemblies = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework'
    return (Test-Path (Join-Path $referenceAssemblies 'v4.6.2'))
}

function Test-WindowsWinmd {
    $unionMetadata = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\UnionMetadata'
    if (-not (Test-Path $unionMetadata)) {
        return $false
    }

    if (Test-Path (Join-Path $unionMetadata 'Windows.winmd')) {
        return $true
    }

    return [bool](Get-ChildItem -Path $unionMetadata -Recurse -Filter 'Windows.winmd' -ErrorAction SilentlyContinue | Select-Object -First 1)
}

function Install-BuildPrerequisites {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw 'winget is required to install prerequisites. Install App Installer from the Microsoft Store, or install the .NET 4.6.2 Developer Pack and Windows 10 SDK manually (see COMPILING.md).'
    }

    if (-not (Test-DotNet462TargetingPack)) {
        Write-Step 'Installing .NET Framework 4.6.2 Developer Pack (winget)'
        winget install Microsoft.DotNet.Framework.DeveloperPack.4.6 --accept-package-agreements --accept-source-agreements
    }

    if (-not (Test-WindowsWinmd)) {
        Write-Step 'Installing Windows 10 SDK 10.0.18362 (winget)'
        winget install Microsoft.WindowsSDK.10.0.18362 --accept-package-agreements --accept-source-agreements
    }
}

function Ensure-BuildPrerequisites {
    if ($InstallPrerequisites) {
        Install-BuildPrerequisites
    }

    $missing = @()
    if (-not (Test-DotNet462TargetingPack)) {
        $missing += '.NET Framework 4.6.2 Developer Pack (winget install Microsoft.DotNet.Framework.DeveloperPack.4.6)'
    }

    if (-not (Test-WindowsWinmd)) {
        $missing += 'Windows 10 SDK with union metadata (winget install Microsoft.WindowsSDK.10.0.18362)'
    }

    if ($missing.Count -gt 0) {
        throw @"
Missing build prerequisites:
$($missing -join [Environment]::NewLine)

Re-run with -InstallPrerequisites to install them automatically, or see COMPILING.md.
"@
    }
}

function Ensure-NuGet {
    if (Test-Path $nugetExe) {
        return
    }

    New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
    Write-Step 'Downloading NuGet.exe'
    Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nugetExe
}

function Stop-InstalledInstance([string] $ExePath) {
    $processes = Get-Process -Name 'EarTrumpet' -ErrorAction SilentlyContinue
    foreach ($process in $processes) {
        try {
            if ($process.Path -and ($process.Path -eq $ExePath)) {
                Write-Host "Stopping EarTrumpet (PID $($process.Id))"
                Stop-Process -Id $process.Id -Force
            }
        }
        catch {
            # Process may have exited while enumerating.
        }
    }
}

Write-Step "Repository: $repoRoot"

Ensure-BuildPrerequisites
Ensure-NuGet

Write-Step 'Restoring NuGet packages'
& $nugetExe restore $packagesConfig -PackagesDirectory $packagesDir -NonInteractive

$msbuild = Get-MsBuildPath
Write-Step "Building $Configuration|x86 with $msbuild"
& $msbuild $projectPath `
    /restore `
    /p:Configuration=$Configuration `
    /p:Platform=x86 `
    /v:minimal `
    /nologo

if (-not (Test-Path (Join-Path $buildOutput 'EarTrumpet.exe'))) {
    throw "Build failed: $(Join-Path $buildOutput 'EarTrumpet.exe') was not created."
}

Write-Step "Installing to $InstallDir"
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

$targetExe = Join-Path $InstallDir 'EarTrumpet.exe'
Stop-InstalledInstance -ExePath $targetExe

Copy-Item -Path (Join-Path $buildOutput '*') -Destination $InstallDir -Recurse -Force

Write-Host "Installed: $targetExe" -ForegroundColor Green

if (-not $SkipLaunch) {
    Write-Step 'Starting EarTrumpet'
    Start-Process -FilePath $targetExe
}

Write-Host "`nDone. To rebuild and reinstall, run:" -ForegroundColor Yellow
Write-Host "  .\scripts\build-and-install.ps1" -ForegroundColor Yellow
