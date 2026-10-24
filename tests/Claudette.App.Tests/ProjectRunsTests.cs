using System.ComponentModel;
using Claudette.App.Conversation;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.Core.ProjectTools;
using Claudette.Core.Settings;

namespace Claudette.App.Tests;

/// <summary>
/// A tab's project runs (DESIGN.md §4, "Sidebar"; §18, "Project tools"): each job makes an entry under the tab's row
/// with its own log, which stays, whatever the result, until the user closes it, and shows in the main area when it's
/// clicked. The engine is a few files in a temporary folder, and every process is a fake.
/// </summary>
public class ProjectRunsTests
{
    private static async Task<(TabTestHarness H, FakeLauncher Launcher, TabViewModel Tab)> OpenAsync()
    {
        var (h, launcher, _) = ProjectToolsTests.UnrealHarness();
        var tab = await h.OpenTabAsync();
        await TabTestHarness.Eventually(() => tab.ProjectTools.Project is not null, "the project");
        return (h, launcher, tab);
    }

    private static ProjectAction Action(TabViewModel tab, string id) => InlineDispatcher.Read(() => tab.ProjectTools.Actions.Single(a => a.Id == id));

    private static IReadOnlyList<ProjectRunViewModel> Runs(TabViewModel tab) => InlineDispatcher.Read(() => tab.ProjectTools.Runs.Items.ToArray());

    /// <summary>Runs <paramref name="id"/> and ends its process with <paramref name="exitCode"/>.</summary>
    private static async Task<ProjectRunViewModel> RunToEndAsync(TabViewModel tab, FakeLauncher launcher, string id, int exitCode, string? line = null)
    {
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, id));
        var run = Runs(tab)[^1];
        var process = launcher.Processes.Last();
        if (line is not null)
        {
            process.WriteOutput(line);
        }
        process.Exit(exitCode);
        await TabTestHarness.Eventually(() => !run.IsRunning, $"{run.Name} to end");
        return run;
    }

    /// <summary>A tab in another group after the first, whose folder is gone, so it starts no session.</summary>
    private static TabViewModel AddOtherTab(TabTestHarness h)
    {
        var other = new TabViewModel(h.Services, h.Shell, new TabState { Folder = Path.Combine(h.Root, "elsewhere") }, isRestored: false);
        var group = new TabGroupViewModel(other.Folder, TabGroupViewModel.Palette[1].Color, isCollapsed: false);
        group.Tabs.Add(other);
        h.Shell.Groups.Add(group);
        h.Shell.OpenTabs.Add(other);
        return other;
    }

    [Fact]
    public async Task Each_job_makes_an_entry_that_keeps_its_log_after_it_ends()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        Assert.False(tab.ProjectTools.Runs.HasRuns);

        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "build-editor"));

        var build = Assert.Single(Runs(tab));
        Assert.True(tab.ProjectTools.Runs.HasRuns);
        Assert.Same(build, tab.ProjectTools.Runs.RunningRun);
        // Its log can be shown, but starting doesn't take the main area from the conversation.
        Assert.Contains(build, h.Shell.OpenTabs);
        Assert.Same(tab, h.Shell.Selected);
        Assert.False(build.IsShown);
        Assert.Equal("Build editor", build.Name);
        Assert.Equal(h.WorkFolder, build.Folder);
        Assert.Equal(h.Time.GetUtcNow(), build.Started);
        Assert.True(build.IsRunning);
        Assert.Equal("●", build.Glyph);
        Assert.Equal("Running · 0s", build.Detail);
        Assert.Equal($"Started {MessageTimes.Short(build.Started, h.Time)} · 0s so far", build.Timing);
        Assert.False(build.CloseCommand.CanExecute(null));

        // The running time ticks from the injected clock.
        var ticks = 0;
        build.PropertyChanged += (_, e) => ticks += e.PropertyName == nameof(ProjectRunViewModel.Detail) ? 1 : 0;
        h.Time.Advance(TimeSpan.FromSeconds(65));
        Assert.True(ticks > 0);
        Assert.Equal("Running · 1m 05s", build.Detail);

        launcher.Processes.Last().WriteOutput("Building NightOwlEditor...");
        await TabTestHarness.Eventually(() => build.Lines().Contains("Building NightOwlEditor..."), "the output");
        launcher.Processes.Last().Exit(0);
        await TabTestHarness.Eventually(() => build.Succeeded, "the end");

        // It stays, with its log, after it ends.
        Assert.Same(build, Assert.Single(Runs(tab)));
        Assert.Null(tab.ProjectTools.Runs.RunningRun);
        Assert.Equal("✓", build.Glyph);
        Assert.Equal($"Succeeded · {MessageTimes.Short(h.Time.GetUtcNow(), h.Time)}", build.Detail);
        Assert.Equal("Build editor succeeded.", build.Lines()[^1]);
        Assert.Contains("took 1m 05s", build.Tip, StringComparison.Ordinal);
        Assert.Equal($"Started {MessageTimes.Short(build.Started, h.Time)} · took 1m 05s", build.Timing);
        Assert.True(build.CloseCommand.CanExecute(null));
        Assert.False(build.StopCommand.CanExecute(null));

        // A second job makes a second entry; the first keeps its log.
        var generate = await RunToEndAsync(tab, launcher, "generate-project-files", 6, "Generating...");

        Assert.Equal([build, generate], Runs(tab));
        Assert.Same(tab, h.Shell.Selected);
        Assert.Contains("Building NightOwlEditor...", build.Lines());
        Assert.DoesNotContain("Building NightOwlEditor...", generate.Lines());
        Assert.Contains("Generating...", generate.Lines());
        Assert.Equal("✕", generate.Glyph);
        Assert.True(generate.Failed);
        Assert.Equal(6, generate.ExitCode);
        Assert.StartsWith("Failed · exit code 6 · ", generate.Detail, StringComparison.Ordinal);
        Assert.Equal("Generate project files failed (exit code 6).", generate.Status);
    }

    [Fact]
    public async Task Closing_an_entry_takes_it_and_its_log_away_and_its_tab_takes_the_logs_place()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var first = await RunToEndAsync(tab, launcher, "build-editor", 0);
        var second = await RunToEndAsync(tab, launcher, "generate-project-files", 1);
        var third = await RunToEndAsync(tab, launcher, "build-editor", 2);

        first.OpenCommand.Execute(null);
        Assert.Same(first, h.Shell.Selected);

        first.CloseCommand.Execute(null);

        Assert.Equal([second, third], Runs(tab));
        Assert.DoesNotContain(first, h.Shell.OpenTabs);
        Assert.Same(tab, h.Shell.Selected);
        Assert.Equal([(IShellTab)tab], h.Shell.Panes);

        // Closing one that isn't showing leaves the main area alone.
        second.CloseCommand.Execute(null);
        Assert.Equal([third], Runs(tab));
        Assert.Same(tab, h.Shell.Selected);

        third.CloseCommand.Execute(null);
        Assert.Empty(Runs(tab));
        Assert.False(tab.ProjectTools.Runs.HasRuns);
        Assert.DoesNotContain(h.Shell.OpenTabs, t => t is ProjectRunViewModel);
    }

    [Fact]
    public async Task A_running_entry_cant_be_closed_but_can_be_stopped_and_then_closed()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "build-editor"));
        var run = Runs(tab).Single();
        var process = launcher.Processes.Last();

        Assert.False(run.CloseCommand.CanExecute(null));
        run.CloseCommand.Execute(null);
        Assert.Same(run, Runs(tab).Single());
        Assert.False(process.Killed);

        Assert.True(run.StopCommand.CanExecute(null));
        run.StopCommand.Execute(null);
        await TabTestHarness.Eventually(() => run.IsStopped, "the stop");

        Assert.True(process.Killed);
        Assert.Equal("■", run.Glyph);
        Assert.StartsWith("Stopped · ", run.Detail, StringComparison.Ordinal);
        Assert.Same(run, Runs(tab).Single());
        Assert.True(run.CloseCommand.CanExecute(null));
        run.CloseCommand.Execute(null);
        Assert.Empty(Runs(tab));
    }

    [Fact]
    public async Task A_job_that_cant_start_makes_a_failed_entry_that_says_why()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        launcher.OnStart = (spec, _) =>
        {
            if (spec.TrackProcessTree)
            {
                throw new Win32Exception("No such file or directory");
            }
        };

        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "build-editor"));

        var run = Runs(tab).Single();
        Assert.Contains(run, h.Shell.OpenTabs);
        Assert.False(tab.ProjectTools.Runs.IsJobRunning);
        Assert.True(run.Failed);
        Assert.Null(run.ExitCode);
        Assert.Null(run.Timing);
        Assert.Equal("Build editor couldn't start: No such file or directory", run.Status);
        Assert.Equal("Couldn't start it: No such file or directory", run.Lines()[^1]);
        Assert.StartsWith("$ ", run.Lines()[0], StringComparison.Ordinal);
        Assert.StartsWith("Couldn't start · ", run.Detail, StringComparison.Ordinal);
        Assert.True(run.CloseCommand.CanExecute(null));
        Assert.Contains(tab.Items, i => i is NoteItem note && note.Text.StartsWith("Couldn't start Build editor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cleans_deletion_makes_an_entry_and_Launch_editor_doesnt()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        Directory.CreateDirectory(Path.Combine(h.WorkFolder, "Intermediate"));
        await tab.ProjectTools.RefreshCommand.ExecuteAsync(null);

        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "launch-editor"));

        Assert.True(launcher.Started.Last().Detached);
        Assert.Empty(Runs(tab));

        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "clean"));
        await h.Shell.Confirmation!.ConfirmCommand.ExecuteAsync(null);
        await TabTestHarness.Eventually(() => Runs(tab) is [{ Succeeded: true }], "the clean");

        var clean = Runs(tab).Single();
        Assert.Equal("Clean intermediates", clean.Name);
        Assert.Contains("Deleted 1 folder.", clean.Lines());
        Assert.False(Directory.Exists(Path.Combine(h.WorkFolder, "Intermediate")));
    }

    [Fact]
    public async Task Clicking_an_entry_shows_its_log_in_the_main_area_and_the_tabs_row_goes_back()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var build = await RunToEndAsync(tab, launcher, "build-editor", 0);
        var generate = await RunToEndAsync(tab, launcher, "generate-project-files", 0);
        var other = AddOtherTab(h);
        h.Shell.SelectedTab = other;

        build.OpenCommand.Execute(null);

        // In the focused pane, in the other tab's place, with no composer and no side panel.
        Assert.Same(build, h.Shell.Selected);
        Assert.Same(build, h.Shell.SelectedRun);
        Assert.Null(h.Shell.SelectedTab);
        Assert.Equal([(IShellTab)build], h.Shell.Panes);
        Assert.True(build.IsSelected);
        Assert.True(build.IsShown);
        Assert.False(generate.IsSelected);
        Assert.False(other.IsShown);
        Assert.False(tab.IsSidePanelOpen);
        Assert.False(h.Shell.Layout.IsSidePanelShown);
        // The sidebar's foot shows its tab's project, and the app state keeps its tab: runs aren't saved.
        Assert.Same(tab, h.Shell.ProjectTab);
        Assert.Equal(tab.Id, h.Services.State.SelectedTabId);

        await build.CopyOutputCommand.ExecuteAsync(null);
        Assert.EndsWith("Build editor succeeded.", h.Platform.Clipboard, StringComparison.Ordinal);

        // The tab's own row goes back to its conversation, in the log's place.
        h.Shell.SelectCommand.Execute(tab);
        Assert.Same(tab, h.Shell.Selected);
        Assert.Equal([(IShellTab)tab], h.Shell.Panes);
        Assert.False(build.IsShown);

        // A new job doesn't take the main area.
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "build-editor"));
        Assert.Same(tab, h.Shell.Selected);
        launcher.Processes.Last().Exit(0);
    }

    [Fact]
    public async Task A_log_opens_beside_the_conversation_and_takes_part_in_the_panes()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "build-editor"));
        var build = Runs(tab).Single();

        h.Shell.OpenBesideCommand.Execute(build);

        Assert.Equal([tab, (IShellTab)build], h.Shell.Panes);
        Assert.Same(build, h.Shell.Selected);
        Assert.True(build.IsInSplit);
        Assert.Equal(1, build.PaneIndex);
        Assert.True(tab.IsShown);
        // Only the tab is kept as a pane: the log is gone after a restart.
        Assert.Null(h.Services.State.PaneTabIds);

        // A click in the conversation's pane focuses it, and the log stays beside it.
        tab.FocusPane();
        Assert.Same(tab, h.Shell.Selected);
        Assert.True(build.IsShown);
        build.FocusPane();
        Assert.Same(build, h.Shell.Selected);

        build.ClosePaneCommand.Execute(null);
        Assert.Equal([(IShellTab)tab], h.Shell.Panes);
        Assert.Same(tab, h.Shell.Selected);
        Assert.Same(build, Runs(tab).Single());
        launcher.Processes.Last().Exit(0);
    }

    [Fact]
    public async Task Close_tab_closes_a_finished_runs_log_and_leaves_a_running_one_for_its_tab()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var finished = await RunToEndAsync(tab, launcher, "build-editor", 0);
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "generate-project-files"));
        var running = Runs(tab)[^1];

        // Running: the key goes back to the tab, and never stops the job.
        running.OpenCommand.Execute(null);
        await h.Shell.CloseSelectedTabCommand.ExecuteAsync(null);
        Assert.Same(tab, h.Shell.Selected);
        Assert.True(running.IsRunning);
        Assert.Equal([finished, running], Runs(tab));

        // Finished: it closes, as its × does.
        finished.OpenCommand.Execute(null);
        await h.Shell.CloseSelectedTabCommand.ExecuteAsync(null);
        Assert.Same(tab, h.Shell.Selected);
        Assert.Equal([running], Runs(tab));
        launcher.Processes.Last().Exit(0);
    }

    [Fact]
    public async Task Next_and_previous_tab_go_from_a_log_to_the_tabs_around_its_entry()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var build = await RunToEndAsync(tab, launcher, "build-editor", 0);
        var other = AddOtherTab(h);

        build.OpenCommand.Execute(null);
        h.Shell.SelectNextCommand.Execute(null);
        Assert.Same(other, h.Shell.Selected);

        build.OpenCommand.Execute(null);
        h.Shell.SelectPreviousCommand.Execute(null);
        Assert.Same(tab, h.Shell.Selected);
    }

    [Fact]
    public async Task Show_output_shows_the_newest_log_once_something_has_run()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        ProjectMenuEntry ShowOutput() => InlineDispatcher.Read(() => tab.ProjectTools.Menu.Single(e => e.Label == "Show output…"));
        Assert.False(ShowOutput().IsEnabled);
        Assert.Equal("No action has run in this tab yet", ShowOutput().Tip);

        await RunToEndAsync(tab, launcher, "build-editor", 0);
        var newest = await RunToEndAsync(tab, launcher, "generate-project-files", 0);

        Assert.True(ShowOutput().IsEnabled);
        ShowOutput().Command!.Execute(null);
        Assert.Same(newest, h.Shell.Selected);
    }

    [Fact]
    public async Task A_notification_click_shows_the_log_of_the_run_that_finished()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var failed = await RunToEndAsync(tab, launcher, "build-editor", 6);
        await TabTestHarness.Eventually(() => h.Notifier.Last("ProjectAction:") is not null, "the notification");
        // In front now: the next one doesn't notify, so the notification is still about the first.
        h.Services.Notifications.SetAppActive(true);
        var shown = h.Notifier.Shown.Count;
        await RunToEndAsync(tab, launcher, "generate-project-files", 0);
        Assert.Equal(shown, h.Notifier.Shown.Count);

        InlineDispatcher.Read(() =>
        {
            tab.ProjectTools.Runs.OpenNotifiedRun();
            return true;
        });

        Assert.Same(failed, h.Shell.Selected);
        Assert.False(tab.IsSidePanelOpen);
        // Looked at now: its notification is no longer news.
        Assert.Contains(h.Notifier.Removed, id => id.StartsWith("ProjectAction:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Closing_an_entry_takes_its_notification_away()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var failed = await RunToEndAsync(tab, launcher, "build-editor", 6);
        await TabTestHarness.Eventually(() => h.Notifier.Last("ProjectAction:") is not null, "the notification");

        failed.CloseCommand.Execute(null);

        Assert.Contains(h.Notifier.Removed, id => id.StartsWith("ProjectAction:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_logs_web_addresses_are_links_and_its_paths_once_theyre_found()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var source = Path.Combine(h.WorkFolder, "Source", "NightOwl", "Player.cpp");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, string.Concat(Enumerable.Range(1, 50).Select(i => $"line {i}\n")));
        var build = await RunToEndAsync(tab, launcher, "build-editor", 6);
        var found = 0;
        build.LinksFound += () => found++;

        // A web address is a link at once.
        var web = Assert.Single(build.LinksIn("See https://dev.epicgames.com/documentation for help."));
        await build.OpenLinkCommand.ExecuteAsync(web);
        Assert.Equal(["https://dev.epicgames.com/documentation"], h.Platform.OpenedUrls);

        // A path is looked for first, from the folder the job ran in, and is a link once it's found.
        const string Error = "Source/NightOwl/Player.cpp(42,7): error C2065: 'Speed': undeclared identifier";
        Assert.Empty(build.LinksIn(Error));
        await TabTestHarness.Eventually(() => found > 0, "the path to be found");
        var link = Assert.Single(build.LinksIn(Error));
        Assert.Equal(("Source/NightOwl/Player.cpp(42,7)", 42, 7), (Error.Substring(link.Start, link.Length), link.File!.Line, link.File.Column));

        // A path that isn't there never is.
        const string Missing = "Wrote Saved/Logs/Missing.log";
        Assert.Empty(build.LinksIn(Missing));
        await Waiting.NeverAsync(() => found > 1, "a path that isn't there to be found");
        Assert.Empty(build.LinksIn(Missing));

        // A click opens the file in the project's Files tab, beside the log, at the line named: on the UI thread, as in the
        // app, so the explorer the tab opens with lists its folder there too.
        build.OpenCommand.Execute(null);
        await InlineDispatcher.RunAsync(() => build.OpenLinkCommand.ExecuteAsync(link));
        var files = h.Shell.Groups.Single(g => g.Tabs.Contains(tab)).Files!;
        await TabTestHarness.Eventually(() => files.Showing?.Path == source, "the file");
        Assert.Equal([build, (IShellTab)files], h.Shell.Panes);
        Assert.Equal(42, files.Showing!.CaretLine);
    }

    [Fact]
    public async Task Closing_a_tab_whose_log_has_the_focus_moves_the_focus_on()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var build = await RunToEndAsync(tab, launcher, "build-editor", 0);
        var other = AddOtherTab(h);
        build.OpenCommand.Execute(null);

        await h.Shell.CloseTabCommand.ExecuteAsync(tab);
        if (h.Shell.Confirmation is { } confirmation)
        {
            await confirmation.ConfirmCommand.ExecuteAsync(null);
        }

        Assert.Same(other, h.Shell.Selected);
        Assert.DoesNotContain(build, h.Shell.OpenTabs);
        Assert.Equal([(IShellTab)other], h.Shell.Panes);
    }

    [Fact]
    public async Task Collapsing_the_tab_hides_its_runs_until_a_log_is_shown()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        var build = await RunToEndAsync(tab, launcher, "build-editor", 6);
        Assert.True(tab.ShowsRuns);
        Assert.Equal("Collapse: hides 1 run", tab.CollapseTip);

        tab.ToggleRowsCollapsedCommand.Execute(null);

        // Out of the sidebar, with the failure on the tab's row (DESIGN.md §4, "Sidebar").
        Assert.False(tab.ShowsRuns);
        Assert.Equal("✕", tab.CollapsedGlyph);
        Assert.True(tab.CollapsedIsError);
        Assert.Equal($"Collapsed: 1 run\nBuild editor · {build.Detail}", tab.CollapsedTip);

        // A run starting doesn't open it.
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "generate-project-files"));
        Assert.True(tab.AreRowsCollapsed);
        Assert.Equal(2, tab.CollapsedRowCount);
        launcher.Processes.Last().Exit(0);
        await TabTestHarness.Eventually(() => !tab.ProjectTools.Runs.IsJobRunning, "the second run to end");

        // A log shown, from Show output… or its notification, brings its entry out.
        build.OpenCommand.Execute(null);

        Assert.Same(build, h.Shell.Selected);
        Assert.False(tab.AreRowsCollapsed);
        Assert.True(tab.ShowsRuns);

        // Once the last entry is closed, there's nothing to collapse: the next run shows.
        tab.ToggleRowsCollapsedCommand.Execute(null);
        foreach (var run in Runs(tab))
        {
            run.CloseCommand.Execute(null);
        }
        Assert.False(tab.HasRowsUnder);
        Assert.False(tab.State.RowsCollapsed);
    }

    [Fact]
    public async Task Closing_the_tab_stops_a_running_job_and_drops_every_entry()
    {
        var (h, launcher, tab) = await OpenAsync();
        await using var _h = h;
        await RunToEndAsync(tab, launcher, "build-editor", 0);
        await tab.ProjectTools.RunActionCommand.ExecuteAsync(Action(tab, "generate-project-files"));
        var running = Runs(tab)[^1];
        var process = launcher.Processes.Last();
        Assert.Equal(2, Runs(tab).Count);
        var notified = h.Notifier.Shown.Count;

        await h.Shell.CloseTabCommand.ExecuteAsync(tab);
        if (h.Shell.Confirmation is { } confirmation)
        {
            // The job's processes are the tab's: stopped with it.
            await confirmation.ConfirmCommand.ExecuteAsync(null);
        }
        await TabTestHarness.Eventually(() => running.IsStopped, "the stop");

        Assert.Empty(Runs(tab));
        Assert.False(tab.ProjectTools.Runs.HasRuns);
        Assert.DoesNotContain(h.Shell.OpenTabs, t => t is ProjectRunViewModel);
        Assert.True(process.Killed);
        Assert.Equal(notified, h.Notifier.Shown.Count);
    }
}
