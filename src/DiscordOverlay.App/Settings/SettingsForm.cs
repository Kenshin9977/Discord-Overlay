using System.Diagnostics;
using DiscordOverlay.App.Hosting;
using DiscordOverlay.App.Resources;
using DiscordOverlay.Core;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;

namespace DiscordOverlay.App.Settings;

public sealed class SettingsForm : Form
{
    private const string DiscordPortalUrl = "https://discord.com/developers/applications";
    private const string RedirectUri = DiscordOAuthCredentials.DefaultRedirectUri;

    private readonly IDiscordSession session;
    private readonly AutoStartManager autoStart;
    private readonly ObsConnectionTester obsTester;

    private readonly GroupBox discordGroup;
    private readonly Panel signedInPanel;
    private readonly Label signedInStatus;
    private readonly Button signOutButton;

    private readonly Panel signedOutPanel;
    private readonly TextBox clientIdBox;
    private readonly TextBox clientSecretBox;
    private readonly Button signInButton;
    private readonly Label discordStatus;

    private readonly TextBox hostBox;
    private readonly NumericUpDown portBox;
    private readonly TextBox passwordBox;
    private readonly TextBox sourceNameBox;
    private readonly Button obsTestButton;
    private readonly Label obsStatus;

    private readonly CheckBox autoStartCheckbox;
    private readonly Button saveButton;
    private readonly Label statusLabel;

    public SettingsForm(
        IDiscordSession session,
        ObsConnectionOptions currentObs,
        AutoStartManager autoStart,
        ObsConnectionTester obsTester)
    {
        this.session = session;
        this.autoStart = autoStart;
        this.obsTester = obsTester;

        Text = Strings.SettingsWindowTitle;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(580, 760);
        Font = new Font("Segoe UI", 9f);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // fall back to default
        }

        discordGroup = new GroupBox
        {
            Text = Strings.SettingsDiscordHeader,
            Location = new Point(16, 8),
            Size = new Size(548, 380),
        };

        // Signed-out sub-panel: portal/copy buttons, client id/secret, sign-in.
        signedOutPanel = new Panel
        {
            Location = new Point(8, 20),
            Size = new Size(532, 352),
            Visible = false,
        };

        var instructions = new Label
        {
            AutoSize = false,
            Size = new Size(516, 140),
            Location = new Point(8, 4),
            Text = Strings.Format(nameof(Strings.WizardDiscordInstructions), RedirectUri),
        };

        var openPortalButton = new Button
        {
            Text = Strings.WizardDiscordOpenPortalButton,
            Location = new Point(8, 152),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        openPortalButton.Click += (_, _) => OpenInBrowser(DiscordPortalUrl);

        var copyRedirectButton = new Button
        {
            Text = Strings.WizardDiscordCopyRedirectButton,
            Location = new Point(232, 152),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        copyRedirectButton.Click += (_, _) =>
        {
            Clipboard.SetText(RedirectUri);
            discordStatus!.ForeColor = SystemColors.ControlText;
            discordStatus.Text = Strings.WizardDiscordRedirectCopied(RedirectUri);
        };

        var clientIdLabel = new Label
        {
            Text = Strings.WizardDiscordClientIdLabel,
            Location = new Point(8, 196),
            AutoSize = true,
        };
        clientIdBox = new TextBox
        {
            Location = new Point(108, 193),
            Size = new Size(416, 23),
        };

        var clientSecretLabel = new Label
        {
            Text = Strings.WizardDiscordClientSecretLabel,
            Location = new Point(8, 226),
            AutoSize = true,
        };
        clientSecretBox = new TextBox
        {
            Location = new Point(108, 223),
            Size = new Size(416, 23),
            UseSystemPasswordChar = true,
        };

        signInButton = new Button
        {
            Text = Strings.WizardDiscordTestSaveButton,
            Location = new Point(8, 258),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
        };
        signInButton.Click += async (_, _) => await OnSignInAsync().ConfigureAwait(true);

        discordStatus = new Label
        {
            AutoSize = false,
            Size = new Size(516, 56),
            Location = new Point(8, 292),
            ForeColor = SystemColors.ControlText,
        };

        signedOutPanel.Controls.AddRange(new Control[]
        {
            instructions,
            openPortalButton,
            copyRedirectButton,
            clientIdLabel, clientIdBox,
            clientSecretLabel, clientSecretBox,
            signInButton,
            discordStatus,
        });

        // Signed-in sub-panel: status + sign-out.
        signedInPanel = new Panel
        {
            Location = new Point(8, 20),
            Size = new Size(532, 352),
            Visible = false,
        };

        signedInStatus = new Label
        {
            AutoSize = false,
            Size = new Size(516, 36),
            Location = new Point(8, 8),
        };
        signOutButton = new Button
        {
            Text = Strings.SettingsSignOutButton,
            Location = new Point(8, 52),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        signOutButton.Click += async (_, _) => await OnSignOutAsync().ConfigureAwait(true);

        signedInPanel.Controls.AddRange(new Control[] { signedInStatus, signOutButton });
        discordGroup.Controls.AddRange(new Control[] { signedOutPanel, signedInPanel });

        var obsGroup = new GroupBox
        {
            Text = Strings.SettingsObsHeader,
            Location = new Point(16, 396),
            Size = new Size(548, 230),
        };

        var obsHint = new Label
        {
            AutoSize = false,
            Size = new Size(528, 32),
            Location = new Point(10, 18),
            ForeColor = SystemColors.GrayText,
            Text = Strings.SettingsObsHint,
        };

        var hostLabel = new Label { Text = Strings.SettingsHostLabel, Location = new Point(10, 60), AutoSize = true };
        hostBox = new TextBox { Location = new Point(140, 57), Size = new Size(396, 23), Text = currentObs.Hostname };

        var portLabel = new Label { Text = Strings.SettingsPortLabel, Location = new Point(10, 90), AutoSize = true };
        portBox = new NumericUpDown
        {
            Location = new Point(140, 87),
            Size = new Size(120, 23),
            Minimum = 1,
            Maximum = 65535,
            Value = currentObs.Port,
        };

        var passwordLabel = new Label { Text = Strings.SettingsPasswordLabel, Location = new Point(10, 120), AutoSize = true };
        passwordBox = new TextBox
        {
            Location = new Point(140, 117),
            Size = new Size(396, 23),
            UseSystemPasswordChar = true,
            Text = currentObs.Password,
        };

        var sourceLabel = new Label { Text = Strings.SettingsBrowserSourceLabel, Location = new Point(10, 150), AutoSize = true };
        sourceNameBox = new TextBox
        {
            Location = new Point(140, 147),
            Size = new Size(396, 23),
            Text = currentObs.BrowserSourceName,
        };

        obsTestButton = new Button
        {
            Text = Strings.WizardObsTestButton,
            Location = new Point(10, 184),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
        };
        obsTestButton.Click += async (_, _) => await OnTestObsAsync().ConfigureAwait(true);

        obsStatus = new Label
        {
            AutoSize = false,
            Size = new Size(386, 32),
            Location = new Point(150, 188),
            ForeColor = SystemColors.GrayText,
        };

        obsGroup.Controls.AddRange(new Control[]
        {
            obsHint,
            hostLabel, hostBox,
            portLabel, portBox,
            passwordLabel, passwordBox,
            sourceLabel, sourceNameBox,
            obsTestButton, obsStatus,
        });

        var startupGroup = new GroupBox
        {
            Text = Strings.SettingsStartupHeader,
            Location = new Point(16, 634),
            Size = new Size(548, 60),
        };
        autoStartCheckbox = new CheckBox
        {
            Text = Strings.SettingsAutoStartCheckbox,
            Location = new Point(10, 26),
            AutoSize = true,
            Checked = autoStart.IsEnabled,
        };
        startupGroup.Controls.Add(autoStartCheckbox);

        statusLabel = new Label
        {
            AutoSize = false,
            Size = new Size(548, 22),
            Location = new Point(16, 700),
            ForeColor = SystemColors.GrayText,
        };

        saveButton = new Button
        {
            Text = Strings.SettingsSaveButton,
            Location = new Point(388, 724),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
        };
        saveButton.Click += async (_, _) => await OnSaveAsync().ConfigureAwait(true);

        var cancelButton = new Button
        {
            Text = Strings.SettingsCancelButton,
            Location = new Point(488, 724),
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.AddRange(new Control[]
        {
            discordGroup,
            obsGroup,
            startupGroup,
            statusLabel,
            saveButton,
            cancelButton,
        });

        ApplyDiscordState();
    }

    private void ApplyDiscordState()
    {
        var bundle = session.Current;
        if (bundle is not null)
        {
            signedInStatus.Text = Strings.SettingsSignedIn(bundle.ClientId);
            signedInPanel.Visible = true;
            signedOutPanel.Visible = false;
            saveButton.Enabled = true;
            obsTestButton.Enabled = true;
            hostBox.Enabled = portBox.Enabled = passwordBox.Enabled = sourceNameBox.Enabled = true;
        }
        else
        {
            signedOutPanel.Visible = true;
            signedInPanel.Visible = false;
            saveButton.Enabled = false;
            obsTestButton.Enabled = false;
            // OBS fields stay enabled so the user can prefill values, but
            // they won't be persisted until Discord is connected and the
            // user clicks Save.
            hostBox.Enabled = portBox.Enabled = passwordBox.Enabled = sourceNameBox.Enabled = true;
        }
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // best-effort
        }
    }

    private async Task OnSignInAsync()
    {
        var clientId = clientIdBox.Text.Trim();
        var clientSecret = clientSecretBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            SetDiscordStatus(Strings.WizardDiscordFieldsRequired, error: true);
            return;
        }

        SetSignInBusy(true);
        SetDiscordStatus(Strings.WizardDiscordConnecting, error: false);

        try
        {
            await session.SetupAsync(clientId, clientSecret).ConfigureAwait(true);
            ApplyDiscordState();
            statusLabel.ForeColor = Color.SeaGreen;
            statusLabel.Text = Strings.SettingsSaveSuccess;
        }
        catch (DiscordRpcException ex)
        {
            SetDiscordStatus(Strings.WizardDiscordRpcError(ex.Message), error: true);
        }
        catch (DiscordOAuthException ex)
        {
            SetDiscordStatus(Strings.WizardDiscordOAuthError(ex.Message), error: true);
        }
        catch (Exception ex)
        {
            SetDiscordStatus(Strings.WizardDiscordSetupFailed(ex.Message), error: true);
        }
        finally
        {
            SetSignInBusy(false);
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

    private async Task OnTestObsAsync()
    {
        var host = hostBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            SetObsStatus(Strings.WizardObsHostRequired, error: true);
            return;
        }

        obsTestButton.Enabled = false;
        SetObsStatus(Strings.WizardObsTesting, error: false);
        try
        {
            var result = await obsTester.TestAsync(host, (int)portBox.Value, passwordBox.Text).ConfigureAwait(true);
            if (result.IsSuccess)
            {
                SetObsStatus(Strings.WizardObsTestSuccess, error: false);
            }
            else
            {
                SetObsStatus(result.ErrorMessage ?? Strings.WizardObsHostRequired, error: true);
            }
        }
        finally
        {
            obsTestButton.Enabled = session.Current is not null;
        }
    }

    private async Task OnSaveAsync()
    {
        if (session.Current is null)
        {
            // Save is disabled in this state, but guard anyway.
            return;
        }

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

            await Task.Delay(600).ConfigureAwait(true);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            statusLabel.ForeColor = Color.Firebrick;
            statusLabel.Text = Strings.SettingsSaveFailed(ex.Message);
        }
    }

    private void SetSignInBusy(bool busy)
    {
        signInButton.Enabled = !busy;
        clientIdBox.Enabled = !busy;
        clientSecretBox.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void SetDiscordStatus(string text, bool error)
    {
        discordStatus.Text = text;
        discordStatus.ForeColor = error ? Color.Firebrick : Color.SeaGreen;
    }

    private void SetObsStatus(string text, bool error)
    {
        obsStatus.Text = text;
        obsStatus.ForeColor = error ? Color.Firebrick : Color.SeaGreen;
    }
}
