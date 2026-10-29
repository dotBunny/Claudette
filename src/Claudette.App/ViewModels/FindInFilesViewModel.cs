using System.Collections.ObjectModel;
using Claudette.App.Services;
using Claudette.Core.ProjectFiles;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// What Find in files starts with in place of what it had last (DESIGN.md §18, "Finding in files"): the text selected in
/// the editor, what the editor's find box has, with its options and, while it's replacing, its replacement, or what Go to
/// file has typed. Null options keep the popup's own.
/// </summary>
public sealed record FindStart(string Query, bool? MatchCase = null, bool? WholeWords = null, bool? Regex = null, string? Replacement = null)
{
    /// <summary>Something to look for, on one line: Find in files looks within lines.</summary>
    public bool IsUsable => Query.Length > 0 && Query.IndexOfAny(['\n', '\r']) < 0;
}

/// <summary>
/// Find in files and Replace in files (DESIGN.md §18, "Project files", "Finding in files"): a popup over the window, as
/// Rider's is, with the query and its options, the replacement, the file mask and where to look, the matches coming in
/// as it's typed, and the selected one's file under them. <c>Enter</c> opens a match in the project's Files tab;
/// <b>Open in Find window</b> keeps the results in a tool window. The chevron at the box's left shows the replacement, as
/// the editor's find box's does, and hides it.
/// </summary>
public sealed partial class FindInFilesViewModel : ViewModelBase, IDisposable
{
    /// <summary>How long after typing stops the search starts.</summary>
    internal static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(200);

    private readonly FindInFilesSearch _search;
    private readonly IFindResultsHost _results;
    private readonly Action _close;
    private readonly UiTimeout _searchWait;
    private readonly InlineConfirmation _replaceAll;
    private readonly ShortcutTips _tips;
    private CancellationTokenSource? _searching;
    private FindMatcher? _matcher;
    private FindSettings? _searched;
    private int _previewGeneration;
    private bool _disposed;

    /// <summary>The files with matches, by full path, with how many each has: what <b>Replace all</b> goes through.</summary>
    private readonly List<FindFileMatches> _files = [];

    internal FindInFilesViewModel(AppServices services, IFindInFilesHost host, IFindResultsHost results, string project, string projectName,
        FindInFilesState remembered, bool replace, string? folder, FindStart? start, Action close)
    {
        _search = new FindInFilesSearch(services, host);
        _results = results;
        _close = close;
        _searchWait = new UiTimeout(services.Time, services.Dispatcher);
        _replaceAll = new InlineConfirmation(ReplaceAllAsync);
        _replaceAll.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsAskingToReplaceAll));
        Project = project;
        ProjectName = projectName;
        _tips = services.Tips;
        // What it had last, with what it was opened with in place of the query (and the options and replacement it brought),
        // and the folder its menu named.
        Query = start?.Query ?? remembered.Query;
        MatchCase = start?.MatchCase ?? remembered.MatchCase;
        WholeWords = start?.WholeWords ?? remembered.WholeWords;
        IsRegex = start?.Regex ?? remembered.Regex;
        Replacement = start?.Replacement ?? remembered.Replacement;
        FileMask = remembered.FileMask;
        var directory = folder ?? (remembered.Directory is { } last && IsIn(last, project) ? last : null);
        Directory = directory ?? project;
        IsInDirectory = directory is not null;
        IsReplaceMode = replace;
        Search();
    }

    /// <summary>The project's folder: its group's.</summary>
    public string Project { get; }

    public string ProjectName { get; }

    public string Title => IsReplaceMode ? "Replace in files" : "Find in files";

    // ---- What to look for --------------------------------------------------------------------------------------------

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial bool MatchCase { get; set; }

    [ObservableProperty]
    public partial bool WholeWords { get; set; }

    [ObservableProperty]
    public partial bool IsRegex { get; set; }

    /// <summary>Replace in files: the replacement shows, under the box. The chevron, and the two shortcuts, switch it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(ModeTip), nameof(KeysText))]
    public partial bool IsReplaceMode { get; set; }

    /// <summary>The chevron's tip: what a click does, and its shortcut.</summary>
    public string ModeTip => IsReplaceMode ? _tips.HideReplace : _tips.ShowReplace;

    /// <summary>
    /// The keys, at the foot: <i>Enter opens · ↑↓ choose · Ctrl+Shift+R shows Replace · Esc closes</i>, as they're bound
    /// now and as this OS writes them.
    /// </summary>
    public string KeysText
    {
        get
        {
            var mode = IsReplaceMode
                ? _tips.Text(KeyboardShortcuts.FindInFiles) is { } find ? $"{find} hides Replace" : null
                : _tips.Text(KeyboardShortcuts.ReplaceInFiles) is { } replace ? $"{replace} shows Replace" : null;
            return string.Join(" · ", new[] { $"{ShortcutTips.Key("Enter")} opens", "↑↓ choose", mode, $"{ShortcutTips.Key("Escape")} closes" }.OfType<string>());
        }
    }

    [ObservableProperty]
    public partial string Replacement { get; set; } = "";

    /// <summary>The <b>File mask</b>: <c>*.cs, *.h</c>, with <c>!*.Designer.cs</c> to leave some out. Empty for every file.</summary>
    [ObservableProperty]
    public partial string FileMask { get; set; } = "";

    /// <summary>The scope is <b>Directory</b>, the folder in <see cref="Directory"/>, rather than <b>In project</b>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInProject))]
    public partial bool IsInDirectory { get; set; }

    public bool IsInProject
    {
        get => !IsInDirectory;
        set => IsInDirectory = !value;
    }

    [ObservableProperty]
    public partial string Directory { get; set; } = "";

    partial void OnQueryChanged(string value) => Search();

    partial void OnMatchCaseChanged(bool value) => Search();

    partial void OnWholeWordsChanged(bool value) => Search();

    partial void OnIsRegexChanged(bool value) => Search();

    partial void OnFileMaskChanged(string value) => Search();

    partial void OnIsInDirectoryChanged(bool value) => Search();

    partial void OnDirectoryChanged(string value) => Search();

    partial void OnIsReplaceModeChanged(bool value)
    {
        if (!value)
        {
            _replaceAll.Cancel();
        }
    }

    /// <summary>The ⋯ beside the folder: another folder to look in.</summary>
    [RelayCommand]
    private async Task PickDirectoryAsync()
    {
        if (await _search.Services.Platform.PickFolderAsync("Choose a folder to find in") is { } picked)
        {
            Directory = picked;
            IsInDirectory = true;
        }
    }

    internal FindSettings Settings => new(Project, Query, MatchCase, WholeWords, IsRegex, FileMask, IsInDirectory && Directory.Trim().Length > 0 ? Directory.Trim() : null);

    /// <summary>What it has now, for the next time it opens.</summary>
    internal FindInFilesState Remember() => new()
    {
        Query = Query,
        MatchCase = MatchCase,
        WholeWords = WholeWords,
        Regex = IsRegex,
        Replacement = Replacement,
        FileMask = FileMask,
        Directory = IsInDirectory ? Directory : null,
    };

    // ---- The matches -------------------------------------------------------------------------------------------------

    public ObservableCollection<FindHitRow> Hits { get; } = [];

    [ObservableProperty]
    public partial FindHitRow? SelectedHit { get; set; }

    partial void OnSelectedHitChanged(FindHitRow? value) => _ = ShowPreviewAsync(value);

    public bool HasHits => Hits.Count > 0;

    /// <summary>Under the list: <i>57 matches in 12 files (first 1,000 shown)</i>, <i>Searching…</i>, <i>No matches</i>.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>What else the search has to say: files skipped, a list cut short.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotes))]
    public partial string Notes { get; private set; } = "";

    public bool HasNotes => Notes.Length > 0;

    /// <summary>Why nothing can be looked for: a regular expression that isn't valid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    public bool HasProblem => Problem is not null;

    [ObservableProperty]
    public partial bool IsSearching { get; private set; }

    /// <summary>How many matches the last search found, kept or not, and in how many files.</summary>
    public int MatchCount { get; private set; }

    public int FileCount { get; private set; }

    /// <summary>Typing, or an option changed: the search starts once typing stops, in place of any still going.</summary>
    private void Search()
    {
        if (_disposed)
        {
            return;
        }
        _searchWait.Restart(SearchDelay, () => _ = RunAsync());
    }

    /// <summary><b>Search again</b>, or after a replace: now.</summary>
    internal Task SearchNowAsync()
    {
        _searchWait.Cancel();
        return RunAsync();
    }

    private async Task RunAsync()
    {
        _searching?.Cancel();
        _searching?.Dispose();
        var cancel = _searching = new CancellationTokenSource();
        _replaceAll.Cancel();
        var settings = Settings;
        Clear();
        if (!FindMatcher.TryCreate(settings.ToQuery(), out var matcher, out var problem))
        {
            _matcher = null;
            Problem = problem;
            IsSearching = false;
            return;
        }
        Problem = null;
        _matcher = matcher;
        _searched = settings;
        IsSearching = true;
        Summary = "Searching…";
        var last = await _search.RunAsync(settings, matcher, batch =>
        {
            // Overtaken by a later search: its batches are dropped.
            if (!ReferenceEquals(cancel, _searching))
            {
                return;
            }
            Add(batch);
        }, cancel.Token);
        if (last is null || !ReferenceEquals(cancel, _searching))
        {
            return;
        }
        IsSearching = false;
        SelectedHit ??= Hits.FirstOrDefault();
    }

    private void Clear()
    {
        Hits.Clear();
        _files.Clear();
        SelectedHit = null;
        MatchCount = 0;
        FileCount = 0;
        Summary = "";
        Notes = "";
        OnPropertyChanged(nameof(HasHits));
        OnPropertyChanged(nameof(ReplaceAllText));
    }

    private void Add(FindBatch batch)
    {
        foreach (var hit in batch.Hits)
        {
            Hits.Add(new FindHitRow(hit));
        }
        _files.AddRange(batch.Files);
        MatchCount = batch.Matches;
        FileCount = batch.FilesWithMatches;
        Summary = FindInFilesSearch.Summary(batch);
        Notes = _search.Notes(batch);
        // The first match is chosen as soon as there is one, so the preview shows while the rest come in.
        SelectedHit ??= Hits.FirstOrDefault();
        OnPropertyChanged(nameof(HasHits));
        OnPropertyChanged(nameof(ReplaceAllText));
    }

    /// <summary><c>Up</c> and <c>Down</c>.</summary>
    internal void Move(int offset)
    {
        if (Hits.Count == 0)
        {
            return;
        }
        var at = SelectedHit is null ? -1 : Hits.IndexOf(SelectedHit);
        SelectedHit = Hits[Math.Clamp(at + offset, 0, Hits.Count - 1)];
    }

    /// <summary><c>Enter</c> or a double-click: the match in the project's Files tab, selected, and the popup closes.</summary>
    [RelayCommand]
    private async Task OpenAsync(FindHitRow? row)
    {
        if ((row ?? SelectedHit) is not { } chosen)
        {
            return;
        }
        _close();
        await _search.Host.OpenHitAsync(chosen.Hit);
    }

    [RelayCommand]
    private void Close() => _close();

    // ---- The preview -------------------------------------------------------------------------------------------------

    /// <summary>The selected match in its file, colored, under the list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    public partial FindPreview? Preview { get; private set; }

    public bool HasPreview => Preview is not null;

    private async Task ShowPreviewAsync(FindHitRow? row)
    {
        var generation = ++_previewGeneration;
        if (row is null)
        {
            Preview = null;
            return;
        }
        // The same file as showing: only the place in it changes.
        if (Preview is { } shown && FindInFiles.PathComparer.Equals(shown.Path, row.Hit.Path))
        {
            Preview = shown with { Line = row.Hit.Line, Column = row.Hit.Column, Length = row.Hit.Length };
            return;
        }
        var preview = await _search.PreviewAsync(row.Hit);
        // The latest choice wins: one made while this file was read has its own preview coming.
        if (generation == _previewGeneration && !_disposed)
        {
            Preview = preview;
        }
    }

    // ---- Replacing ---------------------------------------------------------------------------------------------------

    /// <summary>What happened at the last replace, and the files it couldn't change.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReplaceNotice))]
    public partial string? ReplaceNotice { get; private set; }

    public bool HasReplaceNotice => ReplaceNotice is not null;

    public ObservableCollection<string> ReplaceProblems { get; } = [];

    /// <summary><b>Replace all</b>'s question, asked in place.</summary>
    public string ReplaceAllText => $"Replace {FindInFilesSearch.Count(MatchCount, "match", "matches")} in {FindInFilesSearch.Count(FileCount, "file", "files")}?";

    public bool IsAskingToReplaceAll => _replaceAll.IsOpen;

    public IRelayCommand ConfirmReplaceAllCommand => _replaceAll.ConfirmCommand;

    public IRelayCommand CancelReplaceAllCommand => _replaceAll.CancelCommand;

    /// <summary>
    /// <b>Replace</b>: the selected match, if it's still where it was found. Its file's matches are found again after, in
    /// place in the list, and the match after it is selected.
    /// </summary>
    [RelayCommand]
    private async Task ReplaceAsync()
    {
        if (SelectedHit is not { } row || _matcher is not { } matcher || IsSearching)
        {
            return;
        }
        var at = Hits.IndexOf(row);
        var outcome = await _search.ReplaceAsync(Project, matcher, Replacement, [new ReplaceTarget(row.Hit.Path, row.Hit.RelativePath) { Only = row.Hit }]);
        Report(outcome);
        var fresh = await _search.SearchFileAsync(row.Hit.Path, row.Hit.RelativePath, matcher);
        if (!ReferenceEquals(matcher, _matcher))
        {
            return;
        }
        Refresh(row.Hit.Path, row.Hit.RelativePath, fresh);
        // Its text changed: the preview reads it again.
        Preview = null;
        SelectedHit = null;
        if (Hits.Count > 0)
        {
            SelectedHit = Hits[Math.Clamp(at, 0, Hits.Count - 1)];
        }
    }

    /// <summary>One file's rows, and the counts, as its matches are now.</summary>
    private void Refresh(string path, string relativePath, IReadOnlyList<FindHit> fresh)
    {
        var first = -1;
        var shown = 0;
        for (var i = 0; i < Hits.Count; i++)
        {
            if (FindInFiles.PathComparer.Equals(Hits[i].Hit.Path, path))
            {
                first = first < 0 ? i : first;
                shown++;
            }
        }
        var at = _files.FindIndex(f => FindInFiles.PathComparer.Equals(f.Path, path));
        var before = at >= 0 ? _files[at].Count : 0;
        if (first >= 0)
        {
            for (var i = 0; i < shown; i++)
            {
                Hits.RemoveAt(first);
            }
            var room = Math.Max(0, FindInFiles.MostHits - Hits.Count);
            foreach (var (hit, offset) in fresh.Take(room).Select((h, i) => (h, i)))
            {
                Hits.Insert(first + offset, new FindHitRow(hit));
            }
        }
        if (at >= 0)
        {
            if (fresh.Count > 0)
            {
                _files[at] = new FindFileMatches(path, relativePath, fresh.Count);
            }
            else
            {
                _files.RemoveAt(at);
            }
        }
        MatchCount += fresh.Count - before;
        FileCount = _files.Count;
        Summary = FindInFilesSearch.Summary(MatchCount, FileCount, MatchCount > Hits.Count, complete: true);
        OnPropertyChanged(nameof(HasHits));
        OnPropertyChanged(nameof(ReplaceAllText));
    }

    /// <summary><b>Replace all</b>: asks first, in place.</summary>
    [RelayCommand]
    private void AskReplaceAll()
    {
        if (_matcher is not null && MatchCount > 0 && !IsSearching)
        {
            _replaceAll.Ask();
        }
    }

    /// <summary>Every match the search found, in every file, each file searched again first; then the search runs again.</summary>
    private async Task ReplaceAllAsync()
    {
        if (_matcher is not { } matcher || _files.Count == 0)
        {
            return;
        }
        var outcome = await _search.ReplaceAsync(Project, matcher, Replacement, [.. _files.Select(f => new ReplaceTarget(f.Path, f.RelativePath))]);
        Report(outcome);
        await SearchNowAsync();
    }

    private void Report(ReplaceOutcome outcome)
    {
        ReplaceProblems.Clear();
        foreach (var problem in outcome.Problems)
        {
            ReplaceProblems.Add(problem);
        }
        ReplaceNotice = outcome.Summary;
    }

    // ---- Keeping the results -----------------------------------------------------------------------------------------

    /// <summary>The Find window is there to keep the results in: <b>Open in Find window</b> shows.</summary>
    public bool CanKeep => _results.CanKeepResults;

    /// <summary><b>Open in Find window</b>, or <c>Ctrl/Cmd+Enter</c>: the results go on in a tab of the Find window, and the popup closes.</summary>
    [RelayCommand]
    private void Keep()
    {
        if (!CanKeep || _searched is not { } searched || _matcher is null)
        {
            return;
        }
        var kept = new FindResultsViewModel(new FindInFilesSearch(_search.Services, _search.Host), searched, IsReplaceMode, Replacement);
        kept.Load([.. Hits.Select(h => h.Hit)], _files, MatchCount, FileCount, Summary, Notes);
        _results.Keep(kept);
        _close();
    }

    private static bool IsIn(string folder, string project)
    {
        var relative = Path.GetRelativePath(project, folder);
        return relative == "." || !FolderListing.IsUp(relative) && !Path.IsPathRooted(relative);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _searchWait.Dispose();
        _searching?.Cancel();
        _searching?.Dispose();
        _searching = null;
    }
}
