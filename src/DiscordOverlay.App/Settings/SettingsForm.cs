using DiscordOverlay.App.Hosting;
using DiscordOverlay.App.Resources;
using DiscordOverlay.Core;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Streaming;

namespace DiscordOverlay.App.Settings;

public sealed class SettingsForm : Form
{
    private readonly IDiscordSession session;
    private readonly AutoStartManager autoStart;

    private readonly TextBox hostBox;
    private readonly NumericUpDown portBox;
    private readonly TextBox passwordBox;
    private readonly TextBox sourceNameBox;
    private readonly CheckBox autoStartCheckbox;
    private readonly Label discordStatus;
    private readonly Button signOutButton;
    private readonly Label statusLabel;

    public SettingsForm(IDiscordSession session, ObsConnectionOptions currentObs, AutoStartManager autoStart)
    {
        this.session = session;
        this.autoStart = autoStart;

        Text = Strings.SettingsWindowTitle;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 470);
        Font = new Font("Segoe UI", 9f);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // fall back to default
        }

        var discordHeader = new Label
        {
            Text = Strings.SettingsDiscordHeader,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 12),
        };

        discordStatus = new Label
        {
            Location = new Point(16, 40),
            Size = new Size(420, 36),
            AutoSize = false,
        };

        signOutButton = new Button
        {
            Text = Strings.SettingsSignOutButton,
            Location = new Point(16, 80),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        signOutButton.Click += async (_, _) => await OnSignOutAsync();

        var obsHeader = new Label
        {
            Text = Strings.SettingsObsHeader,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 130),
        };

        var hint = new Label
        {
            AutoSize = false,
            Size = new Size(430, 30),
            Location = new Point(16, 158),
            ForeColor = SystemColors.GrayText,
            Text = Strings.SettingsObsHint,
        };

        var hostLabel = new Label { Text = Strings.SettingsHostLabel, Location = new Point(16, 200), AutoSize = true };
        hostBox = new TextBox { Location = new Point(140, 197), Size = new Size(300, 23), Text = currentObs.Hostname };

        var portLabel = new Label { Text = Strings.SettingsPortLabel, Location = new Point(16, 230), AutoSize = true };
        portBox = new NumericUpDown
        {
            Location = new Point(140, 227),
            Size = new Size(100, 23),
            Minimum = 1,
            Maximum = 65535,
            Value = currentObs.Port,
        };

        var passwordLabel = new Label { Text = Strings.SettingsPasswordLabel, Location = new Point(16, 260), AutoSize = true };
        passwordBox = new TextBox
        {
            Location = new Point(140, 257),
            Size = new Size(300, 23),
            UseSystemPasswordChar = true,
            Text = currentObs.Password,
        };

        var sourceLabel = new Label { Text = Strings.SettingsBrowserSourceLabel, Location = new Point(16, 290), AutoSize = true };
        sourceNameBox = new TextBox
        {
            Location = new Point(140, 287),
            Size = new Size(300, 23),
            Text = currentObs.BrowserSourceName,
        };

        var startupHeader = new Label
        {
            Text = Strings.SettingsStartupHeader,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 326),
        };

        autoStartCheckbox = new CheckBox
        {
            Text = Strings.SettingsAutoStartCheckbox,
            Location = new Point(16, 354),
            AutoSize = true,
            Checked = autoStart.IsEnabled,
        };

        statusLabel = new Label
        {
            AutoSize = false,
            Size = new Size(420, 24),
            Location = new Point(16, 388),
            ForeColor = SystemColors.GrayText,
        };

        var saveButton = new Button
        {
            Text = Strings.SettingsSaveButton,
            Location = new Point(268, 420),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
            DialogResult = DialogResult.OK,
        };
        saveButton.Click += async (_, _) => await OnSaveAsync();

        var cancelButton = new Button
        {
            Text = Strings.SettingsCancelButton,
            Location = new Point(368, 420),
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };
        AcceptButton = saveButton;
        CancelButton = cancelButton;

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
            startupHeader,
            autoStartCheckbox,
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
            discordStatus.Text = Strings.SettingsSignedIn(clientId);
            signOutButton.Enabled = true;
        }
        else
        {
            discordStatus.Text = Strings.SettingsNotSignedIn;
            signOutButton.Enabled = false;
        }
    }

    private async Task OnSignOutAsync()
    {
        var confirm = MessageBox.Show(
            this,
            Strings.SettingsSignOutPrompt,
            Strings.SettingsSignOutTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        signOutButton.Enabled = false;
        try
        {
            await session.SignOutAsync().ConfigureAwait(true);
            DialogResult = DialogResult.Abort;
            Close();
        }
        catch (Exception ex)
        {
            statusLabel.ForeColor = Color.Firebrick;
            statusLabel.Text = Strings.SettingsSignOutFailed(ex.Message);
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

            try
            {
                if (autoStartCheckbox.Checked)
                {
                    autoStart.Enable();
                }
                else
                {
                    autoStart.Disable();
                }
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Color.DarkOrange;
                statusLabel.Text = Strings.SettingsAutoStartFailed(ex.Message);
                return;
            }

            statusLabel.ForeColor = Color.SeaGreen;
            statusLabel.Text = Strings.SettingsSaveSuccess;

            await Task.Delay(800).ConfigureAwait(true);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            statusLabel.ForeColor = Color.Firebrick;
            statusLabel.Text = Strings.SettingsSaveFailed(ex.Message);
        }
    }
}
