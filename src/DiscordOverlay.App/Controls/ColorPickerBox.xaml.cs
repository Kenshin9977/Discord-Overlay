using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiscordOverlay.App.Resources;

namespace DiscordOverlay.App.Controls;

/// <summary>
/// A colour swatch and a hex box, with PixiEditor's hue ring and
/// saturation/value square in the popup.
///
/// WPF-UI 4.3 ships no colour picker, so the wheel geometry comes from
/// PixiEditor.ColorPicker (MIT) rather than being rewritten here. Only its
/// <c>SquarePicker</c> though: the <c>StandardColorPicker</c> that wraps it adds
/// a hex field and HSV/HSL/RGB spin boxes whose right-hand column is clipped at
/// every width tried, 230 through 380 — its own layout bug, not a sizing mistake
/// on this side. <c>SquarePicker</c> alone renders clean and carries none of the
/// library's chrome, so it sits inside the Fluent popup without looking foreign.
///
/// The swatch and hex box stay ours, and hex remains the primary input: it is
/// what StreamKit, OBS and every palette site hand you.
/// </summary>
public partial class ColorPickerBox : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(ColorPickerBox),
            new FrameworkPropertyMetadata(
                "#000000",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    private bool suppressFeedback;

    public ColorPickerBox()
    {
        InitializeComponent();
        Apply(Value);
    }

    /// <summary>The colour as <c>#rrggbb</c>. Unparseable text leaves the swatch on its last good colour.</summary>
    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string PickerToolTip => Strings.OverlayPickColorTooltip;

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerBox box && !box.suppressFeedback)
        {
            box.Apply((string)e.NewValue);
        }
    }

    /// <summary>Push a value into every control at once without them echoing back.</summary>
    private void Apply(string? hex)
    {
        var normalized = Normalize(hex);
        var color = Parse(normalized);

        suppressFeedback = true;
        try
        {
            HexBox.Text = normalized;
            Swatch.Background = new SolidColorBrush(color);
            Picker.SelectedColor = color;
            if (Value != normalized) Value = normalized;
        }
        finally
        {
            suppressFeedback = false;
        }
    }

    private void OnSwatchClick(object sender, RoutedEventArgs e) => PickerPopup.IsOpen = !PickerPopup.IsOpen;

    private void OnPickerColorChanged(object? sender, RoutedEventArgs e)
    {
        if (suppressFeedback) return;
        var c = Picker.SelectedColor;
        Apply($"#{c.R:x2}{c.G:x2}{c.B:x2}");
    }

    private void OnHexTextChanged(object sender, TextChangedEventArgs e)
    {
        // Half-typed input is normal on the way to a full value, so an
        // incomplete hex leaves everything else alone rather than snapping the
        // swatch to black under the user's cursor.
        if (suppressFeedback) return;
        if (TryParse(HexBox.Text, out _))
        {
            Apply(HexBox.Text);
        }
    }

    private static string Normalize(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return "#000000";
        return text[0] == '#' ? text.ToLowerInvariant() : "#" + text.ToLowerInvariant();
    }

    private static Color Parse(string? value) => TryParse(value, out var color) ? color : Colors.Black;

    private static bool TryParse(string? value, out Color color)
    {
        color = Colors.Black;
        var text = (value ?? string.Empty).Trim().TrimStart('#');
        if (text.Length != 6) return false;
        if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = Color.FromRgb((byte)((rgb >> 16) & 0xff), (byte)((rgb >> 8) & 0xff), (byte)(rgb & 0xff));
        return true;
    }
}
