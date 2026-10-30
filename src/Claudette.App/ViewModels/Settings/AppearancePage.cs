using Claudette.Core.Settings;

namespace Claudette.App.ViewModels.Settings;

/// <summary>A Style choice in Settings → Appearance (DESIGN.md §3, "Visual style").</summary>
public sealed record StyleOption(AppStyle Style, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Settings → Appearance → Motion's choices (DESIGN.md §3, "Accessibility").</summary>
public sealed record MotionChoice(MotionSetting Setting)
{
    public override string ToString() => Setting switch
    {
        MotionSetting.Reduce => "Reduce motion",
        MotionSetting.Full => "Don't reduce",
        _ => "Follow the system",
    };
}

/// <summary>Claudette's sizes in Settings → Appearance (DESIGN.md §5, "Claudette on the composer").</summary>
public sealed record ClaudetteSizeChoice(ClaudetteSize Size)
{
    public override string ToString() => Size switch
    {
        ClaudetteSize.Small => "Small",
        ClaudetteSize.Large => "Large",
        _ => "Medium",
    };
}

/// <summary>How often Claudette does something, in Settings → Appearance (DESIGN.md §5, "Claudette on the composer").</summary>
public sealed record ClaudetteLivelinessChoice(ClaudetteLiveliness Liveliness)
{
    public override string ToString() => Liveliness == ClaudetteLiveliness.Calm ? "Calm: every 30 to 90 seconds" : "Lively: every 10 to 30 seconds";
}

/// <summary>Settings → Appearance → Zoom's steps (DESIGN.md §3, "Accessibility").</summary>
public sealed record ZoomChoice(int Percent)
{
    public override string ToString() => $"{Percent}%";
}

/// <summary>
/// Settings → Appearance (DESIGN.md §14): the preview, theme, style, density and width, the fonts, zoom and motion, and
/// Claudette on the composer with her games.
/// </summary>
public sealed class AppearancePage : SettingsPage
{
    public AppearancePage(SettingsContext context) : base(context, SettingsCategory.Appearance)
    {
        Section("look", "Look",
        [
            Setting("appearance.theme", "Theme", s => s.Appearance.Theme, (s, v) => s.Appearance.Theme = v,
                "Light, dark, or as your computer is.", keywords: ["dark mode", "light mode", "colors"]),
            Setting("appearance.style", "Style", s => s.Appearance.Style, (s, v) => s.Appearance.Style = v,
                "Claudette's own look, or the Claude apps'.",
                "Claude looks like the Claude apps: ivory and warm greys, Claude's orange, rounder cards and serif replies. Standard uses neutral greys and your computer's accent color.",
                ["claude style", "standard", "look", "theme"]),
            Setting("appearance.density", "Density", s => s.Appearance.Density, (s, v) => s.Appearance.Density = v,
                "Compact tightens the conversation, the sidebar's rows and the message box.", keywords: ["compact", "comfortable", "spacing"]),
            Setting("appearance.fullWidthConversation", "Full-width conversation", s => s.Appearance.FullWidthConversation, (s, v) => s.Appearance.FullWidthConversation = v,
                "Fill the tab's width, rather than stopping at a width that's easy to read.", keywords: ["wide", "narrow", "max width"]),
            Setting("appearance.systemTitleBar", "Use the system's title bar", s => s.Appearance.SystemTitleBar, (s, v) => s.Appearance.SystemTitleBar = v,
                "Your system's title bar on each window, rather than Claudette's own.",
                "Claudette's own: the main window's header is its title bar, and its other windows have one in the same style. Drag a title bar's gaps to move the window, and double-click them to maximize it. Linux always has the system's.",
                ["title bar", "window frame", "caption"]),
        ]);
        Section("text", "Text",
        [
            Setting("appearance.conversationFont", "Conversation font", s => s.Appearance.ConversationFont, (s, v) => s.Appearance.ConversationFont = v,
                "Empty uses Claudette's own; one that isn't installed falls back to it.", keywords: ["typeface", "font family"]),
            Setting("appearance.conversationFontSize", "Conversation font size", s => s.Appearance.ConversationFontSize, (s, v) => s.Appearance.ConversationFontSize = v,
                keywords: ["text size", "bigger", "smaller"]),
            Setting("appearance.codeFont", "Code font", s => s.Appearance.CodeFont, (s, v) => s.Appearance.CodeFont = v,
                "Empty uses Cascadia Mono, Consolas or Menlo.", keywords: ["monospace", "typeface"]),
            Setting("appearance.codeFontSize", "Code font size", s => s.Appearance.CodeFontSize, (s, v) => s.Appearance.CodeFontSize = v,
                keywords: ["monospace", "text size"]),
        ]);
        Section("accessibility", "Accessibility",
        [
            Setting("appearance.zoom", "Zoom", s => s.Appearance.Zoom, (s, v) => s.Appearance.Zoom = v,
                "Everything in the main window, bigger or smaller.",
                "Ctrl and + or −, or Ctrl+0, change it too (Cmd on a Mac). Each computer keeps its own, to suit its screen.",
                ["scale", "bigger", "magnify"]),
            Setting("appearance.motion", "Motion", s => s.Appearance.Motion, (s, v) => s.Appearance.Motion = v,
                "Whether the busy dots pulse, the working line twinkles and the icon moves.",
                "Following the system, motion is reduced when your computer's settings, or Claude Code's prefersReducedMotion, ask for it. Claudette on the composer stands still then too.",
                ["reduce motion", "animation", "accessibility"]),
        ]);
        Section("claudette", "Claudette",
        [
            Setting("appearance.showClaudette", "Show Claudette on the composer", s => s.Appearance.ShowClaudette, (s, v) => s.Appearance.ShowClaudette = v,
                "She stands on the message box and does little things every so often.",
                "She walks about, dances, juggles, sits on the edge, and sometimes falls off behind it. While Claude works she works too, with whatever suits what it's doing, and she points out other tabs that need you. Click her to startle her, drag her about, or right-click to hide her. She stands still when motion is reduced.",
                ["mascot", "character"]),
            Setting("appearance.claudetteSize", "Size", s => s.Appearance.ClaudetteSize, (s, v) => s.Appearance.ClaudetteSize = v, keywords: ["claudette", "mascot"]),
            Setting("appearance.claudetteLiveliness", "How often", s => s.Appearance.ClaudetteLiveliness, (s, v) => s.Appearance.ClaudetteLiveliness = v,
                "How often she does something.", keywords: ["claudette", "mascot", "calm", "lively"]),
            Setting("appearance.claudetteTips", "Shares tips", s => s.Appearance.ClaudetteTips, (s, v) => s.Appearance.ClaudetteTips = v,
                "Now and then she holds up a tip, such as a shortcut you might not know.",
                "At most every ten minutes, and never while you're typing.", ["claudette", "hints"]),
            Setting("appearance.gamesWithClaudette", "Games with Claudette", s => s.Appearance.GamesWithClaudette, (s, v) => s.Appearance.GamesWithClaudette = v,
                "Tic-tac-toe, Connect Four, hangman or Minesweeper against her while Claude works.",
                "Or while a usage limit holds a task. Start one by right-clicking her, from the command palette, or from the usage limit bar; it opens in the Games tool window. She plays whether or not she's on the composer.",
                ["play", "tic-tac-toe", "connect four", "hangman", "minesweeper"]),
        ]);
        context.SettingsSaved += OnPreviewSettingsSaved;
        context.AllPagesChanged += OnPreviewSettingsSaved;
    }

    public override string IconKey => "IconSettingsAppearance";

    // ---- The preview (DESIGN.md §14, "Appearance"): a little of a tab, as these settings draw it ------------------------

    /// <summary>The preview's cards and message box are drawn compact.</summary>
    public bool IsCompact => Settings.Appearance.Density == Density.Compact;

    /// <summary>The preview's in-between steps are quieter, as Settings → Conversation says.</summary>
    public bool QuietSteps => Settings.Appearance.QuietSteps;

    /// <summary>The preview's message starts further in, as Settings → Conversation says.</summary>
    public bool IndentYou => Settings.Appearance.IndentYourMessages;

    /// <summary>The preview's thinking starts a little in, as Settings → Conversation says.</summary>
    public bool IndentSteps => Settings.Appearance.IndentClaudesSteps;

    /// <summary>The preview drawn at the main window's zoom.</summary>
    public double ZoomScale => Settings.Appearance.Zoom / 100.0;

    /// <summary>Screen pixels in a cell of Claudette's sprite, for her size in the preview.</summary>
    public double ClaudetteCell => Settings.Appearance.ClaudetteSize switch
    {
        Core.Settings.ClaudetteSize.Small => 2,
        Core.Settings.ClaudetteSize.Large => 4,
        _ => 3,
    };

    /// <summary>A setting saved anywhere (Conversation's quieter steps, the zoom keys) shows in the preview.</summary>
    private void OnPreviewSettingsSaved()
    {
        OnPropertyChanged(nameof(IsCompact));
        OnPropertyChanged(nameof(QuietSteps));
        OnPropertyChanged(nameof(IndentYou));
        OnPropertyChanged(nameof(IndentSteps));
        OnPropertyChanged(nameof(ZoomScale));
        OnPropertyChanged(nameof(ClaudetteCell));
    }

    public override string Description => "How Claudette looks: in every tab, and on this computer's screen.";

    public IReadOnlyList<ThemeChoice> Themes { get; } = [ThemeChoice.System, ThemeChoice.Light, ThemeChoice.Dark];

    public ThemeChoice Theme
    {
        get => Settings.Appearance.Theme;
        set => Set(value, v => Settings.Appearance.Theme = v);
    }

    /// <summary>Settings → Appearance → Style (DESIGN.md §3, "Visual style"), named as the list shows them.</summary>
    public IReadOnlyList<StyleOption> StyleOptions { get; } =
        [new(AppStyle.Standard, "Standard"), new(AppStyle.Claude, "Claude")];

    public StyleOption Style
    {
        get => StyleOptions.FirstOrDefault(o => o.Style == Settings.Appearance.Style) ?? StyleOptions[0];
        set => Set(value, v => Settings.Appearance.Style = v?.Style ?? AppStyle.Standard);
    }

    public IReadOnlyList<Density> Densities { get; } = [Density.Comfortable, Density.Compact];

    /// <summary>Compact tightens the conversation, the sidebar's rows and the composer (DESIGN.md §14).</summary>
    public Density Density
    {
        get => Settings.Appearance.Density;
        set => Set(value, v => Settings.Appearance.Density = v);
    }

    /// <summary>The conversation and the composer fill the tab's width rather than stopping at a readable one (DESIGN.md §14).</summary>
    public bool FullWidthConversation
    {
        get => Settings.Appearance.FullWidthConversation;
        set => Set(value, v => Settings.Appearance.FullWidthConversation = v);
    }

    public bool SystemTitleBar
    {
        get => Settings.Appearance.SystemTitleBar;
        set => Set(value, v => Settings.Appearance.SystemTitleBar = v);
    }

    /// <summary>Linux always has the system's title bar, so the switch is only for Windows and macOS.</summary>
    public bool CanMergeTitleBar => Context.Services.Platform.CanMergeTitleBar;

    // ---- Fonts (DESIGN.md §14, "Appearance") ----------------------------------------------------------------------

    /// <summary>The fonts installed here, to pick from; any other name can be typed.</summary>
    public IReadOnlyList<string> InstalledFonts => field ??= Services.Platform.InstalledFonts();

    /// <summary>Empty for Claudette's own font.</summary>
    public string ConversationFont
    {
        get => Settings.Appearance.ConversationFont ?? "";
        set => Set(value, v => Settings.Appearance.ConversationFont = string.IsNullOrWhiteSpace(v) ? null : v.Trim());
    }

    public decimal? ConversationFontSize
    {
        get => (decimal)Settings.Appearance.ConversationFontSize;
        set => Set(value, v => Settings.Appearance.ConversationFontSize = Math.Clamp((double)(v ?? 14), 9, 28));
    }

    /// <summary>Empty for the default monospace fonts.</summary>
    public string CodeFont
    {
        get => Settings.Appearance.CodeFont ?? "";
        set => Set(value, v => Settings.Appearance.CodeFont = string.IsNullOrWhiteSpace(v) ? null : v.Trim());
    }

    public decimal? CodeFontSize
    {
        get => (decimal)Settings.Appearance.CodeFontSize;
        set => Set(value, v => Settings.Appearance.CodeFontSize = Math.Clamp((double)(v ?? 13), 8, 28));
    }

    // ---- Accessibility (DESIGN.md §3): the main window's scale, and whether anything moves -----------------------

    public IReadOnlyList<ZoomChoice> ZoomChoices { get; } = [.. Core.Accessibility.Zoom.Steps.Select(percent => new ZoomChoice(percent))];

    /// <summary>The main window's content, scaled (DESIGN.md §3, "Accessibility"); Ctrl/Cmd +, − and 0 change it too.</summary>
    public ZoomChoice Zoom
    {
        get => ZoomChoices.FirstOrDefault(c => c.Percent == Settings.Appearance.Zoom) ?? new ZoomChoice(Settings.Appearance.Zoom);
        set => Set(value?.Percent ?? Core.Accessibility.Zoom.Default, v => Settings.Appearance.Zoom = v);
    }

    public IReadOnlyList<MotionChoice> MotionChoices { get; } =
        [new(MotionSetting.System), new(MotionSetting.Reduce), new(MotionSetting.Full)];

    /// <summary>Whether the busy dots pulse, the working glyph twinkles and the taskbar or Dock icon moves.</summary>
    public MotionChoice Motion
    {
        get => MotionChoices.FirstOrDefault(c => c.Setting == Settings.Appearance.Motion) ?? MotionChoices[0];
        set => Set(value?.Setting ?? MotionSetting.System, v => Settings.Appearance.Motion = v);
    }

    // ---- Claudette on the composer (DESIGN.md §5) -----------------------------------------------------------------

    /// <summary>Claudette standing on the composer, doing little things now and then (DESIGN.md §5, "Claudette on the composer").</summary>
    public bool ShowClaudette
    {
        get => Settings.Appearance.ShowClaudette;
        set => Set(value, v => Settings.Appearance.ShowClaudette = v);
    }

    public IReadOnlyList<ClaudetteSizeChoice> ClaudetteSizes { get; } =
        [new(Core.Settings.ClaudetteSize.Small), new(Core.Settings.ClaudetteSize.Medium), new(Core.Settings.ClaudetteSize.Large)];

    /// <summary>How big she is: 24, 36 or 48 pixels tall at 100%.</summary>
    public ClaudetteSizeChoice ClaudetteSize
    {
        get => ClaudetteSizes.FirstOrDefault(c => c.Size == Settings.Appearance.ClaudetteSize) ?? ClaudetteSizes[1];
        set => Set(value?.Size ?? Core.Settings.ClaudetteSize.Medium, v => Settings.Appearance.ClaudetteSize = v);
    }

    public IReadOnlyList<ClaudetteLivelinessChoice> ClaudetteLivelinesses { get; } =
        [new(Core.Settings.ClaudetteLiveliness.Lively), new(Core.Settings.ClaudetteLiveliness.Calm)];

    /// <summary>How often she does something.</summary>
    public ClaudetteLivelinessChoice ClaudetteLiveliness
    {
        get => ClaudetteLivelinesses.FirstOrDefault(c => c.Liveliness == Settings.Appearance.ClaudetteLiveliness) ?? ClaudetteLivelinesses[0];
        set => Set(value?.Liveliness ?? Core.Settings.ClaudetteLiveliness.Lively, v => Settings.Appearance.ClaudetteLiveliness = v);
    }

    /// <summary>She shares a tip now and then, such as a shortcut.</summary>
    public bool ClaudetteTips
    {
        get => Settings.Appearance.ClaudetteTips;
        set => Set(value, v => Settings.Appearance.ClaudetteTips = v);
    }

    /// <summary>Tic-tac-toe, Connect Four, hangman and Minesweeper against her (DESIGN.md §18, "Games with Claudette").</summary>
    public bool GamesWithClaudette
    {
        get => Settings.Appearance.GamesWithClaudette;
        set => Set(value, v => Settings.Appearance.GamesWithClaudette = v);
    }
}
