using System.ComponentModel;
using DiscordOverlay.App.Resources;

namespace DiscordOverlay.App.Settings;

/// <summary>
/// A colour swatch that opens the system colour picker, paired with the hex box
/// people actually have on hand — StreamKit, OBS and every palette site speak
/// hex, so typing one in has to work as well as clicking.
/// </summary>
internal sealed class ColorField : FlowLayoutPanel
{
    private readonly Button swatch;
    private readonly TextBox hexBox;
    private bool updating;

    public ColorField(string initialHex)
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FlowDirection = FlowDirection.LeftToRight;
        Margin = new Padding(0, 2, 0, 2);
        WrapContents = false;

        swatch = new Button
        {
            Size = new Size(34, 23),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 6, 0),
            TabStop = false,
        };
        swatch.FlatAppearance.BorderColor = SystemColors.ControlDark;
        swatch.Click += OnPickColor;

        hexBox = new TextBox
        {
            Size = new Size(84, 23),
            Margin = Padding.Empty,
            MaxLength = 7,
        };
        hexBox.TextChanged += OnHexTyped;

        var tip = new ToolTip();
        tip.SetToolTip(swatch, Strings.OverlayPickColorTooltip);

        Controls.Add(swatch);
        Controls.Add(hexBox);

        Value = initialHex;
    }

    /// <summary>The colour as <c>#rrggbb</c>. Setting an unparseable value falls back to black.</summary>
    // Runtime state, not a design-time property: this control is built in code,
    // never dropped on a designer surface.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    public string Value
    {
        get => hexBox.Text;
        set
        {
            updating = true;
            hexBox.Text = Normalize(value);
            updating = false;
            swatch.BackColor = Parse(hexBox.Text);
        }
    }

    private void OnPickColor(object? sender, EventArgs e)
    {
        using var dialog = new ColorDialog
        {
            Color = Parse(hexBox.Text),
            FullOpen = true,
            AnyColor = true,
        };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            Value = $"#{dialog.Color.R:x2}{dialog.Color.G:x2}{dialog.Color.B:x2}";
        }
    }

    private void OnHexTyped(object? sender, EventArgs e)
    {
        // Half-typed input is normal — "#ff" on the way to "#ff00aa" — so the
        // swatch just holds its last good colour rather than flashing black.
        if (updating) return;
        if (TryParse(hexBox.Text, out var color))
        {
            swatch.BackColor = color;
        }
    }

    private static string Normalize(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return "#000000";
        return text[0] == '#' ? text : "#" + text;
    }

    private static Color Parse(string? value) =>
        TryParse(value, out var color) ? color : Color.Black;

    private static bool TryParse(string? value, out Color color)
    {
        color = Color.Black;
        var text = (value ?? string.Empty).Trim().TrimStart('#');
        if (text.Length != 6) return false;
        if (!int.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = Color.FromArgb((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
        return true;
    }
}
