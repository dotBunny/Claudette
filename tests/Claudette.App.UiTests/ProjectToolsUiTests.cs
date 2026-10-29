using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels.ToolWindows;
using Claudette.App.Views;
using Claudette.Core.ProjectTools.Unreal;
using Claudette.Core.Settings;

namespace Claudette.App.UiTests;

/// <summary>
/// Project tools rendered (DESIGN.md §18): the project's row at the sidebar's foot and its menu, with the folder's
/// links, the runs under a tab's row in the sidebar, and a run's log in the main area.
/// </summary>
public class ProjectToolsUiTests
{
    [AvaloniaFact]
    public async Task A_tab_in_an_Unreal_project_shows_the_project_on_its_groups_row_and_its_menu_lists_the_actions()
    {
        await using var h = new TabTestHarness(settings => settings.ProjectTools.SetValue(UnrealProvider.KindId, UnrealProvider.ProjectFilesKey, nameof(ProjectFileFormat.VisualStudio)), dispatcher: new AvaloniaUiDispatcher());
        UnrealFixture.Write(h.Root, h.WorkFolder);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.Project is not null, "the project");

        // The project's icon sits on its group's row, before the Project files icon, with the project in its tip; the
        // composer's bar has no project chip.
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Project tools" && b.IsEffectivelyVisible);
        Assert.Equal("ProjectButton", button.Name);
        Assert.StartsWith("NightOwl · UE 5.4 (Unreal Engine)", ToolTip.GetTip(button) as string, StringComparison.Ordinal);
        Assert.Null(button.FindAncestorOfType<TabView>());
        var files = window.GetVisualDescendants().OfType<Button>().Single(b => b is { Name: "ProjectFilesButton", IsEffectivelyVisible: true });
        Assert.Same(files.Parent, button.Parent);
        Assert.True(button.TranslatePoint(default, window)!.Value.X < files.TranslatePoint(default, window)!.Value.X);
        Assert.Equal(button.TranslatePoint(new Point(0, button.Bounds.Height / 2), window)!.Value.Y, files.TranslatePoint(new Point(0, files.Bounds.Height / 2), window)!.Value.Y, 0.5);
        // Unreal Engine's logo, at the 14 pixels Epic allowed, in the middle of the button.
        var logo = button.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.IsEffectivelyVisible);
        Assert.Contains("unreal", logo.Classes);
        Assert.Equal(14, Math.Max(logo.RenderedGeometry!.Bounds.Width, logo.RenderedGeometry.Bounds.Height), 0.5);
        var drawn = logo.TranslatePoint(logo.RenderedGeometry.Bounds.Center, button)!.Value;
        Assert.Equal(button.Bounds.Width / 2, drawn.X, 0.5);

        var flyout = Assert.IsType<Flyout>(button.Flyout);
        flyout.ShowAt(button);
        UiText.Settle(window);
        var menu = Assert.IsAssignableFrom<Control>(flyout.Content);
        await UiText.SettleUntilAsync(window, () => menu.IsEffectivelyVisible, "the menu");
        var shown = UiText.Describe(menu, (h.Root, "{root}"));
        var openSolution = menu.GetVisualDescendants().OfType<Button>().Single(b => UiText.Describe(b).Contains("Open solution", StringComparison.Ordinal));
        Assert.False(openSolution.IsEffectivelyEnabled);
        Assert.Equal("Generate project files first.", ToolTip.GetTip(openSolution));
        flyout.Hide();

        // The tab's menu leaves the project to the group's icon: it has no project entries.
        var row = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("tab") && b.DataContext == tab);
        var headers = row.ContextMenu!.Items.OfType<MenuItem>().Select(m => m.Header as string).ToList();
        Assert.Contains("Tab settings…", headers);
        Assert.DoesNotContain("NightOwl (UE 5.4)", headers);
        Assert.DoesNotContain("Add an action…", headers);

        // Verify resumes off the UI thread, so it comes last.
        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task A_tabs_runs_are_listed_under_its_row_with_Stop_while_running_and_close_once_done()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher(), launcher: launcher);
        UnrealFixture.Write(h.Root, h.WorkFolder);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.Project is not null, "the project");
        var list = window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Classes.Contains("runs"));
        Assert.False(list.IsEffectivelyVisible);

        // One that failed, then one that's running.
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(tab.ProjectTools.Actions.Single(a => a.Id == "generate-project-files"));
        launcher.Processes.Last().WriteOutput("Generating...");
        launcher.Processes.Last().Exit(6);
        var failed = tab.ProjectTools.Runs.Items.Single();
        await UiText.SettleUntilAsync(window, () => failed.Failed, "the failure");
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(tab.ProjectTools.Actions.Single(a => a.Id == "build-editor"));
        var running = tab.ProjectTools.Runs.Items[^1];
        UiText.Settle(window);

        Assert.True(list.IsEffectivelyVisible);
        var shown = UiText.Describe(list);
        var rows = list.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("runrow")).ToList();
        Assert.Equal([failed, running], rows.Select(r => r.DataContext));
        // Stop takes the close button's place while a job runs, so a stray click never ends a build.
        static Button Visible(Button row, string name) =>
            row.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == name);
        Assert.DoesNotContain(rows[1].GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Close");
        Assert.Equal("Stop: ends the job and every process it started", ToolTip.GetTip(Visible(rows[1], "Stop")));
        Assert.Equal("Build editor is running…\nStarted 12:00.", ToolTip.GetTip(rows[1]));
        // The detail, under the name, is muted, or in the error color for a failure.
        TextBlock Detail(Button row) => row.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("rundetail"));
        Assert.True(Application.Current!.TryGetResource("MutedTextBrush", window.ActualThemeVariant, out var muted));
        Assert.True(Application.Current.TryGetResource("ErrorTextBrush", window.ActualThemeVariant, out var error));
        Assert.Equal(((ISolidColorBrush)muted!).Color, ((ISolidColorBrush)Detail(rows[1]).Foreground!).Color);
        Assert.Equal(((ISolidColorBrush)error!).Color, ((ISolidColorBrush)Detail(rows[0]).Foreground!).Color);

        // A click on an entry shows its log in the main area, in the conversation's place, with no composer.
        rows[0].Command!.Execute(null);
        UiText.Settle(window);
        Assert.True(failed.IsSelected);
        Assert.Contains("selected", rows[0].Classes);
        var view = Shown<ProjectRunView>(window);
        Assert.Same(failed, view.DataContext);
        Assert.Equal("Generate project files failed (exit code 6).", view.FindControl<SelectableTextBlock>("RunStatus")!.Text);
        Assert.Contains("Generating...", Log(view).Document.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<ComposerView>(), c => c.IsEffectivelyVisible);
        // The tab's own row goes back to the conversation.
        window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("tab") && b.DataContext == tab).Command!.Execute(tab);
        UiText.Settle(window);
        Assert.False(view.IsEffectivelyVisible);
        Assert.Contains(window.GetVisualDescendants().OfType<ComposerView>(), c => c.IsEffectivelyVisible);
        // The actions are in the project's menu at the sidebar's foot, and the logs in the main area, not on the page.
        h.ShowToolWindow(ToolWindowKinds.Project);
        UiText.Settle(window);
        var page = window.GetVisualDescendants().OfType<ScrollViewer>().Single(p => p.Name == "ProjectPage");
        Assert.True(page.IsEffectivelyVisible);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), b => tab.ProjectTools.Actions.Any(a => Equals(b.Content, a.Label)));
        Assert.Empty(page.GetVisualDescendants().OfType<TextEditor>());

        // Stop, and the running one becomes a closable entry; closing takes the entry away.
        Visible(rows[1], "Stop").Command!.Execute(null);
        await UiText.SettleUntilAsync(window, () => running.IsStopped, "the stop");
        Visible(rows[1], "Close").Command!.Execute(null);
        UiText.Settle(window);
        Assert.Equal([failed], list.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("runrow")).Select(r => r.DataContext));

        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task A_long_log_follows_its_newest_line_while_the_oldest_are_dropped()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher(), launcher: launcher);
        UnrealFixture.Write(h.Root, h.WorkFolder);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.Project is not null, "the project");
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(tab.ProjectTools.Actions.Single(a => a.Id == "build-editor"));
        var run = tab.ProjectTools.Runs.Items.Single();
        run.OpenCommand.Execute(null);
        UiText.Settle(window);
        var log = Log(Shown<ProjectRunView>(window));

        // In batches, as a busy build writes them, past the limit more than once.
        var process = launcher.Processes.Last();
        for (var i = 1; i <= 6200; i++)
        {
            process.WriteOutput($"line {i}");
            if (i % 400 == 0)
            {
                await UiText.SettleUntilAsync(window, () => run.Lines()[^1] == $"line {i}", "the batch");
            }
        }
        await UiText.SettleUntilAsync(window, () => run.Lines()[^1] == "line 6200", "the output");
        UiText.Settle(window);

        // The editor keeps step with the run's lines, the oldest dropped from both.
        Assert.Equal(1500, run.OutputDropped);
        Assert.Equal(string.Join("\n", run.Output), log.Document.Text);
        // It follows the newest line, as a terminal does.
        var view = log.TextArea.TextView;
        Assert.True(view.VisualLinesValid);
        Assert.Equal(log.Document.LineCount, view.VisualLines[^1].FirstDocumentLine.LineNumber);
        Assert.DoesNotContain(view.VisualLines, l => l.FirstDocumentLine.LineNumber == 1);
        var stayCurrent = Shown<ProjectRunView>(window).FindControl<Avalonia.Controls.Primitives.ToggleButton>("StayCurrentToggle")!;
        Assert.True(stayCurrent.IsChecked);

        // Scrolled up, it stays where it was as lines come, and Stay current is off.
        var scroller = log.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.Offset = new Vector(0, 0);
        UiText.Settle(window);
        Assert.False(run.StayCurrent);
        Assert.False(stayCurrent.IsChecked);
        process.WriteOutput("line 6201");
        await UiText.SettleUntilAsync(window, () => run.Lines()[^1] == "line 6201", "the line");
        UiText.Settle(window);
        Assert.Equal(1, view.VisualLines[0].FirstDocumentLine.LineNumber);

        // Turned on again, it goes to the newest line.
        stayCurrent.IsChecked = true;
        UiText.Settle(window);
        Assert.True(run.StayCurrent);
        Assert.Equal(log.Document.LineCount, view.VisualLines[^1].FirstDocumentLine.LineNumber);

        // Scrolling back to the end turns it on too.
        scroller.Offset = new Vector(0, 0);
        UiText.Settle(window);
        Assert.False(run.StayCurrent);
        scroller.Offset = new Vector(0, scroller.Extent.Height);
        UiText.Settle(window);
        Assert.True(run.StayCurrent);
        process.Exit(0);
    }

    [AvaloniaFact]
    public async Task An_unreal_log_shows_its_lines_in_columns_and_its_filter_hides_lines()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher(), launcher: new FakeLauncher());
        var uproject = UnrealFixture.Write(h.Root, h.WorkFolder);
        var processes = new FakeSystemProcesses();
        h.Services.ProjectTools.Processes = processes;
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.Project is not null, "the project");
        var logs = Path.Combine(h.WorkFolder, "Saved", "Logs");
        Directory.CreateDirectory(logs);
        File.WriteAllLines(Path.Combine(logs, "NightOwl.log"),
        [
            "Log file open, 09/28/26 12:00:01",
            "LogInit: Display: Starting",
            "[2026.09.28-12.00.02:000][  0]LogBlueprint: Error: Missing node",
            "  on the graph's second line",
            "[2026.09.28-12.00.02:100][  1]LogTemp: Loaded the map",
            "[2026.09.28-12.00.02:200][  2]LogNet: Verbose: Tick",
        ]);
        processes.Start(new Core.ProjectTools.SystemProcess(4412, "UnrealEditor", $"UnrealEditor.exe \"{uproject}\"", h.Time.GetUtcNow()));
        h.Time.Advance(TimeSpan.FromSeconds(EditorLogService.ReadsPerCheck));
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.Logs.Count == 1 && tab.ProjectTools.Logs[0].Output.Count == 6, "the log");
        var editorLog = tab.ProjectTools.Logs[0];
        editorLog.OpenCommand.Execute(null);
        UiText.Settle(window);
        var view = Shown<EditorLogView>(window);
        var editor = view.FindControl<TextEditor>("Log")!;
        var textView = editor.TextArea.TextView;
        await UiText.SettleUntilAsync(window, () => textView.VisualLinesValid && textView.VisualLines.Count == 6, "the lines");

        // The filter: a chip for each level with its count, the categories, and the text filter.
        var filter = view.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("logfilter"));
        var described = UiText.Describe(filter);

        // The time, category and verbosity in columns, the file's text underneath: each message starts in the same place.
        AvaloniaEdit.Rendering.VisualLine Line(int number) => textView.VisualLines.Single(l => l.FirstDocumentLine.LineNumber == number);
        double MessageX(int number, string message)
        {
            var text = editor.Document.GetText(editor.Document.GetLineByNumber(number));
            return textView.GetVisualPosition(new TextViewPosition(number, text.IndexOf(message, StringComparison.Ordinal) + 1), AvaloniaEdit.Rendering.VisualYPosition.LineTop).X;
        }
        Assert.Equal(30, Line(3).Elements[0].DocumentLength);
        Assert.Equal(0, Line(2).Elements[0].DocumentLength);
        Assert.Equal(MessageX(3, "Missing node"), MessageX(2, "Starting"));
        Assert.Equal(MessageX(3, "Missing node"), MessageX(5, "Loaded the map"));
        // A line carrying the entry on starts there too, its own indent kept.
        Assert.Equal(MessageX(3, "Missing node") + 2 * textView.WideSpaceWidth, MessageX(4, "on the graph's"), 3);
        Assert.All(textView.VisualLines, l => Assert.Single(l.TextLines));
        Assert.Equal("[2026.09.28-12.00.02:000][  0]LogBlueprint: Error: Missing node", editor.Document.GetText(editor.Document.GetLineByNumber(3)));
        Assert.True(Application.Current!.TryGetResource("AccentStatusBrush", window.ActualThemeVariant, out var accent));
        Assert.Equal(((ISolidColorBrush)accent!).Color, ((ISolidColorBrush)Line(3).Elements[1].TextRunProperties.ForegroundBrush!).Color);

        // A level's chip hides its lines, and a line carrying an entry on goes with it.
        var chips = filter.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Where(b => b.Classes.Contains("logchip")).ToArray();
        chips.Single(c => c.DataContext is ViewModels.LogLevelChip { Name: "Errors" }).IsChecked = false;
        UiText.Settle(window);
        Assert.DoesNotContain("Missing node", editor.Document.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("second line", editor.Document.Text, StringComparison.Ordinal);
        Assert.Equal("Showing 4 of 6 lines", view.FindControl<TextBlock>("ShownNote")!.Text);

        // The text filter, as it's typed.
        view.FindControl<TextBox>("TextFilterBox")!.Text = "tick";
        UiText.Settle(window);
        Assert.Equal("[2026.09.28-12.00.02:200][  2]LogNet: Verbose: Tick", editor.Document.Text);

        // Wrap lines off: it scrolls sideways.
        view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("WrapToggle")!.IsChecked = false;
        UiText.Settle(window);
        Assert.False(editor.WordWrap);
        Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, editor.HorizontalScrollBarVisibility);

        await Verify(described);
    }

    [AvaloniaFact]
    public async Task A_logs_long_lines_wrap_and_its_links_open_with_a_click()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher(), launcher: launcher);
        UnrealFixture.Write(h.Root, h.WorkFolder);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.Project is not null, "the project");
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(tab.ProjectTools.Actions.Single(a => a.Id == "build-editor"));
        var run = tab.ProjectTools.Runs.Items.Single();
        run.OpenCommand.Execute(null);
        var process = launcher.Processes.Last();
        process.WriteOutput("See https://dev.epicgames.com/documentation for help.");
        process.WriteOutput("error C2065: very_long_" + string.Concat(Enumerable.Repeat("unbreakable_", 30)) + ": undeclared identifier");
        await UiText.SettleUntilAsync(window, () => run.Output.Count == 3, "the output");
        var log = Log(Shown<ProjectRunView>(window));
        var textView = log.TextArea.TextView;
        await UiText.SettleUntilAsync(window, () => textView.VisualLinesValid && textView.VisualLines.Count == 3, "the lines");

        // Long lines wrap at the width shown: nothing scrolls sideways.
        Assert.True(textView.VisualLines[2].TextLines.Count > 1, "the long line wraps");
        Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, log.HorizontalScrollBarVisibility);

        // The address is a link; a click on it opens it.
        var line = textView.VisualLines[1];
        Assert.Contains(line.Elements, e => e.GetType().Name == "LinkText");
        var start = "See ".Length + 4;
        var at = textView.GetVisualPosition(new AvaloniaEdit.TextViewPosition(2, start), AvaloniaEdit.Rendering.VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var point = textView.TranslatePoint(at, window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, Avalonia.Input.MouseButton.Left);
        await UiText.SettleUntilAsync(window, () => h.Platform.OpenedUrls.Count == 1, "the link to open");
        Assert.Equal(["https://dev.epicgames.com/documentation"], h.Platform.OpenedUrls);
        process.Exit(0);
    }

    [AvaloniaFact]
    public async Task A_running_editors_log_is_a_row_under_the_tab_and_its_errors_and_warnings_are_colored()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher(), launcher: new FakeLauncher());
        var uproject = UnrealFixture.Write(h.Root, h.WorkFolder);
        var processes = new FakeSystemProcesses();
        h.Services.ProjectTools.Processes = processes;
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.Project is not null, "the project");
        var list = window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Classes.Contains("logs"));
        Assert.False(list.IsEffectivelyVisible);

        var logs = Path.Combine(h.WorkFolder, "Saved", "Logs");
        Directory.CreateDirectory(logs);
        File.WriteAllLines(Path.Combine(logs, "NightOwl.log"),
        [
            "Log file open, 09/28/26 12:00:01",
            "LogInit: Display: Starting",
            "[2026.09.28-12.00.02:000][  0]LogBlueprint: Error: Missing node",
            "LogTemp: Warning: Slow frame",
        ]);
        processes.Start(new Core.ProjectTools.SystemProcess(4412, "UnrealEditor", $"UnrealEditor.exe \"{uproject}\"", h.Time.GetUtcNow()));
        h.Time.Advance(TimeSpan.FromSeconds(EditorLogService.ReadsPerCheck));
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.Logs.Count == 1 && tab.ProjectTools.Logs[0].Output.Count == 4, "the log");
        var editorLog = tab.ProjectTools.Logs[0];

        // A row under the tab's, with the log icon in the accent while the editor runs, and × at any time.
        Assert.True(list.IsEffectivelyVisible);
        var shown = UiText.Describe(list);
        var row = list.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("logrow"));
        Assert.Equal($"Unreal editor running since 12:00 (PID 4412).\n{Path.Combine(logs, "NightOwl.log")}", ToolTip.GetTip(row));
        var close = row.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Close");
        Assert.True(close.IsEffectivelyVisible);
        Assert.Equal(ViewModels.EditorLogViewModel.CloseTip, ToolTip.GetTip(close));
        Assert.True(Application.Current!.TryGetResource("AccentStatusBrush", window.ActualThemeVariant, out var accent));
        var icon = row.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Classes.Contains("logicon"));
        Assert.Equal(((ISolidColorBrush)accent!).Color, ((ISolidColorBrush)icon.Stroke!).Color);

        // A click shows it in the main area, with no composer: its error line in the error color, its warning in the warning color.
        row.Command!.Execute(null);
        UiText.Settle(window);
        var view = Shown<EditorLogView>(window);
        Assert.Same(editorLog, view.DataContext);
        Assert.Equal("NightOwl.log", view.FindControl<TextBlock>("LogName")!.Text);
        Assert.Equal("Unreal editor running since 12:00 (PID 4412).", view.FindControl<SelectableTextBlock>("LogStatus")!.Text);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<ComposerView>(), c => c.IsEffectivelyVisible);
        var textView = view.FindControl<TextEditor>("Log")!.TextArea.TextView;
        await UiText.SettleUntilAsync(window, () => textView.VisualLinesValid && textView.VisualLines.Count == 4, "the lines");
        // The message's, after Unreal's columns.
        IBrush? Color(int line) => textView.VisualLines.Single(l => l.FirstDocumentLine.LineNumber == line).Elements[^1].TextRunProperties.ForegroundBrush;
        Assert.True(Application.Current.TryGetResource("ErrorTextBrush", window.ActualThemeVariant, out var error));
        Assert.True(Application.Current.TryGetResource("WarningTextBrush", window.ActualThemeVariant, out var warning));
        Assert.Equal(((ISolidColorBrush)error!).Color, ((ISolidColorBrush)Color(3)!).Color);
        Assert.Equal(((ISolidColorBrush)warning!).Color, ((ISolidColorBrush)Color(4)!).Color);
        Assert.NotEqual(((ISolidColorBrush)error).Color, (Color(2) as ISolidColorBrush)?.Color);

        await Verify(shown);
    }

    /// <summary>The one <typeparamref name="T"/> on screen.</summary>
    private static T Shown<T>(Window window) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.IsEffectivelyVisible);

    /// <summary>A run's log, in its view.</summary>
    private static TextEditor Log(ProjectRunView view) => view.FindControl<TextEditor>("Log")!;

    [AvaloniaFact]
    public async Task A_folder_with_no_project_has_the_icon_with_the_folder_in_its_tip_and_its_menu_lists_the_links()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        File.WriteAllText(Path.Combine(h.WorkFolder, Core.ProjectTools.ProjectFile.SharedName), """
            { "links": [
                { "name": "Board", "url": "https://example.atlassian.net/jira/software/projects/ABC/boards/1" },
                { "name": "Pull request", "url": "https://github.com/org/repo/compare/{branch}?expand=1" },
            ] }
            """);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => tab.ProjectTools.HasLinks, "the links");

        // No provider recognized a project, and there are no actions: the icon is there all the same, with the folder in its tip.
        Assert.Null(tab.ProjectTools.Project);
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Project tools" && b.IsEffectivelyVisible);
        Assert.StartsWith(tab.Folder, ToolTip.GetTip(button) as string, StringComparison.Ordinal);

        var flyout = Assert.IsType<Flyout>(button.Flyout);
        flyout.ShowAt(button);
        UiText.Settle(window);
        var menu = Assert.IsAssignableFrom<Control>(flyout.Content);
        await UiText.SettleUntilAsync(window, () => menu.IsEffectivelyVisible, "the menu");
        var shown = UiText.Describe(menu, (h.Root, "{root}"));
        var buttons = menu.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
        var board = buttons.Single(b => UiText.Describe(b).Contains("Board", StringComparison.Ordinal));
        Assert.True(board.IsEffectivelyEnabled);
        Assert.Equal("https://example.atlassian.net/jira/software/projects/ABC/boards/1", ToolTip.GetTip(board));
        var pullRequest = buttons.Single(b => UiText.Describe(b).Contains("Pull request", StringComparison.Ordinal));
        Assert.False(pullRequest.IsEffectivelyEnabled);
        Assert.Equal("No git branch.", ToolTip.GetTip(pullRequest));
        // No Project page without project tools, so nothing to show the output of.
        Assert.DoesNotContain(buttons, b => UiText.Describe(b).Contains("Show output…", StringComparison.Ordinal));
        flyout.Hide();

        await Verify(shown);
    }
}
