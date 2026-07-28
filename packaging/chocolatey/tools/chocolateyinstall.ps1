$ErrorActionPreference = 'Stop'

$packageName = 'discord-overlay'
$toolsDir    = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Injected at pack time by the release workflow from the artifact it just built,
# signed and verified. Left as placeholders in the repo so a stray copy of this
# file can never point at a stale download.
$url64      = '$url64$'
$checksum64 = '$checksum64$'

# The portable zip, not Setup.exe, and that is the whole reason this package
# looks different from the WinGet manifest next door.
#
# Velopack installs per user, into the profile of whoever runs it. Chocolatey
# runs elevated, so Setup.exe would install into the administrator's profile and
# then be invisible to the person who typed the command. The portable build has
# no such opinion: it lands in the Chocolatey library, machine-wide, where every
# account can reach it.
$packageArgs = @{
  packageName    = $packageName
  unzipLocation  = $toolsDir
  url64bit       = $url64
  checksum64     = $checksum64
  checksumType64 = 'sha256'
}

Install-ChocolateyZipPackage @packageArgs

# A tray app, not a console tool. Left unshimmed, `discord-overlay` on the command
# line would launch a window that takes no arguments, and Chocolatey would shim
# every dependency executable beside it too.
Get-ChildItem $toolsDir -Filter *.exe | ForEach-Object {
  New-Item "$($_.FullName).ignore" -ItemType File -Force | Out-Null
}

$exe = Join-Path $toolsDir 'DiscordOverlay.exe'

if (Test-Path $exe) {
  Install-ChocolateyShortcut `
    -ShortcutFilePath (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Discord-Overlay.lnk') `
    -TargetPath $exe `
    -Description 'Follows your Discord voice channel in the OBS overlay'
}

Write-Host 'Installed as a portable build, so it updates through Chocolatey rather than on its own.'
