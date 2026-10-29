using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Claudette.App.Services;

namespace Claudette.App.Controls;

/// <summary>
/// Whether Claudette's windows draw their own title bars (DESIGN.md §3, "Window frame"), as the main window's header is its
/// title bar: on Windows and macOS (<see cref="IPlatformServices.CanMergeTitleBar"/>), unless Settings → Appearance → Use
/// the system's title bar is on. Until Claudette has its services, as in tests, windows have the system's.
/// </summary>
public static class WindowFrame
{
    /// <summary>The window's buttons Avalonia draws on Windows, when they can't be found: three, 45 pixels wide each.</summary>
    internal const double DrawnButtonsWidth = 3 * 45;

    /// <summary>Room at a title bar's left for macOS's traffic lights.</summary>
    internal const double TrafficLightsWidth = 72;

    private static AppServices? _services;

    /// <summary>The windows' own title bars came or went: the setting changed.</summary>
    public static event Action? Changed;

    /// <summary>The windows draw their own title bars.</summary>
    public static bool Merges => _services is { } services && services.Platform.CanMergeTitleBar && !services.Settings.Appearance.SystemTitleBar;

    /// <summary>From the app's start: its services say which, and Settings changing it applies at once.</summary>
    public static void Configure(AppServices services)
    {
        _services = services;
        services.SettingsChanged += (_, _) => Changed?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>
    /// The room a title bar of the window's own leaves the window's buttons, in the units of what's in it (the main
    /// window's header is zoomed, the buttons aren't): at its left for macOS's traffic lights, at its right for the buttons
    /// Avalonia draws on Windows, measured where they are. None while the window has the system's title bar.
    /// </summary>
    public static Thickness ButtonsRoom(Window window, double zoom = 1)
    {
        if (!window.IsExtendedIntoWindowDecorations)
        {
            return default;
        }
        if (OperatingSystem.IsMacOS())
        {
            return new Thickness(TrafficLightsWidth / zoom, 0, 0, 0);
        }
        var buttons = window.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(p => p is { Name: "PART_OverlayPanel", IsEffectivelyVisible: true });
        return new Thickness(0, 0, (buttons?.Bounds.Width ?? DrawnButtonsWidth) / zoom, 0);
    }
}

/// <summary>
/// A window's title bar of its own (DESIGN.md §3, "Window frame"), at the top of each window but the main one, whose
/// header is its title bar. While windows draw their own (<see cref="WindowFrame.Merges"/>), the window draws into it:
/// its gaps drag the window and its controls take their own clicks, it has the window's title unless what's in it names
/// what the window shows (<see cref="ShowsTitle"/>), and it leaves the window's buttons their room. With the system's
/// title bar, it's what's in it, and with nothing in it, nothing. Its look is Themes/WindowFrameStyles.axaml.
/// </summary>
public sealed class TitleBar : ContentControl
{
    public static readonly StyledProperty<bool> ShowsTitleProperty = AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowsTitle), true);

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<TitleBar, string?>(nameof(Title));

    public static readonly StyledProperty<Thickness> ButtonsRoomProperty = AvaloniaProperty.Register<TitleBar, Thickness>(nameof(ButtonsRoom));

    public static readonly StyledProperty<bool?> MergesProperty = AvaloniaProperty.Register<TitleBar, bool?>(nameof(Merges));

    private Window? _window;

    public TitleBar() => LayoutUpdated += (_, _) => Fit();

    /// <summary>It has the window's title, while the window draws it: off where what's in it names what the window shows.</summary>
    public bool ShowsTitle
    {
        get => GetValue(ShowsTitleProperty);
        set => SetValue(ShowsTitleProperty, value);
    }

    /// <summary>The window's title, kept with it.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        private set => SetValue(TitleProperty, value);
    }

    /// <summary>The room the window's buttons take, which what's in it keeps clear of.</summary>
    public Thickness ButtonsRoom
    {
        get => GetValue(ButtonsRoomProperty);
        private set => SetValue(ButtonsRoomProperty, value);
    }

    /// <summary>For tests: the window draws its title bar, or not, whatever <see cref="WindowFrame.Merges"/> says.</summary>
    public bool? Merges
    {
        get => GetValue(MergesProperty);
        set => SetValue(MergesProperty, value);
    }

    /// <summary>The window draws into it now.</summary>
    public bool IsMerged => Merges ?? WindowFrame.Merges;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is null)
        {
            return;
        }
        _window.PropertyChanged += OnWindowChanged;
        WindowFrame.Changed += Apply;
        Apply();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WindowFrame.Changed -= Apply;
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowChanged;
            _window = null;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == MergesProperty)
        {
            Apply();
        }
    }

    private void OnWindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.TitleProperty)
        {
            Title = _window?.Title;
        }
    }

    /// <summary>The window draws its title bar here, or has the system's; it's there with the title, or what's in it.</summary>
    private void Apply()
    {
        var merged = IsMerged;
        PseudoClasses.Set(":merged", merged);
        IsVisible = merged || Content is not null;
        if (_window is not { } window)
        {
            return;
        }
        window.ExtendClientAreaToDecorationsHint = merged;
        Title = window.Title;
        Fit();
    }

    /// <summary>The window's title bar as tall as this, so the window's buttons are, and this clear of them.</summary>
    private void Fit()
    {
        if (_window is not { } window)
        {
            return;
        }
        var room = WindowFrame.ButtonsRoom(window);
        if (ButtonsRoom != room)
        {
            ButtonsRoom = room;
        }
        if (window.IsExtendedIntoWindowDecorations && Bounds.Height > 0 && Math.Abs(window.ExtendClientAreaTitleBarHeightHint - Bounds.Height) > 0.5)
        {
            window.ExtendClientAreaTitleBarHeightHint = Bounds.Height;
        }
    }
}
