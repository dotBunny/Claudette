using System.Text;
using System.Text.Json.Nodes;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.Core.Files;
using Claudette.Core.ProjectFiles;
using Claudette.Core.Settings;

namespace Claudette.App.Tests;

/// <summary>
/// A project's Project Files tab (DESIGN.md §18, "Project files"): its place in the group, the explorer, open files and
/// their tabs, saving and closing, files Claude changes, finding a file by name, and what comes back after a restart.
/// </summary>
public class ProjectFilesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>The group's Files tab, opened from its button and on screen.</summary>
    private static async Task<(TabViewModel Tab, ProjectFilesViewModel Files)> OpenFilesAsync(TabTestHarness h)
    {
        var tab = await h.OpenTabAsync();
        var group = h.Shell.Groups.Single();
        // Opened on the UI thread, as its button does, the explorer lists the folder there too, adding the rows and then
        // watching the folder, which a change on disk needs: the watch says the listing is done.
        InlineDispatcher.Run(() => h.Shell.OpenProjectFilesCommand.Execute(group));
        var files = group.Files!;
        await TabTestHarness.Eventually(() => h.FolderWatcher.Folders.Contains(files.Folder), "the explorer");
        return (tab, files);
    }

    private static PlainEditorDocument Text(ProjectFileViewModel file) => (PlainEditorDocument)file.Document!;

    /// <summary>A successful Edit of <paramref name="path"/> by the tab's session; the file on disk is the test's to write.</summary>
    private static void EmitEdit(TabTestHarness h, string id, string path, string before)
    {
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject
            {
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = "tool_use", ["id"] = id, ["name"] = "Edit",
                    ["input"] = new JsonObject { ["file_path"] = path, ["old_string"] = "a", ["new_string"] = "b" },
                }),
            },
        });
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = "The file has been updated." }) },
            ["tool_use_result"] = new JsonObject { ["filePath"] = path, ["oldString"] = "a", ["newString"] = "b", ["originalFile"] = before },
        });
    }

    // ---- In the sidebar ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_groups_button_opens_one_Files_tab_and_selects_it()
    {
        await using var h = new TabTestHarness();
        var (tab, files) = await OpenFilesAsync(h);
        var group = h.Shell.Groups.Single();

        Assert.Same(files, h.Shell.Selected);
        Assert.Same(files, h.Shell.SelectedFiles);
        Assert.Null(h.Shell.SelectedTab);
        // The project's settings and menu still have a session to go by.
        Assert.Same(tab, h.Shell.ProjectTab);
        Assert.True(files is { IsShown: true, IsSelected: true });
        Assert.Contains(files, h.Shell.OpenTabs);

        h.Shell.SelectedTab = tab;
        h.Shell.OpenProjectFilesCommand.Execute(group);

        Assert.Same(files, group.Files);
        Assert.Same(files, h.Shell.Selected);
        Assert.Single(h.Shell.OpenTabs, t => t is ProjectFilesViewModel);
    }

    [Fact]
    public async Task Moving_between_tabs_with_the_keyboard_skips_the_Files_tab()
    {
        await using var h = new TabTestHarness();
        var first = await h.OpenTabAsync();
        var second = await h.OpenTabAsync();
        InlineDispatcher.Run(() => h.Shell.OpenProjectFilesCommand.Execute(h.Shell.Groups.Single()));

        h.Shell.SelectNextCommand.Execute(null);
        Assert.Same(first, h.Shell.Selected);
        h.Shell.SelectNextCommand.Execute(null);
        Assert.Same(second, h.Shell.Selected);
        h.Shell.SelectNextCommand.Execute(null);
        Assert.Same(first, h.Shell.Selected);
    }

    [Fact]
    public async Task The_group_stays_while_its_Files_tab_is_open_and_goes_with_it()
    {
        await using var h = new TabTestHarness();
        var (tab, files) = await OpenFilesAsync(h);
        var group = h.Shell.Groups.Single();

        await h.Shell.CloseTabCommand.ExecuteAsync(tab);

        Assert.Same(group, Assert.Single(h.Shell.Groups));
        Assert.Same(files, group.Files);
        Assert.Same(files, h.Shell.Selected);

        Assert.True(await h.Shell.CloseProjectFilesAsync(files));

        Assert.Empty(h.Shell.Groups);
        Assert.Empty(h.Shell.OpenTabs);
        Assert.Null(h.Shell.Selected);
    }

    [Fact]
    public async Task Ctrl_W_closes_the_file_showing_and_then_the_tab()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\n");
        var (tab, files) = await OpenFilesAsync(h);
        await files.OpenFileAsync(path, preview: false);

        await h.Shell.CloseSelectedTabCommand.ExecuteAsync(null);

        Assert.Empty(files.OpenFiles);
        Assert.Same(files, h.Shell.Selected);

        await h.Shell.CloseSelectedTabCommand.ExecuteAsync(null);

        Assert.Null(h.Shell.Groups.Single().Files);
        Assert.Same(tab, h.Shell.Selected);
    }

    // ---- The explorer ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_explorer_lists_folders_first_and_shows_a_folder_when_its_opened()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "zeta.txt"), "");
        Write(Path.Combine(h.WorkFolder, "Source", "Player.cpp"), "");
        Write(Path.Combine(h.WorkFolder, ".git", "HEAD"), "ref: refs/heads/main\n");
        var (_, files) = await OpenFilesAsync(h);
        await TabTestHarness.Eventually(() => files.Rows.Any(r => r.Name == "zeta.txt"), "the files");

        Assert.Equal(["Source", "zeta.txt"], files.Rows.Select(r => r.Name).Where(n => n is "Source" or "zeta.txt" or ".git"));
        var source = files.Rows.Single(r => r.Name == "Source");

        await files.ToggleRowCommand.ExecuteAsync(source);

        var player = files.Rows[files.Rows.IndexOf(source) + 1];
        Assert.Equal(("Player.cpp", 1), (player.Name, player.Depth));
        Assert.Contains(source.FullPath, h.FolderWatcher.Folders);

        await files.ToggleRowCommand.ExecuteAsync(source);

        Assert.DoesNotContain(files.Rows, r => r.Name == "Player.cpp");
        Assert.DoesNotContain(source.FullPath, h.FolderWatcher.Folders);
    }

    [Fact]
    public async Task A_file_added_on_disk_shows_in_the_explorer_once_things_settle()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "a.txt"), "");
        var (_, files) = await OpenFilesAsync(h);

        var added = Write(Path.Combine(h.WorkFolder, "b.txt"), "");
        h.FolderWatcher.Changed(added);
        h.Time.Advance(ProjectFilesViewModel.RefreshDelay);

        await TabTestHarness.Eventually(() => files.Rows.Any(r => r.Name == "b.txt"), "the new file");
    }

    // ---- New files and folders ---------------------------------------------------------------------------------------

    [Fact]
    public async Task New_file_on_a_folders_menu_opens_the_folder_and_makes_the_file_in_it_opened_to_stay()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "Source", "Player.cpp"), "");
        var (_, files) = await OpenFilesAsync(h);
        var source = files.Rows.Single(r => r.Name == "Source");

        await files.NewFileCommand.ExecuteAsync(source);

        // The box takes the top of the folder, which opens.
        var row = Assert.IsType<NewEntryRow>(files.Rows[files.Rows.IndexOf(source) + 1]);
        Assert.Same(row, files.NamingRow);
        Assert.True(source.IsExpanded);
        Assert.Equal((1, "File name"), (row.Depth, row.Placeholder));

        row.NameText = "Weapon.cpp";
        await files.CommitNamingCommand.ExecuteAsync(null);

        var path = Path.Combine(h.WorkFolder, "Source", "Weapon.cpp");
        Assert.Equal("", File.ReadAllText(path));
        Assert.Null(files.NamingRow);
        Assert.DoesNotContain(files.Rows, r => r is NewEntryRow);
        Assert.Equal("Source/Weapon.cpp", files.SelectedRow?.RelativePath);
        Assert.Equal(path, files.Showing?.Path);
        Assert.False(files.Showing!.IsPreview);
    }

    [Fact]
    public async Task New_folder_goes_beside_a_file_or_at_the_top_and_makes_the_folders_typed()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "Source", "Player.cpp"), "");
        var (_, files) = await OpenFilesAsync(h);
        var source = files.Rows.Single(r => r.Name == "Source");
        await files.ToggleRowCommand.ExecuteAsync(source);

        // A file's menu: in the folder it's in.
        await files.NewFolderCommand.ExecuteAsync(files.Rows.Single(r => r.Name == "Player.cpp"));

        Assert.Equal(Path.Combine(h.WorkFolder, "Source"), files.NamingRow!.InFolder);
        Assert.Equal(files.Rows.IndexOf(source) + 1, files.Rows.IndexOf(files.NamingRow));

        // The header's, with nothing selected: the top of the project, in place of the other.
        files.SelectedRow = null;
        await files.NewFolderCommand.ExecuteAsync(files.SelectedRow);

        var row = files.NamingRow!;
        Assert.Equal((0, 0, "Folder name"), (files.Rows.IndexOf(row), row.Depth, row.Placeholder));
        Assert.Single(files.Rows, r => r is NewEntryRow);

        row.NameText = "Art/Icons";
        await files.CommitNamingCommand.ExecuteAsync(null);

        Assert.True(Directory.Exists(Path.Combine(h.WorkFolder, "Art", "Icons")));
        Assert.Equal("Art/Icons", files.SelectedRow?.RelativePath);
        Assert.True(files.Rows.Single(r => r.Name == "Art").IsExpanded);
        Assert.Empty(files.OpenFiles);
    }

    [Fact]
    public async Task A_name_thats_taken_is_refused_in_the_box_and_Esc_makes_nothing()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "a.txt"), "");
        var (_, files) = await OpenFilesAsync(h);
        files.ShowOnlyChanged = true;

        await files.NewFileCommand.ExecuteAsync(null);
        var row = files.NamingRow!;

        Assert.True(files.ShowsTree);
        row.NameText = "a.txt";
        Assert.Equal("There's already a file called a.txt here.", row.Problem);

        await files.CommitNamingCommand.ExecuteAsync(null);

        Assert.Same(row, files.NamingRow);
        row.NameText = "b.txt";
        Assert.Null(row.Problem);

        // What changes on disk meanwhile waits for the box to go, rather than drawing the tree again from under it.
        var added = Write(Path.Combine(h.WorkFolder, "c.txt"), "");
        h.FolderWatcher.Changed(added);
        h.Time.Advance(ProjectFilesViewModel.RefreshDelay);

        Assert.Contains(row, files.Rows);
        Assert.DoesNotContain(files.Rows, r => r.Name == "c.txt");

        // Esc on the UI thread, as in the app: the reading it starts finishes there, so the wait can't see it halfway.
        InlineDispatcher.Run(() => files.CancelNamingCommand.Execute(null));

        Assert.Null(files.NamingRow);
        await TabTestHarness.Eventually(() => files.Rows.Any(r => r.Name == "c.txt"), "the file added meanwhile");
        Assert.DoesNotContain(row, files.Rows);
        Assert.False(File.Exists(Path.Combine(h.WorkFolder, "b.txt")));
    }

    // ---- Renaming and deleting ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Rename_puts_a_box_in_the_rows_place_and_the_open_file_follows_with_its_edits()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "Source", "Brain.cpp"), "think\n");
        var (_, files) = await OpenFilesAsync(h);
        await files.ToggleRowCommand.ExecuteAsync(files.Rows.Single(r => r.Name == "Source"));
        var file = await files.OpenFileAsync(path, preview: false);
        Text(file).Insert(0, "x");
        file.FlushBackup();
        var row = files.Rows.Single(r => r.Name == "Brain.cpp");
        var index = files.Rows.IndexOf(row);

        files.RenameRowCommand.Execute(row);

        // The box takes the row's place, with its name, up to the extension selected.
        var box = Assert.IsType<RenameRow>(files.Rows[index]);
        Assert.Same(box, files.NamingRow);
        Assert.Equal(("Brain.cpp", 5, "File name", 1), (box.NameText, box.SelectedLength, box.Placeholder, box.Depth));

        box.NameText = "Mind.h";
        var told = new List<string?>();
        files.PropertyChanged += (_, e) => told.Add(e.PropertyName);
        await InlineDispatcher.RunAsync(() => files.CommitNamingCommand.ExecuteAsync(null));

        var moved = Path.Combine(h.WorkFolder, "Source", "Mind.h");
        Assert.Equal("think\n", await File.ReadAllTextAsync(moved, Token));
        Assert.False(File.Exists(path));
        Assert.Null(files.NamingRow);
        Assert.DoesNotContain(files.Rows, r => r is NameRow || r.Name == "Brain.cpp");
        Assert.Equal("Source/Mind.h", files.SelectedRow?.RelativePath);
        // The open file follows, its edits and all, kept under its new name.
        Assert.Same(file, Assert.Single(files.OpenFiles));
        Assert.Equal((moved, "Mind.h", "Source/Mind.h"), (file.Path, file.TabLabel, file.RelativePath));
        // The sidebar's row, which names the file showing, hears of it.
        Assert.Contains(nameof(ProjectFilesViewModel.RowDetail), told);
        Assert.True(file.IsModified);
        Assert.Equal("xthink\n", file.Document!.Text);
        await TabTestHarness.Eventually(() => h.Services.EditorBackups.Load(moved) is not null && h.Services.EditorBackups.Load(path) is null,
            "its edits kept under the new name");
    }

    [Fact]
    public async Task Rename_refuses_a_name_thats_taken_and_Esc_puts_the_row_back()
    {
        await using var h = new TabTestHarness();
        var a = Write(Path.Combine(h.WorkFolder, "a.txt"), "");
        Write(Path.Combine(h.WorkFolder, "b.txt"), "");
        var (_, files) = await OpenFilesAsync(h);
        var row = files.Rows.Single(r => r.Name == "a.txt");
        var index = files.Rows.IndexOf(row);

        files.RenameRowCommand.Execute(row);
        var box = (RenameRow)files.NamingRow!;
        box.NameText = "b.txt";

        Assert.Equal("There's already a file called b.txt here.", box.Problem);
        await InlineDispatcher.RunAsync(() => files.CommitNamingCommand.ExecuteAsync(null));
        Assert.Same(box, files.NamingRow);

        InlineDispatcher.Run(() => files.CancelNamingCommand.Execute(null));

        Assert.Null(files.NamingRow);
        Assert.Same(row, files.Rows[index]);
        Assert.Same(row, files.SelectedRow);
        Assert.True(File.Exists(a));
    }

    [Fact]
    public async Task A_renamed_folder_stays_expanded_with_the_folders_in_it_and_their_open_files_follow()
    {
        await using var h = new TabTestHarness();
        var eye = Write(Path.Combine(h.WorkFolder, "AI", "Parts", "Eye.cpp"), "see\n");
        var (_, files) = await OpenFilesAsync(h);
        var ai = files.Rows.Single(r => r.Name == "AI");
        await files.ToggleRowCommand.ExecuteAsync(ai);
        await files.ToggleRowCommand.ExecuteAsync(files.Rows.Single(r => r.Name == "Parts"));
        var file = await files.OpenFileAsync(eye, preview: false);

        files.RenameRowCommand.Execute(ai);
        files.NamingRow!.NameText = "Brain";
        await InlineDispatcher.RunAsync(() => files.CommitNamingCommand.ExecuteAsync(null));

        var moved = Path.Combine(h.WorkFolder, "Brain", "Parts", "Eye.cpp");
        Assert.Equal(moved, file.Path);
        Assert.Equal(["Brain", "Parts", "Eye.cpp"], files.Rows.Select(r => r.Name).Where(n => n is "Brain" or "Parts" or "Eye.cpp" or "AI"));
        Assert.True(files.Rows.Single(r => r.Name == "Brain").IsExpanded);
        Assert.True(files.Rows.Single(r => r.Name == "Parts").IsExpanded);
        Assert.Equal("Brain", files.SelectedRow?.Name);
        Assert.Contains(Path.Combine(h.WorkFolder, "Brain", "Parts"), h.FolderWatcher.Folders);
        Assert.DoesNotContain(Path.Combine(h.WorkFolder, "AI", "Parts"), h.FolderWatcher.Folders);
    }

    [Fact]
    public async Task Delete_asks_first_then_sends_it_to_the_trash_closes_it_and_selects_the_next()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "a.txt"), "");
        var b = Write(Path.Combine(h.WorkFolder, "b.txt"), "b\n");
        Write(Path.Combine(h.WorkFolder, "c.txt"), "");
        var (_, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(b, preview: false);
        Text(file).Insert(0, "x");
        file.FlushBackup();

        await InlineDispatcher.RunAsync(() => files.DeleteRowCommand.ExecuteAsync(files.Rows.Single(r => r.Name == "b.txt")));

        Assert.True(files.IsAskingToDelete);
        Assert.Equal("Delete b.txt? It goes to the Recycle Bin. b.txt has unsaved edits, which go with it.", files.Deleting!.Text);
        Assert.Equal("Delete", files.Deleting.ConfirmText);
        Assert.True(File.Exists(b));

        await InlineDispatcher.RunAsync(() => files.ConfirmDeleteCommand.ExecuteAsync(null));

        Assert.False(files.IsAskingToDelete);
        Assert.Equal([b], h.Trash.Taken);
        Assert.False(File.Exists(b));
        Assert.Empty(files.OpenFiles);
        Assert.DoesNotContain(files.Rows, r => r.Name == "b.txt");
        Assert.Equal("c.txt", files.SelectedRow?.Name);
        await TabTestHarness.Eventually(() => h.Services.EditorBackups.Load(b) is null, "its unsaved copy to go");
    }

    [Fact]
    public async Task Deleting_a_folder_says_how_many_files_go_with_it_and_Cancel_leaves_it()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "AI", "Brain.cpp"), "");
        Write(Path.Combine(h.WorkFolder, "AI", "Parts", "Eye.cpp"), "");
        var (_, files) = await OpenFilesAsync(h);
        var ai = files.Rows.Single(r => r.Name == "AI");

        await InlineDispatcher.RunAsync(() => files.DeleteRowCommand.ExecuteAsync(ai));

        Assert.Equal("Delete the folder AI and the 2 files in it? It goes to the Recycle Bin.", files.Deleting!.Text);

        InlineDispatcher.Run(() => files.CancelDeleteCommand.Execute(null));

        Assert.False(files.IsAskingToDelete);
        Assert.Empty(h.Trash.Taken);
        Assert.True(Directory.Exists(Path.Combine(h.WorkFolder, "AI")));
        Assert.Contains(ai, files.Rows);
    }

    [Fact]
    public async Task Where_theres_no_trash_it_asks_again_to_delete_it_for_good()
    {
        await using var h = new TabTestHarness();
        var a = Write(Path.Combine(h.WorkFolder, "a.txt"), "");
        var (_, files) = await OpenFilesAsync(h);
        h.Trash.Next = new TrashResult(TrashOutcome.NoTrash, "Its drive has no Recycle Bin");

        await InlineDispatcher.RunAsync(() => files.DeleteRowCommand.ExecuteAsync(files.Rows.Single(r => r.Name == "a.txt")));
        await InlineDispatcher.RunAsync(() => files.ConfirmDeleteCommand.ExecuteAsync(null));

        Assert.True(files.Deleting!.IsPermanent);
        Assert.Equal("a.txt can't go to the Recycle Bin. Its drive has no Recycle Bin. Delete it permanently? It can't be put back.", files.Deleting.Text);
        Assert.Equal("Delete permanently", files.Deleting.ConfirmText);
        Assert.True(File.Exists(a));

        await InlineDispatcher.RunAsync(() => files.ConfirmDeleteCommand.ExecuteAsync(null));

        Assert.False(files.IsAskingToDelete);
        Assert.False(File.Exists(a));
        Assert.Empty(h.Trash.Taken);
    }

    [Fact]
    public async Task When_the_trash_fails_the_question_stays_and_says_why()
    {
        await using var h = new TabTestHarness();
        var a = Write(Path.Combine(h.WorkFolder, "a.txt"), "");
        var (_, files) = await OpenFilesAsync(h);
        h.Trash.Next = new TrashResult(TrashOutcome.Failed, "It's open in another program");

        await InlineDispatcher.RunAsync(() => files.DeleteRowCommand.ExecuteAsync(files.Rows.Single(r => r.Name == "a.txt")));
        await InlineDispatcher.RunAsync(() => files.ConfirmDeleteCommand.ExecuteAsync(null));

        Assert.True(files.IsAskingToDelete);
        Assert.Equal("Claudette couldn't move it to the Recycle Bin. It's open in another program.", files.Deleting!.Problem);
        Assert.True(files.Deleting.CanConfirm);
        Assert.True(File.Exists(a));
    }

    // ---- Open files --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_click_opens_a_preview_which_the_next_one_replaces_until_its_kept()
    {
        await using var h = new TabTestHarness();
        foreach (var name in new[] { "a.txt", "b.txt", "c.txt" })
        {
            Write(Path.Combine(h.WorkFolder, name), $"{name}\n");
        }
        var (_, files) = await OpenFilesAsync(h);
        await TabTestHarness.Eventually(() => files.Rows.Any(r => r.Name == "c.txt"), "the files");
        ExplorerRow Row(string name) => files.Rows.Single(r => r.Name == name);

        await files.OpenRowCommand.ExecuteAsync(Row("a.txt"));
        await files.OpenRowCommand.ExecuteAsync(Row("b.txt"));

        var b = Assert.Single(files.OpenFiles);
        Assert.True(b is { Name: "b.txt", IsPreview: true, IsLoaded: true });

        // Typing in it keeps it.
        Text(b).Insert(0, "x");
        Assert.False(b.IsPreview);
        await files.OpenRowCommand.ExecuteAsync(Row("c.txt"));

        Assert.Equal(["b.txt", "c.txt"], files.OpenFiles.Select(f => f.Name));
        Assert.True(files.OpenFiles[1].IsPreview);

        await files.OpenRowToStayCommand.ExecuteAsync(Row("c.txt"));

        Assert.False(files.OpenFiles[1].IsPreview);
        Assert.Same(files.OpenFiles[1], files.Showing);
    }

    [Fact]
    public async Task Pinned_files_sit_first_and_stay_through_close_all()
    {
        await using var h = new TabTestHarness();
        var paths = new[] { "a.txt", "b.txt", "c.txt" }.Select(n => Write(Path.Combine(h.WorkFolder, n), "")).ToArray();
        var (_, files) = await OpenFilesAsync(h);
        foreach (var path in paths)
        {
            await files.OpenFileAsync(path, preview: false);
        }
        var c = files.OpenFiles[2];

        files.TogglePinFileCommand.Execute(c);

        Assert.Equal(["c.txt", "a.txt", "b.txt"], files.OpenFiles.Select(f => f.Name));
        // Dragged, it stays among the pinned.
        Assert.False(files.MoveFile(c, 2));

        await files.CloseAllFilesCommand.ExecuteAsync(null);

        Assert.Same(c, Assert.Single(files.OpenFiles));
        Assert.Same(c, files.Showing);
    }

    [Fact]
    public async Task Two_open_files_with_one_name_show_their_folders()
    {
        await using var h = new TabTestHarness();
        var player = Write(Path.Combine(h.WorkFolder, "Player", "Config.h"), "");
        var enemy = Write(Path.Combine(h.WorkFolder, "Enemy", "Config.h"), "");
        var (_, files) = await OpenFilesAsync(h);

        var first = await files.OpenFileAsync(player, preview: false);
        Assert.Equal("Config.h", first.TabLabel);
        var second = await files.OpenFileAsync(enemy, preview: false);

        Assert.Equal(("Player/Config.h", "Enemy/Config.h"), (first.TabLabel, second.TabLabel));
    }

    [Fact]
    public async Task A_file_that_isnt_text_offers_the_oss_app()
    {
        await using var h = new TabTestHarness();
        var path = Path.Combine(h.WorkFolder, "icon.png");
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0x0D], Token);
        var (_, files) = await OpenFilesAsync(h);

        var file = await files.OpenFileAsync(path, preview: false);

        Assert.True(file is { IsLoaded: true, HasProblem: true, IsText: false, Problem: TextFileProblem.Binary });
        Assert.Null(file.Document);
    }

    [Fact]
    public async Task The_chip_says_how_the_file_ends_its_lines_and_indents_and_changes_them()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.cs"), "class A\r\n{\r\n\tint x;\r\n}\r\n");
        var (_, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);

        Assert.Equal(("CRLF", "\r\n"), (file.LineEndingLabel, file.NewLine));
        Assert.True(file.Indent.UseTabs);
        Assert.Equal("C#", file.Language.Name);

        file.SetLineEndingsCommand.Execute(LineEndingKind.Lf);

        Assert.Equal("class A\n{\n\tint x;\n}\n", file.Document!.Text);
        Assert.True(file.IsModified);
        Assert.Equal("LF", file.LineEndingLabel);

        file.SetIndentCommand.Execute(file.IndentChoices.Single(c => c is { UseTabs: false, Size: 2 }));

        Assert.Equal(new IndentStyle(false, 2), file.Indent);
        // The lines already there stay as they were.
        Assert.Equal("class A\n{\n\tint x;\n}\n", file.Document.Text);
    }

    // ---- Saving and closing ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Saving_writes_the_edits_in_the_files_own_encoding_and_line_endings()
    {
        await using var h = new TabTestHarness();
        var path = Path.Combine(h.WorkFolder, "a.txt");
        await File.WriteAllBytesAsync(path, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("one\r\ntwo\r\n")], Token);
        var (_, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        Text(file).Insert(0, "zero\r\n");

        Assert.True(file.IsModified);
        Assert.Equal(1, files.UnsavedCount);

        await files.SaveCommand.ExecuteAsync(null);

        byte[] expected = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("zero\r\none\r\ntwo\r\n")];
        Assert.Equal(expected, await File.ReadAllBytesAsync(path, Token));
        Assert.False(file.IsModified);
        Assert.Equal(0, files.UnsavedCount);
    }

    [Fact]
    public async Task Closing_a_file_with_unsaved_edits_asks_first()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\n");
        var (_, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        Text(file).Insert(0, "x");
        file.FlushBackup();
        Assert.NotNull(h.Services.EditorBackups.Load(path));

        var closing = files.CloseFileCommand.ExecuteAsync(file);

        Assert.True(files.IsAskingToClose);
        Assert.Contains("a.txt", files.Closing!.Text, StringComparison.Ordinal);
        files.CancelCloseCommand.Execute(null);
        await closing;
        Assert.Same(file, Assert.Single(files.OpenFiles));
        Assert.False(files.IsAskingToClose);

        closing = files.CloseFileCommand.ExecuteAsync(file);
        files.CloseWithoutSavingCommand.Execute(null);
        await closing;

        Assert.Empty(files.OpenFiles);
        Assert.Equal("a\n", await File.ReadAllTextAsync(path, Token));
        await TabTestHarness.Eventually(() => h.Services.EditorBackups.Load(path) is null, "its unsaved copy to go");
    }

    [Fact]
    public async Task Save_on_the_question_saves_and_then_closes()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\n");
        var (_, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        Text(file).Insert(0, "x");

        var closing = h.Shell.CloseProjectFilesAsync(files);
        Assert.True(files.Closing!.ClosesTab);
        await files.SaveAndCloseCommand.ExecuteAsync(null);

        Assert.True(await closing);
        Assert.Equal("xa\n", await File.ReadAllTextAsync(path, Token));
        Assert.Null(h.Shell.Groups.Single().Files);
    }

    [Fact]
    public async Task A_file_changed_on_disk_since_it_was_read_isnt_written_over_without_asking()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\n");
        var (_, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        Text(file).Insert(0, "mine ");
        await File.WriteAllTextAsync(path, "theirs\n", Token);

        await files.SaveCommand.ExecuteAsync(null);

        Assert.Equal("theirs\n", await File.ReadAllTextAsync(path, Token));
        Assert.True(file.IsChangedBanner);
        Assert.Equal("a.txt changed on disk while you were editing it.", file.BannerText);

        file.KeepMineCommand.Execute(null);
        await files.SaveCommand.ExecuteAsync(null);

        Assert.Equal("mine a\n", await File.ReadAllTextAsync(path, Token));
        Assert.False(file.HasBanner);
    }

    // ---- Claude's changes --------------------------------------------------------------------------------------------

    [Fact]
    public async Task Claudes_edit_shows_at_once_in_a_file_without_unsaved_edits()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\nkeep\n");
        var (tab, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);

        await File.WriteAllTextAsync(path, "b\nkeep\n", Token);
        EmitEdit(h, "e1", path, before: "a\nkeep\n");

        await TabTestHarness.Eventually(() => file.Document!.Text == "b\nkeep\n", "the file read again");
        Assert.False(file.IsModified);
        await TabTestHarness.Eventually(() => tab.ChangedFiles.HasClaudeChange(path), "the change recorded");
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);
        await TabTestHarness.Eventually(() => file.Change is not null && file.HasChanges, "its marks");

        var block = Assert.Single(file.Markers.Blocks);
        Assert.Equal((1, 1, LineChange.Modified), (block.FirstLine, block.LineCount, file.Markers.KindOf(1)));
        Assert.Equal("M", files.Rows.Single(r => r.Name == "a.txt").ChangeLetter);
        Assert.True(file is { CanReview: true, ShowsModeBar: true });
        // Undo takes Claude's change back out of the editor, as one step.
        Assert.True(file.Document!.CanUndo);
    }

    [Fact]
    public async Task Claudes_edit_to_a_file_with_unsaved_edits_asks_what_to_keep()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\n");
        var (tab, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        Text(file).Insert(0, "mine ");

        await File.WriteAllTextAsync(path, "b\n", Token);
        EmitEdit(h, "e1", path, before: "a\n");
        await TabTestHarness.Eventually(() => tab.ChangedFiles.HasClaudeChange(path), "the change recorded");
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);

        await TabTestHarness.Eventually(() => file.IsChangedBanner && file.Change is not null, "the banner");
        Assert.Equal($"Claude changed a.txt in {tab.DisplayName} while you were editing it.", file.BannerText);
        Assert.Equal("mine a\n", file.Document!.Text);

        file.UseTheirsCommand.Execute(null);

        Assert.Equal("b\n", file.Document.Text);
        Assert.False(file.IsModified);
        Assert.False(file.HasBanner);
    }

    [Fact]
    public async Task Reviewed_ticks_the_file_in_the_tab_that_changed_it_until_Claude_changes_it_again()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "b\n");
        var (tab, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        EmitEdit(h, "e1", path, before: "a\n");
        await TabTestHarness.Eventually(() => tab.ChangedFiles.HasClaudeChange(path), "the change recorded");
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);
        await TabTestHarness.Eventually(() => file.CanReview, "the change in the Files tab");

        file.ToggleReviewedCommand.Execute(null);

        Assert.True(file.IsReviewed);
        Assert.True(tab.ChangedFiles.IsFileReviewed(path));
        Assert.True(files.Rows.Single(r => r.Name == "a.txt").IsReviewed);

        h.Time.Advance(TimeSpan.FromMinutes(1));
        await File.WriteAllTextAsync(path, "c\n", Token);
        EmitEdit(h, "e2", path, before: "b\n");
        await TabTestHarness.Eventually(() => !tab.ChangedFiles.IsFileReviewed(path), "the new change");
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);

        await TabTestHarness.Eventually(() => !file.IsReviewed, "the tick to go");
    }

    [Fact]
    public async Task The_peeks_header_goes_to_the_tab_that_made_the_change_beside_the_Files_tab()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "b\n");
        var (tab, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        EmitEdit(h, "e1", path, before: "a\n");
        await TabTestHarness.Eventually(() => tab.ChangedFiles.HasClaudeChange(path), "the change recorded");
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);
        await TabTestHarness.Eventually(() => file.CanShowChange, "the change in the Files tab");
        Assert.False(tab.IsShown);
        Assert.Equal($"Show the change in {tab.DisplayName}", file.ShowChangeTip);

        file.ShowChangeCommand.Execute(null);

        Assert.Equal([(IShellTab)files, tab], h.Shell.Panes);
        Assert.Same(tab, h.Shell.Selected);
        // The tab's view, once it's there, scrolls to the Edit that made the change.
        var tool = Assert.IsType<Conversation.ToolUseItem>(tab.TakePendingScroll());
        Assert.Equal("e1", tool.ToolUseId);
    }

    [Fact]
    public async Task A_double_click_on_a_line_of_the_diff_goes_back_to_Edit_at_that_line()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "b\nkeep\n");
        var (tab, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        EmitEdit(h, "e1", path, before: "a\nkeep\n");
        await TabTestHarness.Eventually(() => tab.ChangedFiles.HasClaudeChange(path), "the change recorded");
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);
        await TabTestHarness.Eventually(() => file.HasChanges, "the change in the Files tab");
        file.Mode = FileViewMode.Inline;
        await TabTestHarness.Eventually(() => file.Diff is { IsLoading: false, InlineRows.Count: > 0 }, "the diff");
        var rows = file.Diff!.InlineRows;

        file.EditAt(rows.Single(r => r.NewNumber == "2"));

        Assert.Equal((FileViewMode.Edit, 2), (file.Mode, file.CaretLine));

        file.Mode = FileViewMode.Inline;
        await TabTestHarness.Eventually(() => file.Diff is { IsLoading: false, InlineRows.Count: > 0 }, "the diff again");
        // A line only the "before" had goes to the line after it.
        file.EditAt(file.Diff!.InlineRows.Single(r => r.IsRemoved));

        Assert.Equal((FileViewMode.Edit, 1), (file.Mode, file.CaretLine));
    }

    [Fact]
    public async Task Each_diff_starts_with_Wrap_lines_as_the_last_one_was_left_in_a_Files_tab_or_the_diff_window()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "b\n");
        var (tab, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        EmitEdit(h, "e1", path, before: "a\n");
        await TabTestHarness.Eventually(() => tab.ChangedFiles.HasClaudeChange(path), "the change recorded");
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);
        await TabTestHarness.Eventually(() => file.HasChanges, "the change in the Files tab");
        file.Mode = FileViewMode.Inline;
        await TabTestHarness.Eventually(() => file.Diff is { IsLoading: false }, "the diff");
        Assert.False(file.Diff!.WordWrap);

        file.Diff.WordWrap = true;

        Assert.True(h.Services.State.DiffWordWrap);
        file.Mode = FileViewMode.SideBySide;
        Assert.True(file.Diff!.WordWrap);
        await TabTestHarness.Eventually(() => file.Diff is { IsLoading: false }, "the diff side by side");

        // The diff window, from Changed files, starts wrapped too, and turning it off there carries back.
        Diffs.DiffSource? requested = null;
        tab.ChangedFiles.DiffRequested += source => requested = source;
        await tab.ChangedFiles.OpenFileCommand.ExecuteAsync(new ChangedFileRow { Path = path, DisplayPath = "a.txt", Status = "M", StatusText = "Modified", Before = "a\n" });
        var window = new Diffs.DiffWindowViewModel(requested!, dark: false);
        await TabTestHarness.Eventually(() => !window.IsLoading, "the diff window");
        Assert.True(window.WordWrap);

        window.WordWrap = false;

        Assert.False(h.Services.State.DiffWordWrap);
        file.Mode = FileViewMode.Inline;
        Assert.False(file.Diff!.WordWrap);
        await TabTestHarness.Eventually(() => file.Diff is { IsLoading: false }, "the diff inline again");
    }

    [Fact]
    public async Task Show_only_changed_files_lists_Claudes_changes_by_path()
    {
        await using var h = new TabTestHarness();
        var changed = Write(Path.Combine(h.WorkFolder, "Source", "Player.cpp"), "b\n");
        Write(Path.Combine(h.WorkFolder, "Source", "Enemy.cpp"), "");
        var (tab, files) = await OpenFilesAsync(h);
        EmitEdit(h, "e1", changed, before: "a\n");
        await TabTestHarness.Eventually(() => tab.ChangedFiles.HasClaudeChange(changed), "the change recorded");
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);
        await TabTestHarness.Eventually(() => files.Rows.Single(r => r.Name == "Source").HasChangesInside, "the folder's dot");

        files.ShowOnlyChanged = true;

        var row = Assert.Single(files.ChangedRows);
        Assert.Equal(("Player.cpp", "Source", "M"), (row.Name, row.FolderText, row.ChangeLetter));
        Assert.True(files is { ShowsChangedList: true, ShowsTree: false, HasNoChanges: false });
    }

    // ---- Find by name ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Finding_by_name_lists_the_best_matches_with_their_folders_and_opens_one_at_its_line()
    {
        await using var h = new TabTestHarness();
        var player = Write(Path.Combine(h.WorkFolder, "Source", "Player", "PlayerController.cpp"), string.Concat(Enumerable.Repeat("line\n", 50)));
        Write(Path.Combine(h.WorkFolder, "Source", "Enemy.cpp"), "");
        var (_, files) = await OpenFilesAsync(h);

        files.StartFindCommand.Execute(null);
        files.FindText = "playcon:42";

        // The search ends off the test's thread, adding the matches one by one and then choosing the first.
        await TabTestHarness.Eventually(() => files.SelectedFound is not null, "the matches");
        var found = files.Found[0];
        Assert.Equal(("PlayerController.cpp", Path.Combine("Source", "Player"), 42), (found.Name, found.Folder, found.Line));
        Assert.Same(found, files.SelectedFound);
        Assert.True(files is { IsListingFound: true, ShowsTree: false });
        Assert.Contains(found.NameParts, p => p.IsMatch);

        await files.OpenFoundCommand.ExecuteAsync(found);

        var file = Assert.Single(files.OpenFiles);
        Assert.Equal(player, file.Path);
        Assert.False(file.IsPreview);
        Assert.Equal(42, file.CaretLine);
        Assert.False(files.IsFinding);
        // The tree comes back with the file selected, expanding its folders off the test's thread: it's waited for, so
        // the rows aren't still being added while the tab is saved.
        await TabTestHarness.Eventually(() => files.SelectedRow?.RelativePath == "Source/Player/PlayerController.cpp", "the file revealed in the tree");
    }

    // ---- Kept and brought back ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_saved_Files_tab_comes_back_at_launch_with_its_group_and_files()
    {
        await using var h = new TabTestHarness();
        var a = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\n");
        var b = Write(Path.Combine(h.WorkFolder, "Source", "b.txt"), "b\n");
        var saved = new ProjectFilesState
        {
            Folder = h.WorkFolder,
            Files = [new OpenFileState { Path = a, IsPinned = true }, new OpenFileState { Path = b, Line = 1, Mode = FileViewMode.Edit }],
            ShowingPath = b,
            ExpandedFolders = ["Source"],
        };
        h.Services.State.ProjectFiles = [saved];
        h.Services.State.SelectedTabId = saved.Id;

        // Restored on the UI thread, as at launch, so the explorer lists and expands there too.
        InlineDispatcher.Run(() => h.Shell.Restore(null));

        var files = Assert.IsType<ProjectFilesViewModel>(h.Shell.Selected);
        Assert.Same(files, Assert.Single(h.Shell.Groups).Files);
        Assert.Empty(h.Shell.AllTabs);
        Assert.Equal(["a.txt", "b.txt"], files.OpenFiles.Select(f => f.Name));
        Assert.True(files.OpenFiles[0].IsPinned);
        Assert.Equal(b, files.Showing?.Path);
        // The explorer adds rows as it lists and expands: watching the folder it expanded comes after its rows, so it's
        // waited for before they're read.
        await TabTestHarness.Eventually(() => h.FolderWatcher.Folders.Contains(Path.GetDirectoryName(b)), "the folder expanded again");
        Assert.Contains(files.Rows, r => r.Name == "b.txt" && r.Depth == 1);
        await TabTestHarness.Eventually(() => files.OpenFiles.All(f => f.IsLoaded), "the files read");
    }

    [Fact]
    public async Task Unsaved_edits_come_back_after_a_restart()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\n");
        var (_, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        Text(file).Insert(0, "unsaved ");

        var snapshot = h.Shell.CaptureForRestart();
        await h.Shell.CloseTabsForRestartAsync();
        InlineDispatcher.Run(() => h.Shell.Restore(null, snapshot));

        var back = Assert.IsType<ProjectFilesViewModel>(h.Shell.Selected);
        Assert.NotSame(files, back);
        var reopened = Assert.Single(back.OpenFiles);
        await TabTestHarness.Eventually(() => reopened.IsLoaded, "the file read");
        Assert.Equal("unsaved a\n", reopened.Document!.Text);
        Assert.True(reopened.IsModified);
        Assert.False(reopened.HasBanner);
        Assert.Equal("a\n", await File.ReadAllTextAsync(path, Token));
    }

    [Fact]
    public async Task Unsaved_edits_to_a_file_that_changed_meanwhile_come_back_with_the_banner()
    {
        await using var h = new TabTestHarness();
        var path = Write(Path.Combine(h.WorkFolder, "a.txt"), "a\n");
        var (_, files) = await OpenFilesAsync(h);
        var file = await files.OpenFileAsync(path, preview: false);
        Text(file).Insert(0, "unsaved ");

        var snapshot = h.Shell.CaptureForRestart();
        await h.Shell.CloseTabsForRestartAsync();
        await File.WriteAllTextAsync(path, "changed\n", Token);
        InlineDispatcher.Run(() => h.Shell.Restore(null, snapshot));

        var reopened = Assert.Single(Assert.IsType<ProjectFilesViewModel>(h.Shell.Selected).OpenFiles);
        await TabTestHarness.Eventually(() => reopened.IsLoaded, "the file read");
        Assert.Equal("unsaved a\n", reopened.Document!.Text);
        Assert.True(reopened.IsChangedBanner);
    }

    [Fact]
    public async Task A_group_with_only_its_Files_tab_keeps_its_place()
    {
        await using var h = new TabTestHarness();
        var other = Path.Combine(h.Root, "other");
        Directory.CreateDirectory(other);
        h.Services.State.ProjectFiles = [new ProjectFilesState { Folder = h.WorkFolder }];
        h.Services.State.Tabs = [new TabState { Folder = other, IsPinned = true }];
        h.Services.State.GroupOrder = [h.WorkFolder, other];

        InlineDispatcher.Run(() => h.Shell.Restore(null));

        Assert.Equal([h.WorkFolder, other], h.Shell.Groups.Select(g => g.Folder));
        Assert.NotNull(h.Shell.Groups[0].Files);
        Assert.Empty(h.Shell.Groups[0].Tabs);
    }

    [Fact]
    public async Task Settings_Editor_puts_the_explorer_on_the_right()
    {
        await using var h = new TabTestHarness();
        var (_, files) = await OpenFilesAsync(h);
        Assert.False(files.IsExplorerOnRight);

        files.MoveExplorerCommand.Execute(null);

        Assert.Equal(ExplorerSide.Right, h.Services.Settings.Editor.ExplorerSide);
        Assert.True(files.IsExplorerOnRight);
        Assert.Equal("Move to the left", files.MoveExplorerText);
    }
}
