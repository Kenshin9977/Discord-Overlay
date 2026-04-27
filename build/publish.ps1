<#
.SYNOPSIS
Publishes Discord-Overlay as a self-contained single-file Windows executable.

.DESCRIPTION
Produces publish/win-x64/DiscordOverlay.exe that bundles the .NET 10 runtime,
so end users do not need to install .NET separately. Uses ReadyToRun for
faster startup and SingleFile compression for a smaller binary.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $RuntimeIdentifier = 'win-x64',
    [switch] $NoCompress
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot 'src/DiscordOverlay.App/DiscordOverlay.App.csproj'
$outDir = Join-Path $repoRoot "publish/$RuntimeIdentifier"

if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

# Prefer the user-scoped .NET 10 SDK if installed there, fall back to PATH.
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = 'dotnet'
}

$publishArgs = @(
    'publish', $proj,
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    '--self-contained',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishReadyToRun=true',
    '-p:DebugType=embedded',
    '-o', $outDir
)
if (-not $NoCompress) {
    $publishArgs += '-p:EnableCompressionInSingleFile=true'
}

Write-Host "Publishing $proj -> $outDir ($RuntimeIdentifier, $Configuration)" -ForegroundColor Cyan
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $outDir 'DiscordOverlay.exe'
if (-not (Test-Path $exe)) {
    throw "Expected output not found: $exe"
}

$size = (Get-Item $exe).Length
Write-Host ""
Write-Host ("Published: {0} ({1:N1} MB)" -f $exe, ($size / 1MB)) -ForegroundColor Green
