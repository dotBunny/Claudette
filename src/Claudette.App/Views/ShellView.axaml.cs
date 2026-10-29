using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Claudette.App.Services;
using Claudette.App.ViewModels;
using Claudette.Core.Settings;

namespace Claudette.App.Views;

public partial class ShellView : UserControl
{
    private TopLevel? _topLevel;
    private ShellViewModel? _shell;
    private ClaudeUpdateViewModel? _updates;

    private const double DragThreshold = 6;

    /// <summary>The tab or group being dragged, or about to be once the pointer moves far enough.</summary>
    private object? _dragItem;
    private Point _dragStart;
    private bool _dragging;

    /// <summary>Where a drag of the sidebar's edge started, and the width then; null when not resizing.</summary>
    private double? _resizeFrom;
    private double _resizeStartWidth;

    public ShellView()
    {
        InitializeComponent();
        // Handled events too: the tab and label buttons handle presses themselves.
        GroupList.AddHandler(PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        GroupList.AddHandler(PointerMovedEvent, OnListPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        GroupList.AddHandler(PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        // A folder dragged from Finder or Explorer onto the sidebar opens a tab there (DESIGN.md §4, "Other ways in").
        DragDrop.SetAllowDrop(Sidebar, true);
        Sidebar.AddHandler(DragDrop.DragOverEvent, OnFolderDragOver);
        Sidebar.AddHandler(DragDrop.DropEvent, OnFolderDrop);
        SidebarEdge.PointerPressed += OnEdgePressed;
        SidebarEdge.PointerMoved += OnEdgeMoved;
        SidebarEdge.PointerReleased += (_, e) => EndResize(e.Pointer);
        SidebarEdge.PointerCaptureLost += (_, _) => EndResize(null);
        SidebarEdge.DoubleTapped += (_, _) =>
        {
            ViewModel?.Layout.ResizeSidebar(ShellLayout.DefaultSidebarWidth);
            ViewModel?.Layout.SaveSidebarWidth();
        };
        // A narrow window shows the sidebar's rail (DESIGN.md §4, "Sidebar").
        SizeChanged += (_, e) => ViewModel?.Layout.SetAvailableWidth(e.NewSize.Width);
        // The lines between tabs side by side, to drag (DESIGN.md §18, "Split panes").
        PaneHost.LayoutUpdated += (_, _) => WatchPanes();
        PaneLines.Dragged += OnPaneLineDragged;
        PaneLines.DragEnded += () => ViewModel?.Layout.SavePaneShares();
        PaneLines.ResetRequested += () => ViewModel?.Layout.ResetPaneShares();
        // The tool windows (DESIGN.md §3, "Tool windows").
        WatchToolWindowsView();
    }

    // ---- Resizing the sidebar -------------------------------------------------------------------------------------

    private void OnEdgePressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { } shell || !e.GetCurrentPoint(SidebarEdge).Properties.IsLeftButtonPressed || e.ClickCount > 1)
        {
            return;
        }
        // Measured against the whole view, which doesn't move as the edge does, from the width it shows.
        _resizeFrom = e.GetPosition(this).X;
        _resizeStartWidth = shell.Layout.SidebarDisplayWidth;
        e.Pointer.Capture(SidebarEdge);
        e.Handled = true;
    }

    private void OnEdgeMoved(object? sender, PointerEventArgs e)
    {
        if (_resizeFrom is { } from)
        {
            ViewModel?.Layout.ResizeSidebar(_resizeStartWidth + e.GetPosition(this).X - from);
        }
    }

    private void EndResize(IPointer? pointer)
    {
        if (_resizeFrom is null)
        {
            return;
        }
        _resizeFrom = null;
        pointer?.Capture(null);
        ViewModel?.Layout.SaveSidebarWidth();
    }

    private void OnFolderDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = DroppedFolders(e).Any() ? DragDropEffects.Link : DragDropEffects.None;

    private void OnFolderDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is not { } shell)
        {
            return;
        }
        foreach (var folder in DroppedFolders(e))
        {
            _ = shell.OpenFolderAsync(folder);
        }
        e.Handled = true;
    }

    private static IEnumerable<string> DroppedFolders(DragEventArgs e) =>
        (e.DataTransfer.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath()).OfType<string>().Where(Directory.Exists);

    // ---- Dragging tabs and groups (DESIGN.md §4) ------------------------------------------------------------------

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Whatever an earlier drag left, such as one whose pointer capture was lost.
        EndTabDrag();
        if (!e.GetCurrentPoint(GroupList).Properties.IsLeftButtonPressed)
        {
            return;
        }
        // The innermost button: a tab's close button or the group's + don't start a drag.
        var button = (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true);
        _dragItem = button switch
        {
            { DataContext: TabViewModel { IsRenaming: false } tab } when button.Classes.Contains("tab") => tab,
            // A Project Files tab's row, and a run's, only go out to the panes (DESIGN.md §18, "Project files", "Project tools").
            { DataContext: ProjectFilesViewModel files } when button.Classes.Contains("tab") => files,
            { DataContext: ProjectRunViewModel run } when button.Classes.Contains("runrow") => run,
            { DataContext: EditorLogViewModel log } when button.Classes.Contains("logrow") => log,
            { DataContext: TabGroupViewModel group } when button.Classes.Contains("grouplabel") => group,
            _ => null,
        };
        _dragStart = e.GetPosition(GroupList);
    }

    /// <summary>
    /// The dragged item moves as soon as the pointer passes the middle of a neighbor, so the list shows the result live. A
    /// tab dragged over the panes shows where it would open there instead (DESIGN.md §18, "Split panes").
    /// </summary>
    private void OnListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragItem is null || ViewModel is not { } shell)
        {
            return;
        }
        var at = e.GetPosition(GroupList);
        if (!_dragging)
        {
            // A group moves only up and down; a tab can also go out to the panes.
            var moved = _dragItem is IShellTab ? Math.Max(Math.Abs(at.X - _dragStart.X), Math.Abs(at.Y - _dragStart.Y)) : Math.Abs(at.Y - _dragStart.Y);
            if (moved < DragThreshold)
            {
                return;
            }
            _dragging = true;
            if (_dragItem is IShellTab dragged)
            {
                // Selected once it's dropped: selecting it now would put it in the focused pane before it's anywhere.
                dragged.IsDragged = true;
            }
        }
        if (_dragItem is IShellTab overPanes && ShowPaneDrop(shell, overPanes, e))
        {
            return;
        }
        var y = at.Y;
        switch (_dragItem)
        {
            case TabViewModel tab when shell.Groups.FirstOrDefault(g => g.Tabs.Contains(tab)) is { } group:
                {
                    // Rows folded away under a collapsed thread aren't where anything can go (DESIGN.md §4, "Sidebar").
                    var buttons = GroupList.GetVisualDescendants().OfType<Button>()
                        .Where(b => b.Classes.Contains("tab") && b.IsEffectivelyVisible && b.DataContext is TabViewModel t && group.Tabs.Contains(t));
                    if (TargetIndex(buttons, y, b => group.Tabs.IndexOf((TabViewModel)b.DataContext!), group.Tabs.IndexOf(tab)) is { } index)
                    {
                        shell.MoveTabTo(tab, index);
                    }
                    break;
                }
            case TabGroupViewModel group:
                {
                    var containers = Enumerable.Range(0, shell.Groups.Count).Select(i => GroupList.ContainerFromIndex(i)).OfType<Control>();
                    if (TargetIndex(containers, y, c => shell.Groups.IndexOf((TabGroupViewModel)c.DataContext!), shell.Groups.IndexOf(group)) is { } index)
                    {
                        shell.MoveGroupTo(group, index);
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// The index of the item the pointer has moved past the middle of, in the direction of travel, or null to stay put.
    /// </summary>
    private int? TargetIndex(IEnumerable<Control> items, double y, Func<Control, int> indexOf, int from)
    {
        foreach (var item in items)
        {
            if (item.TranslatePoint(default, GroupList) is not { } origin)
            {
                continue;
            }
            var index = indexOf(item);
            var middle = origin.Y + item.Bounds.Height / 2;
            if (index > from && y > middle && y < origin.Y + item.Bounds.Height || index < from && y < middle && y > origin.Y)
            {
                return index;
            }
        }
        return null;
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging)
        {
            // Releasing the capture un-presses the button without a click, so dropping a group doesn't also collapse it.
            e.Pointer.Capture(null);
            e.Handled = true;
            if (_dragItem is IShellTab tab && ViewModel is { } shell)
            {
                // On a pane it opens there (DESIGN.md §18, "Split panes"); anywhere else it's selected, as a click would.
                if (PaneDropAt(shell, tab, e) is { } drop)
                {
                    shell.DropOnPane(tab, drop.Index, drop.After);
                }
                else
                {
                    shell.Selected = tab;
                }
            }
        }
        EndTabDrag();
    }

    /// <summary>The drag is over, dropped or lost: the row and the panes go back to how they look.</summary>
    private void EndTabDrag()
    {
        if (_dragItem is IShellTab tab)
        {
            tab.IsDragged = false;
        }
        PaneDropLayer.IsVisible = false;
        _dragItem = null;
        _dragging = false;
    }

    // ---- Tabs side by side (DESIGN.md §18, "Split panes") ---------------------------------------------------------

    /// <summary>The panel the panes are laid out in, once it's made.</summary>
    private Controls.PanesPanel? Panes => PaneHost.ItemsPanelRoot as Controls.PanesPanel;

    private Controls.PanesPanel? _panesWatched;

    /// <summary>The lines' strip follows the panes as they're laid out.</summary>
    private void WatchPanes()
    {
        if (Panes is not { } panel || ReferenceEquals(panel, _panesWatched))
        {
            return;
        }
        _panesWatched = panel;
        panel.Arranged += (_, _) => PaneLines.Lines = panel.Lines;
    }

    /// <summary>Dragging the line after pane <paramref name="line"/>: each pane keeps its conversation's least.</summary>
    private void OnPaneLineDragged(int line, double x)
    {
        if (ViewModel is not { } shell || Panes is not { } panel)
        {
            return;
        }
        var leasts = shell.Panes.Select(_ => ShellLayout.MinConversationWidth).ToList();
        shell.Layout.ResizePanes(line, x, panel.Bounds.Width, leasts);
    }

    /// <summary>Where a tab dragged from the sidebar would open: the pane under the pointer, and which half of it; null off the panes.</summary>
    private (int Index, bool After, Rect Pane)? PaneDropAt(ShellViewModel shell, IShellTab tab, PointerEventArgs e)
    {
        if (Panes is not { } panel)
        {
            return null;
        }
        var at = e.GetPosition(panel);
        if (at.Y < 0 || at.Y > panel.Bounds.Height)
        {
            return null;
        }
        foreach (var pane in panel.Children.Where(c => c.IsVisible && Controls.PanesPanel.GetIndex(c) >= 0))
        {
            var bounds = pane.Bounds;
            if (at.X >= bounds.Left && at.X < bounds.Right && shell.PaneDropFor(tab, Controls.PanesPanel.GetIndex(pane)) != PaneDrop.None)
            {
                return (Controls.PanesPanel.GetIndex(pane), at.X >= bounds.Center.X, bounds);
            }
        }
        return null;
    }

    /// <summary>
    /// Over the panes, the half or the pane a dragged tab would take is marked, and says what dropping it does. True while
    /// it's over one; off them, the mark goes and the sidebar's own reordering carries on.
    /// </summary>
    private bool ShowPaneDrop(ShellViewModel shell, IShellTab tab, PointerEventArgs e)
    {
        if (PaneDropAt(shell, tab, e) is not { } drop)
        {
            PaneDropLayer.IsVisible = false;
            return false;
        }
        var kind = shell.PaneDropFor(tab, drop.Index);
        var pane = drop.Pane.Deflate(8);
        var target = kind == PaneDrop.Replace ? pane
            : drop.After ? new Rect(pane.Center.X, pane.Top, pane.Width / 2, pane.Height)
            : new Rect(pane.Left, pane.Top, pane.Width / 2, pane.Height);
        Canvas.SetLeft(PaneDropTarget, target.Left);
        Canvas.SetTop(PaneDropTarget, target.Top);
        PaneDropTarget.Width = target.Width;
        PaneDropTarget.Height = target.Height;
        PaneDropText.Text = kind == PaneDrop.Replace ? "Open here, in its place"
            : tab.IsShown ? "Move here"
            : "Open beside";
        PaneDropLayer.IsVisible = true;
        return true;
    }

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;

    /// <summary>
    /// <b>Change color…</b> in a group's menu opens its color picker beside the group's label, in the sidebar or the rail
    /// (DESIGN.md §4, "Grouped by folder").
    /// </summary>
    private void OnChangeGroupColor(object? sender, RoutedEventArgs e) => ShowBesideGroupLabel(sender, (shell, group, flyout) =>
    {
        var picker = shell.PickGroupColor(group);
        picker.Done += flyout.Hide;
        return new GroupColorPicker { DataContext = picker };
    });

    /// <summary><b>Rename…</b> in a group's menu opens its name beside the group's label, in the sidebar or the rail.</summary>
    private void OnRenameGroup(object? sender, RoutedEventArgs e) => ShowBesideGroupLabel(sender, (shell, group, flyout) =>
    {
        var editor = shell.RenameGroup(group);
        editor.Done += flyout.Hide;
        return new GroupNameEditor { DataContext = editor };
    });

    /// <summary>
    /// Opens what <paramref name="content"/> makes in a flyout beside the label of the group whose menu
    /// <paramref name="sender"/> is in. Posted, so the menu has closed before it opens.
    /// </summary>
    private void ShowBesideGroupLabel(object? sender, Func<ShellViewModel, TabGroupViewModel, Flyout, Control> content)
    {
        if (ViewModel is not { } shell || (sender as MenuItem)?.DataContext is not TabGroupViewModel group)
        {
            return;
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var label = this.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Classes.Contains("grouplabel") && b.DataContext == group && b.IsEffectivelyVisible);
            if (label is null)
            {
                return;
            }
            var flyout = new Flyout { Placement = PlacementMode.RightEdgeAlignedTop };
            flyout.Content = content(shell, group, flyout);
            FlyoutBase.SetAttachedFlyout(label, flyout);
            flyout.ShowAt(label);
        });
    }

    /// <summary>The project's menu open, from a group's row or the rail, for its entries to close.</summary>
    private Flyout? _projectMenu;

    /// <summary>
    /// A group's project menu opened: look at the project's files again, so what's enabled is current (a solution
    /// generated from a terminal, say). The menu updates in place when that's done (DESIGN.md §18).
    /// </summary>
    private void OnProjectMenuOpened(object? sender, EventArgs e)
    {
        _projectMenu = sender as Flyout;
        if (_projectMenu?.Target?.DataContext is TabGroupViewModel { ProjectTab: { } tab })
        {
            tab.ProjectTools.RefreshCommand.Execute(null);
        }
    }

    /// <summary>
    /// An entry of the project's menu was picked: the menu closes, as a menu does, once the entry has run its command,
    /// which a button does after raising Click.
    /// </summary>
    private void OnProjectMenuItemPicked(object? sender, RoutedEventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _projectMenu?.Hide());

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellChanged;
            _shell.ToolWindows.FocusRequested -= OnToolFocusRequested;
        }
        _shell = ViewModel;
        if (_shell is not null)
        {
            _shell.ShowSettingsWindow = ShowSettingsAsync;
            _shell.ShowAboutWindow = () => this.FindAncestorOfType<Window>() is { DataContext: MainWindowViewModel main } owner
                ? AboutWindow.ShowAsync(main.Services, owner)
                : Task.CompletedTask;
            _shell.ShowImageWindow = viewer => ImageWindow.Open(viewer, TopLevel.GetTopLevel(this) as Window);
            _shell.PropertyChanged += OnShellChanged;
            _shell.ToolWindows.FocusRequested += OnToolFocusRequested;
            if (Bounds.Width > 0)
            {
                _shell.Layout.SetAvailableWidth(Bounds.Width);
            }
        }
        WatchUpdates();
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Updates))
        {
            WatchUpdates();
        }
    }

    /// <summary>A clicked "update ready" notification opens the badge's dialog (DESIGN.md §12).</summary>
    private void WatchUpdates()
    {
        if (_updates is not null)
        {
            _updates.OpenRequested -= OpenUpdateDialog;
        }
        _updates = _shell?.Updates;
        if (_updates is not null)
        {
            _updates.OpenRequested += OpenUpdateDialog;
        }
    }

    private void OpenUpdateDialog()
    {
        if (UpdateBadge.IsVisible)
        {
            UpdateBadge.Flyout?.ShowAt(UpdateBadge);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        // Tunnel, so shortcuts work wherever the focus is (DESIGN.md §4, "Keyboard").
        _topLevel?.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// The window's shortcuts, as set in Settings → Keyboard (DESIGN.md §4, §9, §14), and each quick suffix's own
    /// shortcut (§5).
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } shell)
        {
            return;
        }
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && shell.IsPickerOpen)
        {
            shell.ClosePicker();
            e.Handled = true;
            return;
        }
        // A tool tab's drag, or a window slid out over the panes (DESIGN.md §3, "Tool windows").
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && EscapeToolWindows())
        {
            e.Handled = true;
            return;
        }
        var keyboard = shell.Keyboard;
        bool Is(string id) => Shortcuts.Matches(keyboard, id, e.Key, e.KeyModifiers);

        if (shell.IsPaletteOpen)
        {
            // The palette has the keys while it's open; its shortcut closes it again.
            if (Is(KeyboardShortcuts.CommandPalette))
            {
                shell.ClosePalette();
                e.Handled = true;
            }
            return;
        }

        if (shell.FindInFiles is { } find)
        {
            // Find in files has the keys while it's open (DESIGN.md §18, "Finding in files"): Esc closes it, and its own
            // shortcuts switch between finding and replacing.
            if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
            {
                shell.CloseFindInFiles();
                e.Handled = true;
            }
            else if (Is(KeyboardShortcuts.FindInFiles) || Is(KeyboardShortcuts.ReplaceInFiles))
            {
                find.IsReplaceMode = Is(KeyboardShortcuts.ReplaceInFiles);
                e.Handled = true;
            }
            return;
        }
        if (Is(KeyboardShortcuts.FindInFiles))
        {
            shell.OpenFindInFilesCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (Is(KeyboardShortcuts.ReplaceInFiles))
        {
            shell.OpenReplaceInFilesCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // With a Project Files tab focused, its own keys, and the window's shortcuts that act on a tab act on its editor
        // (DESIGN.md §18, "Project files", "Keyboard").
        if (shell.SelectedFiles is { } files && HandleFilesKey(files, e, Is))
        {
            e.Handled = true;
            return;
        }

        if (Is(KeyboardShortcuts.NextTab))
        {
            shell.SelectNextCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.GoToFile))
        {
            shell.GoToFileCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.PreviousTab))
        {
            shell.SelectPreviousCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.NewTab))
        {
            shell.NewTabCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.CloseTab))
        {
            shell.CloseSelectedTabCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.History))
        {
            shell.OpenHistoryCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.Settings))
        {
            shell.OpenSettingsCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.ToggleSidebar))
        {
            shell.Layout.ToggleSidebarCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.CommandPalette))
        {
            shell.OpenPaletteCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.NextTabNeedingInput))
        {
            shell.SelectNextNeedingInputCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.Find) && shell.SelectedTab is { } findTab)
        {
            findTab.OpenFindCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.Find) && shell.SelectedLog is { } findLog)
        {
            // Find in a run's log or an editor's (DESIGN.md §18, "Project tools", "Editor logs").
            findLog.RequestFind();
        }
        else if (JumpFor(Is) is { } jump && shell.SelectedTab is { } jumpTab)
        {
            jumpTab.JumpToMessage(jump);
        }
        else if (Is(KeyboardShortcuts.ZoomIn))
        {
            shell.ZoomInCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.ZoomOut))
        {
            shell.ZoomOutCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.ResetZoom))
        {
            shell.ResetZoomCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.RunProjectAction) && shell.SelectedTab is { } projectTab)
        {
            // Launch the editor, for Unreal (DESIGN.md §18, "Project tools").
            projectTab.ProjectTools.RunMainActionCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.GoToTab) && Shortcuts.Digit(e.Key) is { } number)
        {
            shell.SelectNumber(number);
        }
        else if (shell.SelectedTab is { } tab && shell.QuickSuffixes.FirstOrDefault(s => KeyChord.TryParse(s.Shortcut, out var chord) && Shortcuts.Matches(chord, e.Key, e.KeyModifiers)) is { } suffix)
        {
            tab.AddSuffixCommand.Execute(suffix);
        }
        else
        {
            return;
        }
        e.Handled = true;
    }

    /// <summary>
    /// A Project Files tab's keys: <c>Ctrl/Cmd+S</c> saves (the composer's Stash elsewhere), <c>Ctrl/Cmd+Alt+S</c> saves
    /// all, Find opens the editor's find, the previous and next message go through the changes, and
    /// <c>Ctrl+PageDown</c>/<c>Ctrl+PageUp</c> (<c>Cmd+Option+→</c>/<c>←</c>) go to the next and previous file. Close tab
    /// is the shell's, which closes the file showing.
    /// </summary>
    private static bool HandleFilesKey(ProjectFilesViewModel files, KeyEventArgs e, Func<string, bool> @is)
    {
        var primary = Shortcuts.IsMac ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.S && e.KeyModifiers == primary)
        {
            files.SaveCommand.Execute(null);
            return true;
        }
        if (e.Key == Key.S && e.KeyModifiers == (primary | KeyModifiers.Alt))
        {
            files.SaveAllCommand.Execute(null);
            return true;
        }
        if (@is(KeyboardShortcuts.Find))
        {
            if (files.Showing is { ShowsEditor: true } showing)
            {
                showing.RequestFind(replace: false);
            }
            else
            {
                files.StartFind();
            }
            return true;
        }
        if (@is(KeyboardShortcuts.PreviousMessage) && files.Showing is { } previous)
        {
            previous.PreviousChangeCommand.Execute(null);
            return true;
        }
        if (@is(KeyboardShortcuts.NextMessage) && files.Showing is { } next)
        {
            next.NextChangeCommand.Execute(null);
            return true;
        }
        var nextFile = Shortcuts.IsMac ? e.Key == Key.Right && e.KeyModifiers == (KeyModifiers.Meta | KeyModifiers.Alt) : e.Key == Key.PageDown && e.KeyModifiers == KeyModifiers.Control;
        var previousFile = Shortcuts.IsMac ? e.Key == Key.Left && e.KeyModifiers == (KeyModifiers.Meta | KeyModifiers.Alt) : e.Key == Key.PageUp && e.KeyModifiers == KeyModifiers.Control;
        if (nextFile || previousFile)
        {
            if (nextFile)
            {
                files.NextFile();
            }
            else
            {
                files.PreviousFile();
            }
            return true;
        }
        return false;
    }

    /// <summary>The message jump a key asks for (DESIGN.md §18, "Conversation cards"); null when it asks for none.</summary>
    private static MessageJump? JumpFor(Func<string, bool> @is) =>
        @is(KeyboardShortcuts.PreviousMessage) ? MessageJump.Previous
        : @is(KeyboardShortcuts.NextMessage) ? MessageJump.Next
        : @is(KeyboardShortcuts.FirstMessage) ? MessageJump.First
        : @is(KeyboardShortcuts.LatestMessage) ? MessageJump.Latest
        : null;

    private void OnTabDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TabViewModel tab)
        {
            tab.StartRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnRenameAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.PropertyChanged += (_, args) =>
            {
                if (args.Property == IsVisibleProperty && box.IsVisible)
                {
                    box.Focus();
                    box.SelectAll();
                }
            };
        }
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if ((sender as Control)?.DataContext is not TabViewModel tab)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            tab.CommitRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tab.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TabViewModel { IsRenaming: true } tab)
        {
            tab.CommitRenameCommand.Execute(null);
        }
    }

    /// <summary>Clicking the dimmed area around the picker closes it.</summary>
    private void OnOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender))
        {
            ViewModel?.ClosePicker();
        }
    }

    /// <summary>A click outside the command palette closes it.</summary>
    /// <summary>A click outside Find in files closes it (DESIGN.md §18, "Finding in files").</summary>
    private void OnFindInFilesOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender))
        {
            ViewModel?.CloseFindInFiles();
        }
    }

    private void OnPaletteOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender))
        {
            ViewModel?.ClosePalette();
        }
    }

    /// <summary>A click outside History closes it without opening anything.</summary>
    private void OnHistoryOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender))
        {
            ViewModel?.CloseHistory();
        }
    }

    /// <summary>The open Settings window; null while it's closed.</summary>
    private SettingsWindow? _settingsWindow;

    /// <summary>
    /// The Settings window where <paramref name="opening"/> says, with the selected tab's project pages (DESIGN.md §14).
    /// It isn't modal: it stays open beside this window and follows its selected tab, and asked for again, it comes to
    /// the front at the page and setting asked for. It closes with this window.
    /// </summary>
    private Task ShowSettingsAsync(SettingsOpening opening)
    {
        if (_settingsWindow is { DataContext: SettingsViewModel open } existing)
        {
            open.Open(opening);
            // Coming to the front takes the focus, which a screenshot run leaves alone (DESIGN.md §9, "Taking screenshots
            // without the focus").
            UnfocusedWindows.Activate(existing);
            return Task.CompletedTask;
        }
        if (this.FindAncestorOfType<Window>() is not { } owner || owner.DataContext is not MainWindowViewModel main)
        {
            opening.Project?.Dispose();
            return Task.CompletedTask;
        }
        var settings = new SettingsViewModel(main.Services, main.AccountText, main.Updates, opening, _shell)
        {
            Account = main.Account,
            AppUpdates = main.AppUpdate,
            SourceUpdates = main.SourceUpdate,
        };
        var window = new SettingsWindow { DataContext = settings };
        _settingsWindow = window;
        window.Closed += (_, _) => _settingsWindow = null;
        window.Show(owner);
        return Task.CompletedTask;
    }
}
