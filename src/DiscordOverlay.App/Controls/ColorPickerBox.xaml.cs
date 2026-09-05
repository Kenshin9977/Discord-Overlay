using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiscordOverlay.App.Resources;

namespace DiscordOverlay.App.Controls;

/// <summary>
/// A colour swatch, a hex box, and a popup with presets and RGB sliders.
///
/// WPF-UI 4.3 ships no colour picker, and the Win32 <c>ChooseColor</c> dialog is
/// the one piece of 1995 that would have shown through the Fluent styling. Hex
/// stays the primary input because that is what StreamKit, OBS and every palette
/// site hand you; the sliders are for when you are choosing rather than pasting.
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

    /// <summary>Colours worth reaching for on a voice overlay: neutrals, then Discord's own palette.</summary>
    private static readonly string[] PresetHexes =
    [
        "#ffffff", "#dcddde", "#b9bbbe", "#72767d", "#4f545c", "#36393f", "#2f3136", "#1e2124",
        "#000000", "#5865f2", "#57f287", "#fee75c", "#eb459e", "#ed4245", "#faa61a", "#00b0f4",
    ];

    private bool suppressFeedback;

    public ColorPickerBox()
    {
        InitializeComponent();
        Presets.ItemsSource = PresetHexes
            .Select(hex => new { Hex = hex, Brush = new SolidColorBrush(Parse(hex)) })
            .ToArray();
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
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            RedValue.Text = color.R.ToString(CultureInfo.InvariantCulture);
            GreenValue.Text = color.G.ToString(CultureInfo.InvariantCulture);
            BlueValue.Text = color.B.ToString(CultureInfo.InvariantCulture);
            if (Value != normalized) Value = normalized;
        }
        finally
        {
            suppressFeedback = false;
        }
    }

    private void OnSwatchClick(object sender, RoutedEventArgs e) => PickerPopup.IsOpen = !PickerPopup.IsOpen;

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            Apply(hex);
            PickerPopup.IsOpen = false;
        }
    }

    private void OnChannelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (suppressFeedback) return;
        Apply($"#{(int)RedSlider.Value:x2}{(int)GreenSlider.Value:x2}{(int)BlueSlider.Value:x2}");
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
