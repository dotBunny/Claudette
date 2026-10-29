using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Claudette.App.ViewModels;

namespace Claudette.App.Views;

/// <summary>
/// A Project Files tab (DESIGN.md §18, "Project files"): the explorer placed on its side, its edge dragged to resize it,
/// the keyboard in the tree and the find box, and a click or the focus anywhere in it focusing its pane.
/// </summary>
public partial class ProjectFilesView : UserControl
{
    private ProjectFilesViewModel? _files;

    /// <summary>Where a drag of the explorer's edge started, and its width then; null when not resizing.</summary>
    private double? _resizeFrom;
    private double _resizeStartWidth;

    public ProjectFilesView()
    {
        InitializeComponent();
        // A click or the focus in the pane: the keyboard and the header follow this tab (DESIGN.md §18, "Split panes").
        AddHandler(PointerPressedEvent, (_, _) => _files?.FocusPane(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, (_, _) => _files?.FocusPane(), RoutingStrategies.Bubble, handledEventsToo: true);
        ExplorerEdge.PointerPressed += OnEdgePressed;
        ExplorerEdge.PointerMoved += OnEdgeMoved;
        ExplorerEdge.PointerReleased += (_, e) => EndResize(e.Pointer);
        ExplorerEdge.PointerCaptureLost += (_, _) => EndResize(null);
        ExplorerEdge.DoubleTapped += (_, _) => _files?.ResetExplorerWidth();
        // Dragging a file's tab along the row moves it there; its button keeps the pointer, so the row listens first.
        FileTabs.AddHandler(PointerPressedEvent, OnFileTabsPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        FileTabs.AddHandler(PointerMovedEvent, OnFileTabsMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        FileTabs.AddHandler(PointerReleasedEvent, (_, _) => _tabDrag = null, RoutingStrategies.Tunnel, handledEventsToo: true);
        ActualThemeVariantChanged += (_, _) => TellTheme();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_files is not null)
        {
            _files.PropertyChanged -= OnFilesChanged;
            _files.FindFocusRequested -= FocusFindBox;
            _files.NamingStarted -= OnNamingStarted;
            _files.TreeFocusRequested -= FocusTree;
            _files.DeleteAsked -= FocusDeleteQuestion;
            _files.PerforceAsked -= FocusPerforceQuestion;
            _files.DiffRequested -= OnDiffRequested;
            _files.FindBoxHasKeyboard = null;
        }
        _files = DataContext as ProjectFilesViewModel;
        if (_files is not null)
        {
            _files.PropertyChanged += OnFilesChanged;
            _files.FindFocusRequested += FocusFindBox;
            _files.NamingStarted += OnNamingStarted;
            _files.TreeFocusRequested += FocusTree;
            _files.DeleteAsked += FocusDeleteQuestion;
            _files.PerforceAsked += FocusPerforceQuestion;
            _files.DiffRequested += OnDiffRequested;
            // Find in files' shortcut, pressed in Go to file's box, looks for what's typed there.
            _files.FindBoxHasKeyboard = () => FindBox.IsKeyboardFocusWithin;
            PlaceExplorer();
            TellTheme();
            if (_files.IsFinding)
            {
                FocusFindBox();
            }
        }
    }

    private void OnFilesChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProjectFilesViewModel.IsExplorerOnRight):
                PlaceExplorer();
                break;
            case nameof(ProjectFilesViewModel.Showing):
                Avalonia.Threading.Dispatcher.UIThread.Post(ShowShowingTab);
                break;
        }
    }

    /// <summary>The explorer on the left or the right, as Settings → Files and diffs says, with its edge on the editor's side.</summary>
    private void PlaceExplorer()
    {
        var right = _files?.IsExplorerOnRight == true;
        Grid.SetColumn(Explorer, right ? 2 : 0);
        Grid.SetColumn(ExplorerEdge, right ? 2 : 0);
        ExplorerEdge.HorizontalAlignment = right ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        ExplorerEdge.Margin = right ? new Thickness(-3, 0, 0, 0) : new Thickness(0, 0, -3, 0);
    }

    private void TellTheme()
    {
        if (_files is not null)
        {
            _files.IsDarkTheme = ActualThemeVariant == ThemeVariant.Dark;
        }
    }

    /// <summary>The tab of the file showing, scrolled into view in the row.</summary>
    private void ShowShowingTab()
    {
        if (_files?.Showing is { } showing && FileTabs.ContainerFromItem(showing) is Control tab)
        {
            tab.BringIntoView();
        }
    }

    private void FocusFindBox() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        FindBox.Focus();
        FindBox.SelectAll();
    });

    // ---- Resizing the explorer -----------------------------------------------------------------------------------

    private void OnEdgePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_files is null || !e.GetCurrentPoint(ExplorerEdge).Properties.IsLeftButtonPressed || e.ClickCount > 1)
        {
            return;
        }
        _resizeFrom = e.GetPosition(this).X;
        _resizeStartWidth = Explorer.Bounds.Width;
        e.Pointer.Capture(ExplorerEdge);
        e.Handled = true;
    }

    /// <summary>
    /// The explorer wider or narrower, leaving the editor its least (DESIGN.md §18, "Project files", "The layout"): on the
    /// right, dragging left widens it.
    /// </summary>
    private void OnEdgeMoved(object? sender, PointerEventArgs e)
    {
        if (_resizeFrom is not { } from || _files is null)
        {
            return;
        }
        var delta = e.GetPosition(this).X - from;
        var width = _resizeStartWidth + (_files.IsExplorerOnRight ? -delta : delta);
        _files.ResizeExplorer(Math.Min(width, Math.Max(ShellLayout.MinExplorerWidth, Bounds.Width - ShellLayout.MinConversationWidth)));
    }

    private void EndResize(IPointer? pointer)
    {
        if (_resizeFrom is null)
        {
            return;
        }
        _resizeFrom = null;
        pointer?.Capture(null);
        _files?.SaveExplorerWidth();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // A narrow pane: the explorer narrows first, down to its least, and the editor keeps its own.
        Explorer.MaxWidth = Math.Max(ShellLayout.MinExplorerWidth, e.NewSize.Width - ShellLayout.MinConversationWidth);
    }

    // ---- The keyboard and the pointer in the explorer ------------------------------------------------------------

    /// <summary>A click on a row: a file opens in the preview tab, a folder opens or closes.</summary>
    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerRow row)
        {
            _files?.OpenRowCommand.Execute(row);
        }
    }

    /// <summary>A double-click on a file: it opens to stay.</summary>
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerRow { IsFile: true } row)
        {
            _files?.OpenRowToStayCommand.Execute(row);
        }
    }

    /// <summary>Enter opens a file to stay; Right and Left open and close folders; Left on a file or a closed folder goes up.</summary>
    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (_files is null || (sender as ListBox)?.SelectedItem is not ExplorerRow row || (e.Source as StyledElement)?.DataContext is NameRow)
        {
            return;
        }
        // Cmd+Backspace on macOS, as Finder's Move to Trash.
        if (OperatingSystem.IsMacOS() && e.Key == Key.Back && e.KeyModifiers == KeyModifiers.Meta)
        {
            _files.DeleteRowCommand.Execute(row);
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.F2:
                _files.RenameRowCommand.Execute(row);
                e.Handled = true;
                break;
            case Key.Delete:
                _files.DeleteRowCommand.Execute(row);
                e.Handled = true;
                break;
            case Key.Enter:
                _files.OpenRowToStayCommand.Execute(row);
                e.Handled = true;
                break;
            case Key.Space when row.IsFile:
                _files.OpenRowCommand.Execute(row);
                e.Handled = true;
                break;
            case Key.Right when row.IsCollapsedFolder:
                _files.ToggleRowCommand.Execute(row);
                e.Handled = true;
                break;
            case Key.Left when row.IsExpanded:
                _files.ToggleRowCommand.Execute(row);
                e.Handled = true;
                break;
            case Key.Left when row.Depth > 0:
                var index = _files.Rows.IndexOf(row);
                _files.SelectedRow = _files.Rows.Take(index).LastOrDefault(r => r.Depth < row.Depth);
                e.Handled = true;
                break;
        }
    }

    /// <summary>In the find box: Up and Down choose, Enter opens, Esc brings the tree back.</summary>
    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        if (_files is null)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Down:
                _files.MoveFound(1);
                e.Handled = true;
                break;
            case Key.Up:
                _files.MoveFound(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                _files.OpenFoundCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                _files.StopFindCommand.Execute(null);
                TreeList.Focus();
                e.Handled = true;
                break;
        }
        if (e.Handled && _files.SelectedFound is { } found)
        {
            FoundList.ScrollIntoView(found);
        }
    }

    private void OnFoundTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is FoundFile found)
        {
            _files?.OpenFoundCommand.Execute(found);
        }
    }

    // ---- New files and folders, renaming and deleting --------------------------------------------------------------

    /// <summary>The name's row in view, so its box is made and takes the keyboard.</summary>
    private void OnNamingStarted(NameRow row) => TreeList.ScrollIntoView(row);

    /// <summary>The box takes the keyboard; renaming, with the name up to its extension selected.</summary>
    private void OnNewRowAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox box)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                box.Focus();
                if (box.DataContext is RenameRow rename)
                {
                    box.SelectionStart = 0;
                    box.SelectionEnd = rename.SelectedLength;
                }
            });
        }
    }

    /// <summary>
    /// In a name's box: Enter makes or renames it, Esc leaves it. Up and Down stay in the box rather than moving through
    /// the tree, which would take the keyboard away.
    /// </summary>
    private void OnNewRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_files is null)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
                _files.CommitNamingCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                _files.CancelNamingCommand.Execute(null);
                FocusTree();
                e.Handled = true;
                break;
            case Key.Up or Key.Down or Key.PageUp or Key.PageDown:
                e.Handled = true;
                break;
        }
    }

    /// <summary>A click elsewhere leaves a name's row without making or renaming anything; the box's own menu (Paste) doesn't.</summary>
    private void OnNewRowLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && box.ContextFlyout is not { IsOpen: true } && box.ContextMenu is not { IsOpen: true })
        {
            _files?.CancelNamingCommand.Execute(null);
        }
    }

    /// <summary>The delete question takes the keyboard on its button, so Enter answers it.</summary>
    private void FocusDeleteQuestion() => Avalonia.Threading.Dispatcher.UIThread.Post(() => ConfirmDeleteButton.Focus());

    /// <summary>Perforce asks first (DESIGN.md §18, "Perforce in the Files tab"): the keyboard goes to its button.</summary>
    private void FocusPerforceQuestion() => Avalonia.Threading.Dispatcher.UIThread.Post(() => ConfirmPerforceButton.Focus());

    private void OnPerforceQuestionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _files?.CancelPerforceCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary><b>Diff against the depot</b>, in the built-in diff view's window, as Changed files opens one (DESIGN.md §8).</summary>
    private void OnDiffRequested(Claudette.App.Diffs.DiffSource source)
    {
        var window = new DiffWindow { DataContext = new Claudette.App.Diffs.DiffWindowViewModel(source, ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark) };
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>Esc in the delete question is Cancel.</summary>
    private void OnDeleteQuestionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _files?.CancelDeleteCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>A name's row has no menu: the empty space's doesn't open over it.</summary>
    private void OnNewRowContextRequested(object? sender, ContextRequestedEventArgs e) => e.Handled = true;

    /// <summary>The keyboard back in the tree, on the selected row, or the first.</summary>
    private void FocusTree() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        (TreeList.ContainerFromIndex(Math.Max(TreeList.SelectedIndex, 0)) ?? (Control)TreeList).Focus());

    // ---- The file tabs ------------------------------------------------------------------------------------------

    /// <summary>Double-clicking a preview's tab keeps it.</summary>
    private void OnFileTabDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ProjectFileViewModel file)
        {
            _files?.KeepFileCommand.Execute(file);
            e.Handled = true;
        }
    }

    /// <summary>The tab being dragged and where the press was; null when none is.</summary>
    private (ProjectFileViewModel File, Point From, bool Moving)? _tabDrag;

    private void OnFileTabsPressed(object? sender, PointerPressedEventArgs e)
    {
        _tabDrag = e.GetCurrentPoint(FileTabs).Properties.IsLeftButtonPressed && (e.Source as StyledElement)?.DataContext is ProjectFileViewModel file
            ? (file, e.GetPosition(FileTabs), false)
            : null;
    }

    /// <summary>Past a few pixels, the dragged tab takes the place of the tab under the pointer.</summary>
    private void OnFileTabsMoved(object? sender, PointerEventArgs e)
    {
        if (_tabDrag is not { } drag || _files is null || !e.GetCurrentPoint(FileTabs).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var at = e.GetPosition(FileTabs);
        if (!drag.Moving && Math.Abs(at.X - drag.From.X) < 6)
        {
            return;
        }
        _tabDrag = drag with { Moving = true };
        for (var i = 0; i < _files.OpenFiles.Count; i++)
        {
            if (FileTabs.ContainerFromIndex(i) is { } tab && tab.TranslatePoint(default, FileTabs) is { } left && at.X >= left.X && at.X < left.X + tab.Bounds.Width)
            {
                _files.MoveFile(drag.File, i);
                return;
            }
        }
    }

    /// <summary>A middle click on a tab closes its file.</summary>
    private void OnFileTabPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Middle && (sender as Control)?.DataContext is ProjectFileViewModel file)
        {
            _files?.CloseFileCommand.Execute(file);
            e.Handled = true;
        }
    }
}

