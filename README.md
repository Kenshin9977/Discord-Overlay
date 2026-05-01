# Discord-Overlay

A Windows tray app that automatically keeps your **OBS Discord StreamKit** voice
overlay pointed at the voice channel you're currently in. Switch servers or
rooms mid-stream and the overlay follows you, no manual URL editing required.

> Reborn project. The previous DirectX 11 capture-window approach has been
> retired in favor of a tray-app + OBS WebSocket design inspired by
> [voice-channel-grabber](https://github.com/dichternebel/voice-channel-grabber),
> with a focus on a friction-free install and one bug fix:
> **forced channel moves are now detected** (when a moderator drags you into
> another channel, the overlay updates correctly).

## How it works

1. The app talks to your local Discord client over its IPC pipe and listens for
   the voice channel you're in. If Discord restarts the IPC reconnects
   automatically with exponential backoff.
2. When the channel changes, it builds the matching Discord StreamKit URL and
   pushes it to OBS via the obs-websocket protocol, directly updating your
   Browser Source's URL.
3. A polling safety net catches the cases where Discord doesn't fire the
   channel-select event (e.g. moderator-initiated moves).

No browser tabs, no OBS plugins, no manual edits.

## Install

1. Download `Discord-Overlay-win-Setup.exe` from the
   [Releases](https://github.com/Kenshin9977/Discord-Overlay/releases) page.
2. Run it. It installs to `%LocalAppData%\Discord-Overlay` (no admin needed)
   and launches into the system tray.
3. The .NET 10 runtime is bundled, no separate install.

## OBS preparation (do this once before launching the app)

1. **OBS** then **Tools** then **WebSocket Server Settings**, tick **Enable
   WebSocket server**. Click **Show Connect Info** and keep that window open:
   you'll need the password during setup.
2. In your scene, add a **Browser Source** named exactly **`Discord-Overlay`**.
   Width/height as you like (e.g. 350x500). Leave the URL empty, the app fills
   it in.

The Browser Source must exist in your current scene collection before the
app tries to push: if it's missing, OBS hangs the request and the URL never
gets updated.

## First-run setup

The app launches a one-time wizard with two steps (about 30 seconds total).

### Step 1: Discord developer app

1. Click **Open Discord developer portal**.
2. **New Application**, name it (e.g. `Discord-Overlay`), create.
3. **OAuth2** then **Redirects**, add this exact URI (the wizard's
   *Copy redirect URI* button copies it for you):
   ```
   http://localhost:3000/callback
   ```
4. Copy your **Client ID** and **Client Secret** into the wizard.
5. Click **Test & save**. Discord pops up a consent dialog inside the
   Discord client itself; click **Authorize**.

Credentials are stored encrypted via Windows DPAPI scoped to your user
account in `%LocalAppData%\DiscordOverlay\credentials.bin`.

### Step 2: OBS connection

1. Confirm host (`localhost`) and port (`4455`).
2. Paste the OBS WebSocket password from the OBS preparation step.
3. Confirm the Browser Source name matches what you created in OBS
   (default `Discord-Overlay`).
4. Click **Test connection**. A green status means OBS accepted the password.
5. Click **Save & finish**. The app restarts automatically to apply OBS
   settings.

The wizard only runs the first time, or whenever OBS settings are missing
from `settings.json`.

## Tray menu

- **Channel:** the voice channel currently in scope.
- **OBS:** the WebSocket connection state.
- **Settings...**: Discord sign-out, OBS connection, auto-start.
- **Check for updates**: pulls the latest Velopack release from GitHub and
  restarts to apply.
- **Open log folder**: `%LocalAppData%\DiscordOverlay\logs\` (daily rolling,
  14-day retention).
- **Quit**: clean shutdown.

## Auto-start

In **Settings...** check **Start with Windows (silently in tray)**. This
writes an `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry pointing
at the current installed binary. Unchecking removes the entry.

## Settings file

Non-secret settings live in `%LocalAppData%\DiscordOverlay\settings.json`
(the Settings dialog is what writes it; you can hand-edit if you like).
Schema:

```json
{
  "Obs": {
    "Hostname": "localhost",
    "Port": 4455,
    "Password": "...",
    "BrowserSourceName": "Discord-Overlay",
    "AutoReconnect": true
  },
  "Watcher": {
    "PollInterval": "00:00:05",
    "RefreshTimeout": "00:00:05"
  },
  "Streamkit": {
    "ShowIcon": true,
    "OnlineOnly": true,
    "Logo": "white",
    "TextColor": "#ffffff",
    "TextSize": 14,
    "BackgroundColor": "#1e2124",
    "BackgroundOpacity": 0,
    "LimitSpeaking": false,
    "SmallAvatars": false,
    "HideNames": false
  },
  "Update": {
    "GitHubRepository": "https://github.com/Kenshin9977/Discord-Overlay"
  }
}
```

Host, port, password and Browser Source name changes take effect after an
app restart since the OBS WebSocket connection is established once at
startup. StreamKit overlay options pick up live.

## Troubleshooting

- **"OBS: Disconnected" in the tray.** Confirm OBS is running and the
  WebSocket server is enabled (Tools, WebSocket Server Settings, Enable
  WebSocket server). Check the password in Settings matches.
- **Browser Source stays blank or stuck on `about:blank`.** The Browser
  Source named in Settings (default `Discord-Overlay`) must exist in your
  current OBS scene collection. If you create or rename it after launch,
  restart the app.
- **"Channel: Not in voice" but you are in a channel.** Verify Discord is
  running. Discord IPC reconnects automatically with backoff after Discord
  restarts; check the log (tray then Open log folder, search for
  `Discord IPC reconnect`) if it stays stuck.
- **Setup wizard says "Discord said no".** The Discord client probably
  wasn't running, or you denied the consent popup. Make sure Discord is
  open and retry.
- **Setup wizard says "OAuth token exchange failed: invalid_grant".** The
  redirect URI in the Discord developer portal must be exactly
  `http://localhost:3000/callback`. Case and trailing slash matter.
- **Channel changes lag a few seconds.** That's the safety-net polling
  interval (5 s by default). Lower it under `Watcher.PollInterval` if you
  want faster catch-up at the cost of a tiny bit more IPC traffic.
- **Logs:** tray menu, Open log folder.

## Building from source

Prerequisites:
- Windows 10 or later.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
  A `global.json` pins the exact version.
- For Velopack packaging only: the .NET 9 runtime
  (`Microsoft.NETCore.App` and `Microsoft.AspNetCore.App`). The `vpk` CLI
  tool targets net9.0.

```powershell
git clone https://github.com/Kenshin9977/Discord-Overlay
cd Discord-Overlay
dotnet build
dotnet test

# Self-contained single-file exe at publish/win-x64/DiscordOverlay.exe
.\build\publish.ps1

# Same plus a Velopack Setup.exe under Releases/
.\build\publish.ps1 -Pack -PackVersion 0.1.0
```

## Project layout

```
src/
  DiscordOverlay.App/             WinForms tray app (entry, UI, hosting)
    Hosting/                      Generic Host glue, tray, dispatcher,
                                  AutoStartManager, AppUpdater
    Setup/                        Two-step setup wizard (Discord, OBS)
    Settings/                     Settings dialog
  DiscordOverlay.Core/            UI-free library
    Auth/                         OAuth flow, DPAPI store, DiscordSession
                                  (with IPC auto-reconnect)
    Discord/                      IPC client, voice channel watcher
    Streaming/                    StreamKit URL builder, OBS updater,
                                  OBS connection tester
tests/
  DiscordOverlay.Core.Tests/      xUnit tests
build/
  publish.ps1                     dotnet publish + vpk pack
```

## Tech stack

- **.NET 10 LTS** with C# `latest`, nullable + implicit usings on, central
  package management.
- **WinForms** for tray + dialogs (BCL, no extra UI framework dep).
- **Microsoft.Extensions.Hosting** Generic Host with DI, options, and
  background services.
- **Serilog** with daily rolling file sink + Debug sink.
- **System.Text.Json** with source generators eligible.
- **System.IO.Pipes** (BCL) for Discord IPC named-pipe transport.
- **System.Security.Cryptography.ProtectedData** (DPAPI) for credential
  encryption.
- **OBSClient** (tinod) for obs-websocket v5 communication.
- **Velopack** for the installer and auto-update channel.

## Why not just use the Discord StreamKit URL directly?

StreamKit URLs hard-code `guild_id` / `channel_id`. The moment you switch
servers or voice channels, the overlay points at the wrong place. This app
is the small bit of glue that updates the URL automatically.

## Why not the previous DirectX 11 capture approach?

The original Discord-Overlay rendered an invisible D3D11 window so Discord
would draw its native overlay onto it for OBS to capture. That worked but
was sensitive to Discord overlay updates and screen scaling. The StreamKit
+ OBS WebSocket approach is more robust, less code, and doesn't depend on
Discord's in-game overlay being available.

## License

MIT.
