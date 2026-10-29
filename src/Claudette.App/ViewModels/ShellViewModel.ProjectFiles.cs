using Claudette.Core.ProjectFiles;
using Claudette.Core.Protocol;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// The folder groups' Project Files tabs (DESIGN.md §18, "Project files"): opening one from its group, closing it,
/// bringing them back at launch, <b>Go to file</b>, and opening a file in the editor from a reply, Changed files or a
/// Markdown file's link.
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>
    /// The folder button of a group with no tab open, where the project's tools button is for a group with one: opens the
    /// folder in the file manager.
    /// </summary>
    [RelayCommand]
    private Task RevealGroupFolder(TabGroupViewModel? group) =>
        group is not null ? _services.Platform.RevealFolderAsync(group.Folder) : Task.CompletedTask;

    /// <summary>
    /// The group's <b>Project files</b> button, and its menu's <b>Open project files</b>: opens its Project Files tab, or
    /// selects it when it's open, with its explorer showing.
    /// </summary>
    [RelayCommand]
    private void OpenProjectFiles(TabGroupViewModel? group)
    {
        if (group is not null)
        {
            var files = ProjectFilesFor(group);
            files.ShowExplorer();
            Selected = files;
        }
    }

    /// <summary>A click on a Project Files tab's row: it takes the focus, as a session tab's does.</summary>
    [RelayCommand]
    private void SelectFiles(ProjectFilesViewModel? files)
    {
        if (files is not null)
        {
            Selected = files;
        }
    }

    /// <summary>The group's Project Files tab, opened if it isn't.</summary>
    internal ProjectFilesViewModel ProjectFilesFor(TabGroupViewModel group)
    {
        if (group.Files is { } open)
        {
            return open;
        }
        // A new Files tab compares as Settings → Git → New Files tabs show says; from then on it has its own (DESIGN.md §18).
        var files = new ProjectFilesViewModel(_services, this, group,
            new ProjectFilesState { Folder = group.Folder, AgainstGitHead = _services.Settings.Git.NewFilesTabsShow == FilesTabChanges.GitHead });
        AddProjectFiles(group, files);
        SaveTabs();
        return files;
    }

    private void AddProjectFiles(TabGroupViewModel group, ProjectFilesViewModel files)
    {
        group.Files = files;
        OpenTabs.Add(files);
    }

    /// <summary>
    /// Closes a Project Files tab, asking first about files with unsaved edits. Its group leaves the sidebar with it when
    /// it has no session tab. False when it was asked to stay open.
    /// </summary>
    internal async Task<bool> CloseProjectFilesAsync(ProjectFilesViewModel files)
    {
        if (!await files.ConfirmCloseAsync())
        {
            return false;
        }
        var group = files.Group;
        var pane = _panes.IndexOf(files);
        if (pane >= 0)
        {
            _panes.RemoveAt(pane);
        }
        OpenTabs.Remove(files);
        group.Files = null;
        if (ReferenceEquals(Selected, files))
        {
            Selected = _panes.Count > 0 ? _panes[Math.Min(pane, _panes.Count - 1)] : group.Tabs.FirstOrDefault() ?? AllTabs.FirstOrDefault();
        }
        else if (pane >= 0)
        {
            UpdatePanes();
        }
        RemoveGroupIfEmpty(group);
        SaveTabs();
        await files.DisposeAsync();
        return true;
    }

    /// <summary>The Project Files tabs saved, each in its group, which is made for it when no tab of its folder came back.</summary>
    private void RestoreProjectFiles()
    {
        var state = _services.State;
        foreach (var saved in state.ProjectFiles.Where(f => f.Folder.Length > 0 && Directory.Exists(f.Folder)).ToArray())
        {
            var group = GroupFor(saved.Folder);
            if (group.Files is null)
            {
                AddProjectFiles(group, new ProjectFilesViewModel(_services, this, group, saved));
            }
        }
        // Unsaved edits no open file has used for a month go.
        var open = Groups.Select(g => g.Files).OfType<ProjectFilesViewModel>().SelectMany(f => f.OpenFiles.Select(o => o.Path)).ToArray();
        _ = Task.Run(() => _services.EditorBackups.DeleteUnused(open));
    }

    /// <summary>
    /// Set by the view: opens a file's window of its own (DESIGN.md §18, "Project files", "A file in a window of its
    /// own"), or brings the one it has to the front. The window puts the file back when it's closed.
    /// </summary>
    public Action<ProjectFileViewModel>? ShowFileWindow { get; set; }

    /// <summary>The groups in the order they were left in, which a group with only its Project Files tab needs.</summary>
    private void RestoreGroupOrder()
    {
        if (_services.State.GroupOrder is not { Count: > 0 } order)
        {
            return;
        }
        var position = 0;
        foreach (var folder in order)
        {
            if (Groups.FirstOrDefault(g => FolderHistory.SamePath(g.Folder, folder)) is { } group)
            {
                Groups.Move(Groups.IndexOf(group), Math.Min(position, Groups.Count - 1));
                position++;
            }
        }
        // Pinned groups go first, however they were left.
        OrderGroups();
    }

    /// <summary>
    /// <c>Ctrl/Cmd+P</c>: the focused tab's project's Files tab, with its explorer's box up to find a file by name. From a
    /// conversation, it opens beside it or in its place, as Settings → Files and diffs says.
    /// </summary>
    [RelayCommand]
    private void GoToFile()
    {
        var group = SelectedFiles?.Group ?? (SelectedTab is { } tab ? Groups.FirstOrDefault(g => g.Tabs.Contains(tab)) : Groups.FirstOrDefault());
        if (group is null)
        {
            return;
        }
        var files = ProjectFilesFor(group);
        ShowFromConversation(files);
        files.StartFind();
    }

    /// <summary>
    /// Settings → Files and diffs → Open files in is Claudette's editor, and <paramref name="path"/> is a file it opens: text, not
    /// over 10 MB. A program or shortcut that isn't text still shows in its folder, and a script opens as text.
    /// </summary>
    internal bool OpensInEditor(string path)
    {
        if (_services.Settings.Editor.OpenFilesIn != OpenFilesIn.Editor)
        {
            return false;
        }
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= TextFile.MaxBytes && !IsUnopenable(path) && !LooksBinary(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>An image, an archive, a build product: a NUL byte near the start, without a mark saying it's UTF-16 or UTF-32.</summary>
    private static bool LooksBinary(string path)
    {
        using var stream = File.OpenRead(path);
        var start = new byte[8000];
        var read = stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
        var bytes = start.AsSpan(0, read);
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]) || bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]) || bytes.StartsWith((ReadOnlySpan<byte>)[0, 0, 0xFE, 0xFF]))
        {
            return false;
        }
        return bytes.IndexOf((byte)0) >= 0;
    }

    /// <summary>
    /// A folder a reply names, in a group's folder, with Settings → Files and diffs → Open files in Claudette's editor: it shows in
    /// that project's explorer, expanded and selected. False when it isn't in one, or files open in the OS's app.
    /// </summary>
    internal async Task<bool> RevealInProjectFilesAsync(string folder)
    {
        if (_services.Settings.Editor.OpenFilesIn != OpenFilesIn.Editor
            || Groups.Where(g => IsInside(folder, g.Folder)).OrderByDescending(g => g.Folder.Length).FirstOrDefault() is not { } group)
        {
            return false;
        }
        var files = ProjectFilesFor(group);
        ShowFromConversation(files);
        await files.RevealFolderInTreeAsync(folder);
        return true;
    }

    /// <summary>Programs and shortcuts that aren't text: <c>.exe</c>, <c>.lnk</c>, <c>.app</c> and the like.</summary>
    private static bool IsUnopenable(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".exe" or ".dll" or ".lnk" or ".app" or ".msi" or ".dmg" or ".pkg" or ".com" or ".scr" or ".so" or ".dylib";

    /// <summary>
    /// Opens <paramref name="path"/> in the Files tab of the group whose folder holds it (the deepest), or for a file in
    /// none, of <paramref name="from"/>'s group, at <paramref name="line"/> when given (DESIGN.md §18, "Project files",
    /// "Opening files in the editor"). From a conversation the Files tab opens beside it, or in its place, as Settings →
    /// Editor says, and takes the focus. Opened beside it for its first file, its explorer starts hidden.
    /// </summary>
    internal async Task OpenInEditorAsync(string path, int? line, int? column, int? endLine, TabViewModel? from, int? endColumn = null)
    {
        var group = Groups.Where(g => IsInside(path, g.Folder)).OrderByDescending(g => g.Folder.Length).FirstOrDefault()
            ?? (from is null ? null : Groups.FirstOrDefault(g => g.Tabs.Contains(from)))
            ?? SelectedFiles?.Group
            ?? Groups.FirstOrDefault();
        if (group is null)
        {
            return;
        }
        var files = ProjectFilesFor(group);
        // One in a window of its own is gone to there, and the Files tab isn't shown for it.
        if (files.IsOpenInWindow(path))
        {
            await files.OpenFileAsync(path, preview: false, line, column, endLine, focus: true, endColumn);
            return;
        }
        if (from is not null)
        {
            var first = files.OpenFiles.Count == 0;
            if (ShowFromConversation(files) && first)
            {
                files.HideExplorerHere();
            }
        }
        else
        {
            Selected = files;
        }
        await files.OpenFileAsync(path, preview: false, line, column, endLine, focus: true, endColumn);
    }

    /// <summary>
    /// Opens a file named in a reply, or linked from a Markdown file's preview (DESIGN.md §18, "Project files", "Opening
    /// files in the editor"; "Markdown preview"): in the project's Files tab at <paramref name="line"/>, as Settings →
    /// Files and diffs says, or else in the app the OS uses. An image shows in the image window, as Settings → Files and
    /// diffs → Open images in says (DESIGN.md §18, "Opening images"). A program or shortcut that isn't text shows in its
    /// folder rather than running, and a folder shows in the explorer when it's in a project, else in Finder or Explorer.
    /// The image window says where it is from <paramref name="folder"/>; <paramref name="from"/> is the tab whose
    /// conversation named it.
    /// </summary>
    internal async Task<OpenedPath> OpenPathAsync(string path, string folder, TabViewModel? from, int? line = null, int? column = null, int? endLine = null)
    {
        var (exists, isFolder, isExecutable) = await Task.Run(() => Inspect(path));
        if (!exists)
        {
            return OpenedPath.NotFound;
        }
        if (isFolder)
        {
            if (await RevealInProjectFilesAsync(path))
            {
                return OpenedPath.InClaudette;
            }
            await _services.Platform.RevealFolderAsync(path);
        }
        else if (ShownPath(path, folder) is var shown && await Task.Run(() => ImageToView(path, shown)) is { } image)
        {
            ShowImage(image);
            return OpenedPath.InClaudette;
        }
        else if (await Task.Run(() => OpensInEditor(path)))
        {
            await OpenInEditorAsync(path, line, column, endLine, from);
            return OpenedPath.InClaudette;
        }
        else if (ReplyFileReference.IsProgram(path, isExecutable))
        {
            if (Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } parent)
            {
                await _services.Platform.RevealFolderAsync(parent);
            }
        }
        else
        {
            await _services.Platform.OpenFileAsync(path);
        }
        return OpenedPath.Elsewhere;
    }

    /// <summary>A path as it's shown: inside <paramref name="folder"/> from there, else in full.</summary>
    internal static string ShownPath(string path, string folder)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative;
    }

    /// <summary>Whether it's there, a folder, and (on macOS and Linux) a file with its execute bit set.</summary>
    private static (bool Exists, bool IsFolder, bool IsExecutable) Inspect(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return (true, true, false);
            }
            if (!File.Exists(path))
            {
                return (false, false, false);
            }
            const UnixFileMode Execute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (true, false, !OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & Execute) != 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (false, false, false);
        }
    }

    /// <summary>
    /// Shows a Files tab from the focused conversation, or run's log: beside it, as <b>Open beside</b> does, or in its
    /// place, as Settings → Files and diffs says. One already on screen is only focused. True when it opened beside.
    /// </summary>
    private bool ShowFromConversation(ProjectFilesViewModel files)
    {
        if (files.IsShown || Selected is not (TabViewModel or LogTabViewModel) || _services.Settings.Editor.FilesFromConversation == FilesFromConversation.InPlace)
        {
            Selected = files;
            return false;
        }
        OpenBeside(files);
        return true;
    }

    /// <summary>A session tab from a Files tab (the peek's header): on screen, it takes the focus; else it opens beside.</summary>
    internal void ShowFromFiles(TabViewModel tab)
    {
        if (tab.IsShown)
        {
            Selected = tab;
            return;
        }
        OpenBeside(tab);
    }

    private static bool IsInside(string path, string folder)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative != "." && !FolderListing.IsUp(relative) && !Path.IsPathRooted(relative);
    }

    /// <summary>The files open from <paramref name="path"/> (the file, or those in the folder) in every Files tab.</summary>
    internal IEnumerable<ProjectFileViewModel> OpenFilesAtOrUnder(string path) =>
        Groups.Select(g => g.Files).OfType<ProjectFilesViewModel>().SelectMany(f => f.OpenFilesAtOrUnder(path));

    /// <summary>
    /// A file or folder renamed in a Files tab's explorer (DESIGN.md §18, "Project files", "Renaming and deleting"): the
    /// files open from it follow it, in every Files tab.
    /// </summary>
    internal void FollowRename(string oldPath, string newPath)
    {
        foreach (var files in Groups.Select(g => g.Files).OfType<ProjectFilesViewModel>())
        {
            files.FollowRename(oldPath, newPath);
        }
    }

    /// <summary>A file or folder deleted from a Files tab's explorer: the files open from it close, in every Files tab.</summary>
    internal void CloseDeleted(string path)
    {
        foreach (var files in Groups.Select(g => g.Files).OfType<ProjectFilesViewModel>())
        {
            files.CloseDeleted(path);
        }
    }

    /// <summary>A tab of the group wrote files (a tool call, or its turn ended): its Files tab looks at its open files again.</summary>
    internal void OnTabWroteFiles(TabViewModel tab)
    {
        if (Groups.FirstOrDefault(g => g.Tabs.Contains(tab))?.Files is { } files)
        {
            files.OnTabWroteFiles();
        }
    }

    /// <summary>
    /// Claudette came to the front: every Files tab looks at its open files again, as an editor does, and every tab's
    /// Changed files at the disk (DESIGN.md §8), for what the user changed meanwhile.
    /// </summary>
    internal void OnAppActivated()
    {
        foreach (var files in Groups.Select(g => g.Files).OfType<ProjectFilesViewModel>())
        {
            _ = files.CheckOpenFilesOnDiskAsync();
        }
        foreach (var tab in AllTabs)
        {
            tab.ChangedFiles.CheckDisk();
        }
        // And the sidebar at Claude Code's sessions, for ones a terminal started meanwhile (DESIGN.md §4, "Claude Code's projects").
        RefreshClaudeProjects();
    }
}
