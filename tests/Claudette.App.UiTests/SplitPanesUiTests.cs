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

/// <summary>Tabs side by side, rendered (DESIGN.md §18, "Split panes").</summary>
public sealed class SplitPanesUiTests
{
    [AvaloniaFact]
    public async Task Two_tabs_sit_side_by_side_with_headers_and_a_click_in_one_focuses_it()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var alpha = await ThreadScript.OpenAsync(h, "Alpha");
        var beta = await ThreadScript.OpenAsync(h, "Beta");
        h.Shell.SelectedTab = alpha;
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1600, 800);

        h.Shell.OpenBesideCommand.Execute(beta);
        UiText.Settle(window);

        var views = window.GetVisualDescendants().OfType<TabView>().Where(v => v.IsEffectivelyVisible).ToList();
        Assert.Equal(2, views.Count);
        var (left, right) = (views.Single(v => v.DataContext == alpha), views.Single(v => v.DataContext == beta));
        var (leftAt, rightAt) = (left.TranslatePoint(default, window)!.Value, right.TranslatePoint(default, window)!.Value);
        Assert.Equal(leftAt.Y, rightAt.Y);
        Assert.True(rightAt.X >= leftAt.X + left.Bounds.Width - 1, "Beta's pane overlaps Alpha's.");
        // The same width, but for the gap between the islands, which the second pane keeps before it.
        Assert.InRange(left.Bounds.Width - right.Bounds.Width, 6 - 1.5, 6 + 1.5);

        // Each pane names its tab; the focused one, Beta, has the accent under its header, and the tool windows show its data.
        Assert.True(Header(left).IsVisible && Header(right).IsVisible);
        Assert.Contains("Alpha", UiText.Describe(Header(left)), StringComparison.Ordinal);
        Assert.Contains("focused", Header(right).Classes);
        Assert.DoesNotContain("focused", Header(left).Classes);
        Assert.Same(beta, h.Shell.ToolWindows.Session);
        // Named over them, since another tab is on screen too (DESIGN.md §3, "Tool windows").
        Assert.True(h.Shell.ToolWindows.NamesSession);

        // A click in Alpha's conversation moves the focus there, and the tool windows follow.
        var conversation = left.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "ConversationScroll");
        var at = conversation.TranslatePoint(new Point(conversation.Bounds.Width / 2, 40), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        UiText.Settle(window);

        Assert.Same(alpha, h.Shell.SelectedTab);
        Assert.Contains("focused", Header(left).Classes);
        Assert.Same(alpha, h.Shell.ToolWindows.Session);
        Assert.True(beta.IsShown);

        // Close pane, on Beta's header: Alpha has the window to itself again, without a header.
        var close = Header(right).GetVisualDescendants().OfType<Button>().Single();
        close.Command!.Execute(close.CommandParameter);
        UiText.Settle(window);
        Assert.Single(window.GetVisualDescendants().OfType<TabView>(), v => v.IsEffectivelyVisible);
        Assert.False(Header(left).IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Dragging_the_line_between_two_panes_widens_one_and_narrows_the_other()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var alpha = await ThreadScript.OpenAsync(h, "Alpha");
        var beta = await ThreadScript.OpenAsync(h, "Beta");
        h.Shell.SelectedTab = alpha;
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1600, 800);
        h.Shell.OpenBesideCommand.Execute(beta);
        UiText.Settle(window);
        var (left, right) = (View(window, alpha), View(window, beta));
        // The second pane's view is a pixel narrower, for the line it draws: each is measured against itself.
        var (before, rightBefore) = (left.Bounds.Width, right.Bounds.Width);
        var lines = window.GetVisualDescendants().OfType<Controls.PaneDividers>().Single();
        Assert.Single(lines.Lines);

        // Pressed just right of the line, where the next pane's margin is, and dragged 150 to the right.
        var start = lines.TranslatePoint(new Point(lines.Lines[0] + 2, lines.Bounds.Height / 2), window)!.Value;
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(75, 0));
        window.MouseMove(start + new Vector(150, 0));
        window.MouseUp(start + new Vector(150, 0), MouseButton.Left);
        UiText.Settle(window);

        // To the pixel, which the shares round to.
        Assert.InRange(left.Bounds.Width - before, 149, 151);
        Assert.InRange(rightBefore - right.Bounds.Width, 149, 151);
        // Kept, for the next launch.
        Assert.Equal(2, h.Services.State.PaneShares!.Count);
        Assert.InRange(left.Bounds.Width - lines.Lines[0], -1, 1);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_tabs_row_dragged_onto_the_right_half_of_the_conversation_opens_beside_it()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var alpha = await ThreadScript.OpenAsync(h, "Alpha");
        var beta = await ThreadScript.OpenAsync(h, "Beta");
        h.Shell.SelectedTab = alpha;
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1600, 800);
        UiText.Settle(window);
        var row = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("tabrow") && b.DataContext == beta);
        var pane = View(window, alpha);

        var from = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
        var to = pane.TranslatePoint(new Point(pane.Bounds.Width * 3 / 4, pane.Bounds.Height / 2), window)!.Value;
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + new Vector(20, 0));
        window.MouseMove(to);
        UiText.Settle(window);

        // The row is marked while it's dragged, and the pane's right half says what dropping it there does; nothing has
        // moved yet.
        Assert.Contains("dragged", row.Classes);
        var target = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PaneDropTarget");
        Assert.True(target.IsEffectivelyVisible);
        Assert.Contains("Open beside", UiText.Describe(target), StringComparison.Ordinal);
        Assert.True(target.TranslatePoint(default, pane)!.Value.X >= pane.Bounds.Width / 2 - 1, "The mark isn't on the right half.");
        Assert.Same(alpha, h.Shell.SelectedTab);
        Assert.False(beta.IsShown);

        window.MouseUp(to, MouseButton.Left);
        UiText.Settle(window);

        Assert.Equal([alpha, beta], h.Shell.Panes);
        Assert.Same(beta, h.Shell.SelectedTab);
        Assert.DoesNotContain("dragged", row.Classes);
        Assert.False(target.IsEffectivelyVisible);
        window.Close();
    }

    private static TabView View(Window window, object tab) =>
        window.GetVisualDescendants().OfType<TabView>().Single(v => v.IsEffectivelyVisible && v.DataContext == tab);

    private static Border Header(TabView view) => view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PaneHeader");
}
