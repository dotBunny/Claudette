using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.Views;

namespace Claudette.App.UiTests;

/// <summary>Claude Code's projects in the sidebar, and the offer to show them (DESIGN.md §4, "Claude Code's projects").</summary>
public class ClaudeProjectsUiTests
{
    private static string UserLine(string sessionId, string text, string cwd, string time) => new JsonObject
    {
        ["type"] = "user",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = text },
        ["cwd"] = cwd,
        ["sessionId"] = sessionId,
        ["timestamp"] = time,
    }.ToJsonString();

    private static void WriteSessions(TabTestHarness h)
    {
        h.WriteTranscript("w1", UserLine("w1", "Fix the login bug", h.WorkFolder, "2026-09-28T09:00:00Z"));
        h.WriteTranscript("w2", UserLine("w2", "Add the settings page", h.WorkFolder, "2026-09-28T11:00:00Z"));
    }

    [AvaloniaFact]
    public async Task A_projects_group_lists_its_past_sessions_under_a_fold_and_a_click_on_one_opens_it()
    {
        await using var h = new TabTestHarness(s => s.Sidebar.ShowClaudeProjects = true, dispatcher: new AvaloniaUiDispatcher());
        h.Services.State.ClaudeProjectsOffered = true;
        WriteSessions(h);
        h.Shell.Restore(null);
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => h.Shell.Groups.Count == 1 && h.Shell.Groups[0].HasPastSessions, "the project in the sidebar");
        h.Shell.ClosePicker();
        UiText.Settle(window);

        // Folded: the row says how many, and lists none.
        var fold = Fold(window);
        Assert.Equal("2 past sessions", Avalonia.Automation.AutomationProperties.GetName(fold));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("pastsession") && b.IsEffectivelyVisible);

        Click(window, fold);
        var group = h.Shell.Groups.Single();
        Assert.True(group.IsPastSessionsExpanded);
        var rows = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("pastsession") && b.IsEffectivelyVisible).ToList();
        Assert.Equal(2, rows.Count);
        var shown = UiText.Describe(GroupList(window), (h.Root, "{root}"));

        // A click resumes the session in a tab in the group, which then leaves the past sessions.
        Click(window, rows[0]);
        await UiText.SettleUntilAsync(window, () => h.Shell.SelectedTab?.State.SessionId == "w2", "the session's tab");
        Assert.Equal("Add the settings page", h.Shell.SelectedTab!.DisplayName);
        Assert.Equal("1 past session", Avalonia.Automation.AutomationProperties.GetName(Fold(window)));

        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task The_offer_sits_at_the_top_of_the_sidebar_and_accepting_it_brings_the_groups()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        WriteSessions(h);
        h.Shell.Restore(null);
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => h.Shell.HasClaudeProjectsOffer, "the offer");
        h.Shell.ClosePicker();
        UiText.Settle(window);

        var card = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ClaudeProjectsOffer");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Empty(h.Shell.Groups);
        var shown = UiText.Describe(card);

        var accept = card.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Show them in the sidebar");
        Click(window, accept);
        await UiText.SettleUntilAsync(window, () => h.Shell.Groups.Count == 1 && h.Shell.Groups[0].HasPastSessions, "the project in the sidebar");
        Assert.False(card.IsEffectivelyVisible);
        Assert.True(h.Services.Settings.Sidebar.ShowClaudeProjects);

        await Verify(shown);
    }

    private static ItemsControl GroupList(Window window) =>
        window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "GroupList");

    /// <summary>The fold's row, <i>2 past sessions</i>, which <b>Show all in History…</b> shares a class with.</summary>
    private static Button Fold(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("pastsessions") && b.IsEffectivelyVisible
            && Avalonia.Automation.AutomationProperties.GetName(b)?.Contains("past session", StringComparison.Ordinal) is true);

    private static void Click(Window window, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        UiText.Settle(window);
    }
}
