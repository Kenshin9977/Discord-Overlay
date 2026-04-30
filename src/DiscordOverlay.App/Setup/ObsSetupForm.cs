using DiscordOverlay.Core;
using DiscordOverlay.Core.Streaming;

namespace DiscordOverlay.App.Setup;

public sealed class ObsSetupForm : Form
{
    private readonly ObsConnectionTester tester;

    private readonly TextBox hostBox;
    private readonly NumericUpDown portBox;
    private readonly TextBox passwordBox;
    private readonly TextBox sourceNameBox;
    private readonly Button testButton;
    private readonly Button skipButton;
    private readonly Button saveButton;
    private readonly Label statusLabel;

    private bool tested;

    public ObsSetupForm(ObsConnectionTester tester, ObsConnectionOptions current)
    {
        this.tester = tester;

        Text = "Discord-Overlay — Setup (step 2 of 2)";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(580, 480);
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
            Text = "Connect to OBS",
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16),
        };

        var instructions = new Label
        {
            AutoSize = false,
            Size = new Size(540, 110),
            Location = new Point(20, 50),
            Text =
                "Discord-Overlay drives an OBS Browser Source via the WebSocket server bundled with OBS 28+.\r\n\r\n" +
                "1. In OBS: Tools → WebSocket Server Settings → tick \"Enable WebSocket server\".\r\n" +
                "2. Click \"Show Connect Info\" in OBS to copy the Server Password — paste it below.\r\n" +
                "3. In your scene, add a Browser Source named exactly the value below (default \"Discord-Overlay\").\r\n" +
                "   Width/height as you like (e.g. 350×500). Leave the URL empty — this app fills it in.",
        };

        var hostLabel = new Label { Text = "Host:", Location = new Point(20, 175), AutoSize = true };
        hostBox = new TextBox
        {
            Location = new Point(160, 172),
            Size = new Size(400, 23),
            Text = current.Hostname,
        };

        var portLabel = new Label { Text = "Port:", Location = new Point(20, 205), AutoSize = true };
        portBox = new NumericUpDown
        {
            Location = new Point(160, 202),
            Size = new Size(120, 23),
            Minimum = 1,
            Maximum = 65535,
            Value = current.Port,
        };

        var passwordLabel = new Label { Text = "Password:", Location = new Point(20, 235), AutoSize = true };
        passwordBox = new TextBox
        {
            Location = new Point(160, 232),
            Size = new Size(400, 23),
            UseSystemPasswordChar = true,
            Text = current.Password,
        };

        var sourceLabel = new Label { Text = "Browser source name:", Location = new Point(20, 265), AutoSize = true };
        sourceNameBox = new TextBox
        {
            Location = new Point(160, 262),
            Size = new Size(400, 23),
            Text = string.IsNullOrWhiteSpace(current.BrowserSourceName) ? "Discord-Overlay" : current.BrowserSourceName,
        };

        testButton = new Button
        {
            Text = "Test connection",
            Location = new Point(20, 310),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
        };
        testButton.Click += async (_, _) => await OnTestAsync();

        statusLabel = new Label
        {
            AutoSize = false,
            Size = new Size(540, 64),
            Location = new Point(20, 350),
            ForeColor = SystemColors.GrayText,
            Text = "Click \"Test connection\" to verify your settings, then \"Save & finish\".",
        };

        skipButton = new Button
        {
            Text = "Skip — configure later",
            Location = new Point(20, 430),
            AutoSize = true,
            DialogResult = DialogResult.Ignore,
        };

        saveButton = new Button
        {
            Text = "Save && finish",
            Location = new Point(458, 430),
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
            Enabled = false,
        };
        saveButton.Click += async (_, _) => await OnSaveAsync();
        AcceptButton = saveButton;
        CancelButton = skipButton;

        Controls.AddRange(new Control[]
        {
            title,
            instructions,
            hostLabel, hostBox,
            portLabel, portBox,
            passwordLabel, passwordBox,
            sourceLabel, sourceNameBox,
            testButton,
            statusLabel,
            skipButton,
            saveButton,
        });

        // Allow Save & finish without an explicit test if the user already
        // has matching settings (e.g. re-running setup).
        UpdateSaveEnabled();
        hostBox.TextChanged += (_, _) => InvalidateTest();
        portBox.ValueChanged += (_, _) => InvalidateTest();
        passwordBox.TextChanged += (_, _) => InvalidateTest();
    }

    private void InvalidateTest()
    {
        tested = false;
        UpdateSaveEnabled();
        statusLabel.ForeColor = SystemColors.GrayText;
        statusLabel.Text = "Settings changed — re-test the connection.";
    }

    private void UpdateSaveEnabled()
    {
        saveButton.Enabled = tested && !string.IsNullOrWhiteSpace(sourceNameBox.Text);
    }

    private async Task OnTestAsync()
    {
        var host = hostBox.Text.Trim();
        var port = (int)portBox.Value;
        var password = passwordBox.Text;

        if (string.IsNullOrWhiteSpace(host))
        {
            SetStatus("Host is required.", error: true);
            return;
        }

        SetBusy(true);
        SetStatus("Testing connection to OBS…", error: false);

        try
        {
            var result = await tester.TestAsync(host, port, password).ConfigureAwait(true);
            if (result.IsSuccess)
            {
                tested = true;
                SetStatus("Connected — authentication accepted. Click \"Save & finish\" to continue.", error: false);
            }
            else
            {
                tested = false;
                SetStatus(result.ErrorMessage ?? "Connection failed.", error: true);
            }
        }
        finally
        {
            SetBusy(false);
            UpdateSaveEnabled();
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

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}", error: true);
        }
    }

    private void SetBusy(bool busy)
    {
        testButton.Enabled = !busy;
        saveButton.Enabled = !busy && tested;
        skipButton.Enabled = !busy;
        hostBox.Enabled = !busy;
        portBox.Enabled = !busy;
        passwordBox.Enabled = !busy;
        sourceNameBox.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void SetStatus(string text, bool error)
    {
        statusLabel.Text = text;
        statusLabel.ForeColor = error ? Color.Firebrick : Color.SeaGreen;
    }
}
