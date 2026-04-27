using DiscordOverlay.Core;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Streaming;

namespace DiscordOverlay.App.Settings;

public sealed class SettingsForm : Form
{
    private readonly IDiscordSession session;

    private readonly TextBox hostBox;
    private readonly NumericUpDown portBox;
    private readonly TextBox passwordBox;
    private readonly TextBox sourceNameBox;
    private readonly Label discordStatus;
    private readonly Button signOutButton;
    private readonly Label statusLabel;

    public SettingsForm(IDiscordSession session, ObsConnectionOptions currentObs)
    {
        this.session = session;

        Text = "Discord-Overlay — Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(440, 400);
        Font = new Font("Segoe UI", 9f);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // fall back to default
        }

        // ----- Discord section -----
        var discordHeader = new Label
        {
            Text = "Discord",
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 12),
        };

        discordStatus = new Label
        {
            Location = new Point(16, 40),
            Size = new Size(380, 36),
            AutoSize = false,
        };

        signOutButton = new Button
        {
            Text = "Sign out and reset credentials",
            Location = new Point(16, 80),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        signOutButton.Click += async (_, _) => await OnSignOutAsync();

        // ----- OBS section -----
        var obsHeader = new Label
        {
            Text = "OBS WebSocket",
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 130),
        };

        var hint = new Label
        {
            AutoSize = false,
            Size = new Size(400, 30),
            Location = new Point(16, 158),
            ForeColor = SystemColors.GrayText,
            Text = "In OBS: Tools → WebSocket Server Settings → Enable. Add a Browser Source named below.",
        };

        var hostLabel = new Label { Text = "Host:", Location = new Point(16, 200), AutoSize = true };
        hostBox = new TextBox { Location = new Point(140, 197), Size = new Size(280, 23), Text = currentObs.Hostname };

        var portLabel = new Label { Text = "Port:", Location = new Point(16, 230), AutoSize = true };
        portBox = new NumericUpDown
        {
            Location = new Point(140, 227),
            Size = new Size(100, 23),
            Minimum = 1,
            Maximum = 65535,
            Value = currentObs.Port,
        };

        var passwordLabel = new Label { Text = "Password:", Location = new Point(16, 260), AutoSize = true };
        passwordBox = new TextBox
        {
            Location = new Point(140, 257),
            Size = new Size(280, 23),
            UseSystemPasswordChar = true,
            Text = currentObs.Password,
        };

        var sourceLabel = new Label { Text = "Browser source:", Location = new Point(16, 290), AutoSize = true };
        sourceNameBox = new TextBox
        {
            Location = new Point(140, 287),
            Size = new Size(280, 23),
            Text = currentObs.BrowserSourceName,
        };

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(248, 350),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
            DialogResult = DialogResult.OK,
        };
        saveButton.Click += async (_, _) => await OnSaveAsync();

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(348, 350),
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        statusLabel = new Label
        {
            AutoSize = false,
            Size = new Size(400, 24),
            Location = new Point(16, 320),
            ForeColor = SystemColors.GrayText,
        };

        Controls.AddRange(new Control[]
        {
            discordHeader,
            discordStatus,
            signOutButton,
            obsHeader,
            hint,
            hostLabel, hostBox,
            portLabel, portBox,
            passwordLabel, passwordBox,
            sourceLabel, sourceNameBox,
            statusLabel,
            saveButton,
            cancelButton,
        });

        UpdateDiscordStatus();
    }

    private void UpdateDiscordStatus()
    {
        if (session.Current is { ClientId: var clientId })
        {
            discordStatus.Text = $"Signed in via your developer app (client {clientId}).";
            signOutButton.Enabled = true;
        }
        else
        {
            discordStatus.Text = "Not signed in. Run the setup wizard to connect to Discord.";
            signOutButton.Enabled = false;
        }
    }

    private async Task OnSignOutAsync()
    {
        var confirm = MessageBox.Show(
            this,
            "Sign out and clear stored credentials? The app will exit; relaunch it to run setup again.",
            "Sign out",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        signOutButton.Enabled = false;
        try
        {
            await session.SignOutAsync().ConfigureAwait(true);
            DialogResult = DialogResult.Abort; // signals to caller "sign-out happened"
            Close();
        }
        catch (Exception ex)
        {
            statusLabel.ForeColor = Color.Firebrick;
            statusLabel.Text = $"Sign out failed: {ex.Message}";
            signOutButton.Enabled = true;
        }
    }

    private async Task OnSaveAsync()
    {
        try
        {
            var existing = await AppConfigStore.LoadAsync().ConfigureAwait(true);
            existing.Obs.Hostname = hostBox.Text.Trim();
            existing.Obs.Port = (int)portBox.Value;
            existing.Obs.Password = passwordBox.Text;
            existing.Obs.BrowserSourceName = sourceNameBox.Text.Trim();

            await AppConfigStore.SaveAsync(existing).ConfigureAwait(true);

            statusLabel.ForeColor = Color.SeaGreen;
            statusLabel.Text = "Saved. Some changes (host/port/password) take effect after restart.";

            // Close after a short delay so the user sees the confirmation.
            await Task.Delay(800).ConfigureAwait(true);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            statusLabel.ForeColor = Color.Firebrick;
            statusLabel.Text = $"Save failed: {ex.Message}";
        }
    }
}
