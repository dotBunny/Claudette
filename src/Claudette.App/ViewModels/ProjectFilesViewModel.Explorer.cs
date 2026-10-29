using System.Collections.ObjectModel;
using Claudette.Core.Diffs;
using Claudette.Core.Git;
using Claudette.Core.ProjectFiles;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// A row of the Project Files tab's explorer (DESIGN.md §18, "Project files", "The explorer"): a folder or a file of the
/// tree, indented by its depth, or in <b>Show only changed files</b>, a file Claude changed with its folder beside it. A
/// name, a new file's or folder's or a new one for a row, is typed in a <see cref="NameRow"/>.
/// </summary>
public partial class ExplorerRow : ObservableObject
{
    public ExplorerRow(TreeEntry entry, int depth, string? folderText = null)
    {
        Entry = entry;
        Depth = depth;
        FolderText = folderText;
        IsIgnored = entry.IsIgnored;
    }

    public TreeEntry Entry { get; private set; }

    public string Name => Entry.Name;

    public string FullPath => Entry.FullPath;

    public string RelativePath => Entry.RelativePath;

    public bool IsFolder => Entry.IsFolder;

    public bool IsFile => !Entry.IsFolder;

    /// <summary>What a screen reader reads for its row.</summary>
    public override string ToString() => Name;

    public bool IsLink => Entry.IsLink;

    /// <summary>A link back up the tree, which isn't expanded.</summary>
    public bool CanExpand => Entry.IsFolder && !Entry.LeadsUp;

    public int Depth { get; }

    /// <summary>How far in it's drawn: 12 pixels a level.</summary>
    public double Indent => Depth * 12;

    /// <summary>In <b>Show only changed files</b>: the file's folder from the project's, muted beside its name.</summary>
    public string? FolderText { get; }

    public bool HasFolderText => FolderText is { Length: > 0 };

    public string Tip => RelativePath.Length > 0 ? RelativePath : Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollapsedFolder))]
    public partial bool IsExpanded { get; set; }

    public bool IsCollapsedFolder => CanExpand && !IsExpanded;

    /// <summary>Git ignores it, or it's a folder the <c>@</c> list skips: drawn dimmed.</summary>
    [ObservableProperty]
    public partial bool IsIgnored { get; set; }

    /// <summary>Claude changed it: <c>M</c>, or <c>A</c> for a file Claude created (with git's HEAD, git's own letters).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChangeLetter))]
    public partial string? ChangeLetter { get; set; }

    public bool HasChangeLetter => ChangeLetter is not null;

    /// <summary>The change is ticked as reviewed: its letter is faint.</summary>
    [ObservableProperty]
    public partial bool IsReviewed { get; set; }

    /// <summary>A folder with a changed file somewhere inside it: it has a dot.</summary>
    [ObservableProperty]
    public partial bool HasChangesInside { get; set; }

    /// <summary>In <b>Show only changed files</b>, a file Claude deleted: struck through, and opening it shows its diff.</summary>
    [ObservableProperty]
    public partial bool IsDeleted { get; set; }

    /// <summary>It's in the project's Perforce workspace: its menu has a Perforce section (DESIGN.md §18, "Perforce in the Files tab").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckOut), nameof(CanMarkForAdd), nameof(CanMarkForDelete), nameof(CanRevert), nameof(CanDiffAgainstDepot))]
    public partial bool InPerforce { get; set; }

    /// <summary>What Perforce says of it, from the cache; null when that isn't known, or isn't shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PerforceMark), nameof(HasPerforceMark), nameof(PerforceTip),
        nameof(CanCheckOut), nameof(CanMarkForAdd), nameof(CanMarkForDelete), nameof(CanRevert), nameof(CanDiffAgainstDepot))]
    public partial Core.Perforce.PerforceFileState? PerforceState { get; set; }

    /// <summary>Its Perforce state beside its name, in Perforce's own words: <c>edit</c>, <c>add</c>, <c>delete</c>, <c>move</c>.</summary>
    public string? PerforceMark => PerforceMarks.Mark(PerforceState);

    public bool HasPerforceMark => PerforceMark is not null;

    /// <summary>The mark's tip, in full: <i>Checked out in CL 12345.</i></summary>
    public string? PerforceTip => PerforceState?.Describe();

    public bool CanCheckOut => InPerforce && PerforceMenu.CanCheckOut(PerforceState, IsFolder);

    public bool CanMarkForAdd => InPerforce && PerforceMenu.CanMarkForAdd(PerforceState, IsFolder);

    public bool CanMarkForDelete => InPerforce && PerforceMenu.CanMarkForDelete(PerforceState, IsFolder);

    public bool CanRevert => InPerforce && PerforceMenu.CanRevert(PerforceState, IsFolder);

    public bool CanDiffAgainstDepot => InPerforce && PerforceMenu.CanDiff(PerforceState, IsFolder);

    internal void Refresh(TreeEntry entry)
    {
        Entry = entry;
        IsIgnored = entry.IsIgnored;
        OnPropertyChanged(nameof(CanExpand));
        OnPropertyChanged(nameof(IsCollapsedFolder));
    }
}

/// <summary>The explorer (DESIGN.md §18, "Project files", "The explorer"): the tree, kept as the rows the list shows, and following the disk.</summary>
public sealed partial class ProjectFilesViewModel
{
    /// <summary>How long after the disk changes the folders shown are read again.</summary>
    internal static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(500);

    private static StringComparer PathComparer => ChangedFiles.PlatformPathComparer;

    private readonly UiTimeout _refreshWait;
    private readonly Dictionary<string, IDisposable> _watches = new(PathComparer);
    private readonly HashSet<string> _foldersToRead = new(PathComparer);
    private readonly HashSet<string> _filesToCheck = new(PathComparer);
    private bool _inRepository;

    /// <summary>The explorer's first listing is done: the tree can be revealed into.</summary>
    private readonly TaskCompletionSource _explorerLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The tree's rows, top to bottom: each folder's entries under it while it's expanded.</summary>
    public ObservableCollection<ExplorerRow> Rows { get; } = [];

    /// <summary>The row the keyboard is on.</summary>
    [ObservableProperty]
    public partial ExplorerRow? SelectedRow { get; set; }

    /// <summary>The project's folder is in a git repository: what git ignores is dimmed, and changes can be against HEAD.</summary>
    public bool IsGitRepository => _inRepository;

    /// <summary>The group's label, at the top of the explorer.</summary>
    public string ExplorerTitle => Group.Label;

    /// <summary>The tree as it was saved comes back: the top folder, then the folders that were expanded.</summary>
    private async Task LoadExplorerAsync()
    {
        try
        {
            await ListExplorerAsync();
        }
        finally
        {
            _explorerLoaded.TrySetResult();
        }
    }

    private async Task ListExplorerAsync()
    {
        _inRepository = Directory.Exists(Folder) && GitInfo.TryGetBranch(Folder) is not null;
        OnPropertyChanged(nameof(IsGitRepository));
        var entries = await ListAsync(Folder, parentIgnored: false);
        Rows.Clear();
        foreach (var entry in entries)
        {
            Rows.Add(NewRow(entry, 0));
        }
        Watch(Folder);
        RefreshPerforce([Folder]);
        // Shallowest first, so a folder's parent is there to expand it in.
        foreach (var relative in State.ExpandedFolders.OrderBy(f => f.Count(c => c == '/')).ToArray())
        {
            if (Rows.FirstOrDefault(r => r.IsFolder && string.Equals(r.RelativePath, relative, StringComparison.Ordinal)) is { } row)
            {
                await ExpandAsync(row);
            }
        }
        UpdateRowChanges();
        if (ShowOnlyChanged)
        {
            BuildChangedRows();
        }
    }

    private ExplorerRow NewRow(TreeEntry entry, int depth)
    {
        var row = new ExplorerRow(entry, depth);
        ApplyChange(row);
        ApplyPerforce(row);
        return row;
    }

    /// <summary>What git ignores is left out of the tree, as Settings → Git → <b>Files git ignores</b> said when it was last read; null before.</summary>
    private bool? _hidesIgnored;

    private bool HidesIgnored => _hidesIgnored ??= _services.Settings.Git.IgnoredFiles == IgnoredFiles.Hidden;

    /// <summary>
    /// A folder's entries for the tree, with what git ignores marked to be dimmed, or left out while Settings → Git →
    /// <b>Files git ignores</b> is <b>Hidden</b> (DESIGN.md §18, "The explorer"). Which is decided once they're read, so
    /// a listing that finishes after the setting changed follows it too.
    /// </summary>
    private async Task<IReadOnlyList<TreeEntry>> ListAsync(string folder, bool parentIgnored)
    {
        var entries = await FolderListing.ReadAsync(Folder, folder, _services.Git, _inRepository, parentIgnored);
        return HidesIgnored ? [.. entries.Where(e => !e.IsIgnored)] : entries;
    }

    /// <summary>
    /// Settings changed: when <b>Files git ignores</b> did, every folder shown is read again, so what git ignores leaves
    /// the tree, or comes back dimmed, at once.
    /// </summary>
    private void FollowIgnoredFilesSetting()
    {
        var hides = _services.Settings.Git.IgnoredFiles == IgnoredFiles.Hidden;
        if (_hidesIgnored == hides)
        {
            return;
        }
        var listed = _hidesIgnored is not null;
        _hidesIgnored = hides;
        if (listed && _started)
        {
            RefreshExplorer();
        }
    }

    /// <summary>A click on a folder, or Right and Left: shows what's in it, or hides it.</summary>
    [RelayCommand]
    private Task ToggleRowAsync(ExplorerRow? row)
    {
        if (row is not { CanExpand: true } or NameRow)
        {
            return Task.CompletedTask;
        }
        if (row.IsExpanded)
        {
            Collapse(row);
            return Task.CompletedTask;
        }
        return ExpandAsync(row);
    }

    /// <summary>A click on a row: a file opens in the preview tab, a folder opens or closes.</summary>
    [RelayCommand]
    private Task OpenRowAsync(ExplorerRow? row)
    {
        if (row is null or NameRow)
        {
            return Task.CompletedTask;
        }
        if (row.IsFolder)
        {
            return ToggleRowAsync(row);
        }
        return row.IsDeleted ? OpenDeletedAsync(row) : OpenFileAsync(row.FullPath, preview: true);
    }

    /// <summary>
    /// A double-click on a file, or Enter: it opens to stay. An image shows in the image window instead, as Settings →
    /// Editor → Open images in says (DESIGN.md §18, "Opening images"); a single click leaves it in the preview tab, so
    /// going down a folder of images doesn't open a window for each.
    /// </summary>
    [RelayCommand]
    private async Task OpenRowToStayAsync(ExplorerRow? row)
    {
        if (row is null or NameRow)
        {
            return;
        }
        if (row.IsFolder)
        {
            await ToggleRowAsync(row);
        }
        else if (row.IsDeleted)
        {
            await OpenDeletedAsync(row);
        }
        else if (await Task.Run(() => _shell.ImageToView(row.FullPath, row.RelativePath)) is { } image)
        {
            _shell.ShowImage(image);
        }
        else
        {
            await OpenFileAsync(row.FullPath, preview: false, focus: true);
        }
    }

    internal async Task ExpandAsync(ExplorerRow row)
    {
        if (!row.CanExpand || row.IsExpanded || Rows.IndexOf(row) < 0)
        {
            return;
        }
        row.IsExpanded = true;
        var entries = await ListAsync(row.FullPath, row.IsIgnored);
        var index = Rows.IndexOf(row);
        // It was collapsed again, or went away, while it was read.
        if (index < 0 || !row.IsExpanded)
        {
            return;
        }
        foreach (var entry in entries)
        {
            Rows.Insert(++index, NewRow(entry, row.Depth + 1));
        }
        Watch(row.FullPath);
        RefreshPerforce([row.FullPath]);
        OnFileStateChanged();
    }

    private void Collapse(ExplorerRow row)
    {
        row.IsExpanded = false;
        var index = Rows.IndexOf(row);
        while (index + 1 < Rows.Count && Rows[index + 1].Depth > row.Depth)
        {
            var child = Rows[index + 1];
            if (child.IsExpanded)
            {
                Unwatch(child.FullPath);
            }
            Rows.RemoveAt(index + 1);
        }
        Unwatch(row.FullPath);
        ForgetNamingRowIfGone();
        OnFileStateChanged();
    }

    /// <summary>The explorer's <b>Collapse all</b>.</summary>
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var row in Rows.Where(r => r.Depth == 0 && r.IsExpanded).ToArray())
        {
            Collapse(row);
        }
    }

    /// <summary>A folder's <b>Find in folder…</b>: Find in files, looking in it (DESIGN.md §18, "Finding in files").</summary>
    [RelayCommand]
    private void FindInFolder(ExplorerRow? row)
    {
        if (row is { IsFolder: true } and not NameRow)
        {
            _shell.ShowFindInFiles(replace: false, row.FullPath, Group);
        }
    }

    /// <summary>A folder's <b>Replace in folder…</b>.</summary>
    [RelayCommand]
    private void ReplaceInFolder(ExplorerRow? row)
    {
        if (row is { IsFolder: true } and not NameRow)
        {
            _shell.ShowFindInFiles(replace: true, row.FullPath, Group);
        }
    }

    /// <summary>
    /// <b>Find in files…</b> in the explorer's ⋯ and the menu of the space under its tree, and in the tab with nothing open:
    /// as its shortcut opens it, for this project.
    /// </summary>
    [RelayCommand]
    private void FindInProject() => _shell.ShowFindInFiles(replace: false, group: Group);

    /// <summary><b>Replace in files…</b>, in the same places.</summary>
    [RelayCommand]
    private void ReplaceInProject() => _shell.ShowFindInFiles(replace: true, group: Group);

    /// <summary>The tab with nothing open: its <b>Command palette</b>.</summary>
    [RelayCommand]
    private void OpenPalette() => _shell.OpenPaletteCommand.Execute(null);

    /// <summary>The editor's find box's <b>Find in files</b>: what it has, for this project.</summary>
    internal void FindInFiles(FindStart start, bool replace) => _shell.ShowFindInFiles(replace, group: Group, start: start);

    /// <summary>The explorer's <b>Refresh</b>: every folder shown is read again.</summary>
    [RelayCommand]
    private void RefreshExplorer()
    {
        RefreshPerforceWorkspace();
        _foldersToRead.Add(Folder);
        foreach (var row in Rows.Where(r => r.IsExpanded))
        {
            _foldersToRead.Add(row.FullPath);
        }
        _ = ReadQueuedFoldersAsync();
    }

    /// <summary>The relative paths of the folders expanded, for the state.</summary>
    private IEnumerable<string> ExpandedFolders() => Rows.Where(r => r.IsExpanded).Select(r => r.RelativePath);

    /// <summary>
    /// <b>Reveal in the explorer</b>: the folders down to <paramref name="path"/> open, and its row is selected. A file
    /// outside the project isn't in the tree.
    /// </summary>
    internal async Task RevealInTreeAsync(string path)
    {
        if (!_started)
        {
            return;
        }
        await _explorerLoaded.Task;
        if (ShowOnlyChanged)
        {
            ShowOnlyChanged = false;
        }
        var relative = FolderListing.Relative(Folder, path);
        if (Path.IsPathRooted(relative) || relative.Length == 0)
        {
            return;
        }
        var parts = relative.Split('/');
        var prefix = "";
        for (var i = 0; i < parts.Length - 1; i++)
        {
            prefix = prefix.Length == 0 ? parts[i] : $"{prefix}/{parts[i]}";
            if (Rows.FirstOrDefault(r => r.IsFolder && string.Equals(r.RelativePath, prefix, StringComparison.Ordinal)) is not { } folder)
            {
                return;
            }
            await ExpandAsync(folder);
        }
        SelectedRow = Rows.FirstOrDefault(r => string.Equals(r.RelativePath, relative, StringComparison.Ordinal));
    }

    /// <summary>A folder shown in the explorer, expanded and selected: one a reply named.</summary>
    internal async Task RevealFolderInTreeAsync(string folder)
    {
        ShowExplorer();
        if (!_started)
        {
            Start();
            await _explorerLoaded.Task;
        }
        await RevealInTreeAsync(folder);
        if (SelectedRow is { IsFolder: true, IsExpanded: false } row)
        {
            await ExpandAsync(row);
        }
    }

    /// <summary>A file tab's <b>Reveal in the explorer</b>, which shows the explorer if it's hidden.</summary>
    [RelayCommand]
    private Task RevealRowInTreeAsync(ProjectFileViewModel? file)
    {
        if (file is null)
        {
            return Task.CompletedTask;
        }
        ShowExplorer();
        return RevealInTreeAsync(file.Path);
    }

    [RelayCommand]
    private Task OpenRowInSystemAsync(ExplorerRow? row) => row is null ? Task.CompletedTask : OpenInSystemAsync(row.FullPath);

    [RelayCommand]
    private Task RevealRowAsync(ExplorerRow? row) => row is null ? Task.CompletedTask : RevealAsync(row.FullPath);

    [RelayCommand]
    private Task CopyRowPathAsync(ExplorerRow? row) => row is null ? Task.CompletedTask : CopyPathAsync(row.FullPath);

    [RelayCommand]
    private Task CopyRowRelativePathAsync(ExplorerRow? row) => row is null ? Task.CompletedTask : _services.Platform.SetClipboardTextAsync(row.RelativePath);

    [RelayCommand]
    private Task OpenRowDiffAsync(ExplorerRow? row) => row is null ? Task.CompletedTask : OpenDiffAsync(row.FullPath);

    // ---- Following the disk ----------------------------------------------------------------------------------------

    /// <summary>Watches a folder shown in the tree, or one an open file is in.</summary>
    private void Watch(string folder)
    {
        if (_disposed || _watches.ContainsKey(folder) || !Directory.Exists(folder))
        {
            return;
        }
        _watches[folder] = _services.FolderWatcher.Watch(folder, changed => _services.Dispatcher.Post(() => OnDiskChanged(folder, changed)));
    }

    /// <summary>Stops watching a folder, unless an expanded row or an open file still needs it.</summary>
    private void Unwatch(string folder)
    {
        if (PathComparer.Equals(folder, Folder) || Rows.Any(r => r.IsExpanded && PathComparer.Equals(r.FullPath, folder))
            || OpenFiles.Any(f => PathComparer.Equals(Path.GetDirectoryName(f.Path) ?? "", folder)))
        {
            return;
        }
        if (_watches.Remove(folder, out var watch))
        {
            watch.Dispose();
        }
    }

    private void StopWatching()
    {
        foreach (var watch in _watches.Values)
        {
            watch.Dispose();
        }
        _watches.Clear();
    }

    /// <summary>
    /// Something in <paramref name="folder"/> appeared, went, was renamed or written: half a second after the last such
    /// change, the folder is read again and the open files in it are looked at.
    /// </summary>
    private void OnDiskChanged(string folder, string? path)
    {
        if (_disposed)
        {
            return;
        }
        _foldersToRead.Add(folder);
        if (path is null)
        {
            foreach (var file in OpenFiles.Where(f => PathComparer.Equals(Path.GetDirectoryName(f.Path) ?? "", folder)))
            {
                _filesToCheck.Add(file.Path);
            }
        }
        else if (OpenFiles.Any(f => PathComparer.Equals(f.Path, path)))
        {
            _filesToCheck.Add(path);
        }
        _refreshWait.Restart(RefreshDelay, () => _ = ReadQueuedFoldersAsync());
    }

    private async Task ReadQueuedFoldersAsync()
    {
        var files = _filesToCheck.ToArray();
        _filesToCheck.Clear();
        foreach (var path in files)
        {
            if (OpenFiles.FirstOrDefault(f => PathComparer.Equals(f.Path, path)) is { } file)
            {
                await file.CheckDiskAsync();
            }
        }
        // While a new file's or folder's name is typed, the tree waits: drawing it again would take the box away.
        if (NamingRow is not null)
        {
            return;
        }
        var folders = _foldersToRead.ToArray();
        _foldersToRead.Clear();
        foreach (var folder in folders)
        {
            await ReadFolderAgainAsync(folder);
        }
        UpdateRowChanges();
        // What changed on disk may have changed in Perforce too: asked again, never waited for.
        RefreshPerforce(folders);
    }

    /// <summary>
    /// Reads a folder shown in the tree again, keeping the rows that are still there (with what's expanded under them and
    /// which is selected), adding new ones in their place and taking away those that went.
    /// </summary>
    private async Task ReadFolderAgainAsync(string folder)
    {
        var isRoot = PathComparer.Equals(folder, Folder);
        var parent = isRoot ? null : Rows.FirstOrDefault(r => r.IsExpanded && PathComparer.Equals(r.FullPath, folder));
        if (!isRoot && parent is null)
        {
            return;
        }
        var entries = await ListAsync(folder, parent?.IsIgnored ?? false);
        var start = parent is null ? 0 : Rows.IndexOf(parent) + 1;
        if (parent is not null && start == 0)
        {
            return;
        }
        var depth = parent is null ? 0 : parent.Depth + 1;
        // Each child now there, with the rows under it.
        var subtrees = new Dictionary<string, List<ExplorerRow>>(PathComparer);
        var end = start;
        while (end < Rows.Count && Rows[end].Depth >= depth)
        {
            if (Rows[end].Depth == depth)
            {
                subtrees[Rows[end].FullPath] = [Rows[end]];
            }
            else if (subtrees.Count > 0)
            {
                subtrees.Values.Last().Add(Rows[end]);
            }
            end++;
        }
        var rebuilt = new List<ExplorerRow>();
        foreach (var entry in entries)
        {
            if (subtrees.Remove(entry.FullPath, out var kept) && kept[0].IsFolder == entry.IsFolder)
            {
                kept[0].Refresh(entry);
                rebuilt.AddRange(kept);
            }
            else
            {
                rebuilt.Add(NewRow(entry, depth));
            }
        }
        // The rows of what went: their folders aren't watched any more.
        foreach (var gone in subtrees.Values.SelectMany(s => s).Where(r => r.IsExpanded))
        {
            gone.IsExpanded = false;
            Unwatch(gone.FullPath);
        }
        var selected = SelectedRow;
        if (Rows.Skip(start).Take(end - start).SequenceEqual(rebuilt))
        {
            return;
        }
        for (var i = end - 1; i >= start; i--)
        {
            Rows.RemoveAt(i);
        }
        for (var i = 0; i < rebuilt.Count; i++)
        {
            Rows.Insert(start + i, rebuilt[i]);
        }
        ForgetNamingRowIfGone();
        if (selected is not null && Rows.Contains(selected))
        {
            SelectedRow = selected;
        }
    }
}
