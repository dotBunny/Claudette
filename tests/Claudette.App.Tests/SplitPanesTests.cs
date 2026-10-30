using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.ToolWindows;

namespace Claudette.App.Tests;

/// <summary>Tabs side by side (DESIGN.md §18, "Split panes").</summary>
public class SplitPanesTests
{
    private static async Task<(TabTestHarness H, TabViewModel A, TabViewModel B, TabViewModel C)> ThreeTabsAsync()
    {
        var h = new TabTestHarness();
        var a = await ThreadScript.OpenAsync(h, "Alpha");
        var b = await ThreadScript.OpenAsync(h, "Beta");
        var c = await ThreadScript.OpenAsync(h, "Gamma");
        h.Shell.SelectedTab = a;
        return (h, a, b, c);
    }

    [Fact]
    public async Task One_tab_on_its_own_is_on_screen_and_the_others_arent()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;

        Assert.Equal([a], h.Shell.Panes);
        Assert.False(h.Shell.IsSplit);
        Assert.True(a is { IsShown: true, IsSelected: true, IsInSplit: false, PaneIndex: 0 });
        Assert.True(b is { IsShown: false, PaneIndex: -1 });
        Assert.False(c.IsShown);
    }

    [Fact]
    public async Task Open_beside_puts_a_tab_after_the_focused_one_and_focuses_it()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;

        h.Shell.OpenBesideCommand.Execute(c);

        Assert.Equal([a, c], h.Shell.Panes);
        Assert.True(h.Shell.IsSplit);
        Assert.Same(c, h.Shell.SelectedTab);
        Assert.True(a is { IsShown: true, IsSelected: false, IsInSplit: true, PaneIndex: 0 });
        Assert.True(c is { IsShown: true, IsSelected: true, IsInSplit: true, PaneIndex: 1 });
        Assert.False(b.IsShown);
        Assert.Equal([a.Id, c.Id], h.Services.State.PaneTabIds);

        // Beside the focused one, so between them.
        h.Shell.SelectedTab = a;
        h.Shell.OpenBesideCommand.Execute(b);
        Assert.Equal([a, b, c], h.Shell.Panes);
    }

    [Fact]
    public async Task Each_panes_header_has_its_groups_color_and_follows_it_when_its_changed()
    {
        var (h, a, _, c) = await ThreeTabsAsync();
        await using var _ = h;
        var group = h.Shell.Groups.Single();

        h.Shell.OpenBesideCommand.Execute(c);

        Assert.Equal([group.Color, group.Color], h.Shell.Panes.Select(p => p.PaneColor));

        h.Shell.SetGroupColor(group, Avalonia.Media.Colors.Orange);

        Assert.Equal([Avalonia.Media.Colors.Orange, Avalonia.Media.Colors.Orange], new[] { a, c }.Select(t => t.PaneColor));
    }

    [Fact]
    public async Task Picking_a_tab_on_screen_focuses_it_and_any_other_takes_the_focused_panes_place()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;
        h.Shell.OpenBesideCommand.Execute(b);

        // A click in a pane, or its row in the sidebar.
        a.FocusPane();
        Assert.Same(a, h.Shell.SelectedTab);
        Assert.Equal([a, b], h.Shell.Panes);

        h.Shell.SelectedTab = c;

        Assert.Equal([c, b], h.Shell.Panes);
        Assert.False(a.IsShown);
        Assert.True(b is { IsShown: true, IsSelected: false });
    }

    [Fact]
    public async Task With_every_pane_taken_a_tab_opened_beside_replaces_the_last_one_not_focused()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;
        var d = await ThreadScript.OpenAsync(h, "Delta");
        h.Shell.SelectedTab = a;
        h.Shell.OpenBesideCommand.Execute(b);
        h.Shell.OpenBesideCommand.Execute(c);
        Assert.Equal([a, b, c], h.Shell.Panes);

        // Gamma has the focus, opened last; Beta is the last pane without it.
        h.Shell.OpenBesideCommand.Execute(d);

        Assert.Equal([a, c, d], h.Shell.Panes);
        Assert.Same(d, h.Shell.SelectedTab);
    }

    [Fact]
    public async Task Closing_a_pane_keeps_its_tab_open_and_the_pane_beside_it_takes_the_focus()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;
        h.Shell.OpenBesideCommand.Execute(b);
        h.Shell.OpenBesideCommand.Execute(c);

        c.ClosePaneCommand.Execute(null);

        Assert.Equal([a, b], h.Shell.Panes);
        Assert.Same(b, h.Shell.SelectedTab);
        Assert.Contains(c, h.Shell.AllTabs);
        Assert.False(c.IsShown);

        h.Shell.CloseOtherPanesCommand.Execute(null);
        Assert.Equal([b], h.Shell.Panes);
        Assert.False(b.IsInSplit);
        Assert.Null(h.Services.State.PaneTabIds);
    }

    [Fact]
    public async Task Closing_a_tab_in_a_pane_takes_its_pane_with_it()
    {
        var (h, a, b, _) = await ThreeTabsAsync();
        await using var __ = h;
        h.Shell.OpenBesideCommand.Execute(b);

        await h.Shell.CloseTabCommand.ExecuteAsync(b);

        Assert.Equal([a], h.Shell.Panes);
        Assert.Same(a, h.Shell.SelectedTab);
        Assert.False(a.IsInSplit);
    }

    [Fact]
    public async Task Panes_fold_away_as_the_window_narrows_and_come_back_as_it_widens()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;
        h.Shell.OpenBesideCommand.Execute(b);
        h.Shell.OpenBesideCommand.Execute(c);
        h.Shell.SelectedTab = a;

        // Room for two beside the sidebar: the focused one and its neighbour.
        h.Shell.Layout.SetAvailableWidth(ShellLayout.DefaultSidebarWidth + 2 * ShellLayout.MinConversationWidth + ToolDockViewModel.StripWidth + 10);
        Assert.Equal([a, b], h.Shell.Panes);
        Assert.False(c.IsShown);
        h.Shell.SelectedTab = c;
        Assert.Equal([b, c], h.Shell.Panes);

        h.Shell.Layout.SetAvailableWidth(2000);
        Assert.Equal([a, b, c], h.Shell.Panes);
        Assert.True(a.IsShown);
    }

    [Fact]
    public async Task A_tab_on_screen_beside_the_focused_one_isnt_unread_and_doesnt_notify()
    {
        var (h, a, b, _) = await ThreeTabsAsync();
        await using var __ = h;
        h.Shell.OpenBesideCommand.Execute(b);
        h.Services.Notifications.SetAppActive(true);
        h.Shell.SelectedTab = a;
        h.Notifier.Shown.Clear();

        // Beta started second: its pretend process is the second.
        h.Transport.EmitTo(1, ThreadScript.Init());
        h.Transport.EmitTo(1, """{"type":"assistant","message":{"content":[{"type":"text","text":"done"}]}}""");
        h.Transport.EmitTo(1, """{"type":"result","subtype":"success","is_error":false,"session_id":"s","duration_ms":1000}""");
        await TabTestHarness.Eventually(() => b.Status == TabStatus.Idle && b.Items.OfType<Conversation.TurnSummaryItem>().Any(), "the turn");

        Assert.Equal(TabStatus.Idle, b.Status);
        Assert.Null(h.Notifier.Last("TurnFinished:"));
    }

    [Fact]
    public async Task A_thread_shows_beside_its_sub_threads()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;
        h.Shell.MakeThreadCommand.Execute(a);
        b.AssignToThreadCommand.Execute(a);
        c.AssignToThreadCommand.Execute(a);
        Assert.True(a.HasSubThreads);
        h.Shell.SelectedTab = c;

        h.Shell.ShowThreadSideBySideCommand.Execute(c);

        Assert.Equal([a, b, c], h.Shell.Panes);
        Assert.Same(a, h.Shell.SelectedTab);
    }

    [Fact]
    public async Task The_panes_come_back_after_a_restart()
    {
        var (h, a, b, _) = await ThreeTabsAsync();
        await using var __ = h;
        h.Shell.OpenBesideCommand.Execute(b);

        var snapshot = h.Shell.CaptureForRestart();

        Assert.Equal([a.Id, b.Id], snapshot.PaneTabIds);
        Assert.Equal(b.Id, snapshot.SelectedTabId);
    }

    [Fact]
    public async Task Dragging_the_line_between_two_panes_trades_their_width_and_each_keeps_its_least()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;
        h.Shell.OpenBesideCommand.Execute(b);
        h.Shell.OpenBesideCommand.Execute(c);
        var layout = h.Shell.Layout;
        Assert.Equal([1 / 3.0, 1 / 3.0, 1 / 3.0], layout.SharesFor(3));
        double[] leasts = [360, 360, 360];

        // The line after the first pane, from 400 out to 430, across 1200: the second pane gives the first its 30, and
        // the third keeps its share.
        layout.ResizePanes(0, 430, 1200, leasts);
        Assert.Equal([430 / 1200.0, 370 / 1200.0, 1 / 3.0], layout.SharesFor(3), new ShareComparer());

        // Not past the second pane's least: of the 800 the two have, it keeps 360.
        layout.ResizePanes(0, 1000, 1200, leasts);
        Assert.Equal([440 / 1200.0, 360 / 1200.0, 1 / 3.0], layout.SharesFor(3), new ShareComparer());
        // Nor past the first's.
        layout.ResizePanes(0, 10, 1200, leasts);
        Assert.Equal([360 / 1200.0, 440 / 1200.0, 1 / 3.0], layout.SharesFor(3), new ShareComparer());

        // Kept once the drag ends, and for as many panes as there are: two panes share evenly.
        layout.SavePaneShares();
        Assert.Equal(3, h.Services.State.PaneShares!.Count);
        Assert.Equal([0.5, 0.5], layout.SharesFor(2));

        // A double-click on a line shares it evenly again.
        layout.ResetPaneShares();
        Assert.Null(h.Services.State.PaneShares);
        Assert.Equal([1 / 3.0, 1 / 3.0, 1 / 3.0], layout.SharesFor(3));
    }

    [Fact]
    public async Task A_pane_coming_or_going_shares_the_width_evenly_again()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;
        h.Shell.OpenBesideCommand.Execute(b);
        h.Shell.Layout.ResizePanes(0, 700, 1000, [360, 360]);
        h.Shell.Layout.SavePaneShares();
        Assert.Equal(0.64, h.Shell.Layout.SharesFor(2)[0], precision: 6);

        h.Shell.OpenBesideCommand.Execute(c);

        Assert.Empty(h.Shell.Layout.PaneShares);
        Assert.Null(h.Services.State.PaneShares);
    }

    [Fact]
    public async Task A_row_dropped_on_a_panes_half_opens_beside_it_there_and_with_no_room_takes_its_place()
    {
        var (h, a, b, c) = await ThreeTabsAsync();
        await using var _ = h;
        var d = await ThreadScript.OpenAsync(h, "Delta");
        h.Shell.SelectedTab = a;

        // On Alpha's left half: Beta opens before it, and takes the focus.
        Assert.Equal(PaneDrop.Beside, h.Shell.PaneDropFor(b, 0));
        h.Shell.DropOnPane(b, 0, after: false);
        Assert.Equal([b, a], h.Shell.Panes);
        Assert.Same(b, h.Shell.SelectedTab);

        // On Alpha's right half: after it.
        h.Shell.DropOnPane(c, 1, after: true);
        Assert.Equal([b, a, c], h.Shell.Panes);

        // Every pane taken: Delta takes the place of the pane it's dropped on.
        Assert.Equal(PaneDrop.Replace, h.Shell.PaneDropFor(d, 1));
        h.Shell.DropOnPane(d, 1, after: false);
        Assert.Equal([b, d, c], h.Shell.Panes);
        Assert.False(a.IsShown);

        // A tab on screen moves to where it's dropped; on its own pane nothing happens.
        Assert.Equal(PaneDrop.None, h.Shell.PaneDropFor(c, 2));
        Assert.Equal(PaneDrop.Beside, h.Shell.PaneDropFor(c, 0));
        h.Shell.DropOnPane(c, 0, after: false);
        Assert.Equal([c, b, d], h.Shell.Panes);
        Assert.Same(c, h.Shell.SelectedTab);
    }

    /// <summary>Shares compared to six places: they're worked out from widths.</summary>
    private sealed class ShareComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-6;

        public int GetHashCode(double obj) => 0;
    }
}
