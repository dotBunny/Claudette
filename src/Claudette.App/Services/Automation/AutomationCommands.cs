using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Claudette.App.Controls;
using Claudette.Platform.Shell.Mac;

namespace Claudette.App.Services.Automation;

/// <summary>
/// Carries out the screenshot scripts' commands (DESIGN.md §9, "Taking screenshots without the focus") on the UI
/// thread, as the mouse or keyboard would, but without the OS's input, so no window comes to the front. A control is the
/// one UI Automation found: in the window with the handle given, or a popup open over it (UI Automation shows a popup
/// in its window), the one whose automation name, id and type are those given, nearest where UI Automation saw it. A
/// click is a pointer's press and release, raised on what a pointer in the control's middle would hit, as Avalonia
/// raises them for a real click; a right-click asks for the context menu there, as a real one does. They never pass
/// through the window's messages, where Avalonia focuses a window clicked in. Other commands go through the control's
/// automation peer, as UI Automation would.
/// </summary>
public sealed class AutomationCommands
{
    private readonly Func<long, TopLevel?> _findWindow;
    private readonly Func<string?, TopLevel?> _findByTitle;
    private readonly Func<IEnumerable<TopLevel>> _popups;
    private readonly Func<TopLevel, IReadOnlyList<MacWindowButton>> _windowButtons;

    /// <param name="findWindow">The window or popup with a native handle; tests give their own.</param>
    /// <param name="popups">The popups open now, which a control may be in whatever window is named.</param>
    /// <param name="findByTitle">The window with a title, or for none the main window, for a request without a handle.</param>
    /// <param name="windowButtons">The OS's buttons over a window, which aren't in what it draws: macOS's traffic lights.</param>
    public AutomationCommands(Func<long, TopLevel?> findWindow, Func<IEnumerable<TopLevel>>? popups = null, Func<string?, TopLevel?>? findByTitle = null,
        Func<TopLevel, IReadOnlyList<MacWindowButton>>? windowButtons = null)
    {
        _findWindow = findWindow;
        _popups = popups ?? (() => []);
        _findByTitle = findByTitle ?? (_ => null);
        _windowButtons = windowButtons ?? (_ => []);
    }

    /// <summary>Claudette's own windows and the popups open over them, found by native handle, or by title.</summary>
    public static AutomationCommands ForApp()
    {
        static IClassicDesktopStyleApplicationLifetime? Desktop() => Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        Func<TopLevel, IReadOnlyList<MacWindowButton>>? windowButtons = null;
        if (OperatingSystem.IsMacOS())
        {
            windowButtons = TrafficLights;
        }
        return new(handle => (Desktop()?.Windows ?? []).Concat(OpenPopups.Roots).FirstOrDefault(t => t.TryGetPlatformHandle()?.Handle == handle),
            () => OpenPopups.Roots,
            title => title is null ? Desktop()?.MainWindow : Desktop()?.Windows.FirstOrDefault(w => w.Title == title),
            windowButtons);
    }

    /// <summary>The traffic lights over a window drawing into its title bar; with the system's, they're in that instead.</summary>
    [SupportedOSPlatform("macos")]
    private static IReadOnlyList<MacWindowButton> TrafficLights(TopLevel window) =>
        window is Window { IsExtendedIntoWindowDecorations: true } && window.TryGetPlatformHandle() is IMacOSTopLevelPlatformHandle mac
            ? MacWindowButtons.Over(mac.NSView)
            : [];

    public AutomationResponse Execute(AutomationRequest request)
    {
        try
        {
            return request.Verb switch
            {
                "ping" => AutomationResponse.Done(),
                "click" => Click(Find(request), right: false),
                "rightclick" => Click(Find(request), right: true),
                "toggle" => Toggle(Find(request), request.On ?? throw new AutomationException("toggle needs on: true or false.")),
                "settext" => SetText(Find(request), request.Text ?? ""),
                "show" => Show(Find(request)),
                "scroll" => Scroll(Find(request), request.Lines),
                "hover" => Tip(Find(request), true),
                "unhover" => Tip(Find(request), false),
                "key" => Press(Window(request), request.Keys ?? throw new AutomationException("key needs keys, such as Ctrl+Shift+P.")),
                "capture" => Capture(Window(request), request.Path ?? throw new AutomationException("capture needs a path to save the picture at.")),
                "resize" => Resize(Window(request), request.Width ?? 0, request.Height ?? 0),
                "close" => Close(Window(request)),
                _ => AutomationResponse.Failed($"There's no command '{request.Verb}'."),
            };
        }
        catch (AutomationException ex)
        {
            return AutomationResponse.Failed(ex.Message);
        }
    }

    // ---- Finding the control ------------------------------------------------------------------------------------------

    private TopLevel Window(AutomationRequest request) => request.Window != 0
        ? _findWindow(request.Window) ?? throw new AutomationException($"No window of Claudette's has the handle {request.Window}.")
        : _findByTitle(request.Title) ?? throw new AutomationException(request.Title is { } title
            ? $"No window of Claudette's is called '{title}'."
            : "Claudette has no main window yet.");

    private Control Find(AutomationRequest request)
    {
        var root = Window(request);
        if (request.Name is null && string.IsNullOrEmpty(request.Id))
        {
            throw new AutomationException("Name the control, by its name or its id.");
        }
        var shown = _popups().Where(p => p != root).Prepend(root)
            .SelectMany(r => r.GetVisualDescendants().Prepend(r)).OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
        var found = shown.Where(c => Matches(c, request, byType: true)).ToList();
        if (found.Count == 0 && request.Type is not null)
        {
            // Avalonia names a few types differently from UI Automation; the name and id are enough.
            found = [.. shown.Where(c => Matches(c, request, byType: false))];
        }
        if (found.Count == 0)
        {
            var names = shown.Select(c => ControlAutomationPeer.CreatePeerForElement(c).GetName()).Where(n => n.Length > 0).Distinct().Take(12);
            throw new AutomationException($"No control named '{request.Name}'{(string.IsNullOrEmpty(request.Id) ? "" : $" (#{request.Id})")} is showing in the window, "
                + $"a {root.GetType().Name}; it shows {string.Join(", ", names.Select(n => $"'{n}'"))}.");
        }
        return request is { X: { } x, Y: { } y } && found.Count > 1 ? found.MinBy(c => DistanceSquared(c, x, y))! : found[0];
    }

    private static bool Matches(Control control, AutomationRequest request, bool byType)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        return (request.Name is null || peer.GetName() == request.Name)
            && (string.IsNullOrEmpty(request.Id) || peer.GetAutomationId() == request.Id)
            && (!byType || request.Type is null || peer.GetAutomationControlType().ToString() == request.Type);
    }

    private static double DistanceSquared(Control control, double x, double y)
    {
        var topLeft = control.PointToScreen(default);
        var bottomRight = control.PointToScreen(new Point(control.Bounds.Width, control.Bounds.Height));
        var dx = (topLeft.X + bottomRight.X) / 2.0 - x;
        var dy = (topLeft.Y + bottomRight.Y) / 2.0 - y;
        return dx * dx + dy * dy;
    }

    /// <summary>The control and the ones around it, nearest first.</summary>
    private static IEnumerable<Control> AndAround(Control control) => control.GetSelfAndVisualAncestors().OfType<Control>();

    // ---- The commands -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The window as Claudette draws it, at its scale, saved as a PNG: for screenshots on any OS, where there's no
    /// PrintWindow to capture with. The OS's own frame, where it draws one, isn't in it, nor are popups. The window's
    /// buttons are: Avalonia's on Windows, which are beside the window in the visual Avalonia hosts it in, and macOS's
    /// traffic lights, which AppKit draws over it (<see cref="DrawTrafficLights"/>).
    /// </summary>
    private AutomationResponse Capture(TopLevel window, string path)
    {
        var scale = window.RenderScaling;
        var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(window.ClientSize.Width * scale)), Math.Max(1, (int)Math.Ceiling(window.ClientSize.Height * scale)));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        bitmap.Render(window.GetVisualParent() ?? window);
        DrawTrafficLights(bitmap, _windowButtons(window));
        var full = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        bitmap.Save(full, PngBitmapEncoderOptions.Default);
        return AutomationResponse.Done(full);
    }

    /// <summary>
    /// macOS's traffic lights where AppKit has them, which otherwise leave a gap at the title bar's left. In the colors of
    /// a window in front, whatever this one's are, as the Windows scripts draw a window's frame as active: the scripts'
    /// windows never take the focus, which would give them theirs.
    /// </summary>
    private static void DrawTrafficLights(RenderTargetBitmap bitmap, IReadOnlyList<MacWindowButton> buttons)
    {
        if (buttons.Count == 0)
        {
            return;
        }
        using var context = bitmap.CreateDrawingContext(clear: false);
        foreach (var button in buttons)
        {
            var (fill, edge) = button switch
            {
                { IsEnabled: false } => (Color.FromArgb(0x55, 0x80, 0x80, 0x80), Colors.Transparent),
                { Kind: MacWindowButtonKind.Close } => (Color.FromRgb(0xFF, 0x5F, 0x57), Color.FromRgb(0xE2, 0x46, 0x3F)),
                { Kind: MacWindowButtonKind.Minimize } => (Color.FromRgb(0xFE, 0xBC, 0x2E), Color.FromRgb(0xE1, 0xA1, 0x16)),
                _ => (Color.FromRgb(0x28, 0xC8, 0x40), Color.FromRgb(0x17, 0xA2, 0x34)),
            };
            // The circle fills the button's frame but for a point at each side, as AppKit draws it.
            var radius = (Math.Min(button.Width, button.Height) - 2) / 2;
            var middle = new Point(button.X + button.Width / 2, button.Y + button.Height / 2);
            context.DrawEllipse(new SolidColorBrush(fill), new Pen(new SolidColorBrush(edge), 0.5), middle, radius - 0.25, radius - 0.25);
        }
    }

    /// <summary>
    /// The window at a size, in device-independent pixels, for the screenshot scripts' windows to be the same size on
    /// every system, whatever its screen: macOS fits a window to its screen as it opens, and Windows keeps one within it
    /// unless the window's largest size is bigger, which Avalonia tells it from <c>MaxWidth</c> and <c>MaxHeight</c>. The
    /// window takes it in its next layout, after the answer.
    /// </summary>
    private static AutomationResponse Resize(TopLevel root, double width, double height)
    {
        if (root is not Window window)
        {
            throw new AutomationException("Only a window resizes.");
        }
        if (width <= 0 || height <= 0)
        {
            throw new AutomationException("resize needs a width and a height.");
        }
        window.WindowState = WindowState.Normal;
        // Infinite, as it is unless a window sets it, tells Windows nothing.
        if (double.IsInfinity(window.MaxWidth) || window.MaxWidth < width)
        {
            window.MaxWidth = width;
        }
        if (double.IsInfinity(window.MaxHeight) || window.MaxHeight < height)
        {
            window.MaxHeight = height;
        }
        window.Width = width;
        window.Height = height;
        return AutomationResponse.Done();
    }

    /// <summary>
    /// A click, or a right-click, in the middle of the control, scrolled into view first when something else is there.
    /// Without the window's input to put it into, or with the control still out of reach, it goes through the control's
    /// automation peer instead (<see cref="Invoke"/>, <see cref="OpenContextMenu"/>).
    /// </summary>
    private static AutomationResponse Click(Control target, bool right)
    {
        if (TopLevel.GetTopLevel(target) is { } root)
        {
            if (Reachable(target, root) is null)
            {
                target.BringIntoView();
                root.UpdateLayout();
            }
            if (Reachable(target, root) is { } at && root.InputHitTest(at) is Interactive hit)
            {
                // A pointer of the commands' own, apart from the real mouse, whose place and buttons it leaves alone.
                var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
                var now = (ulong)Environment.TickCount64;
                var button = right ? MouseButton.Right : MouseButton.Left;
                var released = new PointerReleasedEventArgs(hit, pointer, root, at, now + 1,
                    new PointerPointProperties(RawInputModifiers.None, right ? PointerUpdateKind.RightButtonReleased : PointerUpdateKind.LeftButtonReleased),
                    KeyModifiers.None, button);
                if (right)
                {
                    var request = new ContextRequestedEventArgs(released);
                    hit.RaiseEvent(request);
                    if (request.Handled)
                    {
                        return AutomationResponse.Done();
                    }
                }
                else
                {
                    hit.RaiseEvent(new PointerPressedEventArgs(hit, pointer, root, at, now,
                        new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
                    hit.RaiseEvent(released);
                    pointer.Capture(null);
                    return AutomationResponse.Done();
                }
            }
        }
        return right ? OpenContextMenu(target) : Invoke(target);
    }

    /// <summary>The middle of the control in its window, if a pointer there would reach it rather than something over it.</summary>
    private static Point? Reachable(Control target, TopLevel root)
    {
        if (target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root) is not { } point)
        {
            return null;
        }
        return root.InputHitTest(point) is Visual hit && (hit == target || target.IsVisualAncestorOf(hit) || hit.IsVisualAncestorOf(target)) ? point : null;
    }

    /// <summary>As a click, through the peers: selects, toggles, invokes or expands the control or the nearest one around it that can.</summary>
    private static AutomationResponse Invoke(Control target)
    {
        foreach (var control in AndAround(target))
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(control);
            if (peer.GetProvider<ISelectionItemProvider>() is { } item)
            {
                item.Select();
            }
            else if (peer.GetProvider<IToggleProvider>() is { } toggle)
            {
                toggle.Toggle();
            }
            else if (peer.GetProvider<IInvokeProvider>() is { } invoke)
            {
                invoke.Invoke();
            }
            else if (peer.GetProvider<IExpandCollapseProvider>() is { } expander)
            {
                if (expander.ExpandCollapseState == ExpandCollapseState.Collapsed)
                {
                    expander.Expand();
                }
                else
                {
                    expander.Collapse();
                }
            }
            else
            {
                continue;
            }
            return AutomationResponse.Done();
        }
        throw new AutomationException($"Nothing around '{Describe(target)}' can be clicked.");
    }

    /// <summary>
    /// As a right-click in the middle of the control: opens the context menu of the control or the nearest one around it
    /// that has one, placed as at the pointer.
    /// </summary>
    private static AutomationResponse OpenContextMenu(Control target)
    {
        foreach (var control in AndAround(target))
        {
            if (control.ContextMenu is { } menu)
            {
                var middle = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), control)
                    ?? new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
                ContextMenuPlacement.OpenAt(control, menu, middle);
                return AutomationResponse.Done();
            }
            if (control.ContextFlyout is { } flyout)
            {
                flyout.ShowAt(control);
                return AutomationResponse.Done();
            }
        }
        throw new AutomationException($"Nothing around '{Describe(target)}' has a context menu.");
    }

    /// <summary>Turns the switch or check box on or off; says which it is now.</summary>
    private static AutomationResponse Toggle(Control target, bool on)
    {
        var toggle = AndAround(target).Select(c => ControlAutomationPeer.CreatePeerForElement(c).GetProvider<IToggleProvider>()).FirstOrDefault(p => p is not null)
            ?? throw new AutomationException($"'{Describe(target)}' isn't a switch.");
        // A three-state box goes through its middle state on the way.
        for (var i = 0; i < 3 && (toggle.ToggleState == ToggleState.On) != on; i++)
        {
            toggle.Toggle();
        }
        return AutomationResponse.Done(toggle.ToggleState == ToggleState.On ? "On" : "Off");
    }

    private static AutomationResponse SetText(Control target, string text)
    {
        var value = ControlAutomationPeer.CreatePeerForElement(target).GetProvider<IValueProvider>()
            ?? throw new AutomationException($"'{Describe(target)}' doesn't take text.");
        value.SetValue(text);
        return AutomationResponse.Done();
    }

    private static AutomationResponse Show(Control target)
    {
        target.BringIntoView();
        return AutomationResponse.Done();
    }

    /// <summary>Scrolls the scroll viewer that is the control, or is around it, by lines: down, or up for fewer than none.</summary>
    private static AutomationResponse Scroll(Control target, int lines)
    {
        var viewer = AndAround(target).OfType<ScrollViewer>().FirstOrDefault()
            ?? target.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()
            ?? throw new AutomationException($"'{Describe(target)}' doesn't scroll.");
        for (var i = 0; i < Math.Abs(lines); i++)
        {
            if (lines > 0)
            {
                viewer.LineDown();
            }
            else
            {
                viewer.LineUp();
            }
        }
        return AutomationResponse.Done();
    }

    /// <summary>As the pointer resting on the control, or leaving it: shows or hides the tip of it or the nearest one around it that has one.</summary>
    private static AutomationResponse Tip(Control target, bool open)
    {
        var owner = AndAround(target).FirstOrDefault(c => ToolTip.GetTip(c) is not null)
            ?? throw new AutomationException($"Nothing around '{Describe(target)}' has a tip.");
        ToolTip.SetIsOpen(owner, open);
        return AutomationResponse.Done();
    }

    /// <summary>As the keys pressed together in the window: to its focused control, or to the window itself.</summary>
    private static AutomationResponse Press(TopLevel window, string keys)
    {
        var (key, modifiers) = ParseKeys(keys);
        var target = window.FocusManager?.GetFocusedElement() as Interactive ?? window;
        var down = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = target };
        target.RaiseEvent(down);
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key, KeyModifiers = modifiers, Source = target });
        return AutomationResponse.Done(down.Handled ? "Handled" : "Unhandled");
    }

    private static AutomationResponse Close(TopLevel window)
    {
        if (window is not Window closable)
        {
            throw new AutomationException("Only a window can be closed.");
        }
        closable.Close();
        return AutomationResponse.Done();
    }

    /// <summary><c>Ctrl+Shift+P</c> as P with Ctrl and Shift; <c>Esc</c> as Escape.</summary>
    internal static (Key Key, KeyModifiers Modifiers) ParseKeys(string keys)
    {
        var modifiers = KeyModifiers.None;
        Key? key = null;
        foreach (var part in keys.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL":
                    modifiers |= KeyModifiers.Control;
                    break;
                case "SHIFT":
                    modifiers |= KeyModifiers.Shift;
                    break;
                case "ALT":
                    modifiers |= KeyModifiers.Alt;
                    break;
                case "CMD" or "META" or "WIN":
                    modifiers |= KeyModifiers.Meta;
                    break;
                case "ESC":
                    key = Key.Escape;
                    break;
                case "DEL":
                    key = Key.Delete;
                    break;
                default:
                    key = Enum.TryParse<Key>(part, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                        ? parsed
                        : throw new AutomationException($"There's no key '{part}'.");
                    break;
            }
        }
        return (key ?? throw new AutomationException($"'{keys}' has no key besides Ctrl, Shift, Alt or Cmd."), modifiers);
    }

    private static string Describe(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName() is { Length: > 0 } name ? name : control.GetType().Name;

    private sealed class AutomationException(string message) : Exception(message);
}

/// <summary>
/// The popups open now (menus, flyouts, tips): windows of their own, which the screenshot scripts find by handle and
/// click in like any other (<see cref="AutomationCommands"/>). Watched only while the command channel is on.
/// </summary>
internal static class OpenPopups
{
    private static readonly Lock Gate = new();
    private static readonly List<WeakReference<Popup>> Popups = [];
    private static bool _watching;

    /// <summary>Starts noting each popup as it opens.</summary>
    public static void Watch()
    {
        lock (Gate)
        {
            if (_watching)
            {
                return;
            }
            _watching = true;
        }
        Popup.IsOpenProperty.Changed.AddClassHandler<Popup>((popup, e) =>
        {
            if (e.NewValue is true)
            {
                lock (Gate)
                {
                    Popups.RemoveAll(w => !w.TryGetTarget(out var p) || p == popup || !p.IsOpen);
                    Popups.Add(new WeakReference<Popup>(popup));
                }
            }
        });
    }

    /// <summary>The top levels of the popups open now.</summary>
    public static IReadOnlyList<TopLevel> Roots
    {
        get
        {
            lock (Gate)
            {
                return
                [
                    .. Popups.Select(w => w.TryGetTarget(out var p) && p.IsOpen && p.Child is { } child ? TopLevel.GetTopLevel(child) : null)
                        .OfType<TopLevel>(),
                ];
            }
        }
    }
}
