using Claudette.App.Editor;
using Claudette.App.Services;
using Claudette.App.ViewModels.ToolWindows;
using Claudette.Core.ProjectFiles;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// Find in files and Replace in files (DESIGN.md §18, "Project files", "Finding in files"): the popup over the window,
/// for the focused tab's project, and what it needs of the Files tabs: their open files' text, and opening a match.
/// </summary>
public sealed partial class ShellViewModel : IFindInFilesHost, IFindResultsHost
{
    /// <summary>The popup, while it's open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFindInFilesOpen))]
    public partial FindInFilesViewModel? FindInFiles { get; private set; }

    public bool IsFindInFilesOpen => FindInFiles is not null;

    /// <summary><c>Ctrl/Cmd+Shift+F</c>, or the palette's <b>Find in files…</b>.</summary>
    [RelayCommand]
    private void OpenFindInFiles() => ShowFindInFiles(replace: false);

    /// <summary><c>Ctrl/Cmd+Shift+R</c>, or the palette's <b>Replace in files…</b>.</summary>
    [RelayCommand]
    private void OpenReplaceInFiles() => ShowFindInFiles(replace: true);

    /// <summary>
    /// Opens the popup for <paramref name="group"/>'s project, or the focused tab's: in <paramref name="folder"/> when a
    /// folder's menu named one, and with <paramref name="start"/> in its box, or else what the focused Files tab offers
    /// (what's typed in the box with the keyboard, or the text selected in the editor), when that's one line.
    /// </summary>
    internal void ShowFindInFiles(bool replace, string? folder = null, TabGroupViewModel? group = null, FindStart? start = null)
    {
        group ??= SelectedFiles?.Group ?? (SelectedTab is { } tab ? Groups.FirstOrDefault(g => g.Tabs.Contains(tab)) : Groups.FirstOrDefault());
        if (group is null)
        {
            return;
        }
        Palette = null;
        start ??= SelectedFiles?.FindInFilesStart();
        FindInFiles?.Dispose();
        FindInFiles = new FindInFilesViewModel(_services, this, this, group.Folder, group.Label, _services.State.FindInFiles, replace, folder,
            start is { IsUsable: true } ? start : null, CloseFindInFiles);
    }

    /// <summary>Esc, a click outside it, or a match opened: it goes, and what it had is kept for the next time.</summary>
    public void CloseFindInFiles()
    {
        if (FindInFiles is not { } find)
        {
            return;
        }
        _services.State.FindInFiles = find.Remember();
        _services.SaveState();
        FindInFiles = null;
        find.Dispose();
    }

    /// <summary>Paths compare as Find in files compares them, as the OS does (its static class's name is the popup's here).</summary>
    private static StringComparer FindPaths => Core.ProjectFiles.FindInFiles.PathComparer;

    private IEnumerable<ProjectFileViewModel> OpenProjectFiles() =>
        Groups.Select(g => g.Files).OfType<ProjectFilesViewModel>().SelectMany(f => f.OpenFiles);

    IReadOnlyDictionary<string, string> IFindInFilesHost.OpenTexts()
    {
        var texts = new Dictionary<string, string>(FindPaths);
        foreach (var file in OpenProjectFiles())
        {
            if (file.Document is { } document)
            {
                texts[file.Path] = document.Text;
            }
        }
        return texts;
    }

    string? IFindInFilesHost.OpenText(string path) =>
        OpenProjectFiles().FirstOrDefault(f => f.Document is not null && FindPaths.Equals(f.Path, path))?.Document!.Text;

    IEditorDocument? IFindInFilesHost.OpenDocument(string path) =>
        OpenProjectFiles().FirstOrDefault(f => f.Document is not null && !f.IsReadOnly && FindPaths.Equals(f.Path, path))?.Document;

    /// <summary>A match, in its project's Files tab with the match selected, as a file clicked in a conversation opens.</summary>
    Task IFindInFilesHost.OpenHitAsync(FindHit hit) =>
        OpenInEditorAsync(hit.Path, hit.Line, hit.Column, hit.Line, SelectedTab, endColumn: hit.Column + hit.Length);

    /// <summary>
    /// Before Replace writes files no editor has open: in a Perforce workspace, they're checked out, in the changelist of
    /// the project's tab focused last, as the Files tab does (DESIGN.md §18, "Opening files in Perforce").
    /// </summary>
    IBeforeFilesWrite IFindInFilesHost.BeforeWriting(string project) =>
        Groups.FirstOrDefault(g => FindPaths.Equals(g.Folder, project)) is { } group
            ? new PerforceBeforeFilesWrite(_services.Perforce.Files, PerforceFilesService.ContextFor(group.Folder, group.Tabs))
            : NothingBeforeFilesWrite.Instance;

    // ---- Keeping results (DESIGN.md §18, "Finding in files") ------------------------------------------------------------

    public bool CanKeepResults => true;

    /// <summary>The Find tool window's searches, each a tab in its header.</summary>
    public FindWindowViewModel FindResults { get; } = new();

    /// <summary><b>Open in Find window</b>: a tab of their own in the Find window, which shows, and disposes them when the tab closes.</summary>
    public void Keep(FindResultsViewModel results)
    {
        FindResults.Add(results);
        ToolWindows.Show(ToolWindowKinds.Find);
    }
}
