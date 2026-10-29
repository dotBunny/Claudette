using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Arc = Avalonia.Controls.Shapes.Arc;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Claudette.App.Services;
using Claudette.App.Controls;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.Views;
using Claudette.Core.Auth;
using Claudette.Core.Library;
using Claudette.Core.Settings;
using Claudette.Usage;
using LiveMarkdown.Avalonia;

namespace Claudette.App.UiTests;

/// <summary>
/// The main window rendered for real, headlessly: the usage header, the sidebar, the conversation and the composer,
/// driven with keyboard and mouse input (DESIGN.md §15, "UI").
/// </summary>
public class MainWindowTests
{
    /// <summary>
    /// The composer's control bar keeps Send in view (DESIGN.md §5): in a window too narrow for its choices and counts on
    /// one line, the counts and Send move to a line of their own under the choices, still at the right. The panel's own
    /// layout is in <see cref="ControlBarPanelTests"/>.
    /// </summary>
    [AvaloniaFact]
    public async Task The_Send_button_stays_in_view_when_the_control_bar_is_too_narrow_for_one_line()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, width: 900);
        var send = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Send");
        var attach = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Attach a file");
        double Right(Control c) => c.TranslatePoint(new Point(c.Bounds.Width, 0), window)!.Value.X;
        double Top(Control c) => c.TranslatePoint(default, window)!.Value.Y;
        var bar = send.FindAncestorOfType<ControlBarPanel>()!;

        Assert.True(send.IsEffectivelyVisible);
        Assert.True(Right(send) <= window.Bounds.Width, $"Send ends at {Right(send)}, past the window's {window.Bounds.Width}");
        Assert.True(bar.IsWrapped);
        Assert.True(Top(send) > Top(attach) + attach.Bounds.Height, "The right-hand group should have moved under the chips");
        // The send key chip goes with Send, just before it, and stays in view too.
        var chip = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SendKeyChip");
        Assert.True(chip.IsEffectivelyVisible);
        Assert.True(Right(chip) <= window.Bounds.Width, $"The send key chip ends at {Right(chip)}, past the window's {window.Bounds.Width}");
        Assert.True(Top(chip) > Top(attach) + attach.Bounds.Height, "The send key chip should have moved under the chips with Send");
    }

    /// <summary>
    /// In a pane too narrow even for the counts on a line of their own (DESIGN.md §5), they wrap, and Send is still last
    /// at the right, with the send key chip before it, neither past the edge.
    /// </summary>
    [AvaloniaFact]
    public async Task In_a_very_narrow_pane_the_counts_wrap_and_Send_stays_last_in_view()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, width: 640);
        var send = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Send");
        var chip = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SendKeyChip");
        var composer = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ComposerBox");
        double Left(Control c) => c.TranslatePoint(default, window)!.Value.X;
        double Right(Control c) => c.TranslatePoint(new Point(c.Bounds.Width, 0), window)!.Value.X;
        double Top(Control c) => c.TranslatePoint(default, window)!.Value.Y;
        var group = send.GetVisualAncestors().OfType<WrapPanel>().First();
        var shown = group.Children.Where(c => c.IsVisible).ToList();

        Assert.True(send.IsEffectivelyVisible && chip.IsEffectivelyVisible);
        Assert.True(Right(send) <= Right(composer), $"Send ends at {Right(send)}, past the composer's {Right(composer)}");
        Assert.True(Right(chip) <= Right(composer), $"The chip ends at {Right(chip)}, past the composer's {Right(composer)}");
        Assert.True(Top(group) + group.Bounds.Height <= window.Bounds.Height);
        Assert.Same(send, shown[^1]);
        Assert.Same(chip, shown[^2]);
        // Send is last: on the chip's line after it, or on a line of its own under it.
        Assert.True(Top(send) > Top(chip) + chip.Bounds.Height / 2 || Left(send) >= Right(chip),
            $"Send at ({Left(send)}, {Top(send)}) should come after the chip, which ends at ({Right(chip)}, {Top(chip)})");
        // Every count is inside the composer.
        Assert.All(shown, c => Assert.True(Right(c) <= Right(composer), $"{c} ends at {Right(c)}, past the composer's {Right(composer)}"));
    }

    [AvaloniaFact]
    public async Task The_window_shows_the_usage_header_the_sidebar_and_the_conversation()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        h.Transport.EmitTurn("Found **two** problems in `Parser.cs`.");
        await using var tracker = new UsageTracker(h.Services, new UsageStore(Path.Combine(h.Root, "usage.db"), h.Time));
        using var usage = new UsageViewModel(h.Services, tracker);
        tracker.OnRateLimitEvent(RateLimitEvent(0.62, h.Time.GetUtcNow().AddHours(2).AddMinutes(14), 0.38));
        // The tab is idle before the turn arrives too: its footer is what says it has, and then that it's idle again.
        await TabTestHarness.Eventually(() => usage.HasData && h.Shell.SelectedTab!.Status != TabStatus.Working
            && h.Shell.SelectedTab!.Items.OfType<Conversation.TurnSummaryItem>().Any(), "the meters and the reply");
        var main = new MainWindowViewModel(h.Services) { CurrentPage = h.Shell, Usage = usage };
        main.Account.Status = new AuthStatus(true, "claude.ai", null, "me@example.com", null, "max", null, null);

        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };
        window.Show();
        await UiText.SettleUntilAsync(window, () => UiText.Describe(window).Contains("Found two problems", StringComparison.Ordinal), "the reply");

        await Verify(UiText.Describe(window, (h.Root, "{root}")));
    }

    /// <summary>Every tab's processes together, in the header before the account (DESIGN.md §4, "Process monitor").</summary>
    [AvaloniaFact]
    public async Task The_header_shows_the_tabs_processes_together_while_the_monitor_is_on()
    {
        await using var h = new TabTestHarness(s => s.Processes.ShowMonitor = true, dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        h.Trees.Trees[4242].Children.Add((5001, "node"));
        h.Time.Advance(Claudette.Platform.Processes.ProcessSampler.SummaryInterval);
        var main = new MainWindowViewModel(h.Services);
        main.UseShell(h.Shell);
        main.Account.Status = new AuthStatus(true, "claude.ai", null, "me@example.com", null, "max", null, null);

        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };
        window.Show();
        await UiText.SettleUntilAsync(window, () => window.GetVisualDescendants().OfType<TextBlock>().Any(t => t is { Name: "ProcessTotals", IsEffectivelyVisible: true }), "the total");
        var totals = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ProcessTotals");
        var account = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Account");

        Assert.True(totals.IsEffectivelyVisible);
        Assert.Equal("21% CPU · 150 MB", totals.Text);
        Assert.StartsWith("Processes of every tab, Claude Code included", ToolTip.GetTip(totals) as string, StringComparison.Ordinal);
        Assert.True(totals.TranslatePoint(default, window)!.Value.X < account.TranslatePoint(default, window)!.Value.X);
    }

    /// <summary>
    /// The header as Rider's (DESIGN.md §3, "Window frame"): Claudette's logo and the menu first, the user guide first in
    /// the menu, and Settings last; on Windows and macOS it's the window's title bar, its controls taking their own clicks,
    /// unless Settings → Appearance → Use the system's title bar is on, when Settings sits over the strip of icons.
    /// </summary>
    [AvaloniaFact]
    public async Task The_header_has_the_logo_and_menu_first_and_Settings_last_and_is_the_title_bar_unless_the_systems_is_used()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var main = new MainWindowViewModel(h.Services);
        main.UseShell(h.Shell);
        main.Account.Status = new AuthStatus(true, "claude.ai", null, "me@example.com", null, "max", null, null);

        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };
        window.Show();
        await UiText.SettleUntilAsync(window, () => window.GetVisualDescendants().OfType<Button>().Any(b => b is { Name: "MainMenu", IsEffectivelyVisible: true }), "the menu");
        var menu = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "MainMenu");
        var logo = window.GetVisualDescendants().OfType<Image>().Single(i => i.Name == "HeaderLogo");
        var plan = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Plan usage");
        var meters = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Usage details");
        var settings = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SettingsButton");
        double Left(Control c) => c.TranslatePoint(default, window)!.Value.X;

        // The menu at the left, and Settings after the plan.
        Assert.True(Left(menu) < Left(plan) && Left(plan) < Left(settings));
        Assert.Equal("Settings", AutomationProperties.GetName(settings));
        Assert.Same(h.Shell.OpenSettingsCommand, settings.Command);
        Assert.Equal(menu.Bounds.Size, settings.Bounds.Size);
        Assert.Equal("Menu", AutomationProperties.GetName(menu));
        // Its three lines, wider than tall, in the middle of it, not drawn from its top.
        var lines = menu.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
        var drawn = lines.TranslatePoint(lines.RenderedGeometry!.Bounds.Center, menu)!.Value;
        Assert.Equal(menu.Bounds.Width / 2, drawn.X, 0.5);
        Assert.Equal(menu.Bounds.Height / 2, drawn.Y, 0.5);
        // Its sections under small titles, which can't be picked, and an icon before each entry.
        var entries = Assert.IsType<MenuFlyout>(menu.Flyout).Items.OfType<MenuItem>().ToList();
        var sections = entries.Where(e => e.Classes.Contains("menusection")).ToList();
        var items = entries.Except(sections).ToList();
        Assert.Equal(["Help", "Claudette"], sections.Select(s => s.Header as string));
        Assert.All(sections, s => Assert.False(s.IsEnabled));
        Assert.Equal(["User Guide", "Ask on Discord", "Report an issue", "About Claudette"], items.Select(i => i.Header as string));
        Assert.All(items, i => Assert.IsType<Avalonia.Controls.Shapes.Path>(i.Icon));
        menu.Flyout!.ShowAt(menu);
        await UiText.SettleUntilAsync(window, () => items[0].Command is not null && items[0].IsEffectivelyVisible, "the open menu");
        Assert.Same(h.Shell.OpenGuideCommand, items[0].Command);
        Assert.Same(h.Shell.OpenAboutCommand, items[3].Command);
        // The line between them runs from edge to edge.
        var presenter = items[0].GetVisualAncestors().OfType<MenuFlyoutPresenter>().First();
        var line = presenter.GetVisualDescendants().OfType<Separator>().Single();
        Assert.Equal(presenter.Bounds.Width - presenter.BorderThickness.Left - presenter.BorderThickness.Right, line.Bounds.Width, 1);
        menu.Flyout.Hide();
        var sidebar = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Sidebar");
        Assert.DoesNotContain(sidebar.GetVisualDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) is "Settings" or "User Guide");

        // The title bar, as on Windows and macOS, which the tests' platform says it can be: the logo first, which drags the
        // window as the header's gaps do, and the header's controls taking their own clicks.
        Assert.True(main.MergesTitleBar);
        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.True(logo.IsEffectivelyVisible);
        Assert.True(Left(logo) < Left(menu));
        Assert.False(logo.IsHitTestVisible);
        // On Windows the logo, with room around it, is the window's icon: the window menu and double-click to close.
        var icon = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "HeaderIcon");
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(3, (int)Win32Properties.GetNonClientHitTestResult(icon));
            Assert.True(icon.IsHitTestVisible);
        }
        else
        {
            Assert.False(icon.IsHitTestVisible);
        }
        Assert.Equal(WindowDecorationsElementRole.User, Avalonia.Controls.Chrome.WindowDecorationProperties.GetElementRole(settings));
        Assert.Equal(WindowDecorationsElementRole.User, Avalonia.Controls.Chrome.WindowDecorationProperties.GetElementRole(meters));

        // The system's title bar: no logo, and Settings over the strip of icons, as wide as one, its middle on theirs.
        h.Services.Settings.Appearance.SystemTitleBar = true;
        h.Services.SaveSettings();
        await UiText.SettleUntilAsync(window, () => !logo.IsEffectivelyVisible, "the system's title bar");
        Assert.False(window.ExtendClientAreaToDecorationsHint);
        var stripIcon = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("stripicon") && b.IsEffectivelyVisible);
        double Middle(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, 0), window)!.Value.X;
        Assert.Equal(stripIcon.Bounds.Width, settings.Bounds.Width);
        Assert.Equal(Middle(stripIcon), Middle(settings), 0.5);
        window.Close();
    }

    /// <summary>
    /// The header's cup after the processes' total turns Keep this computer awake on and off, and steams while the
    /// computer is kept awake (DESIGN.md §9, "Keeping the computer awake").
    /// </summary>
    [AvaloniaFact]
    public async Task The_cup_after_the_processes_keeps_the_computer_awake_and_steams_while_it_does()
    {
        await using var h = new TabTestHarness(s =>
        {
            s.Processes.ShowMonitor = true;
            s.General.KeepAwakeWhen = KeepAwakeWhen.ClaudetteRuns;
        }, dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        h.Trees.Trees[4242].Children.Add((5001, "node"));
        h.Time.Advance(Claudette.Platform.Processes.ProcessSampler.SummaryInterval);
        var main = new MainWindowViewModel(h.Services);
        main.UseShell(h.Shell);
        main.Account.Status = new AuthStatus(true, "claude.ai", null, "me@example.com", null, "max", null, null);

        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };
        window.Show();
        await UiText.SettleUntilAsync(window, () => window.GetVisualDescendants().OfType<TextBlock>().Any(t => t is { Name: "ProcessTotals", IsEffectivelyVisible: true }), "the total");
        var totals = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ProcessTotals");
        var cup = window.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "KeepAwakeToggle");
        var steam = cup.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Name == "KeepAwakeSteam");

        // Off, it's there to turn on, after the total.
        Assert.True(cup.IsEffectivelyVisible);
        Assert.True(cup.TranslatePoint(default, window)!.Value.X > totals.TranslatePoint(default, window)!.Value.X);
        Assert.False(cup.IsChecked);
        Assert.False(steam.IsEffectivelyVisible);
        Assert.Equal("Keep this computer awake", AutomationProperties.GetName(cup));
        Assert.Equal("This computer can sleep.\nClick to keep it awake while Claudette runs.\nRight-click to choose when.", ToolTip.GetTip(cup));

        Click(window, cup);

        Assert.True(h.Services.Settings.General.KeepAwake);
        Assert.True(h.SleepBlocker.IsBlocking);
        Assert.True(cup.IsChecked);
        Assert.True(steam.IsEffectivelyVisible);
        Assert.Equal("Keep this computer awake: keeping it awake", AutomationProperties.GetName(cup));
        Assert.StartsWith("Keeping this computer awake.", ToolTip.GetTip(cup) as string, StringComparison.Ordinal);

        // Its menu chooses when: only while Claude has work, which this tab has none of.
        var menu = Assert.IsType<ContextMenu>(cup.ContextMenu);
        menu.Open(cup);
        UiText.Settle(window);
        var items = menu.Items.OfType<MenuItem>().ToArray();
        Assert.Equal(["Only while Claude has work to finish", "Whenever Claudette is running", "Keep-awake settings…"], items.Select(i => i.Header as string));
        Assert.Equal([false, true, false], items.Select(i => i.IsChecked));
        Assert.Equal("General#keepAwake", items[2].CommandParameter);
        ClickMenuItem(window, items[0]);
        menu.Close();

        Assert.Equal(KeepAwakeWhen.ClaudeHasWork, h.Services.Settings.General.KeepAwakeWhen);
        Assert.True(cup.IsChecked);
        Assert.False(h.SleepBlocker.IsBlocking);
        Assert.False(steam.IsEffectivelyVisible);

        Click(window, cup);
        Assert.False(h.Services.Settings.General.KeepAwake);
        Assert.False(cup.IsChecked);
    }

    [AvaloniaFact]
    public async Task The_chevron_draws_the_usage_header_taller_with_charts()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await using var tracker = new UsageTracker(h.Services, new UsageStore(Path.Combine(h.Root, "usage.db"), h.Time));
        using var usage = new UsageViewModel(h.Services, tracker);
        tracker.OnRateLimitEvent(RateLimitEvent(0.62, h.Time.GetUtcNow().AddHours(2).AddMinutes(14), 0.38));
        await TabTestHarness.Eventually(() => usage.HasData, "the meters");
        var main = new MainWindowViewModel(h.Services) { CurrentPage = h.Shell, Usage = usage };
        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };
        window.Show();
        UiText.Settle(window);
        var details = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UsageDetails");
        var chevron = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UsageDetailsToggle");
        Assert.False(details.IsEffectivelyVisible);
        Assert.Equal("Expand the usage header", AutomationProperties.GetName(chevron));

        Click(window, chevron);
        await UiText.SettleUntilAsync(window, () => details.IsEffectivelyVisible, "the charts");

        // The charts are made when the header first expands.
        var sessionChart = details.GetVisualDescendants().OfType<UsageDetailChart>().Single(c => c.Name == "SessionChart");
        var weekChart = details.GetVisualDescendants().OfType<UsageDetailChart>().Single(c => c.Name == "WeekChart");
        var busiest = details.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "BusiestTabs");
        Assert.True(h.Services.State.DetailedUsageHeader);
        Assert.Equal("Collapse the usage header", AutomationProperties.GetName(chevron));
        Assert.True(sessionChart.IsEffectivelyVisible && weekChart.IsEffectivelyVisible && busiest.IsEffectivelyVisible);
        // About 130 px of chart under each title, and the session gets the larger share of the width.
        Assert.InRange(sessionChart.Bounds.Height, 120, 150);
        Assert.Equal(sessionChart.Bounds.Height, weekChart.Bounds.Height);
        Assert.True(sessionChart.Bounds.Width > weekChart.Bounds.Width, $"The session chart is {sessionChart.Bounds.Width} px wide, the week's {weekChart.Bounds.Width}.");
        Assert.True(weekChart.Bounds.Width > 200, $"The weekly chart is {weekChart.Bounds.Width} px wide.");
        var wide = sessionChart.Bounds.Width;
        // They draw, empty or not.
        foreach (var chart in new[] { sessionChart, weekChart })
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)chart.Bounds.Width, (int)chart.Bounds.Height));
            bitmap.Render(chart);
        }

        // A narrow window leaves out the busiest tabs, then the weekly chart, and the session chart takes the room.
        window.Width = UsageViewModel.DetailsTabsMinWidth - 20;
        UiText.Settle(window);
        Assert.False(busiest.IsEffectivelyVisible);
        Assert.True(weekChart.IsEffectivelyVisible);
        window.Width = UsageViewModel.DetailsWeekMinWidth - 20;
        UiText.Settle(window);
        Assert.False(weekChart.IsEffectivelyVisible);
        Assert.True(sessionChart.IsEffectivelyVisible);
        Assert.True(sessionChart.Bounds.Width > wide * 0.9, $"The session chart is {sessionChart.Bounds.Width} px wide.");

        Click(window, chevron);
        UiText.Settle(window);
        Assert.False(details.IsEffectivelyVisible);
        Assert.False(h.Services.State.DetailedUsageHeader);
    }

    /// <summary>
    /// The usage meters are one compact chip at the header's right: a short label, a thin bar and the percent for each,
    /// and a click opens the popover with the detail, the sparkline and the way to the Usage panel (DESIGN.md §6).
    /// </summary>
    [AvaloniaFact]
    public async Task The_usage_chip_sits_at_the_right_and_opens_a_popover_with_the_detail()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var store = new UsageStore(Path.Combine(h.Root, "usage.db"), h.Time);
        var reset = h.Time.GetUtcNow().AddHours(2);
        Assert.True(store.AddSample(new UsageSnapshot(
            [
                new LimitReading(LimitKind.Session, "Session", 62, reset, null, true),
                new LimitReading(LimitKind.WeeklyAll, "Weekly", 80, reset.AddDays(3), null, true),
                new LimitReading(LimitKind.WeeklyModel, "Fable", 100, reset.AddDays(3), null, true),
            ],
            h.Time.GetUtcNow(),
            UsageSource.GetUsage)));
        await using var tracker = new UsageTracker(h.Services, store);
        using var usage = new UsageViewModel(h.Services, tracker);
        await TabTestHarness.Eventually(() => usage.Weekly.Count == 2, "the weekly meters");
        var main = new MainWindowViewModel(h.Services) { CurrentPage = h.Shell, Usage = usage };
        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };
        window.Show();
        UiText.Settle(window);
        var chip = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UsageChip");
        var meters = chip.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "ChipMeters");
        var chevron = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UsageDetailsToggle");
        Point At(Control c) => c.TranslatePoint(default, window)!.Value;

        // One line: S, W and F, each with a bar and its percent, ending before the chevron.
        Assert.Equal(["S", "W", "F"], usage.Chip.Items.Select(i => i.ShortLabel));
        Assert.Equal(["62%", "80%", "100%"], usage.Chip.Items.Select(i => i.PercentText));
        var bars = meters.GetVisualDescendants().OfType<ProgressBar>().ToArray();
        Assert.Equal(3, bars.Length);
        Assert.All(bars, b => Assert.True(b.IsEffectivelyVisible));
        Assert.True(bars[1].Classes.Contains("warning") && bars[2].Classes.Contains("critical") && !bars[0].Classes.Contains("warning"));
        Assert.Equal(At(bars[0]).Y, At(bars[2]).Y);
        Assert.True(chip.Bounds.Height < 40, $"The chip is {chip.Bounds.Height} px high.");
        Assert.True(At(chip).X > window.Width / 2, "The chip is at the header's right.");
        Assert.True(At(chip).X + chip.Bounds.Width <= At(chevron).X + 1, "The chip ends where the chevron begins.");
        Assert.Contains("Weekly 80%, resets", AutomationProperties.GetName(chip), StringComparison.Ordinal);
        Assert.Contains("Session 62%, resets in 2h", AutomationProperties.GetName(chip), StringComparison.Ordinal);
        Assert.True(chip.Focusable);

        // A click opens the popover: every meter with its reset, the sparkline, and the link to the Usage panel.
        Click(window, chip);
        UiText.Settle(window);
        var flyout = Assert.IsType<Flyout>(chip.Flyout);
        Assert.True(flyout.IsOpen);
        var popover = Assert.IsAssignableFrom<Control>(flyout.Content);
        var texts = popover.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
        Assert.Contains("Fable", texts);
        Assert.Contains("80%", texts);
        Assert.Contains(texts, t => t?.StartsWith("resets in ", StringComparison.Ordinal) == true);
        Assert.Contains(texts, t => t?.StartsWith("resets ", StringComparison.Ordinal) == true && !t.StartsWith("resets in", StringComparison.Ordinal));
        Assert.Single(popover.GetVisualDescendants().OfType<UsageChart>());
        var link = popover.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UsagePanelLink");
        Assert.Equal("Open the Usage panel", link.Content);
        var opened = 0;
        usage.ShowUsagePanel = () =>
        {
            opened++;
            return Task.CompletedTask;
        };
        link.Command!.Execute(null);
        Assert.Equal(1, opened);

        // Esc closes it.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        UiText.Settle(window);
        Assert.False(flyout.IsOpen);

        // A narrow window drops the bars and keeps the labels and percents, clear of the title area.
        window.Width = UsageChipViewModel.BarsMinWidth - 20;
        UiText.Settle(window);
        Assert.All(bars, b => Assert.False(b.IsEffectivelyVisible));
        Assert.True(chip.IsEffectivelyVisible);
        Assert.True(At(chip).X + chip.Bounds.Width <= window.Width);
        window.Width = 1200;
        UiText.Settle(window);
        Assert.All(bars, b => Assert.True(b.IsEffectivelyVisible));
    }

    [AvaloniaFact]
    public async Task Typing_in_the_composer_and_pressing_Enter_sends_the_message()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var composer = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Composer");

        composer.Focus();
        window.KeyTextInput("Fix the build");
        UiText.Settle(window);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        UiText.Settle(window);

        await TabTestHarness.Eventually(() => h.Transport.Sent.Any(IsUserMessage("Fix the build")), "the message");
        Assert.True(string.IsNullOrEmpty(composer.Text));
        // Shift+Enter is a new line, not a send.
        window.KeyTextInput("one");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        UiText.Settle(window);
        Assert.Single(h.Transport.Sent, m => m["type"]?.GetValue<string>() == "user");
    }

    [AvaloniaFact]
    public async Task While_Claude_works_a_twinkling_line_sits_above_the_composer()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        // Before the tab opens: its start reads the verbs too, in the background, and a read from before the file was
        // written could finish after the one below and put the built-in verbs back.
        var settings = Path.Combine(h.WorkFolder, ".claude", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        await File.WriteAllTextAsync(settings, """{ "spinnerVerbs": { "mode": "replace", "verbs": ["Noodling"] } }""", TestContext.Current.CancellationToken);
        var tab = await h.OpenTabAsync();
        await tab.LoadSpinnerVerbsAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var line = window.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "WorkingLine");
        Assert.False(line.IsEffectivelyVisible);

        tab.ComposerText = "Fix the build";
        await tab.SendCommand.ExecuteAsync(null);
        await UiText.SettleUntilAsync(window, () => line.IsEffectivelyVisible, "the working line");

        // The Stop shortcut as it reads on this OS: "Esc", or "⎋" on macOS.
        var stop = h.Services.Tips.Text(KeyboardShortcuts.Stop);
        Assert.Equal($"· Noodling… 0s · {stop} to stop", string.Join(' ', line.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));

        // A frame drawn in a taller font (✶ falls back to Consolas on Windows once a code block has used it) doesn't make
        // the line taller, which would move the conversation above it as the glyph twinkles.
        var height = line.Bounds.Height;
        var glyph = line.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("working-glyph"));
        glyph.FontSize = 40;
        UiText.Settle(window);
        Assert.Equal(height, line.Bounds.Height);

        h.Transport.Emit("""{"type":"result","subtype":"success","is_error":false,"session_id":"s1"}""");
        await UiText.SettleUntilAsync(window, () => !line.IsEffectivelyVisible, "the end of the turn");
    }

    /// <summary>
    /// In a narrow pane nothing in the composer runs past its edge into the side panel beside it (DESIGN.md §5): a long
    /// command on the working line trims, the chips wrap under each other, and the folder on the status line trims.
    /// </summary>
    [AvaloniaFact]
    public async Task In_a_narrow_pane_the_composer_stays_within_its_width()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, width: 700);
        tab.ComposerText = "Fix the build";
        await tab.SendCommand.ExecuteAsync(null);
        tab.Working.SetActivity("Running cd /d/Workspaces/dotBunny/DETHOL/Projects/DETHOL && dotnet test --filter Category!=Live…");
        var line = window.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "WorkingLine");
        await UiText.SettleUntilAsync(window, () => line.IsEffectivelyVisible, "the working line");

        var composer = window.GetVisualDescendants().OfType<ComposerView>().Single();
        double Right(Visual c) => c.TranslatePoint(new Point(c.Bounds.Width, 0), window)!.Value.X;
        var overrun = composer.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && c is TextBlock or Button && c.FindAncestorOfType<MascotLayer>() is null)
            .Where(c => Right(c) > Right(composer) + 0.5)
            .Select(c => $"{c.GetType().Name} {(c as TextBlock)?.Text ?? AutomationProperties.GetName(c)} ends at {Right(c)}")
            .ToList();
        Assert.True(overrun.Count == 0, $"Past the composer's right edge at {Right(composer)}:\n{string.Join('\n', overrun)}");
    }

    [AvaloniaFact]
    public async Task A_permission_card_shows_the_command_and_a_click_on_Allow_answers_it()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        tab.ComposerText = "clean up";
        await tab.SendCommand.ExecuteAsync(null);
        var window = UiText.Show(new ShellView { DataContext = h.Shell });

        h.Transport.Emit("""{"type":"control_request","request_id":"p1","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{"command":"rm -rf build","description":"Delete the build output"}}}""");
        await TabTestHarness.Eventually(() => tab.Status == TabStatus.NeedsInput, "the prompt");
        UiText.Settle(window);
        var conversation = window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "ConversationItems");
        var shown = UiText.Describe(conversation, (h.Root, "{root}"));

        var allow = conversation.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Allow");
        Click(window, allow);

        await TabTestHarness.Eventually(() => h.Transport.Sent.Any(m =>
            m["type"]?.GetValue<string>() == "control_response" && m["response"]?["request_id"]?.GetValue<string>() == "p1"
            && m["response"]?["response"]?["behavior"]?.GetValue<string>() == "allow"), "the answer");
        Assert.NotEqual(TabStatus.NeedsInput, tab.Status);
        // Verify resumes off the UI thread, so it comes last.
        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task The_sidebar_shortcut_collapses_it_to_the_rail()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var primary = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPressQwerty(PhysicalKey.B, primary);
        window.KeyReleaseQwerty(PhysicalKey.B, primary);
        UiText.Settle(window);

        Assert.True(h.Shell.Layout.IsSidebarCollapsed);
        var sidebar = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "Sidebar");
        await Verify(UiText.Describe(sidebar, (h.Root, "{root}")));
    }

    [AvaloniaFact]
    public async Task Typing_a_slash_lists_matching_commands_and_Enter_picks_one()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var initialize = h.Transport.Answers["initialize"];
        h.Transport.Answers["initialize"] = request =>
        {
            var answer = initialize(request)!;
            answer["commands"] = new JsonArray(
                new JsonObject { ["name"] = "compact", ["description"] = "Clear the conversation but keep a summary", ["argumentHint"] = "" },
                new JsonObject { ["name"] = "review", ["description"] = "Review a pull request", ["argumentHint"] = "<pr>" },
                new JsonObject { ["name"] = "release-notes", ["description"] = "View release notes", ["argumentHint"] = "" });
            return answer;
        };
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var composer = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Composer");

        composer.Focus();
        window.KeyTextInput("/re");
        UiText.Settle(window);
        var popup = window.GetVisualDescendants().OfType<Popup>().Single(p => p.Name == "CompletionPopup");
        Assert.True(popup.IsOpen);
        var listed = UiText.Describe(popup.Child!);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        UiText.Settle(window);

        Assert.StartsWith("/review", composer.Text, StringComparison.Ordinal);
        Assert.False(popup.IsOpen);
        // Picking a command doesn't send it.
        Assert.DoesNotContain(h.Transport.Sent, m => m["type"]?.GetValue<string>() == "user");
        await Verify(listed);
    }

    [AvaloniaFact]
    public async Task A_folder_typed_after_an_at_lists_its_entries_with_ignored_ones_dimmed()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        Directory.CreateDirectory(Path.Combine(h.WorkFolder, "node_modules", "lib"));
        Directory.CreateDirectory(Path.Combine(h.WorkFolder, "src"));
        await File.WriteAllTextAsync(Path.Combine(h.WorkFolder, "src", "app.cs"), "", TestContext.Current.CancellationToken);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var composer = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Composer");
        var popup = window.GetVisualDescendants().OfType<Popup>().Single(p => p.Name == "CompletionPopup");

        composer.Focus();
        window.KeyTextInput("@./");
        await Waiting.UntilAsync(() => popup.IsOpen && tab.Completions.Items.Count == 2, "the folder's entries", poll: () => UiText.Settle(window));
        UiText.Settle(window);

        // node_modules is a folder the list skips outside git, so it's dimmed, as the Project Files explorer has it.
        var rows = popup.Child!.GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("completionrow")).ToArray();
        Assert.Equal([0.5, 1.0], rows.Select(r => r.Opacity));
        await Verify(UiText.Describe(popup.Child!));
    }

    [AvaloniaFact]
    public async Task Sync_to_other_machines_ticks_in_the_tab_menu_and_marks_the_row()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var icon = window.GetVisualDescendants().OfType<Border>().Single(b => AutomationProperties.GetName(b) == "Synced to the session library");
        var menu = icon.FindAncestorOfType<Button>()!.ContextMenu!;
        menu.Open(icon.FindAncestorOfType<Button>()!);
        UiText.Settle(window);
        var item = menu.Items.OfType<MenuItem>().Single(m => m.Header as string == "Sync to other machines");
        var syncNow = menu.Items.OfType<MenuItem>().Single(m => m.Header as string == "Sync now");
        Assert.False(icon.IsEffectivelyVisible);
        Assert.False(item.IsChecked);
        Assert.False(syncNow.IsVisible);

        ClickMenuItem(window, item);

        Assert.True(tab.SyncToLibrary);
        Assert.True(item.IsChecked);
        Assert.True(icon.IsEffectivelyVisible);
        // Sync now comes with syncing, disabled until there's a session to copy, with the reason as its tip.
        Assert.True(syncNow.IsVisible);
        Assert.False(syncNow.IsEffectivelyEnabled);
        Assert.Equal(tab.SyncNowTip, ToolTip.GetTip(syncNow));

        ClickMenuItem(window, item);

        Assert.False(tab.SyncToLibrary);
        Assert.False(item.IsChecked);
        Assert.False(icon.IsEffectivelyVisible);
        Assert.False(syncNow.IsVisible);

        // Another machine has the session open, so it can't sync from here too: the item unticks again.
        tab.State.SessionId = "held-1";
        var source = Path.Combine(h.Root, "held-1.jsonl");
        File.WriteAllText(source, "{}\n");
        await h.Services.Library.Library.SaveAsync(new SessionRecord { SessionId = "held-1", Machine = "LAPTOP-02", LastUsed = h.Time.GetUtcNow() }, source, subagentsDirectory: null);
        File.WriteAllText(Path.Combine(h.Services.Library.Library.GetSessionFolder("held-1"), LeaseManager.FileName),
            new JsonObject { ["machine"] = "LAPTOP-02", ["owner"] = "other", ["updatedAt"] = h.Time.GetUtcNow().ToString("O") }.ToJsonString());

        ClickMenuItem(window, item);

        Assert.False(tab.SyncToLibrary);
        Assert.False(item.IsChecked);
        Assert.False(icon.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task A_tabs_row_has_a_ring_for_its_context_with_the_context_in_its_tip()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        h.Transport.Answers["get_context_usage"] = _ => new JsonObject
        {
            ["totalTokens"] = 150000, ["maxTokens"] = 200000, ["percentage"] = 75, ["autoCompactThreshold"] = 160000, ["isAutoCompactEnabled"] = true,
        };
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });

        var ring = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ContextRing");
        Assert.True(ring.IsEffectivelyVisible);
        Assert.Same(tab, ring.FindAncestorOfType<Button>()!.DataContext);
        Assert.Contains("tabrow", ring.FindAncestorOfType<Button>()!.Classes);
        Assert.Equal("Context 75% (150,000 of 200,000 tokens · auto-compacts at 160,000)", ToolTip.GetTip(ring));
        Assert.Equal("Context 75%", AutomationProperties.GetName(ring));
        // Three quarters round, in amber: near the point where Claude Code compacts by itself.
        var arc = ring.GetVisualDescendants().OfType<Arc>().Single();
        Assert.Equal(270, arc.SweepAngle, precision: 6);
        Assert.Same(arc.FindResource(arc.ActualThemeVariant, "MeterWarningBrush"), arc.Stroke);
        Assert.Equal(new Size(16, 16), ring.Bounds.Size);

        // Settings → Appearance can turn it off.
        h.Services.Settings.Appearance.ShowContextOnTabs = false;
        h.Services.SaveSettings();
        UiText.Settle(window);

        Assert.False(ring.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Copy_on_a_code_block_copies_its_code_and_says_Copied_for_a_moment()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        h.Transport.EmitTurn("Run the tests:\n\n```bash\ndotnet build\ndotnet test\n```\n\nBoth should pass.");
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => window.GetVisualDescendants().OfType<CodeBlock>().Any(b => b.Inlines.Count > 0), "the code block");
        var block = window.GetVisualDescendants().OfType<CodeBlock>().Single();
        var copy = block.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Copy code");
        var shown = UiText.Describe(block);

        Click(window, copy);
        await UiText.SettleUntilAsync(window, () => h.Platform.Clipboard is not null, "the copied code");

        // The code, without the fences, in the OS's line endings.
        Assert.Equal($"dotnet build{Environment.NewLine}dotnet test", h.Platform.Clipboard);
        Assert.Contains("copied", block.Classes);
        Assert.Equal("[button] Copy code: Copied", UiText.Describe(copy).Trim());

        h.Time.Advance(TabViewModel.CopiedFor);
        UiText.Settle(window);

        Assert.DoesNotContain("copied", block.Classes);
        Assert.Equal("[button] Copy code: Copy", UiText.Describe(copy).Trim());
        // Verify resumes off the UI thread, so it comes last.
        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task A_replys_time_shows_on_its_card_and_Copy_on_hover_and_on_keyboard_focus()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        h.Transport.EmitTurn("Found **two** problems.");
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => UiText.Describe(window).Contains("Found two problems.", StringComparison.Ordinal), "the reply");
        var reply = window.GetVisualDescendants().OfType<Card>().Single(c => c.Classes.Contains("claude"));
        var tools = reply.GetVisualDescendants().OfType<ContentPresenter>().Single(c => c.Name == "PART_Actions");
        var copy = tools.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Copy message");
        // The time is on the card's header line, always (DESIGN.md §18, "Conversation cards").
        var time = reply.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Time");
        Assert.True(time.IsEffectivelyVisible);
        Assert.Equal(0, tools.Opacity);
        Assert.False(tools.IsHitTestVisible);
        Assert.Equal("12:00", time.Text);
        Assert.Equal("Monday, 28 September 2026 12:00", ToolTip.GetTip(time));

        window.MouseMove(reply.TranslatePoint(new Point(20, reply.Bounds.Height / 2), window)!.Value);
        UiText.Settle(window);
        Assert.Equal(1, tools.Opacity);
        Click(window, copy);
        await UiText.SettleUntilAsync(window, () => h.Platform.Clipboard is not null, "the copied reply");

        // The reply's Markdown, as Claude wrote it.
        Assert.Equal("Found **two** problems.", h.Platform.Clipboard);
        Assert.Equal("[button] Copy message: Copied", UiText.Describe(copy).Trim());

        // Away from it, and with the focus elsewhere, it's quiet again; tabbing to its button shows it too.
        window.MouseMove(new Point(600, 700));
        window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Composer").Focus();
        h.Time.Advance(TabViewModel.CopiedFor);
        UiText.Settle(window);
        Assert.Equal(0, tools.Opacity);
        copy.Focus(NavigationMethod.Tab);
        UiText.Settle(window);
        Assert.Equal(1, tools.Opacity);
        Assert.Equal("[button] Copy message", UiText.Describe(copy).Trim());
    }

    [AvaloniaFact]
    public async Task Compact_density_tightens_the_rows_the_conversation_and_the_composer()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        tab.ComposerText = "Fix the build";
        await tab.SendCommand.ExecuteAsync(null);
        h.Transport.EmitTurn("Done.");
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => UiText.Describe(window).Contains("Done.", StringComparison.Ordinal), "the reply");
        var row = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("tabrow"));
        // The conversation is virtualized; each item keeps its distance from the next with its margin.
        var items = window.GetVisualDescendants().OfType<ConversationPanel>().Single(p => p.Classes.Contains("conversation"));
        Thickness Gap() => items.Children.OfType<ContentPresenter>().First().Margin;
        var composer = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ComposerBox");
        var comfortable = (Row: row.Bounds.Height, Composer: composer.Bounds.Height);
        Assert.Equal(new Thickness(6, 5, 4, 5), row.Padding);
        Assert.Equal(new Thickness(0, 0, 0, 8), Gap());

        h.Services.Settings.Appearance.Density = Density.Compact;
        h.Services.SaveSettings();
        UiText.Settle(window);

        Assert.Contains("compact", window.GetVisualDescendants().OfType<ShellView>().Single().Classes);
        Assert.Equal(new Thickness(6, 2, 4, 2), row.Padding);
        Assert.Equal(comfortable.Row - 6, row.Bounds.Height, precision: 3);
        Assert.Equal(new Thickness(0, 0, 0, 3), Gap());
        Assert.True(composer.Bounds.Height < comfortable.Composer, $"The composer is {composer.Bounds.Height} px high, as before.");

        // Back to Comfortable, as it was.
        h.Services.Settings.Appearance.Density = Density.Comfortable;
        h.Services.SaveSettings();
        UiText.Settle(window);

        Assert.Equal(comfortable.Row, row.Bounds.Height, precision: 3);
        Assert.Equal(comfortable.Composer, composer.Bounds.Height, precision: 3);
    }

    [AvaloniaFact]
    public async Task Full_width_conversation_lets_the_conversation_and_the_composer_fill_the_tab()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        tab.ComposerText = "Fix the build";
        await tab.SendCommand.ExecuteAsync(null);
        h.Transport.EmitTurn("Done.");
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, width: 1800);
        await UiText.SettleUntilAsync(window, () => UiText.Describe(window).Contains("Done.", StringComparison.Ordinal), "the reply");
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "ConversationScroll");
        var conversation = window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "ConversationItems");
        var composer = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Classes.Contains("composerarea"));
        // Off by default: a column that's easy to read, in the middle of a wide window.
        Assert.Equal(900, conversation.Bounds.Width, precision: 3);
        Assert.Equal(948, composer.Bounds.Width, precision: 3);

        h.Services.Settings.Appearance.FullWidthConversation = true;
        h.Services.SaveSettings();
        UiText.Settle(window);

        Assert.Contains("fullwidth", window.GetVisualDescendants().OfType<ShellView>().Single().Classes);
        Assert.Equal(scroll.Viewport.Width - conversation.Margin.Left - conversation.Margin.Right, conversation.Bounds.Width, precision: 3);
        Assert.True(composer.Bounds.Width > 948, $"The composer is {composer.Bounds.Width} px wide, as before.");

        h.Services.Settings.Appearance.FullWidthConversation = false;
        h.Services.SaveSettings();
        UiText.Settle(window);

        Assert.Equal(900, conversation.Bounds.Width, precision: 3);
        Assert.Equal(948, composer.Bounds.Width, precision: 3);
    }

    /// <summary>What the menu does with a click on a check item: it ticks or unticks the item, then raises Click.</summary>
    private static void ClickMenuItem(Window window, MenuItem item)
    {
        item.SetCurrentValue(MenuItem.IsCheckedProperty, !item.IsChecked);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiText.Settle(window);
    }

    [AvaloniaFact]
    public async Task Pasting_a_screenshot_shows_a_thumbnail_that_can_be_removed()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var composer = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Composer");
        var strip = window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "AttachmentList");
        Assert.False(strip.IsVisible);
        h.Platform.ClipboardImage = Png;

        composer.Focus();
        window.KeyTextInput("The walk takes half the screen");
        PressPaste(window);
        await UiText.SettleUntilAsync(window, () => strip.IsVisible && strip.GetVisualDescendants().OfType<Image>().Any(), "the thumbnail");

        var thumbnail = strip.GetVisualDescendants().OfType<Image>().Single();
        Assert.IsType<Bitmap>(thumbnail.Source);
        // Shown small whatever the image's size: 56 px high, at most 120 wide.
        Assert.Equal(56, thumbnail.Bounds.Height);
        Assert.True(thumbnail.Bounds.Width <= 120, $"The thumbnail is {thumbnail.Bounds.Width} px wide.");
        Assert.Equal("The walk takes half the screen", composer.Text);
        Assert.True(tab.SendCommand.CanExecute(null));
        var shown = UiText.Describe(strip) + UiText.Describe(composer);

        var remove = strip.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Remove image");
        Click(window, remove);

        Assert.Empty(tab.Attachments);
        Assert.False(strip.IsVisible);
        Assert.Empty(strip.GetVisualDescendants().OfType<Image>());
        Assert.Equal("The walk takes half the screen", composer.Text);
        // Verify resumes off the UI thread, so it comes last.
        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task Pasting_text_that_comes_with_a_picture_of_it_pastes_the_text()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var composer = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Composer");
        // The text box pastes text from the window's own clipboard; the tab looks at the fake one.
        await window.Clipboard!.SetTextAsync("=SUM(A1:A3)");
        await h.Platform.SetClipboardTextAsync("=SUM(A1:A3)");
        h.Platform.ClipboardImage = Png;

        composer.Focus();
        PressPaste(window);
        await UiText.SettleUntilAsync(window, () => composer.Text == "=SUM(A1:A3)", "the pasted text");

        Assert.Empty(tab.Attachments);
        // Paste on the text box's context menu calls the same method as the shortcut.
        await h.Platform.SetClipboardTextAsync("");
        composer.Paste();
        await UiText.SettleUntilAsync(window, () => tab.Attachments.Count == 1, "the pasted image");
        Assert.Equal("=SUM(A1:A3)", composer.Text);
    }

    /// <summary>The text box's own paste shortcut, as the platform defines it: Ctrl+V, or Cmd+V on macOS.</summary>
    private static void PressPaste(Window window)
    {
        var paste = Application.Current!.PlatformSettings!.HotkeyConfiguration.Paste[0];
        Assert.Equal(Key.V, paste.Key);
        // KeyModifiers and RawInputModifiers use the same bits.
        var modifiers = (RawInputModifiers)(int)paste.KeyModifiers;
        window.KeyPressQwerty(PhysicalKey.V, modifiers);
        window.KeyReleaseQwerty(PhysicalKey.V, modifiers);
        UiText.Settle(window);
    }

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static void Click(Window window, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        UiText.Settle(window);
    }

    private static Func<JsonObject, bool> IsUserMessage(string text) => m =>
        m["type"]?.GetValue<string>() == "user" && m.ToJsonString().Contains(text, StringComparison.Ordinal);

    private static Core.Protocol.RateLimitEventMessage RateLimitEvent(double session, DateTimeOffset resetsAt, double weekly)
    {
        var info = new JsonObject
        {
            ["status"] = "allowed",
            ["rateLimitType"] = "five_hour",
            ["unifiedWindows"] = new JsonObject
            {
                ["five_hour"] = new JsonObject { ["utilization"] = session, ["resetsAt"] = resetsAt.ToUnixTimeSeconds() },
                ["seven_day"] = new JsonObject { ["utilization"] = weekly, ["resetsAt"] = resetsAt.AddDays(3).ToUnixTimeSeconds() },
            },
        };
        return new Core.Protocol.RateLimitEventMessage(info, new JsonObject { ["type"] = "rate_limit_event", ["rate_limit_info"] = info.DeepClone() });
    }
}
