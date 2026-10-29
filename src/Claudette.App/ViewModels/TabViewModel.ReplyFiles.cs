using Claudette.App.Conversation;
using Claudette.Core.Protocol;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>Files named in Claude's replies (DESIGN.md §18): finding them, and a click on one.</summary>
public sealed partial class TabViewModel
{
    private ReplyFiles? _replyFiles;

    /// <summary>The files the tab's replies name: which inline code is a file, and where a clicked link's file is.</summary>
    public ReplyFiles ReplyFiles => _replyFiles ??= new ReplyFiles(
        new ReplyFileFinder(() => Folder, () => ExtraFolders, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), () => FileIndex,
            // The user's environment, as claude has it, for a path such as %TEMP%\shot.png or $TMPDIR/shot.png.
            name => _services.UserEnvironment.Current is { } environment ? environment.GetValueOrDefault(name) : Environment.GetEnvironmentVariable(name)),
        () => _conversation.FilePaths);

    /// <summary>
    /// A click on a file a reply names. A file opens in the app the OS uses for it and a folder in Finder or Explorer;
    /// a program, script or shortcut shows in its folder instead, since a reply could be prompt-injected to point at one
    /// under an innocent name. A name that matches several files offers them in a menu at the click, and one that matches
    /// none says so.
    /// </summary>
    private async Task OpenFileReferenceAsync(ReplyFileReference reference, object? anchor)
    {
        IReadOnlyList<string> found;
        try
        {
            found = await ReplyFiles.FindAsync(reference, clicked: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            found = [];
        }
        if (found.Count == 0)
        {
            _conversation.AddNote($"Couldn't find {reference.Path}.", NoteKind.Warning);
            return;
        }
        var path = found[0];
        if (found.Count > 1)
        {
            if (await _services.Platform.ChooseAsync(anchor, [.. found.Select(ShownPath)]) is not { } chosen || chosen < 0 || chosen >= found.Count)
            {
                return;
            }
            path = found[chosen];
        }
        if (await OpenPathAsync(path, reference.Line, reference.Column, reference.EndLine) == OpenedPath.NotFound)
        {
            _conversation.AddNote($"Couldn't find {reference.Path}.", NoteKind.Warning);
        }
    }

    /// <summary>
    /// Opens a file the session named (DESIGN.md §18, "Project files", "Opening files in the editor"), as
    /// <see cref="ShellViewModel.OpenPathAsync"/> does, from this tab's conversation.
    /// </summary>
    internal Task<OpenedPath> OpenPathAsync(string path, int? line = null, int? column = null, int? endLine = null) =>
        _shell.OpenPathAsync(path, Folder, this, line, column, endLine);

    /// <summary>A tool card's <b>Open</b>: the file it worked on, at the first line an edit changed or where a read started.</summary>
    [RelayCommand]
    private async Task OpenToolFileAsync(ToolUseItem? tool)
    {
        if (tool?.FilePath is not { } file)
        {
            return;
        }
        var path = Path.IsPathRooted(file) ? file : Path.Combine(Folder, file);
        if (await OpenPathAsync(path, tool.FileLine) == OpenedPath.NotFound)
        {
            _conversation.AddNote($"Couldn't find {file}.", NoteKind.Warning);
        }
    }

    /// <summary>
    /// A Files tab's peek went to the change this tab made (DESIGN.md §18, "Project files", "Claude's changes"): its tool
    /// call, expanded, with the subagent groups around it open.
    /// </summary>
    internal void ShowToolUse(string toolUseId)
    {
        if (_conversation.FindToolUse(toolUseId) is not { } tool)
        {
            return;
        }
        for (var owner = Agents.OwnerOf(tool.ToolUseId); owner?.Item is { } group; owner = owner.Parent)
        {
            group.IsExpanded = true;
        }
        ScrollTo(tool);
    }

    /// <summary>A file in the menu of several: inside the tab's folder from there, else in full.</summary>
    private string ShownPath(string path) => ShellViewModel.ShownPath(path, Folder);
}

/// <summary>Where <see cref="ShellViewModel.OpenPathAsync"/> opened a path.</summary>
internal enum OpenedPath
{
    /// <summary>It isn't there.</summary>
    NotFound,

    /// <summary>In Claudette's window: the file in the project's Files tab, or the folder in its explorer.</summary>
    InClaudette,

    /// <summary>In the OS's app for it, or Finder, Explorer or the file manager.</summary>
    Elsewhere,
}
