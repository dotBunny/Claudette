using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Claudette.App.Controls;
using Claudette.App.Services;
using Claudette.App.Tests;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.Views;
using Claudette.Core.Settings;

namespace Claudette.App.UiTests;

/// <summary>Threads rendered (DESIGN.md §18, "Threads"): the sidebar's rows, and the strip over a thread's composer.</summary>
public class ThreadUiTests
{
    [AvaloniaFact]
    public async Task A_threads_sub_threads_sit_indented_under_it_and_the_thread_has_its_icon()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        h.Factory.ProcessPerSession = true;
        var plan = await ThreadScript.OpenAsync(h, "Plan");
        await ThreadScript.OpenAsync(h, "Other");
        var art = await ThreadScript.OpenAsync(h, "Art page");
        h.Shell.MakeThreadCommand.Execute(plan);
        art.AssignToThreadCommand.Execute(plan);
        var window = UiText.Show(new ShellView { DataContext = h.Shell });

        var rows = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("tabrow")).ToList();
        double Left(Control c) => c.TranslatePoint(default, window)!.Value.X;

        Assert.Equal(["Plan", "Art page", "Other"], rows.Select(r => ((TabViewModel)r.DataContext!).DisplayName));
        Assert.True(Left(rows[1]) > Left(rows[0]), "The sub-thread should be indented under its thread");
        Assert.Equal(Left(rows[0]), Left(rows[2]));
        var icon = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b is { Name: "ThreadIcon", IsEffectivelyVisible: true });
        Assert.Equal("A thread, with 1 sub-thread", ToolTip.GetTip(icon));
        Assert.Equal(plan, icon.DataContext);
    }

    [AvaloniaFact]
    public async Task A_collapsed_thread_leaves_its_sub_threads_out_and_shows_its_badge()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        h.Factory.ProcessPerSession = true;
        var plan = await ThreadScript.OpenAsync(h, "Plan");
        await ThreadScript.OpenAsync(h, "Other");
        var art = await ThreadScript.OpenAsync(h, "Art page");
        var gallery = await ThreadScript.OpenAsync(h, "Gallery");
        h.Shell.MakeThreadCommand.Execute(plan);
        art.AssignToThreadCommand.Execute(plan);
        gallery.AssignToThreadCommand.Execute(plan);
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        List<Button> Rows() => [.. window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("tabrow") && b.IsEffectivelyVisible)];
        double Top(Control c) => c.TranslatePoint(default, window)!.Value.Y;
        var gap = Top(Rows()[1]) - (Top(Rows()[0]) + Rows()[0].Bounds.Height);

        // The chevron is only on the row with something under it.
        var fold = Assert.Single(window.GetVisualDescendants().OfType<Button>(), b => b is { Name: "RowFold", IsEffectivelyVisible: true });
        Assert.Same(plan, fold.DataContext);
        Assert.Equal("Collapse tab", AutomationProperties.GetName(fold));
        Assert.Equal("Collapse: hides 2 sub-threads", ToolTip.GetTip(fold));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), b => b is { Name: "FoldBadge", IsEffectivelyVisible: true });

        fold.Command!.Execute(null);
        UiText.Settle(window);

        var rows = Rows();
        Assert.Equal(["Plan", "Other"], rows.Select(r => ((TabViewModel)r.DataContext!).DisplayName));
        // The sub-threads folded away leave no gap behind.
        Assert.Equal(gap, Top(rows[1]) - (Top(rows[0]) + rows[0].Bounds.Height), 3);
        Assert.Equal("Expand tab", AutomationProperties.GetName(fold));
        var badge = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b is { Name: "FoldBadge", IsEffectivelyVisible: true });
        Assert.Same(plan, badge.DataContext);
        Assert.Equal("Collapsed: 2 sub-threads", ToolTip.GetTip(badge));
        Assert.Contains(badge.GetVisualDescendants().OfType<TextBlock>(), t => t is { Text: "2", IsEffectivelyVisible: true });
    }

    [AvaloniaFact]
    public async Task The_chevron_sits_on_the_groups_line_level_with_the_name_and_takes_no_room_before_it()
    {
        await using var h = new TabTestHarness(s => s.Appearance.ShowSubagents = SubagentDisplay.InSidebar, dispatcher: new AvaloniaUiDispatcher());
        h.Factory.ProcessPerSession = true;
        var plan = await ThreadScript.OpenAsync(h, "Plan");
        var other = await ThreadScript.OpenAsync(h, "Other");
        var art = await ThreadScript.OpenAsync(h, "Art page");
        h.Shell.MakeThreadCommand.Execute(plan);
        art.AssignToThreadCommand.Execute(plan);
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        Point Middle(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
        double Left(Control c) => c.TranslatePoint(default, window)!.Value.X;
        TextBlock Name(TabViewModel tab) => window.GetVisualDescendants().OfType<TextBlock>().First(t => t.DataContext == tab && t.Text == tab.DisplayName);
        IEnumerable<Button> Folds() => window.GetVisualDescendants().OfType<Button>().Where(b => b is { Name: "RowFold", IsEffectivelyVisible: true });
        Button Fold(TabViewModel tab) => Folds().Single(b => b.DataContext == tab);

        var fold = Assert.Single(Folds());
        var line = Assert.Single(window.GetVisualDescendants().OfType<SideLine>(), l => l is { Name: "GroupLine", IsEffectivelyVisible: true });
        var icon = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b is { Name: "ThreadIcon", IsEffectivelyVisible: true });
        var onLine = Left(line) + line.DrawnThickness / 2;

        Assert.Same(plan, fold.DataContext);
        Assert.Equal(onLine, Middle(fold).X, 0.01);
        Assert.Equal(Middle(Name(plan)).Y, Middle(fold).Y, 0.5);
        // The line leaves a gap for it rather than running under it: the sidebar can be see-through (Mica).
        SideLineTests.AssertGaps(line, fold);
        // What comes first in the thread's line, its thread icon, is where the other tab's name starts, and its gap in the
        // line, taller than the name, leaves the row as tall as the others.
        Assert.Equal(Left(Name(other)), Left(icon), 0.01);
        Assert.True(fold.Bounds.Height > Name(plan).Bounds.Height, "The gap should reach past the name's line");
        Button Row(TabViewModel tab) => window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("tabrow") && b.DataContext == tab);
        Assert.Equal(Row(other).Bounds.Height, Row(plan).Bounds.Height);
        // Clicking it reaches it, though it's outside the row.
        Assert.Contains(fold, (UiText.HitTest(window, Middle(fold)) as Visual)?.GetSelfAndVisualAncestors() ?? []);

        // A sub-thread's, 12 further in, is on the same line.
        h.Transport.EmitTo(2, ThreadScript.Init());
        h.Transport.EmitTo(2, Wire.Agent("a1", "Find the gallery's cards", "Find them", "Explore"));
        // The view model has the row before the window lays it out, so wait for the chevron itself.
        await UiText.SettleUntilAsync(window, () => Folds().Any(b => b.DataContext == art), "the sub-thread's chevron");
        UiText.Settle(window);
        Assert.True(Left(Name(art)) > Left(Name(other)), "The sub-thread should be indented");
        Assert.Equal(onLine, Middle(Fold(art)).X, 0.01);
        Assert.Equal(Middle(Name(art)).Y, Middle(Fold(art)).Y, 0.5);
        SideLineTests.AssertGaps(line, Fold(plan), Fold(art));

        // Out of the thread, the thread's chevron goes, and its gap with it.
        h.Shell.DetachFromThreadCommand.Execute(art);
        await UiText.SettleUntilAsync(window, () => Folds().Select(b => b.DataContext).SequenceEqual([art]), "the thread's chevron to go");
        UiText.Settle(window);
        SideLineTests.AssertGaps(line, Fold(art));
    }

    [AvaloniaFact]
    public async Task The_strip_over_a_threads_composer_shows_a_message_to_let_go_and_a_sub_thread_waiting()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        h.Factory.ProcessPerSession = true;
        var plan = await ThreadScript.OpenAsync(h, "Plan");
        var art = await ThreadScript.OpenAsync(h, "Art page");
        h.Shell.MakeThreadCommand.Execute(plan);
        art.AssignToThreadCommand.Execute(plan);
        h.Shell.SelectTab(plan.Id);
        var window = UiText.Show(new ShellView { DataContext = h.Shell });

        h.Transport.EmitTo(1, ThreadScript.Init());
        h.Transport.EmitTo(1, ThreadScript.CanUseTool("req-1", "npm test"));
        h.Transport.EmitTo(0, ThreadScript.SendMessage("hk1", "Art page", "Build the gallery next, with the same cards as the art page."));
        await UiText.SettleUntilAsync(window, () => plan.ThreadApprovals.Count == 1 && plan.WaitingSubThreads.Count == 1, "the strip's rows");
        var strip = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b is { Name: "ThreadStrip", IsEffectivelyVisible: true });

        await Verify(UiText.Describe(strip));
    }
}
