#Requires -Version 5.1
<#
.SYNOPSIS
    Builds EarTrumpet Dev and installs it locally with Start menu integration.

.DESCRIPTION
    Restores NuGet packages, builds EarTrumpet (x86 Debug with DEVBUILD), copies output to
    %LOCALAPPDATA%\Programs\EarTrumpet-Dev as EarTrumpetDev.exe, registers a Start menu shortcut,
    and starts the app. Uses a separate single-instance mutex so it can run beside the Store app.

.PARAMETER Configuration
    MSBuild configuration (Debug or Release). Default: Debug.

.PARAMETER InstallDir
    Folder to install the built app. Default: %LOCALAPPDATA%\Programs\EarTrumpet-Dev

.PARAMETER SkipLaunch
    Install only; do not start EarTrumpet after copying files.

.PARAMETER SkipShortcuts
    Do not create or update Start menu / optional startup shortcuts.

.PARAMETER Startup
    Also register EarTrumpet Dev to run at sign-in (Startup folder; shows in Settings > Apps > Startup).

.PARAMETER InstallPrerequisites
    Only if build fails: install missing .NET 4.6.2 targeting pack or Windows SDK via winget.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\EarTrumpet-Dev'),

    [switch] $SkipLaunch,

    [switch] $SkipShortcuts,

    [switch] $Startup,

    [switch] $InstallPrerequisites
)

$ErrorActionPreference = 'Stop'

$appName = 'EarTrumpet Dev'
$devExeName = 'EarTrumpetDev.exe'
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

Visual Studio may be installed without the .NET 4.6.2 targeting pack or Windows SDK union metadata.
Re-run with -InstallPrerequisites to install them, or add those workloads in the Visual Studio Installer.
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

function Stop-DevInstance([string] $InstallDirectory) {
    foreach ($name in @('EarTrumpetDev', 'EarTrumpet')) {
        $processes = Get-Process -Name $name -ErrorAction SilentlyContinue
        foreach ($process in $processes) {
            try {
                if ($process.Path -and $process.Path.StartsWith($InstallDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
                    Write-Host "Stopping $($process.ProcessName) (PID $($process.Id))"
                    Stop-Process -Id $process.Id -Force
                }
            }
            catch {
                # Process may have exited while enumerating.
            }
        }
    }

    Start-Sleep -Milliseconds 500
}

function New-Shortcut([string] $LinkPath, [string] $TargetPath, [string] $WorkingDirectory, [string] $Description) {
    $parent = Split-Path $LinkPath -Parent
    if (-not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    if (Test-Path $LinkPath) {
        Remove-Item $LinkPath -Force
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($LinkPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = $Description
    $shortcut.Save()
}

function Register-DevShortcuts([string] $InstallDirectory, [string] $ExePath, [switch] $AddStartup) {
    $programsDir = [Environment]::GetFolderPath('Programs')
    $startMenuLink = Join-Path $programsDir "$appName.lnk"
    New-Shortcut -LinkPath $startMenuLink -TargetPath $ExePath -WorkingDirectory $InstallDirectory `
        -Description 'EarTrumpet Dev — per-app and per-device volume control'
    Write-Host "Start menu: $startMenuLink" -ForegroundColor Green

    $startupDir = [Environment]::GetFolderPath('Startup')
    $startupLink = Join-Path $startupDir "$appName.lnk"

    if ($AddStartup) {
        New-Shortcut -LinkPath $startupLink -TargetPath $ExePath -WorkingDirectory $InstallDirectory `
            -Description 'EarTrumpet Dev — run at sign-in'
        Write-Host "Startup (run at sign-in): $startupLink" -ForegroundColor Green
    }
    elseif (Test-Path $startupLink) {
        Remove-Item $startupLink -Force
        Write-Host 'Removed startup shortcut (re-run with -Startup to enable).' -ForegroundColor Yellow
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

$builtExe = Join-Path $buildOutput 'EarTrumpet.exe'
if (-not (Test-Path $builtExe)) {
    throw "Build failed: $builtExe was not created."
}

Write-Step "Installing to $InstallDir"
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

$targetExe = Join-Path $InstallDir $devExeName
Stop-DevInstance -InstallDirectory $InstallDir

Copy-Item -Path (Join-Path $buildOutput '*') -Destination $InstallDir -Recurse -Force

if (Test-Path $targetExe) {
    Remove-Item $targetExe -Force
}

Rename-Item -Path (Join-Path $InstallDir 'EarTrumpet.exe') -NewName $devExeName

$legacyLog = Join-Path $InstallDir 'eartrumpet-dev.log'
if (Test-Path $legacyLog) {
    Remove-Item $legacyLog -Force -ErrorAction SilentlyContinue
}

Write-Host "Installed: $targetExe" -ForegroundColor Green
Write-Host "Tray tooltip prefix: EarTrumpet Dev:" -ForegroundColor Green

if (-not $SkipShortcuts) {
    Write-Step 'Registering shortcuts'
    Register-DevShortcuts -InstallDirectory $InstallDir -ExePath $targetExe -AddStartup:$Startup
}

if (-not $SkipLaunch) {
    Write-Step 'Starting EarTrumpet Dev'
    Start-Process -FilePath $targetExe
}

Write-Host "`nDone." -ForegroundColor Yellow
Write-Host "  Rebuild:  .\scripts\build-and-install.ps1" -ForegroundColor Yellow
Write-Host "  Startup:  .\scripts\build-and-install.ps1 -Startup" -ForegroundColor Yellow
Write-Host "  Open app: Start > EarTrumpet Dev" -ForegroundColor Yellow
