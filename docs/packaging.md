# Publishing Discord-Overlay to WinGet and Chocolatey

The two channels get different artifacts, and the reason is worth knowing before
you change either.

Velopack installs **per user**, into the profile of whoever runs Setup.exe, and
updates itself from there. Chocolatey runs **elevated**. Put those together and
`choco install` would install into the administrator's profile, where the person
who typed the command cannot see it.

So:

| | Artifact | Scope | Who updates it |
| --- | --- | --- | --- |
| WinGet | `Discord-Overlay-win-Setup.exe` | user (declared in the manifest) | the app itself |
| Chocolatey | `Discord-Overlay-win-Portable.zip` | machine | `choco upgrade` |

Both are legitimate installs. The Chocolatey one trades self-updating for
working the way a Chocolatey user expects.

---

## The licence

MIT, in `LICENSE` at the repository root. Both channels need it: without a
licence, redistribution is not permitted by default, which is exactly what they
do. WinGet requires the `License` field and Chocolatey moderators check the
`licenseUrl` resolves.

If the licence ever changes, three places have to change with it: `LICENSE`, the
`License:` line in `packaging/winget/*.locale.en-US.yaml`, and `licenseUrl` in
the nuspec. A manifest claiming a licence the repository does not carry is the
kind of thing moderation catches.

---

## Chocolatey

### One time

1. Create an account at <https://community.chocolatey.org/account/Register>.
2. Copy your API key from <https://community.chocolatey.org/account>.
3. Add it as the repository secret `CHOCOLATEY_API_KEY`.

### Every release

Automatic. The `chocolatey` job fills the version, URL and SHA-256 of the
artifact the release job just signed and verified, downloads it to confirm the
checksum matches reality, packs and pushes.

Without the secret the job still packs and only skips the push, so a broken
package fails the run before you have an API key rather than after.

### The first version is moderated

Expect a few days and expect comments. What moderators check, and where it is
handled:

| What they check | Where |
| --- | --- |
| No embedded binaries without a `VERIFICATION.txt` | `tools/VERIFICATION.txt`; nothing is embedded |
| A checksum on every download | `checksum64`, injected at pack time |
| Uninstall actually uninstalls | `chocolateyuninstall.ps1` stops the tray process first |
| `licenseUrl` present and real | the nuspec, once the LICENSE exists |

A rejected version number cannot be reused.

### Testing locally

```powershell
choco pack packaging/chocolatey/discord-overlay.nuspec --out packaging/chocolatey
choco install discord-overlay --source packaging/chocolatey --version <version> -y
choco uninstall discord-overlay -y
```

Substitute the placeholders in `chocolateyinstall.ps1` first, or it will try to
download from a literal `$url64$`.

---

## WinGet

WinGet has no publisher account. Every version is a pull request against
`microsoft/winget-pkgs`.

### The first submission is manual

The automated step uses `wingetcreate update`, which needs an existing manifest.
Create the first one:

1. Fork <https://github.com/microsoft/winget-pkgs>.

2. Fill in the three manifests in `packaging/winget/`, replacing every
   `<FILL:...>`:

   - `<FILL:VERSION>` the release version
   - `<FILL:URL>` the release asset URL of `Discord-Overlay-win-Setup.exe`
   - `<FILL:SHA256>` its SHA-256, uppercase:
     `(Get-FileHash .\Discord-Overlay-win-Setup.exe -Algorithm SHA256).Hash`

3. Validate and, more importantly, actually install from it:

   ```powershell
   winget validate --manifest packaging\winget
   winget install --manifest packaging\winget
   ```

   `winget validate` only checks the schema. `winget install --manifest` is what
   proves the silent switch and the scope are right. A manifest that validates
   and does not install silently is the usual reason a first PR bounces.

4. Copy them into your fork under
   `manifests/k/Kenshin9977/DiscordOverlay/<version>/` and open a pull request.
   The path is derived from `PackageIdentifier`, not from the repository
   name: `Kenshin9977.DiscordOverlay` means `Kenshin9977/DiscordOverlay`,
   with no hyphen. The validation bot rejects a mismatch.

### Every release after that

Automatic once `WINGET_TOKEN` is set.

Create a **classic** personal access token with the `public_repo` scope at
<https://github.com/settings/tokens> and add it as the repository secret
`WINGET_TOKEN`. A fine-grained token will not do: `wingetcreate` has to fork and
push, and fine-grained tokens cannot fork.

Without the secret the job logs what it would have submitted and exits clean, so
releases keep working while you sort the token out.

### One thing to watch

Velopack updates the app in place. WinGet will then believe the installed
version is the one it installed, and may offer an "upgrade" to a version already
running. This is normal for self-updating apps and is why `AppsAndFeaturesEntries`
is declared: it at least lets WinGet find the install rather than offering it
again as new.

### The identifier

`Kenshin9977.DiscordOverlay` is permanent once merged. `Moniker: discord-overlay` is
what people type and does not have to be unique.
