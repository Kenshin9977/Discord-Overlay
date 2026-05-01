using System.Diagnostics;
using DiscordOverlay.App.Resources;
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

        Text = Strings.WizardDiscordWindowTitle;
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
            Text = Strings.WizardDiscordHeader,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16),
        };

        var instructions = new Label
        {
            AutoSize = false,
            Size = new Size(520, 180),
            Location = new Point(20, 50),
            Text = Strings.Format(nameof(Strings.WizardDiscordInstructions), RedirectUri),
        };

        var openPortalButton = new Button
        {
            Text = Strings.WizardDiscordOpenPortalButton,
            Location = new Point(20, 240),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        openPortalButton.Click += (_, _) => OpenInBrowser(DiscordPortalUrl);

        var copyRedirectButton = new Button
        {
            Text = Strings.WizardDiscordCopyRedirectButton,
            Location = new Point(220, 240),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
        };
        copyRedirectButton.Click += (_, _) =>
        {
            Clipboard.SetText(RedirectUri);
            statusLabel!.ForeColor = SystemColors.ControlText;
            statusLabel.Text = Strings.WizardDiscordRedirectCopied(RedirectUri);
        };

        var clientIdLabel = new Label
        {
            Text = Strings.WizardDiscordClientIdLabel,
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
            Text = Strings.WizardDiscordClientSecretLabel,
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
            Text = Strings.WizardDiscordTestSaveButton,
            Location = new Point(120, 360),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
        };
        testButton.Click += async (_, _) => await OnTestAndSaveAsync();
        AcceptButton = testButton;

        cancelButton = new Button
        {
            Text = Strings.WizardDiscordCancelButton,
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
            SetStatus(Strings.WizardDiscordFieldsRequired, error: true);
            return;
        }

        SetBusy(true);
        SetStatus(Strings.WizardDiscordConnecting, error: false);

        try
        {
            var bundle = await session.SetupAsync(clientId, clientSecret).ConfigureAwait(true);
            Bundle = bundle;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (DiscordRpcException ex)
        {
            SetStatus(Strings.WizardDiscordRpcError(ex.Message), error: true);
        }
        catch (DiscordOAuthException ex)
        {
            SetStatus(Strings.WizardDiscordOAuthError(ex.Message), error: true);
        }
        catch (Exception ex)
        {
            SetStatus(Strings.WizardDiscordSetupFailed(ex.Message), error: true);
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
