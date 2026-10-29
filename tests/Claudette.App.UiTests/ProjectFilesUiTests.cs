using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.Views;
using Claudette.App.Views.Editor;
using Claudette.Core.Settings;

namespace Claudette.App.UiTests;

/// <summary>A project's Project Files tab, rendered (DESIGN.md §18, "Project files").</summary>
public sealed class ProjectFilesUiTests
{
    private static string Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>The tab's session changed <paramref name="path"/> from <paramref name="before"/> to what's on disk.</summary>
    private static async Task ClaudeEditedAsync(TabTestHarness h, TabViewModel tab, string path, string before)
    {
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject
            {
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = "tool_use", ["id"] = "e1", ["name"] = "Edit",
                    ["input"] = new JsonObject { ["file_path"] = path, ["old_string"] = "a", ["new_string"] = "b" },
                }),
            },
        });
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "e1", ["content"] = "The file has been updated." }) },
            ["tool_use_result"] = new JsonObject { ["filePath"] = path, ["oldString"] = "a", ["newString"] = "b", ["originalFile"] = before },
        });
        await TabTestHarness.Eventually(() => tab.ChangedFiles.HasClaudeChange(path), "the change recorded");
    }

    /// <summary>A starfall-like project: a folder, a file at the top, and Player.cpp, which Claude changed.</summary>
    private static async Task<(TabViewModel Tab, ProjectFilesViewModel Files, string Player, Window Window)> ShowAsync(TabTestHarness h, double width = 1400)
    {
        var tab = await h.OpenTabAsync();
        var player = Write(Path.Combine(h.WorkFolder, "Source", "Player.cpp"),
            "#include \"Player.h\"\n\nvoid APlayer::Dash()\n{\n    if (!HasAuthority())\n    {\n        return;\n    }\n    Spend(DashCost);\n}\n");
        Write(Path.Combine(h.WorkFolder, "Starfall.uproject"), "{}\n");
        await ClaudeEditedAsync(h, tab, player, "#include \"Player.h\"\n\nvoid APlayer::Dash()\n{\n    Spend(DashCost);\n}\n");
        h.Shell.OpenProjectFilesCommand.Execute(h.Shell.Groups.Single());
        var files = h.Shell.Groups.Single().Files!;
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, width, 800);
        await UiText.SettleUntilAsync(window, () => files.Rows.Count == 2, "the explorer");
        var file = await files.OpenFileAsync(player, preview: false);
        h.Time.Advance(ProjectFilesViewModel.ChangesDelay);
        await UiText.SettleUntilAsync(window, () => file.HasChanges && file.CanReview, "Claude's change");
        return (tab, files, player, window);
    }

    private static ProjectFilesView View(Window window) => window.GetVisualDescendants().OfType<ProjectFilesView>().Single(v => v.IsEffectivelyVisible);

    private static TextEditor Editor(Window window) =>
        window.GetVisualDescendants().OfType<CodeEditor>().Single(e => e.IsEffectivelyVisible).GetVisualDescendants().OfType<TextEditor>().First();

    [AvaloniaFact]
    public async Task The_tab_shows_the_explorer_the_files_tabs_and_the_editor_with_Claudes_change_and_the_chip()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, player, window) = await ShowAsync(h);

        var editor = Editor(window);
        Assert.Equal(File.ReadAllText(player), editor.Document.Text);
        Assert.False(editor.IsReadOnly);
        // The bars beside the line numbers, and the explorer on the left by default.
        Assert.Single(editor.TextArea.LeftMargins.OfType<ChangeMargin>());
        var explorer = View(window).GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TreeList");
        Assert.True(explorer.TranslatePoint(default, window)!.Value.X < editor.TranslatePoint(default, window)!.Value.X);

        var shown = UiText.Describe(View(window), (h.WorkFolder, "{work}"));
        Assert.Contains("\"Ln 1, Col 1\"", shown, StringComparison.Ordinal);
        Assert.Contains("\"C++\"", shown, StringComparison.Ordinal);
        window.Close();
        await Verify(shown);
    }

    /// <summary>
    /// A file popped out by the row's button (DESIGN.md §18, "A file in a window of its own"): its window has the editor,
    /// colored, and the toolbar's Put back; the tab keeps its tab, marked, and lets go of the editor until it's put back.
    /// </summary>
    [AvaloniaFact]
    public async Task The_rows_button_opens_the_file_in_a_window_and_Put_back_brings_it_back()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, player, window) = await ShowAsync(h);
        var shell = window.GetVisualDescendants().OfType<ShellView>().Single();
        var file = files.Showing!;
        Point Middle(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), TopLevel.GetTopLevel(c)!)!.Value;
        // The row's ⋯ and the button beside it, centered in the row.
        var row = View(window).GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("filetabs"));
        foreach (var name in new[] { "Every open file", "Open in a window" })
        {
            var icon = View(window).GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name).GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
            Assert.Equal(Middle(row).Y, Middle(icon).Y, 1);
        }

        var button = View(window).GetVisualDescendants().OfType<Button>().Single(b => b.Name == "OpenInWindowButton");
        window.MouseDown(Middle(button), MouseButton.Left);
        window.MouseUp(Middle(button), MouseButton.Left);
        var popped = Assert.Single(shell.FileWindows).Value;
        await UiText.SettleUntilAsync(popped, () => popped.GetVisualDescendants().OfType<TextEditor>().Any(e => e.Document is not null), "the window's editor");
        UiText.Settle(window);

        Assert.True(file.IsInWindow);
        var editor = popped.GetVisualDescendants().OfType<TextEditor>().Single(e => e.Document is not null);
        Assert.Equal(File.ReadAllText(player), editor.Document.Text);
        // The tab's editor let go of it, and its tab stays, marked.
        Assert.DoesNotContain(View(window).GetVisualDescendants().OfType<TextEditor>(), e => e.Document is not null);
        var tab = View(window).GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("filetab"));
        Assert.True(tab.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Classes.Contains("inwindow")).IsEffectivelyVisible);
        Assert.Contains("\"Player.cpp is in a window of its own\"", UiText.Describe(View(window)), StringComparison.Ordinal);
        var shown = UiText.Describe(popped, (h.WorkFolder, "{work}"));

        var putBack = popped.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PutBackButton");
        popped.MouseDown(Middle(putBack), MouseButton.Left);
        popped.MouseUp(Middle(putBack), MouseButton.Left);
        UiText.Settle(window);

        Assert.Empty(shell.FileWindows);
        Assert.False(file.IsInWindow);
        Assert.Same(file, files.Showing);
        Assert.Equal(File.ReadAllText(player), Editor(window).Document.Text);
        window.Close();
        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task A_click_on_a_changes_bar_shows_what_was_there_under_it()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, _, window) = await ShowAsync(h);
        var file = files.Showing!;
        var block = Assert.Single(file.Markers.Blocks);
        Assert.Equal((5, 4), (block.FirstLine, block.LineCount));

        file.TogglePeekAt(6);
        UiText.Settle(window);

        var peek = window.GetVisualDescendants().OfType<ChangePeek>().Single(p => p.IsEffectivelyVisible);
        var shown = UiText.Describe(peek);
        Assert.Contains("These lines are new", shown, StringComparison.Ordinal);
        // Under the change's last line, the width of the text.
        var editor = Editor(window);
        var lastLine = editor.TextArea.TextView.GetVisualLine(8)!;
        var peekTop = peek.TranslatePoint(default, editor.TextArea.TextView)!.Value.Y;
        Assert.True(peekTop >= lastLine.VisualTop + editor.TextArea.TextView.DefaultLineHeight - editor.TextArea.TextView.VerticalOffset - 1,
            $"The peek starts at {peekTop}, above the end of line 8.");

        file.ClosePeekCommand.Execute(null);
        UiText.Settle(window);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<ChangePeek>(), p => p.IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Inline_changes_show_Claudes_change_read_only_with_Revert()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, _, window) = await ShowAsync(h);
        var file = files.Showing!;

        file.Mode = Core.ProjectFiles.FileViewMode.Inline;
        await UiText.SettleUntilAsync(window, () => file.Diff is { IsLoading: false }, "the diff");

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<CodeEditor>(), e => e.IsEffectivelyVisible);
        var shown = UiText.Describe(View(window));
        Assert.Contains("[button] Revert", shown, StringComparison.Ordinal);
        Assert.Contains("[toggle] Whole file", shown, StringComparison.Ordinal);
        Assert.Contains("[toggle on] Inline changes", shown, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_file_comes_back_scrolled_where_it_was_left()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var path = Write(Path.Combine(h.WorkFolder, "long.txt"), string.Concat(Enumerable.Range(1, 300).Select(i => $"line {i}\n")));
        var saved = new Core.ProjectFiles.ProjectFilesState
        {
            Folder = h.WorkFolder,
            Files = [new Core.ProjectFiles.OpenFileState { Path = path, Line = 150, Scroll = 1000 }],
            ShowingPath = path,
        };
        h.Services.State.ProjectFiles = [saved];
        h.Services.State.SelectedTabId = saved.Id;

        h.Shell.Restore(null);
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1400, 800);

        var files = Assert.IsType<ProjectFilesViewModel>(h.Shell.Selected);
        await UiText.SettleUntilAsync(window, () => files.Showing is { IsLoaded: true } && Editor(window).TextArea.TextView.ScrollOffset.Y > 990,
            "the file scrolled where it was");
        window.Close();
    }

    [AvaloniaFact]
    public async Task With_Settings_so_the_explorer_is_on_the_right()
    {
        await using var h = new TabTestHarness(s => s.Editor.ExplorerSide = ExplorerSide.Right, dispatcher: new AvaloniaUiDispatcher());
        var (_, _, _, window) = await ShowAsync(h);

        var explorer = View(window).GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TreeList");
        Assert.True(explorer.TranslatePoint(default, window)!.Value.X > Editor(window).TranslatePoint(default, window)!.Value.X);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Find_takes_the_keyboard_so_what_is_typed_is_searched_for()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, _, _, window) = await ShowAsync(h);
        var editor = Editor(window);
        editor.TextArea.Focus();
        UiText.Settle(window);

        var primary = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.F, primary);
        window.KeyReleaseQwerty(PhysicalKey.F, primary);
        UiText.Settle(window);
        window.KeyTextInput("Dash");
        UiText.Settle(window);

        Assert.False(editor.SearchPanel.IsClosed);
        Assert.Equal("Dash", editor.SearchPanel.SearchPattern);
        Assert.Equal(File.ReadAllText(Path.Combine(h.WorkFolder, "Source", "Player.cpp")), editor.Document.Text);
        window.Close();
    }

    /// <summary>
    /// The find box's <b>Find in files</b>, before its ×, takes what it has to Find in files, its options too; and so does
    /// Find in files' shortcut, pressed with the keyboard in the box (DESIGN.md §18, "Finding in files").
    /// </summary>
    [AvaloniaFact]
    public async Task Finds_Find_in_files_button_and_shortcut_take_what_it_has_to_Find_in_files()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, _, _, window) = await ShowAsync(h);
        var editor = Editor(window);
        editor.TextArea.Focus();
        UiText.Settle(window);
        var primary = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.F, primary);
        window.KeyReleaseQwerty(PhysicalKey.F, primary);
        UiText.Settle(window);
        window.KeyTextInput("Dash");
        editor.SearchPanel.MatchCase = true;
        UiText.Settle(window);

        var buttons = editor.SearchPanel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
        var findInFiles = buttons.Single(b => b.Name == SearchPanelButtons.FindInFilesName);
        // Just before ×, which stays last.
        Assert.Equal(buttons.IndexOf(findInFiles) + 1, buttons.Count - 1);
        Assert.Equal("Find in files", AutomationProperties.GetName(findInFiles));
        Assert.Equal(h.Services.Tips.FindInFiles, ToolTip.GetTip(findInFiles));
        Point Middle(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
        window.MouseDown(Middle(findInFiles), MouseButton.Left);
        window.MouseUp(Middle(findInFiles), MouseButton.Left);
        UiText.Settle(window);

        Assert.Equal(("Dash", true, false), (h.Shell.FindInFiles!.Query, h.Shell.FindInFiles.MatchCase, h.Shell.FindInFiles.IsReplaceMode));
        h.Shell.CloseFindInFiles();
        UiText.Settle(window);

        // The shortcut, with the keyboard in the box.
        editor.SearchPanel.GetVisualDescendants().OfType<TextBox>().First().Focus();
        editor.SearchPanel.SearchPattern = "Spend";
        UiText.Settle(window);
        window.KeyPressQwerty(PhysicalKey.F, primary | RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.F, primary | RawInputModifiers.Shift);
        UiText.Settle(window);
        Assert.Equal("Spend", h.Shell.FindInFiles!.Query);
        window.Close();
    }

    /// <summary>With nothing open, the tab offers what to do next, each with its shortcut as it's bound now.</summary>
    [AvaloniaFact]
    public async Task With_nothing_open_it_offers_Go_to_file_Find_and_Replace_in_files_and_the_palette()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "Starfall.uproject"), "{}\n");
        h.Shell.OpenProjectFilesCommand.Execute(h.Shell.Groups.Single());
        var files = h.Shell.Groups.Single().Files!;
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1400, 800);
        await UiText.SettleUntilAsync(window, () => files.Rows.Count == 1, "the explorer");

        var actions = View(window).GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "NoFileActions");
        Assert.True(actions.IsEffectivelyVisible);
        var text = UiText.Describe(actions);
        var tips = h.Services.Tips;
        foreach (var (label, keys) in new[] { ("Go to file", tips.GoToFileKeys), ("Find in files", tips.FindInFilesKeys), ("Replace in files", tips.ReplaceInFilesKeys), ("Command palette", tips.CommandPaletteKeys) })
        {
            Assert.Contains(label, text, StringComparison.Ordinal);
            Assert.Contains(keys, text, StringComparison.Ordinal);
        }

        var find = actions.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Find in files"));
        Point Middle(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
        window.MouseDown(Middle(find), MouseButton.Left);
        window.MouseUp(Middle(find), MouseButton.Left);
        UiText.Settle(window);
        Assert.Equal(h.WorkFolder, h.Shell.FindInFiles?.Project);
        window.Close();
    }

    /// <summary>
    /// The find box's × closes it wherever the keyboard is, as after <b>Use theirs</b> on the changed-on-disk banner, whose
    /// button had it and is gone: AvaloniaEdit's close is a routed command, which looks for its handler from the focus.
    /// </summary>
    [AvaloniaFact]
    public async Task Finds_close_button_closes_it_after_Use_theirs_took_the_keyboard()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, player, window) = await ShowAsync(h);
        var editor = Editor(window);
        var file = files.Showing!;
        editor.TextArea.Focus();
        UiText.Settle(window);
        var primary = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.F, primary);
        window.KeyReleaseQwerty(PhysicalKey.F, primary);
        UiText.Settle(window);
        window.KeyTextInput("Dash");
        UiText.Settle(window);
        Assert.False(editor.SearchPanel.IsClosed);

        // An edit of the user's, then the file changed on disk under it: the banner asks.
        editor.TextArea.Focus();
        editor.Document.Insert(0, "// mine\n");
        File.WriteAllText(player, File.ReadAllText(player).Replace("Dash", "Sprint", StringComparison.Ordinal));
        await file.CheckDiskAsync();
        await UiText.SettleUntilAsync(window, () => file.IsChangedBanner, "the banner");
        var useTheirs = window.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && UiText.Describe(b).Contains("Use theirs", StringComparison.Ordinal));
        Point Middle(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
        window.MouseDown(Middle(useTheirs), MouseButton.Left);
        window.MouseUp(Middle(useTheirs), MouseButton.Left);
        await UiText.SettleUntilAsync(window, () => !file.HasBanner, "their text");
        Assert.DoesNotContain("// mine", editor.Document.Text, StringComparison.Ordinal);
        Assert.False(editor.SearchPanel.IsClosed);

        // The keyboard somewhere else in the window, as it may be once the banner's button is gone: the × still works.
        View(window).GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TreeList").Focus();
        UiText.Settle(window);
        var close = editor.SearchPanel.GetVisualDescendants().OfType<Button>().Last(b => b.IsEffectivelyVisible);
        Assert.True(close.IsEffectivelyEnabled, "The × should stay enabled with the keyboard elsewhere");
        window.MouseDown(Middle(close), MouseButton.Left);
        window.MouseUp(Middle(close), MouseButton.Left);
        UiText.Settle(window);
        Assert.True(editor.SearchPanel.IsClosed);
        window.Close();
    }

    /// <summary>
    /// Opened with the keyboard elsewhere, as the Files tab's own Find can, the find box's buttons still work: AvaloniaEdit
    /// works out once, from what last had the keyboard, whether its routed commands can run.
    /// </summary>
    [AvaloniaFact]
    public async Task Finds_buttons_work_when_it_was_opened_with_the_keyboard_elsewhere()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, _, window) = await ShowAsync(h);
        var editor = Editor(window);
        View(window).GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TreeList").Focus();
        UiText.Settle(window);

        files.Showing!.RequestFind(replace: false);
        UiText.Settle(window);
        Assert.False(editor.SearchPanel.IsClosed);
        View(window).GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TreeList").Focus();
        UiText.Settle(window);

        var close = editor.SearchPanel.GetVisualDescendants().OfType<Button>().Last(b => b.IsEffectivelyVisible);
        Assert.True(close.IsEffectivelyEnabled, "The × should be enabled");
        Point Middle(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
        window.MouseDown(Middle(close), MouseButton.Left);
        window.MouseUp(Middle(close), MouseButton.Left);
        UiText.Settle(window);
        Assert.True(editor.SearchPanel.IsClosed);
        window.Close();
    }

    private static async Task<TextBox> NewEntryBoxAsync(Window window)
    {
        TextBox? box = null;
        await UiText.SettleUntilAsync(window,
            () => (box = View(window).GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.DataContext is NewEntryRow)) is { IsFocused: true },
            "the new row's box to take the keyboard");
        return box!;
    }

    private static void Press(TopLevel keys, PhysicalKey key)
    {
        keys.KeyPressQwerty(key, RawInputModifiers.None);
        keys.KeyReleaseQwerty(key, RawInputModifiers.None);
    }

    [AvaloniaFact]
    public async Task A_new_files_box_takes_the_keyboard_says_why_a_name_cant_be_used_and_Enter_makes_it()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, _, window) = await ShowAsync(h);
        var source = files.Rows.Single(r => r.Name == "Source");
        files.SelectedRow = source;

        await files.NewFileCommand.ExecuteAsync(source);
        var box = await NewEntryBoxAsync(window);
        var keys = TopLevel.GetTopLevel(box)!;
        keys.KeyTextInput("Player.cpp");
        UiText.Settle(window);

        var shown = UiText.Describe(View(window).GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TreeList"));
        Assert.Contains("\"There's already a file called Player.cpp here.\"", shown, StringComparison.Ordinal);

        // Space types a space, rather than opening the row selected; Up stays in the box.
        box.SelectAll();
        keys.KeyTextInput("Weapon");
        keys.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        keys.KeyTextInput(" ");
        keys.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        keys.KeyTextInput("Data.cpp");
        Press(keys, PhysicalKey.ArrowUp);
        UiText.Settle(window);
        Assert.Equal("Weapon Data.cpp", box.Text);
        Assert.Same(source, files.SelectedRow);
        Assert.True(box.IsFocused);

        Press(keys, PhysicalKey.Enter);
        var path = Path.Combine(h.WorkFolder, "Source", "Weapon Data.cpp");
        await UiText.SettleUntilAsync(window, () => files.Showing?.Path == path, "the new file to open");
        Assert.True(File.Exists(path));
        Assert.Null(files.NamingRow);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Esc_or_a_click_elsewhere_leaves_the_new_row_without_making_anything()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, _, window) = await ShowAsync(h);
        var tree = View(window).GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TreeList");

        await files.NewFolderCommand.ExecuteAsync(null);
        var box = await NewEntryBoxAsync(window);
        TopLevel.GetTopLevel(box)!.KeyTextInput("Art");
        Press(TopLevel.GetTopLevel(box)!, PhysicalKey.Escape);
        UiText.Settle(window);

        Assert.Null(files.NamingRow);
        Assert.True(tree.IsKeyboardFocusWithin);

        await files.NewFileCommand.ExecuteAsync(null);
        _ = await NewEntryBoxAsync(window);
        Editor(window).TextArea.Focus();
        UiText.Settle(window);

        Assert.Null(files.NamingRow);
        Assert.DoesNotContain(files.Rows, r => r is NewEntryRow);
        Assert.False(Directory.Exists(Path.Combine(h.WorkFolder, "Art")));
        window.Close();
    }

    [AvaloniaFact]
    public async Task F2_renames_with_the_name_up_to_its_extension_selected_and_Delete_asks_with_its_button_ready()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (_, files, _, window) = await ShowAsync(h);
        var tree = View(window).GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "TreeList");
        var project = files.Rows.Single(r => r.Name == "Starfall.uproject");
        files.SelectedRow = project;
        UiText.Settle(window);
        ((Control)tree.ContainerFromItem(project)!).Focus();
        UiText.Settle(window);

        Press(TopLevel.GetTopLevel(tree)!, PhysicalKey.F2);
        TextBox? box = null;
        await UiText.SettleUntilAsync(window,
            () => (box = View(window).GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.DataContext is RenameRow)) is { IsFocused: true },
            "the rename box to take the keyboard");

        // Typing replaces the name and keeps its extension.
        Assert.Equal(("Starfall.uproject", 0, 8), (box!.Text, box.SelectionStart, box.SelectionEnd));
        var keys = TopLevel.GetTopLevel(box)!;
        keys.KeyTextInput("Nightfall");
        Press(keys, PhysicalKey.Enter);
        var renamed = Path.Combine(h.WorkFolder, "Nightfall.uproject");
        await UiText.SettleUntilAsync(window, () => File.Exists(renamed) && files.SelectedRow?.Name == "Nightfall.uproject" && tree.IsKeyboardFocusWithin,
            "the rename, with the keyboard back in the tree");

        Press(TopLevel.GetTopLevel(tree)!, PhysicalKey.Delete);
        var confirm = View(window).GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ConfirmDeleteButton");
        await UiText.SettleUntilAsync(window, () => files.IsAskingToDelete && confirm.IsFocused, "the question, with its button ready");

        Assert.Equal("Delete Nightfall.uproject? It goes to the Recycle Bin.", files.Deleting!.Text);
        Press(TopLevel.GetTopLevel(confirm)!, PhysicalKey.Escape);
        UiText.Settle(window);

        Assert.False(files.IsAskingToDelete);
        Assert.True(File.Exists(renamed));
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_group_has_its_Files_row_first_and_its_button_beside_new_tab()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1400, 800);
        var sidebar = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "Sidebar");
        Assert.Contains("[button] Project files", UiText.Describe(sidebar), StringComparison.Ordinal);

        h.Shell.OpenProjectFilesCommand.Execute(h.Shell.Groups.Single());
        UiText.Settle(window);

        var shown = UiText.Describe(sidebar).Split('\n');
        var files = Array.FindIndex(shown, l => l.Contains("\"Project Files\"", StringComparison.Ordinal));
        var tab = Array.FindIndex(shown, l => l.Contains(h.Shell.AllTabs.Single().DisplayName, StringComparison.Ordinal) && l.Contains('"'));
        Assert.True(files >= 0 && files < tab, string.Join('\n', shown));
        window.Close();
    }
}
