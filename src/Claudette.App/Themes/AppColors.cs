using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Claudette.Core.Settings;

namespace Claudette.App.Themes;

/// <summary>
/// The colors of Settings → Appearance → Style (DESIGN.md §3, "Visual style"). Standard is App.axaml's tokens, Rider's
/// Islands colors, and gives Fluent Rider's palette: its greys and blue. Claude puts <see cref="ClaudeColors"/> over those
/// tokens, and gives Fluent a palette of the Claude apps' warm greys, ivory and orange. Fluent draws with the palette what
/// it draws itself: window backgrounds, text, controls and the accent, whose lighter and darker shades it works out from
/// it. Both work in light and dark. The Claude style's shapes are styles under the "claude" class, which the shell's
/// <see cref="Controls.AppearanceScope"/> takes.
/// </summary>
public sealed class AppColors(Application app)
{
    /// <summary>Claude's orange.</summary>
    public static readonly Color ClaudeAccent = Color.Parse("#D97757");

    /// <summary>Rider's blue.</summary>
    public static readonly Color RiderAccent = Color.Parse("#3574F0");

    /// <summary>The Standard style's interface font, Rider's: Inter, which Claudette ships.</summary>
    public static readonly FontFamily InterFont = new("fonts:Inter#Inter, $Default");

    /// <summary>
    /// Serifs close to the Claude apps' reply typeface, most alike first, with one on each OS: Charter on macOS (and
    /// as Bitstream Charter on Linux), Georgia and Cambria on Windows, Noto Serif and DejaVu Serif on Linux.
    /// </summary>
    public const string SerifFonts = "Charter, Bitstream Charter, Georgia, Cambria, Noto Serif, DejaVu Serif";

    private readonly Dictionary<(ThemeVariant Variant, object Key), object?> _standard = [];
    private ClaudeColors? _claude;

    /// <summary>The style applied; null until the first is.</summary>
    public AppStyle? Current { get; private set; }

    /// <summary>The interface's font: Inter in the Standard style, as Rider's; the system's in the Claude style.</summary>
    public static FontFamily UiFont(AppStyle style) => style == AppStyle.Standard ? InterFont : FontFamily.Default;

    /// <summary>
    /// The font of Claude's replies: the conversation font when one is set, otherwise a serif in the Claude style, as
    /// the Claude apps set replies, and the interface's in Standard.
    /// </summary>
    public static FontFamily ReplyFont(AppStyle style, string? conversationFont) =>
        conversationFont is { } font ? new FontFamily($"{font}, {FontFamily.DefaultFontFamilyName}")
        : style == AppStyle.Claude ? new FontFamily($"{SerifFonts}, {FontFamily.DefaultFontFamilyName}")
        : UiFont(style);

    public void Apply(AppStyle style)
    {
        if (style == Current)
        {
            return;
        }
        Current = style;
        var claude = style == AppStyle.Claude;
        _claude ??= new ClaudeColors();
        foreach (var (variant, provider) in _claude.ThemeDictionaries)
        {
            if (provider is not IResourceDictionary claudeTokens
                || !app.Resources.ThemeDictionaries.TryGetValue(variant, out var target)
                || target is not IResourceDictionary tokens)
            {
                continue;
            }
            foreach (var key in claudeTokens.Keys.ToList())
            {
                if (!_standard.ContainsKey((variant, key)))
                {
                    _standard[(variant, key)] = tokens.TryGetValue(key, out var original) ? original : null;
                }
                if (claude)
                {
                    tokens[key] = claudeTokens[key];
                }
                else if (_standard[(variant, key)] is { } original)
                {
                    tokens[key] = original;
                }
                else
                {
                    tokens.Remove(key);
                }
            }
        }
        // Fluent reads most palette colors once, when its resources are first used, so a palette set on the running
        // theme would only change the accent. A fresh theme with the palette already set reads them all.
        if (app.Styles.OfType<FluentTheme>().FirstOrDefault() is not { } fluent)
        {
            return;
        }
        var fresh = new FluentTheme { DensityStyle = fluent.DensityStyle };
        fresh.Palettes[ThemeVariant.Light] = claude ? LightPalette() : RiderLightPalette();
        fresh.Palettes[ThemeVariant.Dark] = claude ? DarkPalette() : RiderDarkPalette();
        app.Styles[app.Styles.IndexOf(fluent)] = fresh;
    }

    /// <summary>Rider's Islands Light: a light grey frame, black text, Rider's greys and blue.</summary>
    private static ColorPaletteResources RiderLightPalette() => new()
    {
        Accent = RiderAccent,
        RegionColor = Color.Parse("#EBECF0"),
        BaseHigh = Color.Parse("#000000"),
        BaseMediumHigh = Color.Parse("#CC000000"),
        BaseMedium = Color.Parse("#99000000"),
        BaseMediumLow = Color.Parse("#66000000"),
        BaseLow = Color.Parse("#33000000"),
        ChromeAltLow = Color.Parse("#000000"),
        ChromeDisabledHigh = Color.Parse("#EBECF0"),
        ChromeDisabledLow = Color.Parse("#A8ADBD"),
        ChromeGray = Color.Parse("#6C707E"),
        ChromeHigh = Color.Parse("#C9CCD6"),
        ChromeLow = Color.Parse("#F7F8FA"),
        ChromeMedium = Color.Parse("#EBECF0"),
        ChromeMediumLow = Color.Parse("#FFFFFF"),
        ListLow = Color.Parse("#0F000000"),
        ListMedium = Color.Parse("#1F000000"),
    };

    /// <summary>Rider's Islands Dark: a grey frame, darker islands, soft white text, Rider's greys and blue.</summary>
    private static ColorPaletteResources RiderDarkPalette() => new()
    {
        Accent = RiderAccent,
        RegionColor = Color.Parse("#26282C"),
        AltHigh = Color.Parse("#191A1C"),
        AltMediumHigh = Color.Parse("#CC191A1C"),
        AltMedium = Color.Parse("#99191A1C"),
        AltMediumLow = Color.Parse("#66191A1C"),
        AltLow = Color.Parse("#33191A1C"),
        BaseHigh = Color.Parse("#DFE1E5"),
        BaseMediumHigh = Color.Parse("#CCDFE1E5"),
        BaseMedium = Color.Parse("#99DFE1E5"),
        BaseMediumLow = Color.Parse("#66DFE1E5"),
        BaseLow = Color.Parse("#33DFE1E5"),
        ChromeAltLow = Color.Parse("#DFE1E5"),
        ChromeDisabledHigh = Color.Parse("#393B40"),
        ChromeDisabledLow = Color.Parse("#6F737A"),
        ChromeGray = Color.Parse("#868A91"),
        ChromeHigh = Color.Parse("#5A5D63"),
        ChromeLow = Color.Parse("#1E1F22"),
        ChromeMedium = Color.Parse("#2B2D30"),
        ChromeMediumLow = Color.Parse("#2B2D30"),
        ListLow = Color.Parse("#14FFFFFF"),
        ListMedium = Color.Parse("#29FFFFFF"),
    };

    /// <summary>Ivory page, warm greys, near-black text.</summary>
    private static ColorPaletteResources LightPalette() => new()
    {
        Accent = ClaudeAccent,
        RegionColor = Color.Parse("#FAF9F5"),
        BaseHigh = Color.Parse("#141413"),
        BaseMediumHigh = Color.Parse("#CC141413"),
        BaseMedium = Color.Parse("#99141413"),
        BaseMediumLow = Color.Parse("#66141413"),
        BaseLow = Color.Parse("#33141413"),
        ChromeAltLow = Color.Parse("#141413"),
        ChromeBlackHigh = Color.Parse("#141413"),
        ChromeBlackMedium = Color.Parse("#99141413"),
        ChromeBlackMediumLow = Color.Parse("#66141413"),
        ChromeBlackLow = Color.Parse("#33141413"),
        ChromeDisabledHigh = Color.Parse("#E8E6DC"),
        ChromeDisabledLow = Color.Parse("#8A8880"),
        ChromeGray = Color.Parse("#73726C"),
        ChromeHigh = Color.Parse("#D6D3C8"),
        ChromeLow = Color.Parse("#F5F4ED"),
        ChromeMedium = Color.Parse("#EDEBE3"),
        ChromeMediumLow = Color.Parse("#FAF9F5"),
        ListLow = Color.Parse("#141F1E1D"),
        ListMedium = Color.Parse("#291F1E1D"),
    };

    /// <summary>Charcoal page, darker sidebar, off-white text.</summary>
    private static ColorPaletteResources DarkPalette() => new()
    {
        Accent = ClaudeAccent,
        RegionColor = Color.Parse("#262624"),
        AltHigh = Color.Parse("#262624"),
        AltMediumHigh = Color.Parse("#CC262624"),
        AltMedium = Color.Parse("#99262624"),
        AltMediumLow = Color.Parse("#66262624"),
        AltLow = Color.Parse("#33262624"),
        BaseHigh = Color.Parse("#FAF9F5"),
        BaseMediumHigh = Color.Parse("#CCFAF9F5"),
        BaseMedium = Color.Parse("#99FAF9F5"),
        BaseMediumLow = Color.Parse("#66FAF9F5"),
        BaseLow = Color.Parse("#33FAF9F5"),
        ChromeAltLow = Color.Parse("#FAF9F5"),
        ChromeDisabledHigh = Color.Parse("#3A3935"),
        ChromeDisabledLow = Color.Parse("#8A8880"),
        ChromeGray = Color.Parse("#9C9A92"),
        ChromeHigh = Color.Parse("#6B6A65"),
        ChromeLow = Color.Parse("#1F1E1D"),
        ChromeMedium = Color.Parse("#2B2A28"),
        ChromeMediumLow = Color.Parse("#30302E"),
        ListLow = Color.Parse("#19FAF9F5"),
        ListMedium = Color.Parse("#33FAF9F5"),
    };
}
