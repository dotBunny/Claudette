using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Claudette.App.Services;
using Claudette.Core.Composer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>A piece of a found file's name, bold where it matched what was typed.</summary>
public sealed record NamePart(string Text, bool IsMatch);

/// <summary>A file found by name (DESIGN.md §18, "Project files", "Finding a file by name").</summary>
/// <param name="Folder">Its folder from the project's, with the OS's separators; empty for a file at the top.</param>
/// <param name="Line">The line typed after the name (<c>player:42</c>), or null.</param>
public sealed record FoundFile(string Name, string Folder, string FullPath, IReadOnlyList<NamePart> NameParts, int? Line)
{
    public bool HasFolder => Folder.Length > 0;

    /// <summary>Its path from the project's folder, whole, for the tip.</summary>
    public string RelativePath => Folder.Length > 0 ? Path.Combine(Folder, Name) : Name;

    /// <summary>What a screen reader reads for its row.</summary>
    public override string ToString() => HasFolder ? $"{Name}, in {Folder}" : Name;
}

/// <summary>
/// Finding a file by name (DESIGN.md §18, "Project files", "Finding a file by name"): what the composer's <c>@</c>
/// lists (what git tracks and the new files it doesn't ignore, or a walk of the folder), ranked as it ranks them.
/// </summary>
public sealed partial class ProjectFilesViewModel
{
    /// <summary>The list holds the best this many.</summary>
    internal const int MostFound = 200;

    private CancellationTokenSource? _findCancel;

    /// <summary>The box is up, over the tree.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListingFound), nameof(ShowsTree), nameof(ShowsChangedList))]
    public partial bool IsFinding { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListingFound), nameof(ShowsTree), nameof(ShowsChangedList), nameof(FindTypedInFilesText))]
    public partial string FindText { get; set; } = "";

    /// <summary>Something's typed: the list shows in the tree's place.</summary>
    public bool IsListingFound => IsFinding && FindText.Trim().Length > 0;

    public ObservableCollection<FoundFile> Found { get; } = [];

    [ObservableProperty]
    public partial FoundFile? SelectedFound { get; set; }

    /// <summary>Under the list: how many, or what's happening.</summary>
    [ObservableProperty]
    public partial string FindSummary { get; private set; } = "";

    /// <summary><b>Find file</b>'s tip, with its shortcut.</summary>
    public string FindTip => _services.Tips.GoToFile;

    /// <summary>The shortcuts as they're bound now, for the tab with nothing open.</summary>
    public ShortcutTips Tips => _services.Tips;

    /// <summary>
    /// Under the list, while something's typed: <i>Find “health” in files</i>, which looks for it in the files' text
    /// instead (DESIGN.md §18, "Finding in files"). Find in files' shortcut does the same with the keyboard in the box.
    /// </summary>
    public string FindTypedInFilesText => $"Find “{SplitLine(FindText.Trim()).Query}” in files";

    public string FindTypedInFilesTip => _services.Tips.FindInFiles;

    /// <summary>The link under the list: Find in files, with what's typed, and the tree back.</summary>
    [RelayCommand]
    private void FindTypedInFiles()
    {
        if (SplitLine(FindText.Trim()).Query is not { Length: > 0 } typed)
        {
            return;
        }
        StopFind();
        FindInFiles(new FindStart(typed), replace: false);
    }

    /// <summary>
    /// What Find in files starts with from this tab: what's typed in Go to file's box while the keyboard's there, or what
    /// the editor showing offers.
    /// </summary>
    internal FindStart? FindInFilesStart() =>
        FindBoxHasKeyboard?.Invoke() == true && SplitLine(FindText.Trim()).Query is { Length: > 0 } typed
            ? new FindStart(typed)
            : Showing?.FindInFilesStart?.Invoke();

    /// <summary>Whether the keyboard's in Go to file's box; null without a view.</summary>
    internal Func<bool>? FindBoxHasKeyboard { get; set; }

    /// <summary>Asks the view to put the keyboard in the box.</summary>
    public event Action? FindFocusRequested;

    /// <summary><b>Find file</b>, or <c>Ctrl/Cmd+P</c>: the box at the top of the explorer, with the keyboard in it.</summary>
    [RelayCommand]
    internal void StartFind()
    {
        ShowExplorer();
        IsFinding = true;
        FindFocusRequested?.Invoke();
        // Listed now, so the first keystroke has something to look through.
        _ = _services.FileIndexFor(Folder).GetAsync();
    }

    /// <summary><c>Esc</c>: the tree again, with the file last opened selected in it.</summary>
    [RelayCommand]
    internal void StopFind()
    {
        _findCancel?.Cancel();
        IsFinding = false;
        FindText = "";
        Found.Clear();
        FindSummary = "";
        if (Showing is { } showing)
        {
            _ = RevealInTreeAsync(showing.Path);
        }
    }

    partial void OnFindTextChanged(string value)
    {
        if (value.Length == 0 && IsFinding)
        {
            Found.Clear();
            FindSummary = "";
        }
        _ = FindAsync(value);
    }

    private async Task FindAsync(string typed)
    {
        _findCancel?.Cancel();
        var cancel = _findCancel = new CancellationTokenSource();
        var (query, line) = SplitLine(typed.Trim());
        if (query.Length == 0)
        {
            return;
        }
        var index = _services.FileIndexFor(Folder);
        if (index.Current.Count == 0)
        {
            FindSummary = "Listing files…";
        }
        IReadOnlyList<IndexedPath> paths;
        try
        {
            paths = await index.GetAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            paths = [];
        }
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        IReadOnlyList<IndexedPath> matches;
        try
        {
            matches = await Task.Run(() => PathMatcher.Match([.. paths.Where(p => !p.IsFolder)], query, int.MaxValue, cancel.Token), cancel.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        Found.Clear();
        foreach (var match in matches.Take(MostFound))
        {
            Found.Add(new FoundFile(match.Name, match.Parent.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar), Path.Combine(Folder, match.Path.Replace('/', Path.DirectorySeparatorChar)),
                NameParts(match.Name, query), line));
        }
        SelectedFound = Found.FirstOrDefault();
        var cut = paths.Count >= index.MaxFiles ? " The project has more files than are listed." : "";
        FindSummary = matches.Count switch
        {
            0 => $"No file's name matches.{cut}",
            1 => $"1 file{(cut.Length > 0 ? $".{cut}" : "")}",
            > MostFound => string.Create(CultureInfo.CurrentCulture, $"{MostFound} of {matches.Count:N0} files. Type more to narrow them.{cut}"),
            _ => string.Create(CultureInfo.CurrentCulture, $"{matches.Count:N0} files{(cut.Length > 0 ? $".{cut}" : "")}"),
        };
    }

    /// <summary><c>Enter</c> or a click: the file opens to stay, at the line typed after its name, and the tree comes back.</summary>
    [RelayCommand]
    private async Task OpenFoundAsync(FoundFile? found)
    {
        found ??= SelectedFound;
        if (found is null)
        {
            return;
        }
        await OpenFileAsync(found.FullPath, preview: false, line: found.Line, focus: true);
        StopFind();
    }

    /// <summary><c>Up</c> and <c>Down</c> in the box.</summary>
    internal void MoveFound(int offset)
    {
        if (Found.Count == 0)
        {
            return;
        }
        var at = SelectedFound is null ? -1 : Found.IndexOf(SelectedFound);
        SelectedFound = Found[Math.Clamp(at + offset, 0, Found.Count - 1)];
    }

    /// <summary>What's typed, and a line after it: <c>player:42</c>.</summary>
    internal static (string Query, int? Line) SplitLine(string typed)
    {
        var match = LineSuffix().Match(typed);
        return match.Success && int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var line) && line > 0
            ? (match.Groups[1].Value, line)
            : (typed, null);
    }

    /// <summary>The name in pieces, bold where the letters typed match it in order.</summary>
    internal static IReadOnlyList<NamePart> NameParts(string name, string query)
    {
        var letters = query.Contains('/') ? query[(query.LastIndexOf('/') + 1)..] : query;
        var matched = new bool[name.Length];
        var at = name.IndexOf(letters, StringComparison.OrdinalIgnoreCase);
        if (letters.Length > 0 && at >= 0)
        {
            for (var i = at; i < at + letters.Length; i++)
            {
                matched[i] = true;
            }
        }
        else
        {
            var next = 0;
            foreach (var letter in letters)
            {
                var found = name.IndexOf(letter.ToString(), next, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    break;
                }
                matched[found] = true;
                next = found + 1;
            }
        }
        var parts = new List<NamePart>();
        var start = 0;
        for (var i = 1; i <= name.Length; i++)
        {
            if (i == name.Length || matched[i] != matched[start])
            {
                parts.Add(new NamePart(name[start..i], matched[start]));
                start = i;
            }
        }
        return parts;
    }

    [GeneratedRegex(@"^(.*?):(\d+)(?::\d+)?$")]
    private static partial Regex LineSuffix();
}
