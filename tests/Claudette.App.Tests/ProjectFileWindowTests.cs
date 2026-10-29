using System.Collections.Concurrent;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using LiveMarkdown.Avalonia;

namespace Claudette.App.Tests;

/// <summary>
/// A Project Files tab's file in a window of its own (DESIGN.md §18, "Project files", "A file in a window of its own"):
/// popping it out and putting it back, its tab meanwhile, opening it again from elsewhere, and closing it.
/// </summary>
public class ProjectFileWindowTests
{
    private static string Write(string path, string text = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private static readonly string TenLines = string.Concat(Enumerable.Range(1, 10).Select(i => $"line {i}\n"));

    /// <summary>The group's Files tab with <paramref name="names"/> open in it, and the windows the view is asked for.</summary>
    private static async Task<(TabViewModel Tab, ProjectFilesViewModel Files, ProjectFileViewModel[] Open, ConcurrentQueue<ProjectFileViewModel> Windows)> OpenAsync(
        TabTestHarness h, params string[] names)
    {
        var tab = await h.OpenTabAsync();
        var paths = names.Select(n => Write(Path.Combine(h.WorkFolder, n), TenLines)).ToArray();
        var windows = new ConcurrentQueue<ProjectFileViewModel>();
        h.Shell.ShowFileWindow = windows.Enqueue;
        var group = h.Shell.Groups.Single();
        InlineDispatcher.Run(() => h.Shell.OpenProjectFilesCommand.Execute(group));
        var files = group.Files!;
        await TabTestHarness.Eventually(() => h.FolderWatcher.Folders.Contains(files.Folder), "the explorer");
        var open = new List<ProjectFileViewModel>();
        foreach (var path in paths)
        {
            open.Add(await files.OpenFileAsync(path, preview: false));
        }
        return (tab, files, [.. open], windows);
    }

    private static LinkClickedEventArgs Click(string address) =>
        new(MarkdownTextBlock.LinkClickEvent, new object(), new Uri(address, UriKind.RelativeOrAbsolute));

    [Fact]
    public async Task Open_in_a_window_takes_the_file_out_of_the_tabs_editor_and_shows_the_next()
    {
        await using var h = new TabTestHarness();
        var (_, files, open, windows) = await OpenAsync(h, "a.txt", "b.txt", "c.txt");
        var (a, b, c) = (open[0], open[1], open[2]);
        InlineDispatcher.Run(() => files.ShowFileCommand.Execute(b));
        Assert.Equal("Open b.txt in a window", files.OpenInWindowTip);

        InlineDispatcher.Run(() => files.ToggleFileWindowCommand.Execute(b));

        Assert.True(b.IsInWindow);
        Assert.Null(b.InTab);
        Assert.Equal([b], windows);
        // Its tab stays in the row; the tab's editor shows the next file.
        Assert.Equal([a, b, c], files.OpenFiles);
        Assert.Same(c, files.Showing);
        Assert.False(b.IsShowing);
        Assert.Equal("Put back", b.WindowMenuText);
        Assert.Equal("Open in a window", a.WindowMenuText);

        // Ctrl+PageDown and Ctrl+PageUp pass it by.
        InlineDispatcher.Run(files.NextFile);
        Assert.Same(a, files.Showing);
        InlineDispatcher.Run(files.PreviousFile);
        Assert.Same(c, files.Showing);
    }

    [Fact]
    public async Task The_rows_button_pops_out_the_file_showing_and_it_keeps_a_preview()
    {
        await using var h = new TabTestHarness();
        var (_, files, open, windows) = await OpenAsync(h, "a.txt");
        var preview = await files.OpenFileAsync(Write(Path.Combine(h.WorkFolder, "b.txt")), preview: true);
        Assert.Same(preview, files.Showing);

        InlineDispatcher.Run(() => files.OpenShowingInWindowCommand.Execute(null));

        Assert.True(preview.IsInWindow);
        Assert.False(preview.IsPreview);
        Assert.Same(open[0], files.Showing);
        Assert.Equal([preview], windows);
    }

    [Fact]
    public async Task Its_tab_or_opening_it_again_brings_the_window_forward_at_the_line()
    {
        await using var h = new TabTestHarness();
        var (_, files, open, windows) = await OpenAsync(h, "a.txt", "b.txt");
        var (a, b) = (open[0], open[1]);
        InlineDispatcher.Run(() => files.PopOut(a));
        windows.Clear();

        InlineDispatcher.Run(() => files.ShowFileCommand.Execute(a));

        Assert.Equal([a], windows);
        Assert.Same(b, files.Showing);

        var again = await files.OpenFileAsync(a.Path, preview: false, line: 7);

        Assert.Same(a, again);
        Assert.Equal([a, a], windows);
        Assert.Equal(7, a.CaretLine);
        Assert.Same(b, files.Showing);
        Assert.True(a.IsInWindow);
    }

    [Fact]
    public async Task Put_back_shows_it_in_its_tab_with_its_edits()
    {
        await using var h = new TabTestHarness();
        var (_, files, open, _) = await OpenAsync(h, "a.txt", "b.txt");
        var (a, b) = (open[0], open[1]);
        ((PlainEditorDocument)a.Document!).Insert(0, "unsaved ");
        InlineDispatcher.Run(() => files.PopOut(a));
        Assert.Equal(1, files.UnsavedCount);

        InlineDispatcher.Run(() => a.PutBackCommand.Execute(null));

        Assert.False(a.IsInWindow);
        Assert.Same(a, a.InTab);
        Assert.Same(a, files.Showing);
        Assert.True(a.IsModified);
        Assert.StartsWith("unsaved ", a.Document!.Text, StringComparison.Ordinal);
        Assert.Equal([a, b], files.OpenFiles);
    }

    [Fact]
    public async Task With_every_file_in_a_window_the_editors_place_says_so()
    {
        await using var h = new TabTestHarness();
        var (_, files, open, _) = await OpenAsync(h, "a.txt", "b.txt");
        var (a, b) = (open[0], open[1]);

        InlineDispatcher.Run(() => files.PopOut(a));
        InlineDispatcher.Run(() => files.PopOut(b));

        Assert.Null(files.Showing);
        Assert.False(files.HasShowing);
        Assert.Equal("2 files are in windows of their own", files.NoFileText);
        // The row still names an open file.
        Assert.Equal("a.txt", files.RowDetail);

        InlineDispatcher.Run(() => files.PutBack(a));

        Assert.Equal("b.txt is in a window of its own", files.NoFileText);

        // Close tab with nothing showing but a file in a window: the Files tab stays open, holding it.
        await InlineDispatcher.RunAsync(() => files.CloseFileCommand.ExecuteAsync(a));
        await InlineDispatcher.RunAsync(files.CloseShowingOrTabAsync);

        Assert.Same(files, h.Shell.Groups.Single().Files);
        Assert.True(b.IsInWindow);
    }

    [Fact]
    public async Task Closing_its_file_closes_the_window_asking_first_about_unsaved_edits()
    {
        await using var h = new TabTestHarness();
        var (_, files, open, _) = await OpenAsync(h, "a.txt", "b.txt");
        var a = open[0];
        ((PlainEditorDocument)a.Document!).Insert(0, "unsaved ");
        InlineDispatcher.Run(() => files.PopOut(a));
        var closed = 0;
        a.Closed += () => Interlocked.Increment(ref closed);

        var closing = InlineDispatcher.RunAsync(() => files.CloseFileCommand.ExecuteAsync(a));
        await TabTestHarness.Eventually(() => files.IsAskingToClose, "the question");
        Assert.Equal(0, Volatile.Read(ref closed));
        InlineDispatcher.Run(() => files.CloseWithoutSavingCommand.Execute(null));
        await closing;

        Assert.Equal(1, Volatile.Read(ref closed));
        Assert.DoesNotContain(a, files.OpenFiles);
        Assert.Equal("No files open", files.NoFileText);
    }

    [Fact]
    public async Task Closing_the_Files_tab_closes_its_windows()
    {
        await using var h = new TabTestHarness();
        var (_, files, open, _) = await OpenAsync(h, "a.txt");
        var a = open[0];
        InlineDispatcher.Run(() => files.PopOut(a));
        var closed = 0;
        a.Closed += () => Interlocked.Increment(ref closed);

        await InlineDispatcher.RunAsync(() => files.CloseCommand.ExecuteAsync(null));

        Assert.Null(h.Shell.Groups.Single().Files);
        Assert.Equal(1, Volatile.Read(ref closed));
    }

    [Fact]
    public async Task A_file_in_a_window_comes_back_in_its_tab_after_a_restart()
    {
        await using var h = new TabTestHarness();
        var (_, files, open, windows) = await OpenAsync(h, "a.txt", "b.txt");
        InlineDispatcher.Run(() => files.PopOut(open[0]));
        windows.Clear();

        var snapshot = h.Shell.CaptureForRestart();
        await h.Shell.CloseTabsForRestartAsync();
        InlineDispatcher.Run(() => h.Shell.Restore(null, snapshot));

        var back = h.Shell.Groups.Single().Files!;
        Assert.NotSame(files, back);
        Assert.Equal(["a.txt", "b.txt"], back.OpenFiles.Select(f => f.Name));
        Assert.All(back.OpenFiles, f => Assert.False(f.IsInWindow));
        Assert.Empty(windows);
    }

    [Fact]
    public async Task A_link_to_a_file_in_a_window_goes_to_the_window_and_not_beside_the_conversation()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var windows = new ConcurrentQueue<ProjectFileViewModel>();
        h.Shell.ShowFileWindow = windows.Enqueue;
        Write(Path.Combine(h.WorkFolder, "a.txt"), TenLines);
        await InlineDispatcher.RunAsync(() => tab.OpenLinkCommand.ExecuteAsync(Click("a.txt")));
        var files = h.Shell.Groups.Single().Files!;
        await TabTestHarness.Eventually(() => files.Rows.Count > 0, "the explorer");
        Assert.Equal([tab, (IShellTab)files], h.Shell.Panes);
        var a = Assert.Single(files.OpenFiles);
        await TabTestHarness.Eventually(() => a.IsLoaded, "the file read");

        // Its last file popped out of a Files tab a conversation opened beside it: the pane closes, the tab stays.
        InlineDispatcher.Run(() => files.PopOut(a));

        Assert.Equal([(IShellTab)tab], h.Shell.Panes);
        Assert.Same(files, h.Shell.Groups.Single().Files);
        Assert.Same(tab, h.Shell.Selected);

        await InlineDispatcher.RunAsync(() => tab.OpenLinkCommand.ExecuteAsync(Click("a.txt#L4")));

        Assert.Equal([a, a], windows);
        Assert.Equal(4, a.CaretLine);
        Assert.Equal([(IShellTab)tab], h.Shell.Panes);
        Assert.Same(tab, h.Shell.Selected);
    }
}
