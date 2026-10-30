using System.Text.Json.Nodes;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;

namespace Claudette.App.Tests;

/// <summary>Claude Code's projects in the sidebar, and the offer to show them (DESIGN.md §4, "Claude Code's projects").</summary>
public class ClaudeProjectsTests
{
    private static string UserLine(string sessionId, string text, string cwd, string time = "2026-09-28T10:00:00Z") => new JsonObject
    {
        ["type"] = "user",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = text },
        ["cwd"] = cwd,
        ["sessionId"] = sessionId,
        ["timestamp"] = time,
    }.ToJsonString();

    /// <summary>Two folders with sessions: the work folder's three, and another folder's one, used more recently.</summary>
    private static string WriteProjects(TabTestHarness h)
    {
        var other = Path.Combine(h.Root, "other");
        Directory.CreateDirectory(other);
        h.WriteTranscript("w1", UserLine("w1", "Fix the login bug", h.WorkFolder, "2026-09-28T09:00:00Z"));
        h.WriteTranscript("w2", UserLine("w2", "Add the settings page", h.WorkFolder, "2026-09-28T10:00:00Z"));
        h.WriteTranscript("w3", UserLine("w3", "Write the release notes", h.WorkFolder, "2026-09-28T11:00:00Z"));
        h.WriteTranscript("o1", UserLine("o1", "Rename the parser", other, "2026-09-28T11:30:00Z"));
        return other;
    }

    /// <summary>
    /// Restores as the app does, on the UI thread, so the read of Claude Code's sessions lays the groups out under the
    /// harness's lock after it: started from the test's thread instead, a wait could see a group before its past sessions.
    /// </summary>
    private static void Restore(TabTestHarness h) => InlineDispatcher.Run(() => h.Shell.Restore(null));

    private static Task ProjectsShown(TabTestHarness h, int count) =>
        TabTestHarness.Eventually(() => InlineDispatcher.Read(() => h.Shell.Groups.Count(g => g.IsClaudeProject) == count), $"{count} projects in the sidebar");

    [Fact]
    public async Task With_the_setting_on_every_folder_with_sessions_is_a_group_with_its_past_sessions_newest_first()
    {
        await using var h = new TabTestHarness(s => s.Sidebar.ShowClaudeProjects = true);
        h.Services.State.ClaudeProjectsOffered = true;
        var other = WriteProjects(h);

        Restore(h);
        await ProjectsShown(h, 2);

        // Nothing is open, so neither group counts as a tab, and both are placed by their latest session.
        Assert.False(h.Shell.HasTabs);
        Assert.Null(h.Shell.ClaudeProjectsOffer);
        var groups = InlineDispatcher.Read(() => h.Shell.Groups.ToArray());
        Assert.Equal([other, h.WorkFolder], groups.Select(g => g.Folder));
        Assert.All(groups, g => Assert.True(g.IsProjectOnly));
        Assert.All(groups, g => Assert.False(g.CanClose));
        var work = groups[1];
        Assert.Equal("3 past sessions", work.PastSessionsText);
        Assert.Equal(["w3", "w2", "w1"], work.PastSessions.Select(r => r.Entry.SessionId));
        Assert.Equal("Write the release notes", work.PastSessions[0].Title);
        Assert.Equal("1 h ago · 1 message", work.PastSessions[0].Detail);
        Assert.False(work.HasMorePastSessions);
        Assert.False(work.IsPastSessionsExpanded);
        // With no tabs to fold them under, they're listed straight away.
        Assert.All(groups, g => Assert.False(g.ShowsPastSessionsFold));
        Assert.All(groups, g => Assert.True(g.ShowsPastSessionList));
        // Groups with nothing open aren't saved: they come from the sessions each time.
        Assert.Empty(h.Services.State.GroupOrder!);
    }

    [Fact]
    public async Task A_past_session_opens_in_a_tab_in_its_group_and_leaves_the_past_sessions()
    {
        await using var h = new TabTestHarness(s => s.Sidebar.ShowClaudeProjects = true);
        h.Services.State.ClaudeProjectsOffered = true;
        WriteProjects(h);
        Restore(h);
        await ProjectsShown(h, 2);
        var work = InlineDispatcher.Read(() => h.Shell.Groups.Single(g => g.Folder == h.WorkFolder));
        var row = work.PastSessions[0];

        await InlineDispatcher.RunAsync(() => h.Shell.OpenPastSessionCommand.ExecuteAsync(row));

        var tab = h.Shell.SelectedTab!;
        Assert.Equal("w3", tab.State.SessionId);
        Assert.Same(work, InlineDispatcher.Read(() => h.Shell.Groups.Single(g => g.Tabs.Contains(tab))));
        Assert.True(h.Shell.HasTabs);
        // The group is a real group now, with its tab above the past sessions, which no longer list the open one.
        Assert.False(work.IsProjectOnly);
        Assert.True(work.CanClose);
        Assert.Equal(["w2", "w1"], work.PastSessions.Select(r => r.Entry.SessionId));
        Assert.True(work.ShowsPastSessionsFold);
        Assert.False(work.ShowsPastSessionList);
        await TabTestHarness.Eventually(() => h.Factory.Launches.Count == 1, "the resume");
        Assert.Equal("w3", h.Factory.Launches[0].Resume);
        // A group with a tab is placed with the tabs, first.
        Assert.Equal([h.WorkFolder, Path.Combine(h.Root, "other")], InlineDispatcher.Read(() => h.Shell.Groups.Select(g => g.Folder).ToArray()));

        // Closed again, the session is a past session once more, and the group stays as one of Claude Code's projects.
        await InlineDispatcher.RunAsync(() => h.Shell.CloseTabCommand.ExecuteAsync(tab));
        await TabTestHarness.Eventually(() => InlineDispatcher.Read(() => work.PastSessions.Count == 3), "the session back in the past sessions");
        Assert.Contains(work, InlineDispatcher.Read(() => h.Shell.Groups.ToArray()));
        Assert.True(work.IsProjectOnly);
        Assert.True(work.ShowsPastSessionList);
    }

    [Fact]
    public async Task Turning_the_setting_off_closes_the_groups_with_nothing_open_and_keeps_the_rest()
    {
        await using var h = new TabTestHarness(s => s.Sidebar.ShowClaudeProjects = true);
        h.Services.State.ClaudeProjectsOffered = true;
        WriteProjects(h);
        Restore(h);
        await ProjectsShown(h, 2);
        var tab = await h.OpenTabAsync();
        var work = InlineDispatcher.Read(() => h.Shell.Groups.Single(g => g.Tabs.Contains(tab)));
        Assert.True(work.HasPastSessions);

        h.Services.Settings.Sidebar.ShowClaudeProjects = false;
        InlineDispatcher.Run(() => h.Services.SaveSettings());

        var groups = InlineDispatcher.Read(() => h.Shell.Groups.ToArray());
        var kept = Assert.Single(groups);
        Assert.Same(work, kept);
        Assert.False(kept.IsClaudeProject);
        Assert.Empty(kept.PastSessions);
        Assert.True(kept.CanClose);

        // And on again: the other folder comes back, with the work folder's past sessions.
        h.Services.Settings.Sidebar.ShowClaudeProjects = true;
        InlineDispatcher.Run(() => h.Services.SaveSettings());
        await ProjectsShown(h, 2);
        Assert.Equal(3, work.PastSessions.Count);
    }

    [Fact]
    public async Task Unfolding_a_groups_past_sessions_is_remembered_for_the_folder()
    {
        await using var h = new TabTestHarness(s => s.Sidebar.ShowClaudeProjects = true);
        h.Services.State.ClaudeProjectsOffered = true;
        WriteProjects(h);
        Restore(h);
        await ProjectsShown(h, 2);
        var work = InlineDispatcher.Read(() => h.Shell.Groups.Single(g => g.Folder == h.WorkFolder));

        InlineDispatcher.Run(() => h.Shell.TogglePastSessionsCommand.Execute(work));

        Assert.True(work.IsPastSessionsExpanded);
        Assert.Equal([h.WorkFolder], h.Services.State.ExpandedPastSessions);

        InlineDispatcher.Run(() => h.Shell.TogglePastSessionsCommand.Execute(work));
        Assert.False(work.IsPastSessionsExpanded);
        Assert.Empty(h.Services.State.ExpandedPastSessions);
    }

    [Fact]
    public async Task Only_the_latest_few_are_listed_and_the_rest_are_in_History()
    {
        await using var h = new TabTestHarness(s => s.Sidebar.ShowClaudeProjects = true);
        h.Services.State.ClaudeProjectsOffered = true;
        for (var i = 0; i < 7; i++)
        {
            h.WriteTranscript($"s{i}", UserLine($"s{i}", $"Prompt {i}", h.WorkFolder, $"2026-09-28T0{i}:00:00Z"));
        }
        Restore(h);
        await ProjectsShown(h, 1);
        var work = InlineDispatcher.Read(() => h.Shell.Groups.Single());

        Assert.Equal("7 past sessions", work.PastSessionsText);
        Assert.Equal(["s6", "s5", "s4", "s3", "s2"], work.ShownPastSessions.Select(r => r.Entry.SessionId));
        Assert.True(work.HasMorePastSessions);
        // Nothing open, so they're listed, with the way to the rest.
        Assert.True(work.ShowsMorePastSessions);

        // Show all in History… opens History on the folder.
        InlineDispatcher.Run(() => h.Shell.OpenHistoryForGroupCommand.Execute(work));
        var history = h.Shell.History!;
        await TabTestHarness.Eventually(() => !history.IsLoading, "History to load");
        Assert.Equal(h.WorkFolder, history.SelectedProject!.Folder);
        Assert.Equal(7, history.Groups.Single().Entries.Count);
    }

    [Fact]
    public async Task The_sidebar_offers_once_and_accepting_turns_the_setting_on()
    {
        await using var h = new TabTestHarness();
        WriteProjects(h);
        Assert.False(h.Services.Settings.Sidebar.ShowClaudeProjects);

        Restore(h);
        await TabTestHarness.Eventually(() => InlineDispatcher.Read(() => h.Shell.HasClaudeProjectsOffer), "the offer");

        Assert.Equal("Claude Code has sessions in 2 folders on this machine.", h.Shell.ClaudeProjectsOffer!.Text);
        Assert.DoesNotContain(h.Shell.Groups, g => g.IsClaudeProject);

        InlineDispatcher.Run(() => h.Shell.AcceptClaudeProjectsCommand.Execute(null));

        Assert.Null(h.Shell.ClaudeProjectsOffer);
        Assert.True(h.Services.Settings.Sidebar.ShowClaudeProjects);
        Assert.True(h.Services.State.ClaudeProjectsOffered);
        await ProjectsShown(h, 2);
    }

    [Fact]
    public async Task Declining_the_offer_leaves_the_setting_off_and_the_offer_is_not_made_again()
    {
        await using var h = new TabTestHarness();
        WriteProjects(h);
        Restore(h);
        await TabTestHarness.Eventually(() => InlineDispatcher.Read(() => h.Shell.HasClaudeProjectsOffer), "the offer");

        InlineDispatcher.Run(() => h.Shell.DeclineClaudeProjectsCommand.Execute(null));

        Assert.Null(h.Shell.ClaudeProjectsOffer);
        Assert.False(h.Services.Settings.Sidebar.ShowClaudeProjects);
        Assert.True(h.Services.State.ClaudeProjectsOffered);

        // Another launch, with the choice made: no offer, and no groups.
        await using var again = new TabTestHarness();
        again.Services.State.ClaudeProjectsOffered = true;
        WriteProjects(again);
        Restore(again);
        await TabTestHarness.Eventually(() => again.Shell.Picker is not null, "the picker, with no tabs");
        await Waiting.NeverAsync(() => InlineDispatcher.Read(() => again.Shell.HasClaudeProjectsOffer || again.Shell.Groups.Count > 0), "an offer or a group");
    }

    [Fact]
    public async Task No_offer_without_sessions_or_with_sessions_only_in_folders_that_are_gone()
    {
        await using var h = new TabTestHarness();
        h.WriteTranscript("g1", UserLine("g1", "In a deleted clone", Path.Combine(h.Root, "gone")));
        Restore(h);
        await TabTestHarness.Eventually(() => h.Shell.Picker is not null, "the picker, with no tabs");

        await Waiting.NeverAsync(() => InlineDispatcher.Read(() => h.Shell.HasClaudeProjectsOffer), "an offer");
        Assert.False(h.Services.State.ClaudeProjectsOffered);
    }

    [Fact]
    public void The_detail_line_says_when_and_how_many_messages_and_whose_machine_when_not_this_one()
    {
        var now = DateTimeOffset.Parse("2026-09-28T12:00:00Z");
        Assert.Equal("1 h ago · 14 messages", ShellViewModel.PastSessionDetail(Entry("This machine", now.AddHours(-1), 14), now));
        Assert.Equal("just now · 1 message", ShellViewModel.PastSessionDetail(Entry("This machine", now, 1), now));
        Assert.Equal("2 days ago · LAPTOP-02", ShellViewModel.PastSessionDetail(Entry("LAPTOP-02", now.AddDays(-2), 0), now));
    }

    [Fact]
    public void Each_projects_folder_is_checked_once_and_a_project_whose_folder_is_gone_is_left_out()
    {
        var now = DateTimeOffset.Parse("2026-09-28T12:00:00Z");
        HistoryEntry In(string id, string folder, int hoursAgo, bool conflict = false) => new()
        {
            SessionId = id,
            Title = id,
            Machine = "This machine",
            ProjectFolder = folder,
            ProjectKey = folder,
            LastActivity = now.AddHours(-hoursAgo),
            IsConflictCopy = conflict,
            Details = "",
        };
        var checkedFolders = new List<string>();

        var projects = HistorySessions.Projects(
            [In("a1", "/work/a", 3), In("a2", "/work/a", 1), In("a3", "/work/a", 2), In("g1", "/gone", 1), In("c1", "/conflicts", 1, conflict: true)],
            folder =>
            {
                checkedFolders.Add(folder);
                return folder != "/gone";
            });

        Assert.Equal(["/work/a", "/gone"], checkedFolders);
        Assert.Equal(["/work/a"], projects.Keys);
        Assert.Equal(["a2", "a3", "a1"], projects["/work/a"].Select(e => e.SessionId));
    }

    private static HistoryEntry Entry(string machine, DateTimeOffset last, int messages) => new()
    {
        SessionId = "s",
        Title = "A session",
        Machine = machine,
        LastActivity = last,
        MessageCount = messages,
        Details = "",
    };
}
