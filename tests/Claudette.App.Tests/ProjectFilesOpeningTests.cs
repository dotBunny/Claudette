using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.Core.Settings;
using LiveMarkdown.Avalonia;

namespace Claudette.App.Tests;

/// <summary>
/// Opening files in Claudette's editor (DESIGN.md §18, "Project files", "Opening files in the editor"): a file a reply
/// names opens in its project's Files tab, beside the conversation or in its place, at the line it names; a folder
/// shows in the explorer; what isn't text, or Settings → Editor, still sends it to the OS's app.
/// </summary>
public class ProjectFilesOpeningTests
{
    private static LinkClickedEventArgs Click(string address) =>
        new(MarkdownTextBlock.LinkClickEvent, new object(), new Uri(address, UriKind.RelativeOrAbsolute));

    /// <summary>
    /// A click on a link in a reply, on the UI thread as in the app. A Files tab it opens lists its folder there too, in one
    /// step once the folder is read: that's waited for, so what the test does next from its own thread can't land in it.
    /// </summary>
    private static async Task OpenLinkAsync(TabTestHarness h, TabViewModel tab, string address)
    {
        await InlineDispatcher.RunAsync(() => tab.OpenLinkCommand.ExecuteAsync(Click(address)));
        if (h.Shell.Groups.Single().Files is { } files)
        {
            await TabTestHarness.Eventually(() => files.Rows.Count > 0, "the explorer");
        }
    }

    private static string Write(string path, string text = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private static readonly string FiftyLines = string.Concat(Enumerable.Range(1, 50).Select(i => $"line {i}\n"));

    [Fact]
    public async Task A_link_to_a_file_opens_it_in_the_Files_tab_beside_the_conversation_at_its_line()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var spec = Write(Path.Combine(h.WorkFolder, "docs", "weapons.md"), FiftyLines);

        await OpenLinkAsync(h, tab, "docs/weapons.md#L12-L14");

        var files = h.Shell.Groups.Single().Files!;
        Assert.Equal([tab, (IShellTab)files], h.Shell.Panes);
        Assert.Same(files, h.Shell.Selected);
        var file = Assert.Single(files.OpenFiles);
        Assert.Equal(spec, file.Path);
        Assert.False(file.IsPreview);
        Assert.Equal((12, 1), (file.CaretLine, file.CaretColumn));
        Assert.Empty(h.Platform.OpenedFiles);
    }

    [Fact]
    public async Task The_first_file_opened_beside_the_conversation_has_the_pane_with_the_explorer_hidden_in_that_tab_alone()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "a.txt"));
        Write(Path.Combine(h.WorkFolder, "b.txt"));

        await OpenLinkAsync(h, tab, "a.txt");

        var files = h.Shell.Groups.Single().Files!;
        Assert.False(files.IsExplorerShown);
        Assert.Equal("Show the explorer", files.ToggleExplorerText);
        Assert.False(h.Shell.Layout.IsExplorerHidden);
        Assert.True(files.CaptureState().ExplorerHidden);

        files.ToggleExplorerCommand.Execute(null);

        Assert.True(files.IsExplorerShown);
        Assert.False(files.CaptureState().ExplorerHidden);

        // Not its first file: the explorer stays as it is.
        h.Shell.SelectedTab = tab;
        files.ClosePaneCommand.Execute(null);
        await OpenLinkAsync(h, tab, "b.txt");

        Assert.Equal([tab, (IShellTab)files], h.Shell.Panes);
        Assert.True(files.IsExplorerShown);
    }

    [Fact]
    public async Task The_groups_button_shows_the_Files_tab_with_its_explorer()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "a.txt"));
        await OpenLinkAsync(h, tab, "a.txt");
        var group = h.Shell.Groups.Single();
        var files = group.Files!;
        Assert.False(files.IsExplorerShown);

        h.Shell.OpenProjectFilesCommand.Execute(group);

        Assert.True(files.IsExplorerShown);
        Assert.False(files.CaptureState().ExplorerHidden);
        Assert.Same(files, h.Shell.Selected);

        // Hidden in every Files tab, too.
        files.ToggleExplorerCommand.Execute(null);
        h.Shell.SelectedTab = tab;
        h.Shell.OpenProjectFilesCommand.Execute(group);

        Assert.True(files.IsExplorerShown);
        Assert.False(h.Shell.Layout.IsExplorerHidden);
    }

    [Fact]
    public async Task An_explorer_hidden_for_the_first_file_comes_back_to_find_a_file_or_reveal_one()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "a.txt"));
        await OpenLinkAsync(h, tab, "a.txt");
        var files = h.Shell.Groups.Single().Files!;

        files.StartFindCommand.Execute(null);

        Assert.True(files.IsExplorerShown);

        files.StopFindCommand.Execute(null);
        files.HideExplorerHere();
        await files.RevealRowInTreeCommand.ExecuteAsync(files.Showing);

        Assert.True(files.IsExplorerShown);
        Assert.Equal("a.txt", files.SelectedRow?.RelativePath);
    }

    [Fact]
    public async Task Closing_the_last_file_opened_beside_the_conversation_closes_the_Files_tab_and_gives_the_conversation_its_width()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "a.txt"));
        Write(Path.Combine(h.WorkFolder, "b.txt"));
        await OpenLinkAsync(h, tab, "a.txt");
        await OpenLinkAsync(h, tab, "b.txt");
        var group = h.Shell.Groups.Single();
        var files = group.Files!;

        await files.CloseFileCommand.ExecuteAsync(files.Showing);

        // A file still open: the Files tab stays beside.
        Assert.Equal([tab, (IShellTab)files], h.Shell.Panes);

        await files.CloseFileCommand.ExecuteAsync(files.Showing);

        Assert.Null(group.Files);
        Assert.Equal([(IShellTab)tab], h.Shell.Panes);
        Assert.Same(tab, h.Shell.Selected);
    }

    [Fact]
    public async Task A_Files_tab_with_its_explorer_showing_or_on_its_own_stays_once_its_last_file_closes()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "a.txt"));
        var b = Write(Path.Combine(h.WorkFolder, "b.txt"));
        await OpenLinkAsync(h, tab, "a.txt");
        var group = h.Shell.Groups.Single();
        var files = group.Files!;
        files.ToggleExplorerCommand.Execute(null);

        await files.CloseFileCommand.ExecuteAsync(files.Showing);

        Assert.Same(files, group.Files);
        Assert.Equal([tab, (IShellTab)files], h.Shell.Panes);

        // On its own, with the explorer hidden.
        await files.OpenFileAsync(b, preview: false);
        files.ToggleExplorerCommand.Execute(null);
        h.Shell.ClosePaneCommand.Execute(tab);
        Assert.False(files.IsExplorerShown);
        Assert.Equal([(IShellTab)files], h.Shell.Panes);

        await files.CloseFileCommand.ExecuteAsync(files.Showing);

        Assert.Same(files, group.Files);
        Assert.Empty(files.OpenFiles);
    }

    [Fact]
    public async Task In_the_conversations_place_the_first_file_keeps_the_explorer()
    {
        await using var h = new TabTestHarness(s => s.Editor.FilesFromConversation = FilesFromConversation.InPlace);
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "a.txt"));

        await OpenLinkAsync(h, tab, "a.txt");

        Assert.True(h.Shell.Groups.Single().Files!.IsExplorerShown);
    }

    [Fact]
    public async Task With_Settings_so_the_Files_tab_takes_the_conversations_place()
    {
        await using var h = new TabTestHarness(s => s.Editor.FilesFromConversation = FilesFromConversation.InPlace);
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "Player.cpp"), FiftyLines);

        await OpenLinkAsync(h, tab, "Player.cpp:42:7");

        var files = h.Shell.Groups.Single().Files!;
        Assert.Equal([(IShellTab)files], h.Shell.Panes);
        Assert.Same(files, h.Shell.Selected);
        Assert.False(tab.IsShown);
        Assert.Equal((42, 7), (files.Showing!.CaretLine, files.Showing.CaretColumn));
    }

    [Fact]
    public async Task A_Files_tab_already_on_screen_only_takes_the_focus()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var a = Write(Path.Combine(h.WorkFolder, "a.txt"));
        var b = Write(Path.Combine(h.WorkFolder, "b.txt"));
        await OpenLinkAsync(h, tab, "a.txt");
        h.Shell.SelectedTab = tab;

        await OpenLinkAsync(h, tab, "b.txt");

        var files = h.Shell.Groups.Single().Files!;
        Assert.Equal([tab, (IShellTab)files], h.Shell.Panes);
        Assert.Equal([a, b], files.OpenFiles.Select(f => f.Path));
        Assert.Equal(b, files.Showing?.Path);
    }

    [Fact]
    public async Task A_file_thats_not_text_still_opens_in_the_oss_app_and_a_program_in_its_folder()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var image = Path.Combine(h.WorkFolder, "icon.png");
        await File.WriteAllBytesAsync(image, [0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0x0D], TestContext.Current.CancellationToken);
        Write(Path.Combine(h.WorkFolder, "bin", "game.exe"));

        await OpenLinkAsync(h, tab, "icon.png");
        await OpenLinkAsync(h, tab, "bin/game.exe");

        Assert.Equal([image], h.Platform.OpenedFiles);
        Assert.Equal([Path.Combine(h.WorkFolder, "bin")], h.Platform.RevealedFolders);
        Assert.Null(h.Shell.Groups.Single().Files);
    }

    [Fact]
    public async Task A_script_opens_as_text_rather_than_running()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var script = Write(Path.Combine(h.WorkFolder, "tools", "build.bat"), "@echo off\r\n");

        await OpenLinkAsync(h, tab, "tools/build.bat");

        Assert.Equal(script, h.Shell.Groups.Single().Files!.Showing?.Path);
        Assert.Empty(h.Platform.OpenedFiles);
        Assert.Empty(h.Platform.RevealedFolders);
    }

    [Fact]
    public async Task A_folder_in_the_project_shows_in_the_explorer_opened_and_selected()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "Source", "Player", "Player.cpp"));

        await OpenLinkAsync(h, tab, "Source/Player/");

        var files = h.Shell.Groups.Single().Files!;
        Assert.Equal("Source/Player", files.SelectedRow?.RelativePath);
        Assert.True(files.SelectedRow!.IsExpanded);
        Assert.Contains(files.Rows, r => r.Name == "Player.cpp");
        Assert.Empty(h.Platform.RevealedFolders);
    }

    [Fact]
    public async Task With_Settings_so_files_open_in_the_oss_app_as_before()
    {
        await using var h = new TabTestHarness(s => s.Editor.OpenFilesIn = OpenFilesIn.System);
        var tab = await h.OpenTabAsync();
        var spec = Write(Path.Combine(h.WorkFolder, "docs", "weapons.md"));

        await OpenLinkAsync(h, tab, "docs/weapons.md");
        await OpenLinkAsync(h, tab, "docs/");

        Assert.Equal([spec], h.Platform.OpenedFiles);
        Assert.Equal([Path.Combine(h.WorkFolder, "docs")], h.Platform.RevealedFolders);
        Assert.Null(h.Shell.Groups.Single().Files);
    }

    [Fact]
    public async Task Go_to_file_opens_the_projects_Files_tab_with_its_box_up()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();

        InlineDispatcher.Run(() => h.Shell.GoToFileCommand.Execute(null));

        var files = h.Shell.Groups.Single().Files!;
        Assert.True(files.IsFinding);
        Assert.Equal([tab, (IShellTab)files], h.Shell.Panes);
        Assert.Same(files, h.Shell.Selected);
    }

    [Fact]
    public async Task A_Files_tab_goes_into_a_pane_and_out_again_keeping_its_files()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"));
        InlineDispatcher.Run(() => h.Shell.OpenProjectFilesCommand.Execute(h.Shell.Groups.Single()));
        var files = h.Shell.Groups.Single().Files!;
        await InlineDispatcher.RunAsync(() => files.OpenFileAsync(path, preview: false));
        h.Shell.SelectedTab = tab;

        h.Shell.OpenBesideCommand.Execute(files);

        Assert.Equal([tab, (IShellTab)files], h.Shell.Panes);
        Assert.True(files is { IsShown: true, IsSelected: true, IsInSplit: true, PaneIndex: 1 });

        files.ClosePaneCommand.Execute(null);

        Assert.Equal([tab], h.Shell.Panes);
        Assert.False(files.IsShown);
        Assert.Same(files, h.Shell.Groups.Single().Files);
        Assert.Equal(path, Assert.Single(files.OpenFiles).Path);
    }
}
