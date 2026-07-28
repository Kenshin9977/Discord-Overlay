# Publishing Discord-Overlay to the Microsoft Store

The Store takes plain EXE installers, hosted by you. No MSIX, no repackaging, no
rewriting of the app. You submit a URL and Partner Center certifies the binary
behind it.

The whole thing is done by hand in Partner Center. There is a submission API, but
it is not worth wiring up for an app that ships a few times a year.

## What the Store requires, and where we already meet it

| Requirement | Status |
| --- | --- |
| `.msi` or `.exe` installer | `Discord-Overlay-store-Setup.exe`, from vpk |
| Every PE file signed, chaining to a CA in the Microsoft Trusted Root Program | Done by the release workflow, which refuses to publish otherwise |
| Versioned HTTPS URL, binary must never change after submission | GitHub release assets are immutable per tag |
| Standalone installer, not a downloader stub | Velopack bundles everything |
| Must install without showing any UI | Needs the switch below |

The signing requirement is about the trust chain, not about EV specifically. Your
Certum certificate satisfies it. EV buys SmartScreen reputation for the
direct download, which is a separate concern from Store acceptance.

## The values to enter

Create the product in Partner Center, reserve the name, then on **Packages**:

| Field | Value |
| --- | --- |
| App type | EXE |
| Package URL | `https://github.com/Kenshin9977/Discord-Overlay/releases/download/v<version>/Discord-Overlay-store-Setup.exe` |
| Architecture | x64 |
| Installer parameters | `--silent` |
| Languages | `en-us`, plus `fr-fr` if the app is translated |

`--silent` is the one field people miss. The Store runs your installer
unattended, and a setup that opens a window fails certification. Velopack's
Setup.exe shows a progress window by default, so the switch is not optional here.

### Installer handling (optional, worth doing)

Partner Center lets you map your installer's exit codes to the messages the Store
shows. Without it, any failure reads as a generic error. Velopack's Setup.exe
returns 0 on success and non-zero on failure, so at minimum map:

| Scenario | Return code |
| --- | --- |
| Installation successful | 0 |

## Every new version

Partner Center, **Update submission**, new versioned URL, submit. Certification
usually takes a day or two.

The version number comes from your installer, not from the Store: Store-side
version numbering is not supported for Win32 apps.

## Self-updating is already handled

Every release builds two channels from the same code:

| | Artifact | Updates |
| --- | --- | --- |
| `win` | `Discord-Overlay-win-Setup.exe` | the app updates itself from GitHub |
| `store` | `Discord-Overlay-store-Setup.exe` | none; the Store is the update channel |

Submit the `store` one. The `store` build has self-updating compiled out via
`StoreEdition=true`, which sets `STORE_EDITION`, and the tray menu drops its
"Check for updates" entry rather than showing a dead one.

This matters because the Store certifies one specific binary. An app that
replaces itself afterwards is running code the Store never reviewed, and its
listing ends up describing a version nobody is running.

The release workflow installs and uninstalls both channels on every run, so
neither can quietly stop being silent.

WinGet and Chocolatey keep getting the `win` build: someone installing with a
package manager expects the app to behave as it does when downloaded directly.

## Account

An individual developer account is free since June 2025. If you ever charge for
anything, non-gaming apps keep 100% when you use your own commerce platform, or
85% through Microsoft's.
