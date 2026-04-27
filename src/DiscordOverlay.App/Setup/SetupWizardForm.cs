using System.Diagnostics;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Discord;

namespace DiscordOverlay.App.Setup;

public sealed class SetupWizardForm : Form
{
    private const string DiscordPortalUrl = "https://discord.com/developers/applications";
    private const string RedirectUri = DiscordOAuthCredentials.DefaultRedirectUri;

    private readonly IDiscordSession session;
    private readonly TextBox clientIdBox;
    private readonly TextBox clientSecretBox;
    private readonly Button testButton;
    private readonly Label statusLabel;
    private readonly Button cancelButton;

    public SetupWizardForm(IDiscordSession session)
    {
        this.session = session;

        Text = "Discord-Overlay — Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(560, 460);
        Font = new Font("Segoe UI", 9f);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // fall back to default
        }

        var title = new Label
        {
            Text = "One-time Discord setup",
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16),
        };

        var instructions = new Label
        {
            AutoSize = false,
            Size = new Size(520, 180),
            Location = new Point(20, 50),
            Text =
                "Discord-Overlay needs its own Discord developer application. This is a one-time " +
                "setup that takes about 30 seconds.\r\n\r\n" +
                "1. Click \"Open Discord developer portal\" below.\r\n" +
                "2. Click \"New Application\", name it (e.g. Discord-Overlay), and create it.\r\n" +
                "3. In the new app, go to OAuth2 → Redirects and add this exact URL:\r\n" +
                $"      {RedirectUri}\r\n" +
                "4. Copy the Client ID (and reset/copy the Client Secret) into the fields below.\r\n" +
                "5. Click \"Test & save\" — Discord will pop up a consent dialog inside the\r\n" +
                "   Discord client; click Authorize to finish.",
        };

        var openPortalButton = new Button
        {
            Text = "Open Discord developer portal",
            Location = new Point(20, 240),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        openPortalButton.Click += (_, _) => OpenInBrowser(DiscordPortalUrl);

        var copyRedirectButton = new Button
        {
            Text = "Copy redirect URI",
            Location = new Point(220, 240),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        copyRedirectButton.Click += (_, _) =>
        {
            Clipboard.SetText(RedirectUri);
            statusLabel!.ForeColor = SystemColors.ControlText;
            statusLabel.Text = $"Copied: {RedirectUri}";
        };

        var clientIdLabel = new Label
        {
            Text = "Client ID:",
            Location = new Point(20, 290),
            AutoSize = true,
        };
        clientIdBox = new TextBox
        {
            Location = new Point(120, 287),
            Size = new Size(420, 23),
        };

        var clientSecretLabel = new Label
        {
            Text = "Client Secret:",
            Location = new Point(20, 322),
            AutoSize = true,
        };
        clientSecretBox = new TextBox
        {
            Location = new Point(120, 319),
            Size = new Size(420, 23),
            UseSystemPasswordChar = true,
        };

        testButton = new Button
        {
            Text = "Test && save",
            Location = new Point(120, 360),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
        };
        testButton.Click += async (_, _) => await OnTestAndSaveAsync();
        AcceptButton = testButton;

        cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(460, 360),
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };
        CancelButton = cancelButton;

        statusLabel = new Label
        {
            AutoSize = false,
            Size = new Size(520, 40),
            Location = new Point(20, 408),
            ForeColor = SystemColors.ControlText,
            Text = "",
        };

        Controls.AddRange(new Control[]
        {
            title,
            instructions,
            openPortalButton,
            copyRedirectButton,
            clientIdLabel,
            clientIdBox,
            clientSecretLabel,
            clientSecretBox,
            testButton,
            cancelButton,
            statusLabel,
        });
    }

    public DiscordCredentialBundle? Bundle { get; private set; }

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

    private async Task OnTestAndSaveAsync()
    {
        var clientId = clientIdBox.Text.Trim();
        var clientSecret = clientSecretBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            SetStatus("Both Client ID and Client Secret are required.", error: true);
            return;
        }

        SetBusy(true);
        SetStatus("Connecting to Discord… check the consent popup inside the Discord client.", error: false);

        try
        {
            var bundle = await session.SetupAsync(clientId, clientSecret).ConfigureAwait(true);
            Bundle = bundle;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (DiscordRpcException ex)
        {
            SetStatus(
                $"Discord said no: {ex.Message}\r\n" +
                "If you saw a consent popup, you may have denied it; if not, check that Discord is running.",
                error: true);
        }
        catch (DiscordOAuthException ex)
        {
            SetStatus(
                $"OAuth token exchange failed: {ex.Message}\r\n" +
                "Verify the Client ID, Client Secret, and that the redirect URI is registered in the developer portal.",
                error: true);
        }
        catch (Exception ex)
        {
            SetStatus($"Setup failed: {ex.Message}", error: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        testButton.Enabled = !busy;
        cancelButton.Enabled = !busy;
        clientIdBox.Enabled = !busy;
        clientSecretBox.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void SetStatus(string text, bool error)
    {
        statusLabel.Text = text;
        statusLabel.ForeColor = error ? Color.Firebrick : Color.SeaGreen;
    }
}
