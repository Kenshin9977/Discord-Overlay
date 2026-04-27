<#
.SYNOPSIS
Publishes Discord-Overlay as a self-contained single-file Windows
executable and (optionally) packs it with Velopack into a Setup.exe
plus delta updates under Releases/.

.DESCRIPTION
Two stages:
  1. dotnet publish: produces publish/<RID>/DiscordOverlay.exe with the
     .NET 10 runtime bundled, so end users do not need to install
     .NET. Compressed single-file with embedded PDBs.
  2. vpk pack: bundles step 1 into Releases/ as a Velopack-installable
     package (Setup.exe + main full release nupkg + delta).

Requires:
  - .NET 10 SDK (user-scoped at %LocalAppData%\Microsoft\dotnet, or
    on PATH).
  - For -Pack: .NET 9 runtime (vpk's TFM) and the local dotnet tool
    `vpk` (declared in dotnet-tools.json — restored automatically).
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $RuntimeIdentifier = 'win-x64',
    [string] $PackVersion = '0.1.0',
    [string] $PackId = 'Discord-Overlay',
    [string] $Channel = 'win',
    [switch] $NoCompress,
    [switch] $Pack
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot 'src/DiscordOverlay.App/DiscordOverlay.App.csproj'
$publishDir = Join-Path $repoRoot "publish/$RuntimeIdentifier"
$releasesDir = Join-Path $repoRoot 'Releases'
$icon = Join-Path $repoRoot 'Discord-Overlay.ico'

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

# Prefer the user-scoped .NET 10 SDK if installed there, fall back to PATH.
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = 'dotnet'
}

# Make the user-scoped install discoverable for sub-commands (vpk, etc.)
# without modifying the user's persistent PATH.
$env:DOTNET_ROOT = Split-Path -Parent $dotnet

# ---------- Stage 1: publish ----------
$publishArgs = @(
    'publish', $proj,
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    '--self-contained',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishReadyToRun=true',
    '-p:DebugType=embedded',
    '-o', $publishDir
)
if (-not $NoCompress) {
    $publishArgs += '-p:EnableCompressionInSingleFile=true'
}

Write-Host "Publishing $proj -> $publishDir ($RuntimeIdentifier, $Configuration)" -ForegroundColor Cyan
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $publishDir 'DiscordOverlay.exe'
if (-not (Test-Path $exe)) {
    throw "Expected publish output not found: $exe"
}

$size = (Get-Item $exe).Length
Write-Host ("Published: {0} ({1:N1} MB)" -f $exe, ($size / 1MB)) -ForegroundColor Green

# ---------- Stage 2: vpk pack ----------
if ($Pack) {
    Write-Host "Restoring local dotnet tools…" -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        & $dotnet tool restore
        if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed" }
    }
    finally {
        Pop-Location
    }

    $vpkArgs = @(
        'vpk', 'pack',
        '--packId', $PackId,
        '--packVersion', $PackVersion,
        '--packDir', $publishDir,
        '--mainExe', 'DiscordOverlay.exe',
        '--outputDir', $releasesDir,
        '--channel', $Channel,
        '-r', $RuntimeIdentifier
    )
    if (Test-Path $icon) {
        $vpkArgs += @('--icon', $icon)
    }

    Write-Host "Packing with vpk -> $releasesDir" -ForegroundColor Cyan
    & $dotnet @vpkArgs
    if ($LASTEXITCODE -ne 0) {
        throw "vpk pack failed with exit code $LASTEXITCODE"
    }

    $setup = Join-Path $releasesDir 'Discord-Overlay-win-Setup.exe'
    if (Test-Path $setup) {
        $setupSize = (Get-Item $setup).Length
        Write-Host ("Setup ready: {0} ({1:N1} MB)" -f $setup, ($setupSize / 1MB)) -ForegroundColor Green
    }
}
