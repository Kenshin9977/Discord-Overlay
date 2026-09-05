using System.Diagnostics;
using System.Windows;
using DiscordOverlay.App.Hosting;
using DiscordOverlay.App.Resources;
using DiscordOverlay.Core;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DiscordOverlay.App.Settings;

public partial class SettingsWindow : FluentWindow
{
    private const string DiscordPortalUrl = "https://discord.com/developers/applications";
    private const string RedirectUri = DiscordOAuthCredentials.DefaultRedirectUri;

    private readonly IDiscordSession session;
    private readonly AutoStartManager autoStart;
    private readonly ObsConnectionTester obsTester;
    private readonly Func<StreamKitOverlayOptions> currentOverlay;
    private readonly Func<CancellationToken, Task>? pushOverlayToObs;

    private bool openedModally;

    /// <param name="currentOverlay">
    /// Read lazily rather than captured, so the appearance window opens on what
    /// is in the file now — including a save made since this window opened.
    /// </param>
    /// <param name="pushOverlayToObs">
    /// Reload configuration and re-push the overlay URL after an appearance
    /// save. Null when there is nothing to push to, e.g. during first-run setup.
    /// </param>
    public SettingsWindow(
        IDiscordSession session,
        ObsConnectionOptions currentObs,
        AutoStartManager autoStart,
        ObsConnectionTester obsTester,
        Func<StreamKitOverlayOptions>? currentOverlay = null,
        Func<CancellationToken, Task>? pushOverlayToObs = null)
    {
        ArgumentNullException.ThrowIfNull(currentObs);
        this.session = session;
        this.autoStart = autoStart;
        this.obsTester = obsTester;
        this.currentOverlay = currentOverlay ?? (static () => new StreamKitOverlayOptions());
        this.pushOverlayToObs = pushOverlayToObs;

        InitializeComponent();
        SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: true);

        Title = Strings.SettingsWindowTitle;
        Titlebar.Title = Strings.SettingsWindowTitle;

        DiscordHeader.Text = Strings.SettingsDiscordHeader;
        SignOutButton.Content = Strings.SettingsSignOutButton;
        WizardInstructions.Text = Strings.Format(nameof(Strings.WizardDiscordInstructions), RedirectUri);
        OpenPortalButton.Content = Strings.WizardDiscordOpenPortalButton;
        CopyRedirectButton.Content = Strings.WizardDiscordCopyRedirectButton;
        ClientIdLabel.Text = Strings.WizardDiscordClientIdLabel;
        ClientSecretLabel.Text = Strings.WizardDiscordClientSecretLabel;
        SignInButton.Content = Strings.WizardDiscordTestSaveButton;

        ObsHeader.Text = Strings.SettingsObsHeader;
        ObsHint.Text = Strings.SettingsObsHint;
        HostLabel.Text = Strings.SettingsHostLabel;
        PortLabel.Text = Strings.SettingsPortLabel;
        PasswordLabel.Text = Strings.SettingsPasswordLabel;
        BrowserSourceLabel.Text = Strings.SettingsBrowserSourceLabel;
        ObsTestButton.Content = Strings.WizardObsTestButton;

        OverlayHeader.Text = Strings.SettingsOverlayHeader;
        OverlayHint.Text = Strings.SettingsOverlayHint;

        StartupHeader.Text = Strings.SettingsStartupHeader;
        AutoStartToggle.Content = Strings.SettingsAutoStartCheckbox;

        SaveButton.Content = Strings.SettingsSaveButton;
        CancelButton.Content = Strings.SettingsCancelButton;

        HostBox.Text = currentObs.Hostname;
        PortBox.Value = currentObs.Port;
        PasswordBox.Password = currentObs.Password;
        SourceNameBox.Text = currentObs.BrowserSourceName;
        AutoStartToggle.IsChecked = autoStart.IsEnabled;

        ApplyDiscordState();
    }

    /// <summary>
    /// True once the user has signed out. The caller exits the app on this, so
    /// the next launch runs the setup wizard.
    /// </summary>
    public bool SignedOut { get; private set; }

    private void ApplyDiscordState()
    {
        var bundle = session.Current;
        var signedIn = bundle is not null;

        SignedInPanel.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        SignedOutPanel.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;

        if (signedIn)
        {
            SignedInStatus.Text = Strings.SettingsSignedIn(bundle!.ClientId);
        }

        // OBS fields stay enabled either way so the user can prefill them, but
        // nothing is persisted until Discord is connected and Save is clicked.
        SaveButton.IsEnabled = signedIn;
        ObsTestButton.IsEnabled = signedIn;
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

    private void OnOpenPortalClick(object sender, RoutedEventArgs e) => OpenInBrowser(DiscordPortalUrl);

    private void OnCopyRedirectClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(RedirectUri);
        SetInfo(DiscordInfo, Strings.WizardDiscordRedirectCopied(RedirectUri), InfoBarSeverity.Informational);
    }

    private void OnOverlayAppearanceClick(object sender, RoutedEventArgs e)
    {
        // Its own window, and its own save: appearance lands in settings.json
        // when you click Save there, without waiting on this window's Save,
        // which is gated on Discord being connected and restarts the app.
        var window = new OverlayAppearanceWindow(currentOverlay(), pushOverlayToObs) { Owner = this };
        window.ShowDialog();
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        var clientId = ClientIdBox.Text.Trim();
        var clientSecret = ClientSecretBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            SetInfo(DiscordInfo, Strings.WizardDiscordFieldsRequired, InfoBarSeverity.Warning);
            return;
        }

        SetSignInBusy(true);
        SetInfo(DiscordInfo, Strings.WizardDiscordConnecting, InfoBarSeverity.Informational);

        try
        {
            await session.SetupAsync(clientId, clientSecret).ConfigureAwait(true);
            ApplyDiscordState();
            SetStatus(Strings.SettingsSaveSuccess, error: false);
        }
        catch (DiscordRpcException ex)
        {
            SetInfo(DiscordInfo, Strings.WizardDiscordRpcError(ex.Message), InfoBarSeverity.Error);
        }
        catch (DiscordOAuthException ex)
        {
            SetInfo(DiscordInfo, Strings.WizardDiscordOAuthError(ex.Message), InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            SetInfo(DiscordInfo, Strings.WizardDiscordSetupFailed(ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            SetSignInBusy(false);
        }
    }

    private async void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title = Strings.SettingsSignOutTitle,
            Content = Strings.SettingsSignOutPrompt,
            PrimaryButtonText = Strings.DialogYes,
            CloseButtonText = Strings.DialogNo,
        };
        if (await confirm.ShowDialogAsync().ConfigureAwait(true) != Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            return;
        }

        SignOutButton.IsEnabled = false;
        try
        {
            await session.SignOutAsync().ConfigureAwait(true);
            SignedOut = true;
            Close();
        }
        catch (Exception ex)
        {
            SetStatus(Strings.SettingsSignOutFailed(ex.Message), error: true);
            SignOutButton.IsEnabled = true;
        }
    }

    private async void OnTestObsClick(object sender, RoutedEventArgs e)
    {
        var host = HostBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            SetInfo(ObsInfo, Strings.WizardObsHostRequired, InfoBarSeverity.Warning);
            return;
        }

        ObsTestButton.IsEnabled = false;
        SetInfo(ObsInfo, Strings.WizardObsTesting, InfoBarSeverity.Informational);
        try
        {
            var result = await obsTester
                .TestAsync(host, (int)PortBox.Value.GetValueOrDefault(4455), PasswordBox.Password)
                .ConfigureAwait(true);

            SetInfo(
                ObsInfo,
                result.IsSuccess ? Strings.WizardObsTestSuccess : result.ErrorMessage ?? Strings.WizardObsHostRequired,
                result.IsSuccess ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        finally
        {
            ObsTestButton.IsEnabled = session.Current is not null;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (session.Current is null)
        {
            // Save is disabled in this state, but guard anyway.
            return;
        }

        try
        {
            var existing = await AppConfigStore.LoadAsync().ConfigureAwait(true);
            existing.Obs.Hostname = HostBox.Text.Trim();
            existing.Obs.Port = (int)PortBox.Value.GetValueOrDefault(4455);
            existing.Obs.Password = PasswordBox.Password;
            existing.Obs.BrowserSourceName = SourceNameBox.Text.Trim();

            await AppConfigStore.SaveAsync(existing).ConfigureAwait(true);

            try
            {
                if (AutoStartToggle.IsChecked ?? false)
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
                SetStatus(Strings.SettingsAutoStartFailed(ex.Message), error: true);
                return;
            }

            SetStatus(Strings.SettingsSaveSuccess, error: false);

            await Task.Delay(700).ConfigureAwait(true);
            if (openedModally) DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            SetStatus(Strings.SettingsSaveFailed(ex.Message), error: true);
        }
    }

    /// <summary>
    /// Assigning <see cref="Window.DialogResult"/> throws on a window opened
    /// with Show rather than ShowDialog, and this one is opened both ways —
    /// modally at first run, modelessly from the tray. Hiding ShowDialog is how
    /// it knows which it is without reaching into WPF's internals.
    /// </summary>
    public new bool? ShowDialog()
    {
        openedModally = true;
        return base.ShowDialog();
    }

    private void SetSignInBusy(bool busy)
    {
        SignInButton.IsEnabled = !busy;
        ClientIdBox.IsEnabled = !busy;
        ClientSecretBox.IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    private static void SetInfo(InfoBar bar, string message, InfoBarSeverity severity)
    {
        bar.Severity = severity;
        bar.Message = message;
        bar.IsOpen = true;
    }

    private void SetStatus(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.Foreground = error
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xed, 0x42, 0x45))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x57, 0xf2, 0x87));
    }
}
