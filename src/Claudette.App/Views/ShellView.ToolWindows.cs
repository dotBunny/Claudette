using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.ToolWindows;

namespace Claudette.App.Views;

/// <summary>
/// The tool windows' side of the shell's view (DESIGN.md §3, "Tool windows"): resizing the right side, the bottom and the
/// line between the right side's two windows by their grips, keeping the panes their least; sharing the right side's
/// height; a press outside an unpinned window hiding it; and the keyboard into a window shown by its icon.
/// </summary>
public partial class ShellView
{
    private enum Grip
    {
        Right,
        Bottom,
        Split,
    }

    private Grip? _toolResizing;
    private Point _toolResizeFrom;
    private double _toolResizeStart;
    private ToolDockViewModel? _watchedDock;

    private ToolDockViewModel? Dock => ViewModel?.ToolWindows;

    /// <summary>From the constructor: the grips, and presses that hide an unpinned window.</summary>
    private void WatchToolWindowsView()
    {
        WatchGrip(RightGrip, Grip.Right);
        WatchGrip(BottomGrip, Grip.Bottom);
        WatchGrip(SplitGrip, Grip.Split);
        DockArea.AddHandler(PointerPressedEvent, OnDockPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        // However wide or tall the windows were dragged, the panes keep their least.
        DockArea.SizeChanged += (_, e) =>
        {
            RightSide.MaxWidth = Math.Max(ToolDockViewModel.MinRightWidth, e.NewSize.Width - ToolDockViewModel.StripWidth - RightSide.Margin.Left - ShellLayout.MinConversationWidth);
            BottomPanel.MaxHeight = Math.Max(ToolDockViewModel.MinBottomHeight, e.NewSize.Height - ToolDockViewModel.MinPanesHeight);
        };
        DataContextChanged += (_, _) => WatchDock(Dock);
        WatchDock(Dock);
    }

    private void WatchDock(ToolDockViewModel? dock)
    {
        if (ReferenceEquals(dock, _watchedDock))
        {
            return;
        }
        if (_watchedDock is not null)
        {
            _watchedDock.PropertyChanged -= OnDockChanged;
            _watchedDock.ShowingChanged -= ShareRightSide;
        }
        _watchedDock = dock;
        if (dock is not null)
        {
            dock.PropertyChanged += OnDockChanged;
            // A window coming or going in either group changes how they share the right side.
            dock.ShowingChanged += ShareRightSide;
        }
        ShareRightSide();
    }

    private void OnDockChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolDockViewModel.SplitShare))
        {
            ShareRightSide();
        }
    }

    /// <summary>
    /// The right side's height: the top group's window and the split group's in their shares with the line between, or the
    /// one showing the whole of it.
    /// </summary>
    private void ShareRightSide()
    {
        if (Dock is not { } dock)
        {
            return;
        }
        var (top, split) = (dock.Top.IsShowing, dock.Split.IsShowing);
        var rows = RightSide.RowDefinitions;
        rows[0].Height = top ? new GridLength(split ? dock.SplitShare : 1, GridUnitType.Star) : new GridLength(0);
        rows[2].Height = split ? new GridLength(top ? 1 - dock.SplitShare : 1, GridUnitType.Star) : new GridLength(0);
        SplitGrip.IsVisible = top && split;
    }

    /// <summary>
    /// The dock asks for the keyboard in a window shown by a click on its icon; not while Claudette leaves the focus alone
    /// (DESIGN.md §9, "Taking screenshots without the focus"), as a focused control would bring its window forward.
    /// </summary>
    private void OnToolFocusRequested(ToolWindowViewModel window)
    {
        if (Services.UnfocusedWindows.IsOn)
        {
            return;
        }
        var panel = window.Group switch
        {
            ToolGroup.Top => TopPanel,
            ToolGroup.Split => SplitPanel,
            _ => BottomPanel,
        };
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Past the header's buttons, into the window itself.
            var content = panel.GetVisualDescendants().OfType<ToolWindowView>().FirstOrDefault();
            if (content?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEnabled) is { } first)
            {
                first.Focus();
            }
        });
    }

    // ---- Resizing ----------------------------------------------------------------------------------------------------

    private void WatchGrip(Border grip, Grip which)
    {
        grip.PointerPressed += (_, e) =>
        {
            if (Dock is null || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed || e.ClickCount > 1)
            {
                return;
            }
            _toolResizing = which;
            _toolResizeFrom = e.GetPosition(DockArea);
            _toolResizeStart = which switch
            {
                Grip.Right => RightSide.Bounds.Width,
                Grip.Bottom => BottomPanel.Bounds.Height,
                _ => TopPanel.Bounds.Height,
            };
            e.Pointer.Capture(grip);
            e.Handled = true;
        };
        grip.PointerMoved += (_, e) =>
        {
            if (_toolResizing != which || Dock is not { } dock)
            {
                return;
            }
            var at = e.GetPosition(DockArea);
            switch (which)
            {
                // The right side widens as its grip goes left, the bottom grows as its grip goes up.
                case Grip.Right:
                    dock.ResizeRight(Math.Min(_toolResizeStart + _toolResizeFrom.X - at.X, RightSide.MaxWidth));
                    break;
                case Grip.Bottom:
                    dock.ResizeBottom(Math.Min(_toolResizeStart + _toolResizeFrom.Y - at.Y, BottomPanel.MaxHeight));
                    break;
                default:
                    var height = RightSide.Bounds.Height - SplitGrip.Bounds.Height;
                    if (height > 0)
                    {
                        dock.ResizeSplit((_toolResizeStart + at.Y - _toolResizeFrom.Y) / height);
                    }
                    break;
            }
        };
        grip.PointerReleased += (_, e) => EndToolResize(e.Pointer);
        grip.PointerCaptureLost += (_, _) => EndToolResize(null);
        grip.DoubleTapped += (_, _) =>
        {
            switch (which)
            {
                case Grip.Right:
                    Dock?.ResetRightWidth();
                    break;
                case Grip.Bottom:
                    Dock?.ResetBottomHeight();
                    break;
                default:
                    Dock?.ResetSplit();
                    break;
            }
        };
    }

    private void EndToolResize(IPointer? pointer)
    {
        if (_toolResizing is null)
        {
            return;
        }
        _toolResizing = null;
        pointer?.Capture(null);
        Dock?.SaveSize();
    }

    // ---- Unpinned windows --------------------------------------------------------------------------------------------

    /// <summary>
    /// A press in the dock area: an unpinned window showing hides, unless the press was in it or on the strip. A press on a
    /// grip is the start of resizing one, so nothing hides.
    /// </summary>
    private void OnDockPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Dock is not { } dock || e.Source is not Visual source || source == RightGrip || source == BottomGrip || source == SplitGrip)
        {
            return;
        }
        var onStrip = ReferenceEquals(source, ToolStrip) || source.GetVisualAncestors().Contains(ToolStrip);
        var panel = source as ToolPanelView ?? source.FindAncestorOfType<ToolPanelView>();
        dock.PressAt((panel?.DataContext as ToolGroupViewModel)?.Showing, onStrip);
    }

    /// <summary>Esc: an icon's drag is cancelled, else an unpinned window showing hides. True when it did either.</summary>
    private bool EscapeToolWindows() => ToolStrip.CancelDrag() || Dock?.Escape() == true;
}
