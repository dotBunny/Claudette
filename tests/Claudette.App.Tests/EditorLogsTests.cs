using System.Globalization;
using Claudette.App.Conversation;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.Core.ProjectTools;

namespace Claudette.App.Tests;

/// <summary>
/// Editor logs (DESIGN.md §18, "Editor logs"): while a project's editor runs, its log is a row under one of the project's
/// tabs, read as it's written, and it stays after the editor ends. The engine and project are a few files in a temporary
/// folder, the editor a made-up process, and its log a file the test writes.
/// </summary>
public class EditorLogsTests
{
    private sealed record Fixture(TabTestHarness H, FakeLauncher Launcher, FakeSystemProcesses Processes, string UProject, string Logs) : IAsyncDisposable
    {
        public string MainLog => Path.Combine(Logs, "NightOwl.log");

        public ValueTask DisposeAsync() => H.DisposeAsync();

        /// <summary>Lets the clock run long enough for a look at the processes, and the reads after it.</summary>
        public void Look() => H.Time.Advance(TimeSpan.FromSeconds(EditorLogService.ReadsPerCheck));

        /// <summary>Starts an Unreal process with the project on its command line.</summary>
        public SystemProcess StartEditor(int pid, string name = "UnrealEditor", string arguments = "")
        {
            var process = new SystemProcess(pid, name, $"C:\\UE\\Engine\\Binaries\\Win64\\{name}.exe \"{UProject}\" {arguments}", H.Time.GetUtcNow());
            Processes.Start(process);
            return process;
        }

        /// <summary>A new log, as Unreal starts one: the line saying when it opened, then <paramref name="lines"/>.</summary>
        public void WriteLog(string path, params string[] lines)
        {
            Directory.CreateDirectory(Logs);
            var opened = H.Time.GetUtcNow().AddSeconds(1).UtcDateTime.ToString("MM/dd/yy HH:mm:ss", CultureInfo.InvariantCulture);
            File.WriteAllText(path, string.Join("\n", [$"Log file open, {opened}", .. lines]) + "\n");
        }

        public static void AppendLog(string path, params string[] lines) => File.AppendAllText(path, string.Join("\n", lines) + "\n");
    }

    private static async Task<(Fixture F, TabViewModel Tab)> OpenAsync(Action<Core.Settings.AppSettings>? configure = null)
    {
        var (h, launcher, uproject) = ProjectToolsTests.UnrealHarness(configure);
        var processes = new FakeSystemProcesses();
        h.Services.ProjectTools.Processes = processes;
        var tab = await h.OpenTabAsync();
        await TabTestHarness.Eventually(() => tab.ProjectTools.Project is not null, "the project");
        return (new Fixture(h, launcher, processes, uproject, Path.Combine(h.WorkFolder, "Saved", "Logs")), tab);
    }

    private static IReadOnlyList<EditorLogViewModel> Logs(TabViewModel tab) => InlineDispatcher.Read(() => tab.ProjectTools.Logs.ToArray());

    [Fact]
    public async Task A_running_editor_gets_a_row_under_the_tab_with_its_log_which_stays_after_it_closes()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        var h = f.H;
        f.WriteLog(f.MainLog, "LogInit: Display: Starting", "[2026.09.28-12.00.01:000][  0]LogBlueprint: Error: Missing node", "LogTemp: Warning: Slow");
        f.StartEditor(4412);
        Assert.Empty(Logs(tab));

        f.Look();

        var log = Assert.Single(Logs(tab));
        Assert.Equal("NightOwl.log", log.FileName);
        Assert.Equal(f.MainLog, log.LogPath);
        Assert.True(log.IsRunning);
        Assert.Equal("Unreal editor", log.Role);
        Assert.Contains("LogTemp: Warning: Slow", log.Lines());
        Assert.Equal(1, log.Errors);
        Assert.Equal(1, log.Warnings);
        Assert.Equal("Unreal editor · 1 error · 1 warning", log.Detail);
        Assert.Equal($"Unreal editor running since {MessageTimes.Short(h.Time.GetUtcNow(), h.Time)} (PID 4412).", log.Status);
        Assert.Null(log.Timing);
        // Its log can be shown, but appearing doesn't take the main area; the tab has a row under it now.
        Assert.Contains(log, h.Shell.OpenTabs);
        Assert.Same(tab, h.Shell.Selected);
        Assert.True(tab.HasRowsUnder);
        Assert.True(tab.ShowsLogs);
        Assert.Equal("1 log", tab.RowsUnderText);

        // New lines come as they're written, each read once.
        Fixture.AppendLog(f.MainLog, "LogTemp: Display: Loaded");
        h.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("LogTemp: Display: Loaded", log.Lines()[^1]);
        Assert.Single(log.Lines(), l => l == "LogTemp: Display: Loaded");

        // Shown, in the main area: its errors are seen.
        Assert.Equal(1, log.UnseenErrors);
        log.OpenCommand.Execute(null);
        Assert.Same(log, h.Shell.Selected);
        Assert.True(log.IsShown);
        Assert.Equal(0, log.UnseenErrors);
        Assert.Same(tab, h.Shell.ProjectTab);

        // The editor closes: the row stays, saying so, with its lines.
        Fixture.AppendLog(f.MainLog, "Log file closed, 09/28/26 12:00:09");
        f.Processes.End(4412);
        f.Look();
        Assert.False(log.IsRunning);
        Assert.Same(log, Assert.Single(Logs(tab)));
        Assert.Equal("Log file closed, 09/28/26 12:00:09", log.Lines()[^1]);
        Assert.Equal($"Closed · {MessageTimes.Short(h.Time.GetUtcNow(), h.Time)} · 1 error · 1 warning", log.Detail);
        Assert.Equal($"Unreal editor closed at {MessageTimes.Short(h.Time.GetUtcNow(), h.Time)}.", log.Status);
        Assert.NotNull(log.Timing);
    }

    [Fact]
    public async Task An_editor_starting_again_brings_the_same_row_back_with_its_new_log()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog, "LogTemp: Error: First session");
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        f.Processes.End(4412);
        f.Look();
        Assert.False(log.IsRunning);

        // As Unreal starts again: the old log becomes a backup, and a new one begins.
        File.Move(f.MainLog, Path.Combine(f.Logs, "NightOwl-backup-2026.09.28-12.00.05.log"));
        f.H.Time.Advance(TimeSpan.FromMinutes(5));
        f.StartEditor(5120);
        f.WriteLog(f.MainLog, "LogInit: Display: Second session");
        f.Look();

        Assert.Same(log, Assert.Single(Logs(tab)));
        Assert.True(log.IsRunning);
        Assert.Contains("LogInit: Display: Second session", log.Lines());
        Assert.DoesNotContain("LogTemp: Error: First session", log.Lines());
        Assert.Equal(0, log.Errors);
        Assert.Contains("(PID 5120)", log.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Closing_the_row_while_the_editor_runs_keeps_it_away_until_the_editor_starts_again()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog, "LogInit: Display: Starting");
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        log.OpenCommand.Execute(null);

        log.CloseCommand.Execute(null);

        Assert.Empty(Logs(tab));
        Assert.DoesNotContain(log, f.H.Shell.OpenTabs);
        // Its tab takes its place.
        Assert.Same(tab, f.H.Shell.Selected);
        Fixture.AppendLog(f.MainLog, "LogTemp: Display: More");
        f.Look();
        Assert.Empty(Logs(tab));

        // The editor ends and starts again: the row is back, with the new log.
        f.Processes.End(4412);
        f.Look();
        f.StartEditor(5120);
        File.Delete(f.MainLog);
        f.WriteLog(f.MainLog, "LogInit: Display: Again");
        f.Look();
        var again = Assert.Single(Logs(tab));
        Assert.Contains("LogInit: Display: Again", again.Lines());

        // Closed once the editor has ended, it's gone; the next start is a log of its own.
        f.Processes.End(5120);
        f.Look();
        again.CloseCommand.Execute(null);
        Assert.Empty(Logs(tab));
        f.StartEditor(6000);
        File.Delete(f.MainLog);
        f.WriteLog(f.MainLog, "LogInit: Display: Third");
        f.Look();
        Assert.NotSame(again, Assert.Single(Logs(tab)));
    }

    [Fact]
    public async Task Ctrl_W_on_an_editors_log_closes_it()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog);
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        log.OpenCommand.Execute(null);

        await f.H.Shell.CloseSelectedTabCommand.ExecuteAsync(null);

        Assert.Empty(Logs(tab));
        Assert.Same(tab, f.H.Shell.Selected);
        Assert.Contains(tab, f.H.Shell.AllTabs);
    }

    [Fact]
    public async Task A_commandlet_beside_the_editor_gets_a_row_for_the_log_it_writes()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog);
        f.StartEditor(4412);
        f.Look();
        f.H.Time.Advance(TimeSpan.FromMinutes(1));

        f.StartEditor(4500, "UnrealEditor-Cmd", "-run=ResavePackages");
        f.WriteLog(Path.Combine(f.Logs, "NightOwl_2.log"), "LogResave: Display: Resaving");
        f.Look();

        Assert.Equal(["NightOwl.log", "NightOwl_2.log"], Logs(tab).Select(l => l.FileName));
        Assert.Equal("Unreal commandlet ResavePackages", Logs(tab)[1].Role);
        Assert.Equal("2 logs", tab.RowsUnderText);
    }

    /// <summary>
    /// Another tab of the project in its group, at <paramref name="index"/>: made as a restored one is, so it starts no
    /// session until it's selected, and its project found.
    /// </summary>
    private static async Task<TabViewModel> AddTabAsync(Fixture f, int index)
    {
        var h = f.H;
        var tab = InlineDispatcher.Read(() =>
        {
            var other = new TabViewModel(h.Services, h.Shell, new Core.Settings.TabState { Folder = h.WorkFolder }, isRestored: true);
            h.Shell.Groups.Single().Tabs.Insert(index, other);
            h.Shell.OpenTabs.Add(other);
            return other;
        });
        await tab.ProjectTools.RefreshAsync();
        return tab;
    }

    [Fact]
    public async Task The_log_goes_under_the_tab_that_launched_the_editor_and_moves_to_another_when_that_tab_closes()
    {
        var (f, first) = await OpenAsync();
        await using var _f = f;
        var h = f.H;
        var second = await AddTabAsync(f, 1);

        await second.ProjectTools.RunActionCommand.ExecuteAsync(InlineDispatcher.Read(() => second.ProjectTools.Actions.Single(a => a.Id == "launch-editor")));
        Assert.NotEmpty(f.Launcher.Started);
        f.WriteLog(f.MainLog, "LogInit: Display: Starting");
        f.StartEditor(4412);
        f.Look();

        Assert.Empty(Logs(first));
        var log = Assert.Single(Logs(second));

        await h.Shell.CloseTabCommand.ExecuteAsync(second);

        // It keeps its lines under the other tab of the project.
        Assert.Same(log, Assert.Single(Logs(first)));
        Assert.Contains("LogInit: Display: Starting", log.Lines());
        Assert.Contains(log, h.Shell.OpenTabs);

        await h.Shell.CloseTabCommand.ExecuteAsync(first);
        Assert.DoesNotContain(log, h.Shell.OpenTabs);
    }

    [Fact]
    public async Task A_commandlet_a_tab_started_goes_under_that_tab_and_one_nobody_started_under_the_first()
    {
        var (f, started) = await OpenAsync();
        await using var _f = f;
        var h = f.H;
        // A tab of the project above it in the sidebar, which would have the log otherwise.
        var above = await AddTabAsync(f, 0);
        // Claude ran it in the tab that opened first, so it's among that tab's processes.
        h.Trees.Trees[h.Transport.ProcessId!.Value].Children.Add((4500, "UnrealEditor-Cmd"));

        f.StartEditor(4500, "UnrealEditor-Cmd", "-run=Cook");
        f.WriteLog(f.MainLog);
        f.Look();

        Assert.Empty(Logs(above));
        Assert.Single(Logs(started));

        f.StartEditor(4600, "UnrealEditor", "-game");
        f.WriteLog(Path.Combine(f.Logs, "NightOwl_2.log"));
        f.Look();
        Assert.Equal("NightOwl_2.log", Assert.Single(Logs(above)).FileName);
    }

    [Fact]
    public async Task Collapsed_the_tab_folds_its_logs_away_and_its_badge_shows_errors_not_seen_and_a_crash()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog, "LogInit: Display: Starting");
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        Assert.False(log.IsError);

        tab.ToggleRowsCollapsedCommand.Execute(null);
        Assert.True(tab.AreRowsCollapsed);
        Assert.False(tab.ShowsLogs);
        Assert.Equal(1, tab.CollapsedRowCount);
        Assert.False(tab.CollapsedIsError);

        Fixture.AppendLog(f.MainLog, "LogBlueprint: Error: Missing node");
        f.H.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(tab.CollapsedIsError);
        Assert.Contains("NightOwl.log · Unreal editor · 1 error", tab.CollapsedTip, StringComparison.Ordinal);

        // Looked at, the errors are seen; the row comes out from under the fold.
        log.OpenCommand.Execute(null);
        Assert.False(tab.AreRowsCollapsed);
        Assert.False(log.IsError);

        Fixture.AppendLog(f.MainLog, "LogWindows: Error: === Critical error: ===");
        f.H.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(log.IsCrashed);
        Assert.True(log.IsError);
        Assert.StartsWith("Crashed", log.Detail, StringComparison.Ordinal);
        Assert.StartsWith("Unreal editor crashed", log.Status, StringComparison.Ordinal);
    }

    /// <summary>
    /// Settings → Project tools → Show editor logs under the tab, changed on the UI thread as the Settings window does:
    /// saving follows the logs again there, and so does the project's refresh it starts, which would otherwise race it for
    /// the same rows.
    /// </summary>
    private static void ShowEditorLogs(Fixture f, bool show) => InlineDispatcher.Run(() =>
    {
        f.H.Services.Settings.ProjectTools.ShowEditorLogs = show;
        f.H.Services.SaveSettings();
    });

    [Fact]
    public async Task With_the_setting_off_there_are_no_logs_and_turning_it_off_takes_them_away()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog);
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        log.OpenCommand.Execute(null);

        ShowEditorLogs(f, false);

        Assert.Empty(Logs(tab));
        Assert.DoesNotContain(log, f.H.Shell.OpenTabs);
        Assert.Same(tab, f.H.Shell.Selected);
        f.Look();
        Assert.Empty(Logs(tab));

        ShowEditorLogs(f, true);
        f.Look();
        Assert.Single(Logs(tab));
    }

    // ---- Filtering it (DESIGN.md §18, "Editor logs", "Filtering it") ----------------------------------------------------

    private static readonly string[] UnrealLines =
    [
        "[2026.09.28-12.00.02:000][  0]LogInit: Display: Starting",
        "[2026.09.28-12.00.02:001][  0]LogBlueprint: Error: Missing node",
        "[2026.09.28-12.00.02:002][  0]LogNet: Verbose: Tick",
        "[2026.09.28-12.00.02:003][  0]LogNet: Warning: Lag",
        "[2026.09.28-12.00.02:004][  1]LogTemp: Loaded the map",
    ];

    [Fact]
    public async Task The_log_has_a_chip_for_each_level_and_its_categories_with_their_counts()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog, UnrealLines);
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));

        Assert.Equal(["Errors", "Warnings", "Display", "Log", "Verbose"], log.Levels.Select(l => l.Name));
        // "Log file open" is a Log line too.
        Assert.Equal([1, 1, 1, 2, 1], log.Levels.Select(l => l.Count));
        Assert.All(log.Levels, l => Assert.True(l.IsShown));
        Assert.True(log.HasCategories);
        Assert.Equal(["LogBlueprint", "LogInit", "LogNet", "LogTemp"], log.Categories.Select(c => c.Name));
        Assert.Equal(2, log.Categories.Single(c => c.Name == "LogNet").Count);
        Assert.Equal("Categories", log.CategoriesLabel);
        Assert.Equal("LogBlueprint".Length, log.CategoryColumn);
        Assert.True(log.HasTimeColumn);
        Assert.Equal("12:00:02.001", log.Output[2].Columns!.Time);
        Assert.False(log.IsFiltered);
        Assert.Null(log.ShownNote);
        Assert.True(log.StayCurrent);

        // A category that comes later takes its place by name, and counts go on.
        Fixture.AppendLog(f.MainLog, "[2026.09.28-12.00.03:000][  2]LogAudio: Warning: No device");
        f.H.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(["LogAudio", "LogBlueprint", "LogInit", "LogNet", "LogTemp"], log.Categories.Select(c => c.Name));
        Assert.Equal(2, log.Levels[1].Count);
    }

    [Fact]
    public async Task Hiding_a_level_a_category_or_lines_without_some_text_hides_their_lines()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog, UnrealLines);
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        var changes = 0;
        log.FilterChanged += () => changes++;
        IEnumerable<string> Shown() => log.Output.Where(log.Shows).Select(l => l.Text[(l.Columns?.MessageStart ?? 0)..]);

        log.Levels.Single(l => l.Name == "Verbose").IsShown = false;
        Assert.Equal(1, changes);
        Assert.DoesNotContain("Tick", Shown());
        Assert.True(log.IsFiltered);
        Assert.Equal("Showing 5 of 6 lines", log.ShownNote);
        Assert.Equal("Verbose hidden: click to show them", log.Levels[^1].Tip);

        log.Categories.Single(c => c.Name == "LogNet").IsShown = false;
        Assert.Equal(2, changes);
        Assert.DoesNotContain("Lag", Shown());
        Assert.Equal("Categories (3 of 4)", log.CategoriesLabel);

        log.TextFilter = "MAP";
        Assert.Equal(3, changes);
        Assert.Equal(["Loaded the map"], Shown());
        Assert.Equal("Showing 1 of 6 lines", log.ShownNote);

        // Copy takes the lines shown, as the file has them.
        await log.CopyOutputCommand.ExecuteAsync(null);
        Assert.Equal("[2026.09.28-12.00.02:004][  1]LogTemp: Loaded the map", f.H.Platform.Clipboard);

        log.ClearTextFilterCommand.Execute(null);
        Assert.Equal("", log.TextFilter);
        Assert.Equal(4, Shown().Count());

        // The menu's search, and Show all and Hide all for what it finds.
        log.CategorySearch = "log";
        Assert.Equal(4, log.FoundCategories.Count);
        log.CategorySearch = "BLUE";
        Assert.Equal(["LogBlueprint"], log.FoundCategories.Select(c => c.Name));
        log.HideAllCategoriesCommand.Execute(null);
        Assert.Equal(5, changes);
        Assert.Equal("Categories (2 of 4)", log.CategoriesLabel);
        log.CategorySearch = "nothing like it";
        Assert.True(log.NoCategoryFound);
        log.CategorySearch = "";
        log.ShowAllCategoriesCommand.Execute(null);
        Assert.All(log.Categories, c => Assert.True(c.IsShown));
        log.Levels[^1].IsShown = true;
        Assert.False(log.IsFiltered);
    }

    [Fact]
    public async Task The_levels_and_categories_hidden_are_the_projects_and_its_next_log_starts_with_them()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog, UnrealLines);
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        var state = f.H.Services.State.ProjectTools;

        log.Levels.Single(l => l.Name == "Display").IsShown = false;
        log.Levels.Single(l => l.Name == "Verbose").IsShown = false;
        log.Categories.Single(c => c.Name == "LogNet").IsShown = false;
        log.TextFilter = "only this log's";
        Assert.Equal("Display,Verbose", state.Get(log.ProjectPath, EditorLogViewModel.HiddenLevelsKey));
        Assert.Equal("LogNet", state.Get(log.ProjectPath, EditorLogViewModel.HiddenCategoriesKey));

        // A commandlet's log, beside the editor's: the project's levels and categories, but not the text.
        f.H.Time.Advance(TimeSpan.FromMinutes(1));
        f.StartEditor(4500, "UnrealEditor-Cmd", "-run=ResavePackages");
        f.WriteLog(Path.Combine(f.Logs, "NightOwl_2.log"), "[2026.09.28-12.01.02:000][  0]LogNet: Display: Resaving", "[2026.09.28-12.01.02:001][  0]LogTemp: Warning: Slow");
        f.Look();
        var commandlet = Logs(tab)[1];
        Assert.Equal([true, true, false, true, false], commandlet.Levels.Select(l => l.IsShown));
        Assert.False(commandlet.Categories.Single(c => c.Name == "LogNet").IsShown);
        Assert.Equal("", commandlet.TextFilter);
        Assert.Equal(["[2026.09.28-12.01.02:001][  0]LogTemp: Warning: Slow"], commandlet.Output.Where(commandlet.Shows).Select(l => l.Text).Skip(1));

        // Showing them all again forgets them.
        foreach (var level in log.Levels)
        {
            level.IsShown = true;
        }
        log.ShowAllCategoriesCommand.Execute(null);
        Assert.Null(state.Get(log.ProjectPath, EditorLogViewModel.HiddenLevelsKey));
        Assert.Null(state.Get(log.ProjectPath, EditorLogViewModel.HiddenCategoriesKey));
    }

    [Fact]
    public async Task A_new_log_in_its_place_counts_afresh_and_keeps_its_filter()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog, UnrealLines);
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        log.Categories.Single(c => c.Name == "LogNet").IsShown = false;
        f.Processes.End(4412);
        f.Look();

        File.Move(f.MainLog, Path.Combine(f.Logs, "NightOwl-backup-2026.09.28-12.00.05.log"));
        f.H.Time.Advance(TimeSpan.FromMinutes(5));
        f.StartEditor(5120);
        f.WriteLog(f.MainLog, "[2026.09.28-12.05.02:000][  0]LogNet: Warning: Again", "[2026.09.28-12.05.02:001][  0]LogInit: Display: Second session");
        f.Look();

        Assert.Equal([0, 1, 1, 1, 0], log.Levels.Select(l => l.Count));
        Assert.Equal(["LogInit", "LogNet"], log.Categories.Select(c => c.Name));
        Assert.False(log.Categories.Single(c => c.Name == "LogNet").IsShown);
        Assert.DoesNotContain(log.Output, l => l.Text.EndsWith("Again", StringComparison.Ordinal) && log.Shows(l));
    }

    [Fact]
    public async Task Each_log_wraps_its_lines_as_the_last_one_was_left()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.WriteLog(f.MainLog, UnrealLines);
        f.StartEditor(4412);
        f.Look();
        var log = Assert.Single(Logs(tab));
        Assert.True(log.WordWrap);

        log.WordWrap = false;
        Assert.False(f.H.Services.State.LogWordWrap);

        f.H.Time.Advance(TimeSpan.FromMinutes(1));
        f.StartEditor(4500, "UnrealEditor-Cmd", "-run=ResavePackages");
        f.WriteLog(Path.Combine(f.Logs, "NightOwl_2.log"), "LogResave: Display: Resaving");
        f.Look();
        Assert.False(Logs(tab)[1].WordWrap);
    }

    [Fact]
    public async Task Another_projects_editor_gets_no_row()
    {
        var (f, tab) = await OpenAsync();
        await using var _f = f;
        f.Processes.Start(new SystemProcess(4412, "UnrealEditor", "UnrealEditor.exe D:/Other/Other.uproject", f.H.Time.GetUtcNow()));
        f.WriteLog(f.MainLog);

        f.Look();

        Assert.Empty(Logs(tab));
        Assert.False(tab.HasRowsUnder);
    }
}
