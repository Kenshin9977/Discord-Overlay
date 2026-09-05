using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DiscordOverlay.App.Hosting;
using DiscordOverlay.App.Resources;
using DiscordOverlay.Core;
using DiscordOverlay.Core.Auth;
using DiscordOverlay.Core.Discord;
using DiscordOverlay.Core.Streaming;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DiscordOverlay.App.Settings;

/// <summary>
/// Every setting the app has, in one window: connection on one tab, the
/// StreamKit overlay's own settings on the other, and a single Save for both.
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    private const string DiscordPortalUrl = "https://discord.com/developers/applications";
    private const string RedirectUri = DiscordOAuthCredentials.DefaultRedirectUri;

    private readonly IDiscordSession session;
    private readonly AutoStartManager autoStart;
    private readonly ObsConnectionTester obsTester;
    private readonly Func<CancellationToken, Task>? pushOverlayToObs;

    private bool openedModally;

    /// <param name="currentOverlay">
    /// Read through a delegate rather than passed by value so the window opens
    /// on what is in the file now, whoever last wrote it.
    /// </param>
    /// <param name="pushOverlayToObs">
    /// Reload configuration and re-push the overlay URL after a save, so an
    /// appearance change shows up without waiting for the next voice-channel
    /// change. Null when there is nothing to push to, e.g. at first run.
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
        this.pushOverlayToObs = pushOverlayToObs;

        InitializeComponent();
        SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: true);

        Title = Strings.SettingsWindowTitle;
        Titlebar.Title = Strings.SettingsWindowTitle;

        ConnectionTab.Header = Strings.SettingsTabConnection;
        OverlayTab.Header = Strings.SettingsTabOverlay;

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

        StartupHeader.Text = Strings.SettingsStartupHeader;
        AutoStartToggle.Content = Strings.SettingsAutoStartCheckbox;

        IntroText.Text = Strings.OverlayIntro;
        TextHeader.Text = Strings.OverlayTextHeader;
        BackgroundHeader.Text = Strings.OverlayBackgroundHeader;
        DisplayHeader.Text = Strings.OverlayDisplayHeader;
        TextColorLabel.Text = Strings.OverlayTextColorLabel;
        TextSizeLabel.Text = Strings.OverlayTextSizeLabel;
        TextOutlineColorLabel.Text = Strings.OverlayTextOutlineColorLabel;
        TextOutlineSizeLabel.Text = Strings.OverlayTextOutlineSizeLabel;
        TextShadowColorLabel.Text = Strings.OverlayTextShadowColorLabel;
        TextShadowSizeLabel.Text = Strings.OverlayTextShadowSizeLabel;
        BackgroundColorLabel.Text = Strings.OverlayBackgroundColorLabel;
        BackgroundOpacityLabel.Text = Strings.OverlayBackgroundOpacityLabel;
        BackgroundShadowColorLabel.Text = Strings.OverlayBackgroundShadowColorLabel;
        BackgroundShadowSizeLabel.Text = Strings.OverlayBackgroundShadowSizeLabel;
        TextOutlineSizeHint.Text = Strings.OverlayZeroDisablesHint;
        TextShadowSizeHint.Text = Strings.OverlayZeroDisablesHint;
        BackgroundShadowSizeHint.Text = Strings.OverlayZeroDisablesHint;
        LimitSpeakingToggle.Content = Strings.OverlayLimitSpeakingCheckbox;
        SmallAvatarsToggle.Content = Strings.OverlaySmallAvatarsCheckbox;
        HideNamesToggle.Content = Strings.OverlayHideNamesCheckbox;
        StreamerAvatarFirstToggle.Content = Strings.OverlayStreamerAvatarFirstCheckbox;
        ResetOverlayButton.Content = Strings.OverlayResetButton;

        SaveButton.Content = Strings.SettingsSaveButton;
        CancelButton.Content = Strings.SettingsCancelButton;

        HostBox.Text = currentObs.Hostname;
        PortBox.Value = currentObs.Port;
        PasswordBox.Password = currentObs.Password;
        SourceNameBox.Text = currentObs.BrowserSourceName;
        AutoStartToggle.IsChecked = autoStart.IsEnabled;

        BackgroundOpacitySlider.ValueChanged += (_, _) => UpdateOpacityReadout();
        ApplyOverlay((currentOverlay ?? (static () => new StreamKitOverlayOptions()))());

        ApplyDiscordState();
    }

    /// <summary>
    /// True once the user has signed out. The caller exits the app on this, so
    /// the next launch runs the setup wizard.
    /// </summary>
    public bool SignedOut { get; private set; }

    /// <summary>Open on the overlay tab — what the tray menu's appearance entry wants.</summary>
    public void SelectOverlayTab() => Tabs.SelectedItem = OverlayTab;

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

    private StreamKitOverlayOptions OverlayResult => new()
    {
        TextColor = TextColorPicker.Value,
        TextSize = (int)TextSizeBox.Value.GetValueOrDefault(14),
        TextOutlineColor = TextOutlineColorPicker.Value,
        TextOutlineSize = (int)TextOutlineSizeBox.Value.GetValueOrDefault(0),
        TextShadowColor = TextShadowColorPicker.Value,
        TextShadowSize = (int)TextShadowSizeBox.Value.GetValueOrDefault(0),
        BackgroundColor = BackgroundColorPicker.Value,
        BackgroundOpacity = BackgroundOpacitySlider.Value / 100.0,
        BackgroundShadowColor = BackgroundShadowColorPicker.Value,
        BackgroundShadowSize = (int)BackgroundShadowSizeBox.Value.GetValueOrDefault(0),
        LimitSpeaking = LimitSpeakingToggle.IsChecked ?? false,
        SmallAvatars = SmallAvatarsToggle.IsChecked ?? false,
        HideNames = HideNamesToggle.IsChecked ?? false,
        StreamerAvatarFirst = StreamerAvatarFirstToggle.IsChecked ?? false,
    };

    private void ApplyOverlay(StreamKitOverlayOptions values)
    {
        TextColorPicker.Value = values.TextColor;
        TextSizeBox.Value = values.TextSize;
        TextOutlineColorPicker.Value = values.TextOutlineColor;
        TextOutlineSizeBox.Value = values.TextOutlineSize;
        TextShadowColorPicker.Value = values.TextShadowColor;
        TextShadowSizeBox.Value = values.TextShadowSize;
        BackgroundColorPicker.Value = values.BackgroundColor;
        BackgroundOpacitySlider.Value = Math.Round(ClampFraction(values.BackgroundOpacity) * 100);
        BackgroundShadowColorPicker.Value = values.BackgroundShadowColor;
        BackgroundShadowSizeBox.Value = values.BackgroundShadowSize;
        LimitSpeakingToggle.IsChecked = values.LimitSpeaking;
        SmallAvatarsToggle.IsChecked = values.SmallAvatars;
        HideNamesToggle.IsChecked = values.HideNames;
        StreamerAvatarFirstToggle.IsChecked = values.StreamerAvatarFirst;
        UpdateOpacityReadout();
    }

    private void UpdateOpacityReadout() =>
        BackgroundOpacityValue.Text = ((int)BackgroundOpacitySlider.Value)
            .ToString(CultureInfo.CurrentCulture) + " %";

    private static double ClampFraction(double value)
    {
        if (double.IsNaN(value)) return 0;
        // Same legacy 0-100 reading as the URL builder, so an old settings.json
        // shows the opacity it is actually producing.
        if (value > 1) value /= 100.0;
        return Math.Clamp(value, 0, 1);
    }

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

        // Fields stay enabled either way so the user can prefill them, but
        // nothing is persisted until Discord is connected and Save is clicked —
        // first-run setup treats a successful Save as "setup finished".
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

    private void OnResetOverlayClick(object sender, RoutedEventArgs e) => ApplyOverlay(new StreamKitOverlayOptions());

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

        SaveButton.IsEnabled = false;
        try
        {
            // Load-then-write, so anything hand-edited that neither tab shows —
            // the Watcher section, say — survives a save from here.
            var config = await AppConfigStore.LoadAsync().ConfigureAwait(true);
            config.Obs.Hostname = HostBox.Text.Trim();
            config.Obs.Port = (int)PortBox.Value.GetValueOrDefault(4455);
            config.Obs.Password = PasswordBox.Password;
            config.Obs.BrowserSourceName = SourceNameBox.Text.Trim();
            config.Streamkit = OverlayResult;

            await AppConfigStore.SaveAsync(config).ConfigureAwait(true);

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
                SaveButton.IsEnabled = true;
                return;
            }

            // Appearance applies live; host/port/password do not, which is what
            // the success message says.
            await TryPushAsync().ConfigureAwait(true);
            SetStatus(Strings.SettingsSaveSuccess, error: false);

            await Task.Delay(700).ConfigureAwait(true);
            if (openedModally) DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            SetStatus(Strings.SettingsSaveFailed(ex.Message), error: true);
            SaveButton.IsEnabled = true;
        }
    }

    private async Task TryPushAsync()
    {
        if (pushOverlayToObs is null) return;
        try
        {
            await pushOverlayToObs(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // OBS being unreachable is not a reason to fail the save. The file
            // is written and the next channel change will carry it.
        }
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
        StatusText.Foreground = new SolidColorBrush(
            error ? Color.FromRgb(0xed, 0x42, 0x45) : Color.FromRgb(0x57, 0xf2, 0x87));
    }
}
