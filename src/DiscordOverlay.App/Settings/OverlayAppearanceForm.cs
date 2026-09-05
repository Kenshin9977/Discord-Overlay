using DiscordOverlay.App.Resources;
using DiscordOverlay.Core;
using DiscordOverlay.Core.Streaming;

namespace DiscordOverlay.App.Settings;

/// <summary>
/// Every setting the StreamKit voice widget reads, as controls.
///
/// Its own dialog rather than a tab in <see cref="SettingsForm"/>: appearance is
/// what you come back to and fiddle with, and that form auto-sizes around a
/// Discord group that swaps between a tall and a short panel — a TabControl,
/// which does not auto-size, would take that away.
/// </summary>
public sealed class OverlayAppearanceForm : Form
{
    private readonly Func<CancellationToken, Task>? pushToObs;

    private readonly ColorField textColor;
    private readonly NumericUpDown textSize;
    private readonly ColorField textOutlineColor;
    private readonly NumericUpDown textOutlineSize;
    private readonly ColorField textShadowColor;
    private readonly NumericUpDown textShadowSize;

    private readonly ColorField backgroundColor;
    private readonly NumericUpDown backgroundOpacity;
    private readonly ColorField backgroundShadowColor;
    private readonly NumericUpDown backgroundShadowSize;

    private readonly CheckBox limitSpeaking;
    private readonly CheckBox smallAvatars;
    private readonly CheckBox hideNames;
    private readonly CheckBox streamerAvatarFirst;

    private readonly Label statusLabel;

    /// <param name="current">Values to populate the form with.</param>
    /// <param name="pushToObs">
    /// Re-push the overlay URL once the file is written, so a colour change is
    /// visible without waiting for the next voice-channel change. Null when
    /// there is nothing to push to.
    /// </param>
    public OverlayAppearanceForm(StreamKitOverlayOptions current, Func<CancellationToken, Task>? pushToObs = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        this.pushToObs = pushToObs;

        Text = Strings.OverlayWindowTitle;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // fall back to default
        }

        textColor = new ColorField(current.TextColor);
        textSize = Number(1, 200, current.TextSize);
        textOutlineColor = new ColorField(current.TextOutlineColor);
        textOutlineSize = Number(0, 20, current.TextOutlineSize);
        textShadowColor = new ColorField(current.TextShadowColor);
        textShadowSize = Number(0, 20, current.TextShadowSize);

        backgroundColor = new ColorField(current.BackgroundColor);
        // Percent in the UI, fraction in the file. A percentage is what people
        // expect to type for an opacity; StreamKit's URL wants 0-1, and making
        // that conversion the form's job rather than the user's is the whole
        // point of having a form.
        backgroundOpacity = Number(0, 100, (int)Math.Round(ClampFraction(current.BackgroundOpacity) * 100));
        backgroundShadowColor = new ColorField(current.BackgroundShadowColor);
        backgroundShadowSize = Number(0, 40, current.BackgroundShadowSize);

        limitSpeaking = Check(Strings.OverlayLimitSpeakingCheckbox, current.LimitSpeaking);
        smallAvatars = Check(Strings.OverlaySmallAvatarsCheckbox, current.SmallAvatars);
        hideNames = Check(Strings.OverlayHideNamesCheckbox, current.HideNames);
        streamerAvatarFirst = Check(Strings.OverlayStreamerAvatarFirstCheckbox, current.StreamerAvatarFirst);

        var intro = new Label
        {
            Text = Strings.OverlayIntro,
            AutoSize = false,
            Size = new Size(430, 34),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 8),
        };

        var textGroup = Group(Strings.OverlayTextHeader, Rows(
            (Strings.OverlayTextColorLabel, textColor, null),
            (Strings.OverlayTextSizeLabel, textSize, null),
            (Strings.OverlayTextOutlineColorLabel, textOutlineColor, null),
            (Strings.OverlayTextOutlineSizeLabel, textOutlineSize, Strings.OverlayZeroDisablesHint),
            (Strings.OverlayTextShadowColorLabel, textShadowColor, null),
            (Strings.OverlayTextShadowSizeLabel, textShadowSize, Strings.OverlayZeroDisablesHint)));

        var backgroundGroup = Group(Strings.OverlayBackgroundHeader, Rows(
            (Strings.OverlayBackgroundColorLabel, backgroundColor, null),
            (Strings.OverlayBackgroundOpacityLabel, backgroundOpacity, "%"),
            (Strings.OverlayBackgroundShadowColorLabel, backgroundShadowColor, null),
            (Strings.OverlayBackgroundShadowSizeLabel, backgroundShadowSize, Strings.OverlayZeroDisablesHint)));

        var displayBody = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        displayBody.Controls.AddRange(new Control[] { limitSpeaking, smallAvatars, hideNames, streamerAvatarFirst });
        var displayGroup = Group(Strings.OverlayDisplayHeader, displayBody);

        statusLabel = new Label
        {
            AutoSize = false,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3, 6, 3, 0),
            ForeColor = SystemColors.GrayText,
        };

        var resetButton = new Button
        {
            Text = Strings.OverlayResetButton,
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 8, 0, 0),
            Anchor = AnchorStyles.Left,
        };
        resetButton.Click += (_, _) => Apply(new StreamKitOverlayOptions());

        var saveButton = new Button
        {
            Text = Strings.SettingsSaveButton,
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
        };
        saveButton.Click += async (_, _) => await OnSaveAsync(saveButton).ConfigureAwait(true);

        var cancelButton = new Button
        {
            Text = Strings.SettingsCancelButton,
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
            DialogResult = DialogResult.Cancel,
        };
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        var buttonRow = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 8, 0, 0),
        };
        buttonRow.Controls.Add(cancelButton);
        buttonRow.Controls.Add(saveButton);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.Controls.Add(resetButton, 0, 0);
        footer.Controls.Add(buttonRow, 1, 0);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(12, 10, 12, 12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (var row = 0; row < root.RowCount; row++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        root.Controls.Add(intro, 0, 0);
        root.Controls.Add(textGroup, 0, 1);
        root.Controls.Add(backgroundGroup, 0, 2);
        root.Controls.Add(displayGroup, 0, 3);
        root.Controls.Add(statusLabel, 0, 4);
        root.Controls.Add(footer, 0, 5);

        Controls.Add(root);
    }

    /// <summary>The values on screen, as the options object they will be saved as.</summary>
    public StreamKitOverlayOptions Result => new()
    {
        TextColor = textColor.Value,
        TextSize = (int)textSize.Value,
        TextOutlineColor = textOutlineColor.Value,
        TextOutlineSize = (int)textOutlineSize.Value,
        TextShadowColor = textShadowColor.Value,
        TextShadowSize = (int)textShadowSize.Value,
        BackgroundColor = backgroundColor.Value,
        BackgroundOpacity = (double)backgroundOpacity.Value / 100.0,
        BackgroundShadowColor = backgroundShadowColor.Value,
        BackgroundShadowSize = (int)backgroundShadowSize.Value,
        LimitSpeaking = limitSpeaking.Checked,
        SmallAvatars = smallAvatars.Checked,
        HideNames = hideNames.Checked,
        StreamerAvatarFirst = streamerAvatarFirst.Checked,
    };

    private void Apply(StreamKitOverlayOptions values)
    {
        textColor.Value = values.TextColor;
        textSize.Value = Clamp(textSize, values.TextSize);
        textOutlineColor.Value = values.TextOutlineColor;
        textOutlineSize.Value = Clamp(textOutlineSize, values.TextOutlineSize);
        textShadowColor.Value = values.TextShadowColor;
        textShadowSize.Value = Clamp(textShadowSize, values.TextShadowSize);
        backgroundColor.Value = values.BackgroundColor;
        backgroundOpacity.Value = Clamp(backgroundOpacity, (int)Math.Round(ClampFraction(values.BackgroundOpacity) * 100));
        backgroundShadowColor.Value = values.BackgroundShadowColor;
        backgroundShadowSize.Value = Clamp(backgroundShadowSize, values.BackgroundShadowSize);
        limitSpeaking.Checked = values.LimitSpeaking;
        smallAvatars.Checked = values.SmallAvatars;
        hideNames.Checked = values.HideNames;
        streamerAvatarFirst.Checked = values.StreamerAvatarFirst;
    }

    private async Task OnSaveAsync(Button saveButton)
    {
        saveButton.Enabled = false;
        try
        {
            // Load-then-write, so the OBS and watcher sections — and any key
            // this form does not show — survive a save from here.
            var config = await AppConfigStore.LoadAsync().ConfigureAwait(true);
            config.Streamkit = Result;
            await AppConfigStore.SaveAsync(config).ConfigureAwait(true);

            var pushed = await TryPushAsync().ConfigureAwait(true);

            statusLabel.ForeColor = pushed ? Color.SeaGreen : SystemColors.GrayText;
            statusLabel.Text = pushed ? Strings.OverlaySaved : Strings.OverlaySavedNotPushed;

            await Task.Delay(600).ConfigureAwait(true);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            statusLabel.ForeColor = Color.Firebrick;
            statusLabel.Text = Strings.OverlaySaveFailed(ex.Message);
            saveButton.Enabled = true;
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

    private static decimal Clamp(NumericUpDown box, int value) =>
        Math.Clamp(value, box.Minimum, box.Maximum);

    private static NumericUpDown Number(int min, int max, int value) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = Math.Clamp(value, min, max),
        Size = new Size(70, 23),
        Margin = new Padding(0, 3, 0, 3),
        TextAlign = HorizontalAlignment.Right,
    };

    private static CheckBox Check(string text, bool value) => new()
    {
        Text = text,
        Checked = value,
        AutoSize = true,
        Margin = new Padding(3, 3, 3, 3),
    };

    private static GroupBox Group(string header, Control body)
    {
        var box = new GroupBox
        {
            Text = header,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(3, 3, 3, 6),
        };
        box.Controls.Add(body);
        return box;
    }

    /// <summary>
    /// Label / control / optional grey note, one row each, in a grid so the
    /// controls line up down the column whatever the label lengths are — which
    /// they are not, between English and French.
    /// </summary>
    private static TableLayoutPanel Rows(params (string Label, Control Control, string? Note)[] rows)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = rows.Length,
            Margin = Padding.Empty,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label
            {
                Text = rows[i].Label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 8, 3),
            }, 0, i);
            grid.Controls.Add(rows[i].Control, 1, i);
            grid.Controls.Add(new Label
            {
                Text = rows[i].Note ?? string.Empty,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(8, 7, 0, 3),
            }, 2, i);
        }

        return grid;
    }
}
