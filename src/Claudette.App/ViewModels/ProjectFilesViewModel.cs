using Claudette.App.Services;
using Claudette.Core.ProjectFiles;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// A folder group's Project Files tab (DESIGN.md §18, "Project files"): the explorer of the project's folders and files,
/// the files open in tabs, and Claude's changes marked in them. It's a tab like a session's in the sidebar and the
/// panes, without a session: no composer, status or notifications, and the tool windows show its group's last session tab. Partials hold the explorer
/// (<c>.Explorer.cs</c>), finding a file by name (<c>.Find.cs</c>), the open files (<c>.Files.cs</c>) and Claude's
/// changes (<c>.Changes.cs</c>).
/// </summary>
public sealed partial class ProjectFilesViewModel : ViewModelBase, IShellTab, IAsyncDisposable
{
    private readonly AppServices _services;
    private readonly ShellViewModel _shell;
    private bool _started;
    private bool _disposed;

    internal ProjectFilesViewModel(AppServices services, ShellViewModel shell, TabGroupViewModel group, ProjectFilesState state)
    {
        _services = services;
        _shell = shell;
        Group = group;
        State = state;
        Folder = state.Folder;
        // Before the switches below, whose changes save the state and read the changes again with them.
        _refreshWait = new UiTimeout(services.Time, services.Dispatcher);
        _changesWait = new UiTimeout(services.Time, services.Dispatcher);
        _stateWait = new UiTimeout(services.Time, services.Dispatcher);
        ShowOnlyChanged = state.ShowOnlyChanged;
        AgainstGitHead = state.AgainstGitHead;
        _explorerHiddenHere = state.ExplorerHidden;
        _services.SettingsChanged += OnSettingsChanged;
        _shell.Layout.PropertyChanged += OnLayoutChanged;
        WatchGroupTabs();
        foreach (var file in state.Files.Where(f => f.Path.Length > 0))
        {
            OpenFiles.Add(new ProjectFileViewModel(services, this, file));
        }
        UpdateTabLabels();
        Showing = OpenFiles.FirstOrDefault(f => FolderHistory.SamePath(f.Path, state.ShowingPath ?? "")) ?? OpenFiles.FirstOrDefault();
    }

    /// <summary>What's saved for the tab; <see cref="CaptureState"/> brings it up to date.</summary>
    internal ProjectFilesState State { get; }

    /// <summary>The group it belongs to, whose first row it is.</summary>
    public TabGroupViewModel Group { get; }

    public string Id => State.Id;

    /// <summary>The project's folder: the group's.</summary>
    public string Folder { get; }

    public string GroupFolder => Folder;

    public string DisplayName => "Project Files";

    // ---- In the sidebar ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Its row's second line: the file showing (with none, the first open, in a window of its own), or <i>No files
    /// open</i>; while files have unsaved edits, how many, in the caution color.
    /// </summary>
    public string RowDetail => UnsavedCount > 0 ? $"{UnsavedCount} unsaved" : (Showing ?? OpenFiles.FirstOrDefault())?.TabLabel ?? "No files open";

    /// <summary>How many open files have edits not saved yet.</summary>
    public int UnsavedCount => OpenFiles.Count(f => f.IsModified);

    public bool HasUnsaved => UnsavedCount > 0;

    /// <summary>The row's tip.</summary>
    public string RowTip => $"The files in {Folder}";

    // ---- Selection and panes (DESIGN.md §18, "Split panes") --------------------------------------------------------

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!IsInSplit)
        {
            IsShown = value;
        }
    }

    [ObservableProperty]
    public partial bool IsShown { get; set; }

    partial void OnIsShownChanged(bool value)
    {
        if (value)
        {
            Start();
            _ = CheckOpenFilesOnDiskAsync();
        }
    }

    [ObservableProperty]
    public partial bool IsInSplit { get; set; }

    [ObservableProperty]
    public partial int PaneIndex { get; set; } = -1;

    /// <summary>Its group's color, for its pane's header with tabs side by side (DESIGN.md §18, "Split panes"); the shell keeps it.</summary>
    [ObservableProperty]
    public partial Avalonia.Media.Color? PaneColor { get; set; }

    [ObservableProperty]
    public partial bool IsDragged { get; set; }

    /// <summary>A click or the focus in its pane: the keyboard and the header follow it now.</summary>
    public void FocusPane()
    {
        if (IsShown && !IsSelected)
        {
            _shell.Selected = this;
        }
    }

    /// <summary><b>Close pane</b> on its pane's header: the tab stays open, out of sight.</summary>
    [RelayCommand]
    private void ClosePane() => _shell.ClosePaneCommand.Execute(this);

    /// <summary>Its row's × and menu's <b>Close</b>: asks about unsaved files first.</summary>
    [RelayCommand]
    private Task CloseAsync() => _shell.CloseProjectFilesAsync(this);

    [RelayCommand]
    private Task RevealFolderAsync() => Directory.Exists(Folder) ? _services.Platform.RevealFolderAsync(Folder) : Task.CompletedTask;

    [RelayCommand]
    private Task CopyFolderPathAsync() => _services.Platform.SetClipboardTextAsync(Folder);

    /// <summary>The first time it shows: the explorer reads the project's top folder, and the open files are read.</summary>
    private void Start()
    {
        if (_started)
        {
            return;
        }
        _started = true;
        _ = LoadExplorerAsync();
        // Its Perforce workspace, looked for in the background (DESIGN.md §18, "Perforce in the Files tab").
        StartPerforce();
        foreach (var file in OpenFiles.ToArray())
        {
            _ = file.LoadAsync();
        }
        UpdateChanges();
    }

    // ---- The layout (DESIGN.md §18, "Project files", "The layout") -------------------------------------------------

    /// <summary>Settings → Files and diffs → Explorer side is Right.</summary>
    public bool IsExplorerOnRight => _services.Settings.Editor.ExplorerSide == ExplorerSide.Right;

    public string MoveExplorerText => IsExplorerOnRight ? "Move to the left" : "Move to the right";

    /// <summary>The explorer's <b>⋯</b> menu's <b>Move to the right</b> (or left), which is the same setting.</summary>
    [RelayCommand]
    private void MoveExplorer()
    {
        _services.Settings.Editor.ExplorerSide = IsExplorerOnRight ? ExplorerSide.Left : ExplorerSide.Right;
        _services.SaveSettings();
    }

    /// <summary>The explorer's width: the same for every Project Files tab.</summary>
    public double ExplorerWidth => _shell.Layout.ExplorerWidth;

    /// <summary>The explorer is hidden in this tab alone: a conversation opened its first file beside it.</summary>
    private bool _explorerHiddenHere;

    /// <summary>The explorer shows; a button at the start of the file tabs' row hides it, for every Project Files tab.</summary>
    public bool IsExplorerShown => !_shell.Layout.IsExplorerHidden && !_explorerHiddenHere;

    [RelayCommand]
    private void ToggleExplorer()
    {
        if (IsExplorerShown)
        {
            _shell.Layout.IsExplorerHidden = true;
        }
        else
        {
            ShowExplorer();
        }
    }

    /// <summary>The explorer shows again, in every Project Files tab and in this one where it started hidden.</summary>
    internal void ShowExplorer()
    {
        _shell.Layout.IsExplorerHidden = false;
        if (_explorerHiddenHere)
        {
            _explorerHiddenHere = false;
            OnExplorerShownChanged();
            OnFileStateChanged();
        }
    }

    /// <summary>
    /// A conversation opened the first file beside it (DESIGN.md §18, "Project files", "Opening files in the editor"): the
    /// explorer starts hidden in this tab, leaving the file the pane.
    /// </summary>
    internal void HideExplorerHere()
    {
        if (_explorerHiddenHere)
        {
            return;
        }
        _explorerHiddenHere = true;
        OnExplorerShownChanged();
        OnFileStateChanged();
    }

    private void OnExplorerShownChanged()
    {
        OnPropertyChanged(nameof(IsExplorerShown));
        OnPropertyChanged(nameof(ToggleExplorerText));
    }

    public string ToggleExplorerText => IsExplorerShown ? "Hide the explorer" : "Show the explorer";

    public void ResizeExplorer(double width) => _shell.Layout.ResizeExplorer(width);

    public void SaveExplorerWidth() => _shell.Layout.SaveExplorerWidth();

    public void ResetExplorerWidth()
    {
        _shell.Layout.ResizeExplorer(ShellLayout.DefaultExplorerWidth);
        _shell.Layout.SaveExplorerWidth();
    }

    private void OnLayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellLayout.ExplorerWidth))
        {
            OnPropertyChanged(nameof(ExplorerWidth));
        }
        else if (e.PropertyName == nameof(ShellLayout.IsExplorerHidden))
        {
            OnExplorerShownChanged();
        }
    }

    /// <summary>The window's theme is dark: the diff modes' colors follow it. The view keeps it up to date.</summary>
    public bool IsDarkTheme
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            foreach (var file in OpenFiles)
            {
                file.UseDarkColors(value);
            }
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsExplorerOnRight));
        OnPropertyChanged(nameof(MoveExplorerText));
        OnPropertyChanged(nameof(WordWrap));
        foreach (var file in OpenFiles)
        {
            file.OnWordWrapChanged();
        }
        FollowIgnoredFilesSetting();
        OnPerforceSettingsChanged();
    }

    /// <summary>Settings → Files and diffs → Word wrap, unless the file showing has its own (<c>Alt+Z</c>).</summary>
    public bool WordWrap => _services.Settings.Editor.WordWrap;

    // ---- Kept with the app state -----------------------------------------------------------------------------------

    private readonly UiTimeout _stateWait;

    /// <summary>Something saved with the tab changed: the state is written once things settle.</summary>
    internal void OnFileStateChanged() => _stateWait.Restart(TimeSpan.FromSeconds(1), () =>
    {
        CaptureState();
        _services.SaveState();
    });

    /// <summary>Brings <see cref="State"/> up to date: the open files, the one showing, the folders expanded.</summary>
    internal ProjectFilesState CaptureState()
    {
        State.Files = [.. OpenFiles.Select(f => f.CaptureState())];
        State.ShowingPath = Showing?.Path;
        // Until the explorer has opened the folders saved, they're still the ones to open.
        if (_explorerLoaded.Task.IsCompleted)
        {
            State.ExpandedFolders = [.. ExpandedFolders()];
        }
        State.ShowOnlyChanged = ShowOnlyChanged;
        State.AgainstGitHead = AgainstGitHead;
        State.ExplorerHidden = _explorerHiddenHere;
        return State;
    }

    /// <summary>Every open file's unsaved text, kept on disk now: Claudette is quitting or restarting.</summary>
    internal void FlushBackups()
    {
        foreach (var file in OpenFiles)
        {
            file.FlushBackup();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;
        FlushBackups();
        _services.SettingsChanged -= OnSettingsChanged;
        _shell.Layout.PropertyChanged -= OnLayoutChanged;
        StopPerforce();
        StopWatchingGroupTabs();
        StopWatching();
        _refreshWait.Dispose();
        _changesWait.Dispose();
        _stateWait.Dispose();
        _findCancel?.Cancel();
        foreach (var file in OpenFiles)
        {
            file.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}
