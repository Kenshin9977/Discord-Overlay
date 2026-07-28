# Publishing Discord-Overlay to the Microsoft Store

The Store takes plain EXE installers, hosted by you. No MSIX, no repackaging, no
rewriting of the app. You submit a URL and Partner Center certifies the binary
behind it.

The whole thing is done by hand in Partner Center. There is a submission API, but
it is not worth wiring up for an app that ships a few times a year.

## What the Store requires, and where we already meet it

| Requirement | Status |
| --- | --- |
| `.msi` or `.exe` installer | `Discord-Overlay-win-Setup.exe`, from vpk |
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
| Package URL | `https://github.com/Kenshin9977/Discord-Overlay/releases/download/v<version>/Discord-Overlay-win-Setup.exe` |
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

## The one thing to decide first

Discord-Overlay updates itself through Velopack. A Store install would then move to a
version the Store did not certify and does not know about, which is the opposite
of what a Store listing promises its users.

Two honest options:

1. **Leave self-updating on.** Simplest, and what most Win32 Store apps do in
   practice. The Store listing drifts behind the installed version.
2. **Build a Store edition with the updater disabled**, and let the Store be the
   update channel. Cleaner, and it costs one build flag plus one more artifact in
   the release, the same way usbscope already splits its two editions.

Option 2 is the right one if the Store ever becomes a meaningful share of
installs. Until then option 1 is defensible, as long as it is a decision rather
than an oversight.

## Account

An individual developer account is free since June 2025. If you ever charge for
anything, non-gaming apps keep 100% when you use your own commerce platform, or
85% through Microsoft's.
