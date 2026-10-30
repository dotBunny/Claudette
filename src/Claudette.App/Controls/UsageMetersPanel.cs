using Avalonia;
using Avalonia.Controls;

namespace Claudette.App.Controls;

/// <summary>A part of the usage header's meters, for <see cref="UsageMetersPanel"/>.</summary>
public enum UsageMetersPart
{
    /// <summary>The session's name, or in the compact header its whole meter. Always there.</summary>
    Session,

    /// <summary>The session's bar, after its name: as wide as <see cref="UsageMetersPanel.BarWidth"/> while there's room.</summary>
    SessionBar,

    /// <summary>The session's percent, after its bar.</summary>
    SessionPercent,

    /// <summary>The session's countdown, after its percent.</summary>
    Reset,

    /// <summary>The plain-language projection, under the session's line and as wide as it.</summary>
    Projection,

    /// <summary>The trendline, as tall as the panel.</summary>
    Trend,

    /// <summary>The weekly meters.</summary>
    Weekly,
}

/// <summary>
/// Lays out the usage header's meters (DESIGN.md §6, "Header meters"), left to right: the session's line, its name, bar,
/// percent and countdown, with the projection under it, then the trendline, then the weekly meters. Where the header
/// hasn't the room for them all, it leaves out the trendline first, then the countdown, then the weekly meters, and last
/// narrows the session's bar from <see cref="BarWidth"/> to <see cref="BarMinWidth"/>, so the session's meter always shows
/// rather than being cut off. What it leaves out is still measured, and laid out past its edge, where the panel clips it,
/// so the room it would take is always known and it comes back as soon as there's room. The bar is given its width rather
/// than asked for it: a progress bar's own measure follows its last layout.
/// </summary>
public sealed class UsageMetersPanel : Panel
{
    public static readonly AttachedProperty<UsageMetersPart> PartProperty =
        AvaloniaProperty.RegisterAttached<UsageMetersPanel, Control, UsageMetersPart>("Part");

    /// <summary>Set on a part the panel left out for want of room.</summary>
    public static readonly AttachedProperty<bool> IsLeftOutProperty =
        AvaloniaProperty.RegisterAttached<UsageMetersPanel, Control, bool>("IsLeftOut");

    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<UsageMetersPanel, double>(nameof(Spacing), 14);

    public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<UsageMetersPanel, double>(nameof(LineSpacing), 8);

    public static readonly StyledProperty<double> BarWidthProperty = AvaloniaProperty.Register<UsageMetersPanel, double>(nameof(BarWidth), 160);

    public static readonly StyledProperty<double> BarMinWidthProperty = AvaloniaProperty.Register<UsageMetersPanel, double>(nameof(BarMinWidth), 40);

    /// <summary>What it shows, richest first: each leaves out one more part than the one before.</summary>
    private static readonly (bool Trend, bool Reset, bool Weekly)[] Levels = [(true, true, true), (false, true, true), (false, false, true), (false, false, false)];

    private (bool Trend, bool Reset, bool Weekly) _shown = Levels[0];
    private double _barWidth;
    private double _lineHeight;

    static UsageMetersPanel()
    {
        AffectsMeasure<UsageMetersPanel>(SpacingProperty, LineSpacingProperty, BarWidthProperty, BarMinWidthProperty);
        ClipToBoundsProperty.OverrideDefaultValue<UsageMetersPanel>(true);
    }

    public static UsageMetersPart GetPart(Control control) => control.GetValue(PartProperty);

    public static void SetPart(Control control, UsageMetersPart value) => control.SetValue(PartProperty, value);

    public static bool GetIsLeftOut(Control control) => control.GetValue(IsLeftOutProperty);

    /// <summary>Between the session's line, the trendline and the weekly meters.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Between the parts of the session's line.</summary>
    public double LineSpacing
    {
        get => GetValue(LineSpacingProperty);
        set => SetValue(LineSpacingProperty, value);
    }

    /// <summary>The session's bar while there's room for it.</summary>
    public double BarWidth
    {
        get => GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    /// <summary>The session's bar at its narrowest, once everything else is left out.</summary>
    public double BarMinWidth
    {
        get => GetValue(BarMinWidthProperty);
        set => SetValue(BarMinWidthProperty, value);
    }

    private Control? Find(UsageMetersPart part) => Children.FirstOrDefault(c => GetPart(c) == part && c.IsVisible);

    private static double WidthOf(Control? part) => part?.DesiredSize.Width ?? 0;

    private static double HeightOf(Control? part) => part?.DesiredSize.Height ?? 0;

    /// <summary>A part of the session's line after the first, with the space before it: none for a part that isn't there.</summary>
    private double After(Control? part, double width) => part is null ? 0 : LineSpacing + width;

    /// <summary>The session's line without its countdown, its bar <paramref name="bar"/> wide.</summary>
    private double SessionWidth(double bar) =>
        WidthOf(Find(UsageMetersPart.Session))
        + After(Find(UsageMetersPart.SessionBar), bar)
        + After(Find(UsageMetersPart.SessionPercent), WidthOf(Find(UsageMetersPart.SessionPercent)));

    /// <summary>How wide the meters are showing <paramref name="shown"/>, the session's bar <paramref name="bar"/> wide.</summary>
    private double WidthOf((bool Trend, bool Reset, bool Weekly) shown, double bar) =>
        SessionWidth(bar)
        + (shown.Reset ? After(Find(UsageMetersPart.Reset), WidthOf(Find(UsageMetersPart.Reset))) : 0)
        + (shown.Trend && Find(UsageMetersPart.Trend) is { } trend ? Spacing + WidthOf(trend) : 0)
        + (shown.Weekly && Find(UsageMetersPart.Weekly) is { } weekly ? Spacing + WidthOf(weekly) : 0);

    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            var part = GetPart(child);
            if (part == UsageMetersPart.SessionBar)
            {
                child.Measure(new Size(BarWidth, double.PositiveInfinity));
            }
            else if (part != UsageMetersPart.Projection)
            {
                child.Measure(unbounded);
            }
        }
        var room = availableSize.Width;
        _shown = Levels.FirstOrDefault(level => WidthOf(level, BarWidth) <= room, Levels[^1]);
        var narrowBy = WidthOf(_shown, BarWidth) - room;
        _barWidth = narrowBy <= 0 ? BarWidth : Math.Max(BarMinWidth, BarWidth - narrowBy);

        var reset = _shown.Reset ? Find(UsageMetersPart.Reset) : null;
        var line = SessionWidth(_barWidth) + After(reset, WidthOf(reset));
        _lineHeight = new[] { Find(UsageMetersPart.Session), Find(UsageMetersPart.SessionBar), Find(UsageMetersPart.SessionPercent), reset }.Max(HeightOf);
        // The projection trims to the line's width rather than widen it.
        var projection = Find(UsageMetersPart.Projection);
        projection?.Measure(new Size(line, double.PositiveInfinity));
        var weekly = _shown.Weekly ? Find(UsageMetersPart.Weekly) : null;
        var height = Math.Max(_lineHeight + HeightOf(projection), HeightOf(weekly));
        return new Size(WidthOf(_shown, _barWidth), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var height = finalSize.Height;
        double Middle(Control part, double within) => Math.Max(0, (within - HeightOf(part)) / 2);
        foreach (var child in Children)
        {
            var shown = GetPart(child) switch
            {
                UsageMetersPart.Trend => _shown.Trend,
                UsageMetersPart.Reset => _shown.Reset,
                UsageMetersPart.Weekly => _shown.Weekly,
                _ => true,
            };
            child.SetValue(IsLeftOutProperty, !shown);
            if (!shown)
            {
                // Past the edge, where the panel clips it, and nothing reaches it.
                child.Arrange(new Rect(finalSize.Width + WidthOf(child) + 100, 0, WidthOf(child), HeightOf(child)));
            }
        }

        // The session's line, its parts one after another, each in the middle of the line's height.
        var x = 0.0;
        var first = true;
        foreach (var part in (UsageMetersPart[])[UsageMetersPart.Session, UsageMetersPart.SessionBar, UsageMetersPart.SessionPercent, UsageMetersPart.Reset])
        {
            if (Find(part) is not { } control || (part == UsageMetersPart.Reset && !_shown.Reset))
            {
                continue;
            }
            x += first ? 0 : LineSpacing;
            first = false;
            var width = part == UsageMetersPart.SessionBar ? _barWidth : WidthOf(control);
            control.Arrange(new Rect(x, Middle(control, _lineHeight), width, HeightOf(control)));
            x += width;
        }
        if (Find(UsageMetersPart.Projection) is { } projection)
        {
            projection.Arrange(new Rect(0, _lineHeight, x, HeightOf(projection)));
        }
        if (_shown.Trend && Find(UsageMetersPart.Trend) is { } trend)
        {
            trend.Arrange(new Rect(x + Spacing, 0, WidthOf(trend), height));
            x += Spacing + WidthOf(trend);
        }
        if (_shown.Weekly && Find(UsageMetersPart.Weekly) is { } weekly)
        {
            weekly.Arrange(new Rect(x + Spacing, Middle(weekly, height), WidthOf(weekly), HeightOf(weekly)));
        }
        return finalSize;
    }
}
