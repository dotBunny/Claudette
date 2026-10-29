using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Claudette.App.Controls;
using Claudette.App.Diffs;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.ToolWindows;
using Claudette.App.Views;

namespace Claudette.App.UiTests;

/// <summary>
/// The tool windows rendered (DESIGN.md §3, "Tool windows"): the strip of icons, a window shown beside it with its header,
/// the right side shared and the bottom under the panes, and what the session's windows show: ticking changed files as
/// reviewed (DESIGN.md §8), and processes (DESIGN.md §4).
/// </summary>
public class ToolWindowsUiTests
{
    /// <summary>The tool window on screen: where its page's controls are.</summary>
    private static Control ShownWindow(Window window) => window.GetVisualDescendants().OfType<ToolWindowView>().First(v => v.IsEffectivelyVisible);

    [AvaloniaFact]
    public async Task The_strip_has_an_icon_for_each_window_and_a_click_shows_it_beside_the_conversation()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        UiText.Settle(window);
        var strip = window.GetVisualDescendants().OfType<ToolStripView>().Single(s => s.IsEffectivelyVisible);
        var icons = strip.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("stripicon")).ToList();
        Assert.Equal(["Changed files", "Agents", "Scratch pad"], icons.Select(AutomationProperties.GetName));
        // Their tips say what each holds now.
        Assert.Equal("Agents · Claude hasn't started any subagents in this tab.", ToolTip.GetTip(icons[1]));
        // Down the strip, the split group's under a line.
        var divider = strip.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Divider");
        Assert.True(divider.IsEffectivelyVisible);
        double Top(Visual v) => v.TranslatePoint(default, strip)!.Value.Y;
        Assert.True(Top(icons[0]) < Top(icons[1]) && Top(icons[1]) < Top(divider) && Top(divider) < Top(icons[2]));
        Assert.Equal(ToolDockViewModel.StripWidth, strip.Bounds.Width);
        var right = window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "RightSide");
        Assert.False(right.IsVisible);

        icons[0].Command!.Execute(null);
        UiText.Settle(window);

        // Its window shows beside the strip, taking room from the conversation, with Rider's header and no tabs.
        Assert.Contains("showing", icons[0].Classes);
        Assert.True(right.IsEffectivelyVisible);
        Assert.Equal(ToolDockViewModel.DefaultRightWidth, right.Bounds.Width);
        var panel = window.GetVisualDescendants().OfType<ToolPanelView>().Single(v => v.Name == "TopPanel");
        Assert.NotNull(ShownWindow(window).GetVisualDescendants().OfType<ListBox>().SingleOrDefault(l => l.Name == "ChangedFilesList"));
        Assert.Equal("Changed files", panel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("tooltitle")).Text);
        Assert.Equal(["Changed files options", "Hide Changed files"],
            panel.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("toolaction")).Select(AutomationProperties.GetName));
        var conversation = window.GetVisualDescendants().OfType<TabView>().Single();
        Assert.True(conversation.TranslatePoint(new Point(conversation.Bounds.Width, 0), window)!.Value.X <= right.TranslatePoint(default, window)!.Value.X + 1);

        // Its − hides it.
        panel.GetVisualDescendants().OfType<Button>().Last(b => b.Classes.Contains("toolaction")).Command!.Execute(null);
        UiText.Settle(window);
        Assert.False(right.IsVisible);
        Assert.DoesNotContain("showing", icons[0].Classes);
    }

    [AvaloniaFact]
    public async Task The_split_groups_window_shares_the_right_side_and_the_bottoms_shows_under_the_panes()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, width: 1400, height: 900);
        h.ShowToolWindow(ToolWindowKinds.Files);
        h.ShowToolWindow(ToolWindowKinds.ScratchPad);
        UiText.Settle(window);

        var (top, split) = (Panel(window, "TopPanel"), Panel(window, "SplitPanel"));
        Assert.True(top.IsEffectivelyVisible && split.IsEffectivelyVisible);
        Assert.True(top.TranslatePoint(default, window)!.Value.Y < split.TranslatePoint(default, window)!.Value.Y);
        // Half each at first.
        Assert.InRange(top.Bounds.Height - split.Bounds.Height, -2, 2);

        InlineDispatcher.Run(() => h.Shell.ToolWindows.MoveTo(h.ToolWindow(ToolWindowKinds.Agents), ToolGroup.Bottom));
        h.ShowToolWindow(ToolWindowKinds.Agents);
        UiText.Settle(window);

        var bottom = Panel(window, "BottomPanel");
        Assert.True(bottom.IsEffectivelyVisible);
        Assert.Equal(ToolDockViewModel.DefaultBottomHeight, bottom.Bounds.Height);
        // Under the conversation, which ends above it, and beside the right side, which keeps the whole height.
        var conversation = window.GetVisualDescendants().OfType<TabView>().Single();
        Assert.True(conversation.TranslatePoint(new Point(0, conversation.Bounds.Height), window)!.Value.Y <= bottom.TranslatePoint(default, window)!.Value.Y + 1);
        Assert.True(bottom.TranslatePoint(new Point(bottom.Bounds.Width, 0), window)!.Value.X <= top.TranslatePoint(default, window)!.Value.X + 1);
    }

    private static ToolPanelView Panel(Window window, string name) => window.GetVisualDescendants().OfType<ToolPanelView>().Single(v => v.Name == name);

    [AvaloniaFact]
    public async Task Each_strip_icons_glyph_is_drawn_in_the_middle_of_it_whatever_its_shape()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        UiText.Settle(window);

        // Changed files and Scratch pad are taller than wide, Agents as wide as tall.
        var icons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("stripicon") && b.IsEffectivelyVisible).ToList();
        Assert.Equal(["Changed files", "Agents", "Scratch pad"], icons.Select(AutomationProperties.GetName));
        foreach (var icon in icons)
        {
            var glyph = icon.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Classes.Contains("stripglyph"));
            var drawn = glyph.TranslatePoint(glyph.RenderedGeometry!.Bounds.Center, icon)!.Value;
            Assert.Equal(icon.Bounds.Width / 2, drawn.X, 0.5);
            Assert.Equal(icon.Bounds.Height / 2, drawn.Y, 0.5);
            // As big as fits its 16 pixels.
            Assert.Equal(16, Math.Max(glyph.RenderedGeometry.Bounds.Width, glyph.RenderedGeometry.Bounds.Height), 0.5);
        }
    }

    /// <summary>A drag with the mouse from <paramref name="from"/>, by <paramref name="by"/>, as the user does it.</summary>
    private static void Drag(Window window, Point from, Vector by)
    {
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + by / 2);
        window.MouseMove(from + by);
        window.MouseUp(from + by, MouseButton.Left);
        UiText.Settle(window);
    }

    [AvaloniaFact]
    public async Task A_tool_windows_edge_drags_it_wider_or_taller_down_to_its_least_pinned_or_not()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, width: 1400, height: 900);
        h.ShowToolWindow(ToolWindowKinds.Files);
        UiText.Settle(window);
        var right = window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "RightSide");
        Assert.Equal(ToolDockViewModel.DefaultRightWidth, right.Bounds.Width);

        // The gap at its left, between it and the conversation, as the sidebar's edge is dragged.
        Point Edge() => right.TranslatePoint(new Point(-3, right.Bounds.Height / 2), window)!.Value;
        Drag(window, Edge(), new Vector(-100, 0));
        Assert.Equal(ToolDockViewModel.DefaultRightWidth + 100, right.Bounds.Width);
        Drag(window, Edge(), new Vector(400, 0));
        Assert.Equal(ToolDockViewModel.MinRightWidth, right.Bounds.Width);
        Assert.Equal(ToolDockViewModel.MinRightWidth, h.Shell.ToolWindows.RightWidth);

        // The line between the right side's two windows: the top one taller as it goes down.
        h.ShowToolWindow(ToolWindowKinds.ScratchPad);
        UiText.Settle(window);
        var top = Panel(window, "TopPanel");
        var was = top.Bounds.Height;
        Drag(window, top.TranslatePoint(new Point(top.Bounds.Width / 2, top.Bounds.Height + 3), window)!.Value, new Vector(0, 80));
        Assert.InRange(top.Bounds.Height - was, 78, 82);

        // The bottom's, by the gap above it.
        InlineDispatcher.Run(() => h.Shell.ToolWindows.MoveTo(h.ToolWindow(ToolWindowKinds.Agents), ToolGroup.Bottom));
        h.ShowToolWindow(ToolWindowKinds.Agents);
        UiText.Settle(window);
        var bottom = Panel(window, "BottomPanel");
        Point Above() => bottom.TranslatePoint(new Point(bottom.Bounds.Width / 2, -3), window)!.Value;
        Drag(window, Above(), new Vector(0, -60));
        Assert.Equal(ToolDockViewModel.DefaultBottomHeight + 60, bottom.Bounds.Height);

        // An unpinned window's edge sizes it too, rather than hiding it as a press elsewhere does.
        InlineDispatcher.Run(() => h.Shell.ToolWindows.SetPinned(h.ToolWindow(ToolWindowKinds.Agents), pinned: false));
        Drag(window, Above(), new Vector(0, -40));
        Assert.True(bottom.IsEffectivelyVisible);
        Assert.Equal(ToolDockViewModel.DefaultBottomHeight + 100, bottom.Bounds.Height);
    }

    [AvaloniaFact]
    public async Task A_changed_files_box_ticks_it_as_reviewed_without_opening_the_diff()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        h.ShowToolWindow(ToolWindowKinds.Files);
        // The panel's pages are only in the visual tree once it has been laid out open.
        UiText.Settle(window);
        var (first, second) = (Path.Combine(h.WorkFolder, "auth.cs"), Path.Combine(h.WorkFolder, "login.cs"));
        await File.WriteAllTextAsync(first, "b\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(second, "b\n", TestContext.Current.CancellationToken);
        h.Transport.Emit(Edit("e1", first));
        h.Transport.Emit(EditResult("e1", first));
        h.Transport.Emit(Edit("e2", second));
        h.Transport.Emit(EditResult("e2", second));
        var view = ShownWindow(window);
        var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "ChangedFilesList");
        await UiText.SettleUntilAsync(window, () => Boxes(list).Count == 2, "the changed files");
        var opened = 0;
        tab.ChangedFiles.DiffRequested += _ => opened++;

        Click(window, Boxes(list)[0]);

        Assert.Equal(0, opened);
        Assert.Null(list.SelectedItem);
        Assert.True(tab.ChangedFiles.Files[0].IsReviewed);
        Assert.Equal([true, false], Boxes(list).Select(b => b.IsChecked == true));
        Assert.Equal("2 files changed · 1 reviewed", tab.ChangedFiles.Summary);
        // A reviewed file is drawn faintly; its box isn't.
        var rows = list.GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("changedfile")).ToList();
        Assert.Equal([0.5, 1.0], rows.Select(r => r.Children.OfType<StackPanel>().Single().Opacity));
        Assert.Equal(1.0, Boxes(list)[0].Opacity);

        // Unticked by a click, then ticked from the view model, the box follows.
        Click(window, Boxes(list)[0]);
        Assert.False(tab.ChangedFiles.Files[0].IsReviewed);
        tab.ChangedFiles.ToggleFileReviewedCommand.Execute(tab.ChangedFiles.Files[0]);
        UiText.Settle(window);
        Assert.True(Boxes(list)[0].IsChecked);

        // Claude changing the file again unticks it.
        var panel = window.GetVisualDescendants().OfType<ToolPanelView>().Single(v => v.Name == "TopPanel");
        var shown = UiText.Describe(panel);
        h.Transport.Emit(Edit("e3", first));
        h.Transport.Emit(EditResult("e3", first));
        await UiText.SettleUntilAsync(window, () => tab.ChangedFiles.Files is [{ LatestChange: "e3" }, _], "Claude's next change");
        Assert.Equal([false, false], Boxes(list).Select(b => b.IsChecked == true));

        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task A_file_changed_since_the_sessions_change_says_so_under_its_folder()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        h.ShowToolWindow(ToolWindowKinds.Files);
        UiText.Settle(window);
        var path = Path.Combine(h.WorkFolder, "auth.cs");
        // Claude's Edit leaves "b"; another line is on disk since.
        await File.WriteAllTextAsync(path, "b\nyours\n", TestContext.Current.CancellationToken);
        h.Transport.Emit(Edit("e1", path));
        h.Transport.Emit(EditResult("e1", path));
        var view = ShownWindow(window);
        var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "ChangedFilesList");
        await UiText.SettleUntilAsync(window, () => list.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Changed since: +1 −0"), "the line for what changed since");

        var since = list.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Changed since: +1 −0");
        Assert.Contains("warning", since.Classes);
        await Verify(UiText.Describe(list));
    }

    [AvaloniaFact]
    public async Task A_change_the_lines_dont_show_is_said_under_its_folder()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        h.ShowToolWindow(ToolWindowKinds.Files);
        UiText.Settle(window);
        var (spaces, newline) = (Path.Combine(h.WorkFolder, "auth.cs"), Path.Combine(h.WorkFolder, "login.cs"));
        // One Edit takes a space off the end of a line; the other, the newline off the end of the file.
        await File.WriteAllTextAsync(spaces, "a\nb\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(newline, "a\nb", TestContext.Current.CancellationToken);
        h.Transport.Emit(Edit("e1", spaces, "a ", "a"));
        h.Transport.Emit(EditResult("e1", spaces, "a ", "a", "a \nb\n"));
        h.Transport.Emit(Edit("e2", newline, "b\n", "b"));
        h.Transport.Emit(EditResult("e2", newline, "b\n", "b", "a\nb\n"));
        var view = ShownWindow(window);
        var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "ChangedFilesList");

        await UiText.SettleUntilAsync(window, () => list.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Only the newline at its end changed: removed"),
            "the line for what the lines don't show");

        var note = list.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Only whitespace changed");
        Assert.Contains("muted", note.Classes);
        await Verify(UiText.Describe(list));
    }

    [AvaloniaFact]
    public async Task Changed_files_in_another_working_tree_are_headed_by_it()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        FakeGitFolders.Repository(h.WorkFolder, "main");
        var worktree = FakeGitFolders.Worktree(h.WorkFolder, "brisk-otter");
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        h.ShowToolWindow(ToolWindowKinds.Files);
        UiText.Settle(window);
        var (home, there) = (Path.Combine(h.WorkFolder, "auth.cs"), Path.Combine(worktree, "login.cs"));
        await File.WriteAllTextAsync(home, "b\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(there, "b\n", TestContext.Current.CancellationToken);
        h.Transport.Emit(Edit("e1", home));
        h.Transport.Emit(EditResult("e1", home));
        h.Transport.Emit(Edit("e2", there));
        h.Transport.Emit(EditResult("e2", there));
        var view = ShownWindow(window);
        var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "ChangedFilesList");

        await UiText.SettleUntilAsync(window, () => list.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "brisk-otter · worktree-brisk-otter" && t.IsEffectivelyVisible),
            "the worktree's heading");

        var heading = list.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "brisk-otter · worktree-brisk-otter");
        Assert.Contains("changedgroup", heading.Classes);
        await Verify(UiText.Describe(list));
    }

    [AvaloniaFact]
    public async Task The_arrow_keys_move_through_changed_files_without_opening_them()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        h.ShowToolWindow(ToolWindowKinds.Files);
        // The panel's pages are only in the visual tree once it has been laid out open.
        UiText.Settle(window);
        var (first, second) = (Path.Combine(h.WorkFolder, "auth.cs"), Path.Combine(h.WorkFolder, "login.cs"));
        await File.WriteAllTextAsync(first, "b\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(second, "b\n", TestContext.Current.CancellationToken);
        h.Transport.Emit(Edit("e1", first));
        h.Transport.Emit(EditResult("e1", first));
        h.Transport.Emit(Edit("e2", second));
        h.Transport.Emit(EditResult("e2", second));
        var view = ShownWindow(window);
        var list = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "ChangedFilesList");
        await UiText.SettleUntilAsync(window, () => Boxes(list).Count == 2, "the changed files");
        var opened = new List<string>();
        tab.ChangedFiles.DiffRequested += source => opened.Add(Path.GetFileName(source.Path));

        list.SelectedIndex = 0;
        list.ContainerFromIndex(0)!.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        UiText.Settle(window);

        Assert.Equal(1, list.SelectedIndex);
        Assert.Empty(opened);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        UiText.Settle(window);
        Assert.Equal(["login.cs"], opened);

        // A click opens a file, the same one again too.
        var name = list.ContainerFromIndex(0)!.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "auth.cs");
        Click(window, name);
        Click(window, name);
        Assert.Equal(["login.cs", "auth.cs", "auth.cs"], opened);
    }

    [AvaloniaFact]
    public async Task Reviewed_in_the_diff_window_ticks_the_file_and_closes_the_window()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"claudette-diff-{Guid.NewGuid():N}");
        var file = Path.Combine(folder, "auth.cs");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(file, "one\ntwo\n", TestContext.Current.CancellationToken);
        try
        {
            string? marked = "none";
            var source = new DiffSource(file, "src/auth.cs", "one\n", "compared with before Claude's first change in this session",
                OpenInDiffTool: null, () => Task.FromResult(false), () => Task.CompletedTask, () => Task.CompletedTask,
                Review: new DiffReview(() => "e1", () => false, change => marked = change));
            var window = new DiffWindow { DataContext = new DiffWindowViewModel(source, dark: false) };
            window.Show();
            var reviewed = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ReviewedButton");
            await UiText.SettleUntilAsync(window, () => ((DiffWindowViewModel)window.DataContext!).IsLoading == false, "the diff");
            var header = reviewed.FindAncestorOfType<Border>()!;
            var shown = UiText.Describe(header);
            var closed = false;
            window.Closed += (_, _) => closed = true;

            reviewed.Command!.Execute(null);
            UiText.Settle(window);

            Assert.Equal("e1", marked);
            Assert.True(closed);
            await Verify(shown);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task A_process_row_lights_up_under_the_pointer_and_while_its_menu_is_open()
    {
        await using var h = new TabTestHarness(s => s.Processes.ShowMonitor = true, dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        h.ShowToolWindow(ToolWindowKinds.Processes);
        h.Time.Advance(TimeSpan.FromSeconds(10));
        // The panel's pages are only in the visual tree once it has been laid out open.
        UiText.Settle(window);
        var view = ShownWindow(window);
        List<Border> Rows() => [.. view.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("processrow"))];
        await UiText.SettleUntilAsync(window, () => Rows().Count > 0, "the processes");
        var row = Rows()[0];
        var hover = Assert.IsAssignableFrom<ISolidColorBrush>(window.FindResource(window.ActualThemeVariant, "HoverBrush")).Color;
        Assert.Equal(Colors.Transparent, Fill(row));

        window.MouseMove(row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value);
        UiText.Settle(window);
        Assert.Equal(hover, Fill(row));

        window.MouseMove(new Point(5, 5));
        UiText.Settle(window);
        Assert.Equal(Colors.Transparent, Fill(row));

        // Its menu open, the row stays lit, so it's plain which process the menu is for.
        row.ContextMenu!.Open(row);
        UiText.Settle(window);
        Assert.Equal(hover, Fill(row));
        row.ContextMenu.Close();
        UiText.Settle(window);
        Assert.Equal(Colors.Transparent, Fill(row));
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_process_with_children_has_an_arrow_that_collapses_them_and_Alt_click_takes_every_level()
    {
        await using var h = new TabTestHarness(s => s.Processes.ShowMonitor = true, dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        // claude → bash → node → esbuild, and rg beside bash.
        var tree = h.Trees.Trees[4242];
        tree.Children.AddRange([(5001, "bash"), (5002, "node"), (5003, "esbuild"), (5004, "rg")]);
        tree.Parents[5002] = 5001;
        tree.Parents[5003] = 5002;
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        h.ShowToolWindow(ToolWindowKinds.Processes);
        h.Time.Advance(TimeSpan.FromSeconds(10));
        // The panel's pages are only in the visual tree once it has been laid out open.
        UiText.Settle(window);
        var view = ShownWindow(window);
        Button Arrow(string name) => view.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("processfold") && b.DataContext is ProcessRow row && row.Name == name);
        IEnumerable<string> Shown() => tab.ProcessMonitor.Processes.Select(p => p.Name);
        await UiText.SettleUntilAsync(window, () => view.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("processfold")) == 4, "the processes");

        // node, two levels under claude, starts collapsed; rg has nothing under it, so no arrow.
        Assert.Equal(["claude", "bash", "node", "rg"], Shown());
        Assert.True(Arrow("bash").IsEffectivelyVisible);
        Assert.Equal("Expand", AutomationProperties.GetName(Arrow("node")));
        Assert.False(Arrow("rg").IsEffectivelyVisible);

        Click(window, Arrow("bash"));
        Assert.Equal(["claude", "bash", "rg"], Shown());
        Assert.Equal("Expand", AutomationProperties.GetName(Arrow("bash")));

        // Alt+click opens every level under it, node's too.
        var at = Arrow("bash").TranslatePoint(new Point(Arrow("bash").Bounds.Width / 2, 6), window)!.Value;
        window.MouseMove(at, RawInputModifiers.Alt);
        window.MouseDown(at, MouseButton.Left, RawInputModifiers.Alt);
        window.MouseUp(at, MouseButton.Left, RawInputModifiers.Alt);
        UiText.Settle(window);
        Assert.Equal(["claude", "bash", "node", "esbuild", "rg"], Shown());
        Assert.Equal("Collapse", AutomationProperties.GetName(Arrow("node")));
        window.Close();
    }

    private static Color? Fill(Border border) => (border.Background as ISolidColorBrush)?.Color;

    private static List<CheckBox> Boxes(ListBox list) =>
        list.GetVisualDescendants().OfType<CheckBox>().Where(c => c.Classes.Contains("reviewed")).ToList();

    private static void Click(Window window, Control control)
    {
        var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        UiText.Settle(window);
    }

    [AvaloniaFact]
    public async Task A_changed_files_letter_and_counts_and_a_tool_tiles_counts_sit_on_their_names_baseline_at_any_code_size()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (window, row) = await ShowChangedFileAsync(h);
        var name = row.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "FileName");
        var aligned = row.GetVisualDescendants().OfType<TextBlock>().Where(t => Baseline.GetAlignWith(t) == name).ToList();
        // An Edit's tile has its counts beside its state (DESIGN.md §18, "Conversation cards").
        var tool = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Name == "TileState" && t.DataContext is Conversation.ToolUseItem { Name: "Edit" });
        var summary = window.GetVisualDescendants().OfType<TextBlock>().Where(t => Baseline.GetAlignWith(t) == tool && t.IsEffectivelyVisible).ToList();

        // The status letter and the counts; the tile's counts.
        Assert.Equal(2, aligned.Count);
        Assert.Single(summary);
        void AllOnBaseline()
        {
            UiText.Settle(window);
            Assert.All(aligned, text => BaselineTests.AssertOnBaseline(text, name, window));
            Assert.All(summary, text => BaselineTests.AssertOnBaseline(text, tool, window));
        }
        AllOnBaseline();
        var resources = Avalonia.Application.Current!.Resources;
        var size = resources["CodeFontSize"];
        try
        {
            resources["CodeFontSize"] = 22.0;
            AllOnBaseline();
            resources["CodeFontSize"] = 9.0;
            AllOnBaseline();
        }
        finally
        {
            resources["CodeFontSize"] = size;
        }
    }

    [AvaloniaFact]
    public async Task A_changed_files_letter_sits_midway_between_its_box_and_its_name_at_any_code_size()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var (window, row) = await ShowChangedFileAsync(h);
        var box = row.Children.OfType<CheckBox>().Single();
        var letter = row.Children.OfType<TextBlock>().Single();
        var name = row.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "FileName");

        void Midway()
        {
            UiText.Settle(window);
            var before = letter.Bounds.Left - box.Bounds.Right;
            var after = name.TranslatePoint(default, row)!.Value.X - letter.Bounds.Right;
            Assert.True(before > 0, $"The letter touches the box ({before}).");
            Assert.Equal(before, after, 0.5);
        }
        Midway();
        var resources = Avalonia.Application.Current!.Resources;
        var size = resources["CodeFontSize"];
        try
        {
            resources["CodeFontSize"] = 28.0;
            Midway();
            resources["CodeFontSize"] = 8.0;
            Midway();
        }
        finally
        {
            resources["CodeFontSize"] = size;
        }
    }

    /// <summary>A tab with its side panel open on one file Claude edited, and that file's row.</summary>
    private static async Task<(Window Window, Grid Row)> ShowChangedFileAsync(TabTestHarness h)
    {
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        h.ShowToolWindow(ToolWindowKinds.Files);
        UiText.Settle(window);
        var file = Path.Combine(h.WorkFolder, "auth.cs");
        await File.WriteAllTextAsync(file, "b\n", TestContext.Current.CancellationToken);
        h.Transport.Emit(Edit("e1", file));
        h.Transport.Emit(EditResult("e1", file));
        var list = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "ChangedFilesList");
        await UiText.SettleUntilAsync(window, () => Boxes(list).Count == 1, "the changed file");
        return (window, list.GetVisualDescendants().OfType<Grid>().Single(g => g.Classes.Contains("changedfile")));
    }

    private static JsonObject Edit(string id, string path, string from = "a", string to = "b") => new()
    {
        ["type"] = "assistant",
        ["message"] = new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "tool_use", ["id"] = id, ["name"] = "Edit", ["input"] = new JsonObject { ["file_path"] = path, ["old_string"] = from, ["new_string"] = to },
            }),
        },
    };

    private static JsonObject EditResult(string id, string path, string from = "a", string to = "b", string originalFile = "a\n") => new()
    {
        ["type"] = "user",
        ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = "The file has been updated." }) },
        ["tool_use_result"] = new JsonObject { ["filePath"] = path, ["oldString"] = from, ["newString"] = to, ["originalFile"] = originalFile },
    };

}
