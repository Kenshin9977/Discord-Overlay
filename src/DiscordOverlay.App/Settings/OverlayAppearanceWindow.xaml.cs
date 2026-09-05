using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DiscordOverlay.App.Resources;
using DiscordOverlay.Core;
using DiscordOverlay.Core.Streaming;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DiscordOverlay.App.Settings;

/// <summary>
/// Every setting the StreamKit voice widget reads.
///
/// Its own window rather than a page in <see cref="SettingsWindow"/>: appearance
/// is what you reopen to fiddle with, and it should not sit behind a Save that
/// is gated on Discord being connected and then restarts the app. It is also
/// reachable straight from the tray menu.
/// </summary>
public partial class OverlayAppearanceWindow : FluentWindow
{
    private readonly Func<CancellationToken, Task>? pushToObs;

    /// <param name="current">Values to populate the window with.</param>
    /// <param name="pushToObs">
    /// Reload configuration and re-push the overlay URL once the file is
    /// written, so a colour change is visible without waiting for the next
    /// voice-channel change. Null when there is nothing to push to.
    /// </param>
    public OverlayAppearanceWindow(StreamKitOverlayOptions current, Func<CancellationToken, Task>? pushToObs = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        this.pushToObs = pushToObs;

        InitializeComponent();
        SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: true);

        Title = Strings.OverlayWindowTitle;
        Titlebar.Title = Strings.OverlayWindowTitle;
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

        ResetButton.Content = Strings.OverlayResetButton;
        SaveButton.Content = Strings.SettingsSaveButton;
        CancelButton.Content = Strings.SettingsCancelButton;

        BackgroundOpacitySlider.ValueChanged += (_, _) => UpdateOpacityReadout();

        Apply(current);
    }

    /// <summary>The values on screen, as the options object they will be saved as.</summary>
    public StreamKitOverlayOptions Result => new()
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

    private void Apply(StreamKitOverlayOptions values)
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

    private void OnResetClick(object sender, RoutedEventArgs e) => Apply(new StreamKitOverlayOptions());

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            // Load-then-write, so the OBS and watcher sections — and any key
            // this window does not show — survive a save from here.
            var config = await AppConfigStore.LoadAsync().ConfigureAwait(true);
            config.Streamkit = Result;
            await AppConfigStore.SaveAsync(config).ConfigureAwait(true);

            var pushed = await TryPushAsync().ConfigureAwait(true);

            StatusText.Foreground = pushed
                ? new SolidColorBrush(Color.FromRgb(0x57, 0xf2, 0x87))
                : (Brush)FindResource("TextFillColorSecondaryBrush");
            StatusText.Text = pushed ? Strings.OverlaySaved : Strings.OverlaySavedNotPushed;

            await Task.Delay(700).ConfigureAwait(true);
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xed, 0x42, 0x45));
            StatusText.Text = Strings.OverlaySaveFailed(ex.Message);
            SaveButton.IsEnabled = true;
        }
    }

    private async Task<bool> TryPushAsync()
    {
        if (pushToObs is null) return false;
        try
        {
            await pushToObs(CancellationToken.None).ConfigureAwait(true);
            return true;
        }
        catch (Exception)
        {
            // OBS being unreachable is not a reason to lose the save. The file
            // is already written and the next channel change will carry it.
            return false;
        }
    }

    private static double ClampFraction(double value)
    {
        if (double.IsNaN(value)) return 0;
        // Same legacy 0-100 reading as the URL builder, so an old settings.json
        // shows the opacity it is actually producing.
        if (value > 1) value /= 100.0;
        return Math.Clamp(value, 0, 1);
    }
}
