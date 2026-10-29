using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.Views;

namespace Claudette.App.UiTests;

/// <summary>A source build's new commits upstream, rendered (DESIGN.md §9, "New commits upstream"): the sidebar's badge, its dialog and Settings → General.</summary>
public class SourceUpdateUiTests
{
    private static List<string?> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Concat(t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text) ?? []))];

    private static List<string> Buttons(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Content as string).OfType<string>()];

    [AvaloniaFact]
    public async Task The_sidebar_badge_opens_the_new_commits_with_what_can_be_done()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(launcher: launcher, dispatcher: new AvaloniaUiDispatcher());
        using var source = await FakeCheckout.StartAsync(h, launcher);
        h.Shell.SourceUpdate = source.Badge;
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1200, 800);
        var badge = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SourceUpdateBadge");
        await UiText.SettleUntilAsync(window, () => badge.IsEffectivelyVisible, "the badge");
        Assert.Contains("3 new commits on origin/main", Texts(badge));

        badge.Flyout!.ShowAt(badge);
        var dialog = Assert.IsType<StackPanel>(((Flyout)badge.Flyout).Content);
        await UiText.SettleUntilAsync(window, () => Texts(dialog).Contains("Commit 3"), "the dialog");

        var texts = Texts(dialog);
        Assert.Equal(["Commit 3", "Commit 2", "Commit 1"], texts.Where(t => t?.StartsWith("Commit ", StringComparison.Ordinal) == true));
        Assert.Contains(texts, t => t?.StartsWith("3333333 · Ada · ", StringComparison.Ordinal) == true);
        Assert.Equal(["Pull, build and restart", "Pull and build", "Skip these commits"], Buttons(dialog));
        badge.Flyout.Hide();
        window.Close();
    }

    /// <summary>
    /// The sidebar's head has New tab, History and collapsing, and its foot, under a line, shows only while a badge does
    /// (DESIGN.md §4, "Sidebar").
    /// </summary>
    [AvaloniaFact]
    public async Task The_sidebars_head_has_History_and_its_foot_shows_only_while_a_badge_does()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(launcher: launcher, dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1200, 800);
        Button Named(string name) => window.GetVisualDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == name && b.IsEffectivelyVisible);
        double Left(Control c) => c.TranslatePoint(default, window)!.Value.X;
        double Top(Control c) => c.TranslatePoint(default, window)!.Value.Y;

        var (newTab, history, collapse) = (Named("New tab"), Named("History"), Named("Collapse the sidebar"));
        Assert.True(Left(newTab) < Left(history) && Left(history) < Left(collapse));
        Assert.Equal(Top(collapse), Top(history), 0.5);
        var foot = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidebarFoot");
        Assert.False(foot.IsVisible);

        using var source = await FakeCheckout.StartAsync(h, launcher);
        h.Shell.SourceUpdate = source.Badge;
        await UiText.SettleUntilAsync(window, () => foot.IsEffectivelyVisible, "the foot");
        Assert.True(window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SourceUpdateBadge").IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_dialog_says_why_it_wont_pull_over_the_checkouts_own_work()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(launcher: launcher, dispatcher: new AvaloniaUiDispatcher());
        using var source = await FakeCheckout.StartAsync(h, launcher, configure: c => c.HasChanges = true);
        h.Shell.SourceUpdate = source.Badge;
        var window = UiText.Show(new ShellView { DataContext = h.Shell }, 1200, 800);
        var badge = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SourceUpdateBadge");
        await UiText.SettleUntilAsync(window, () => badge.IsEffectivelyVisible, "the badge");

        badge.Flyout!.ShowAt(badge);
        var dialog = Assert.IsType<StackPanel>(((Flyout)badge.Flyout).Content);
        await UiText.SettleUntilAsync(window, () => Texts(dialog).Contains("Commit 3"), "the dialog");

        Assert.Contains("The checkout has uncommitted changes. Commit or stash them to pull.", Texts(dialog));
        Assert.Equal(["Skip these commits"], Buttons(dialog));
        badge.Flyout.Hide();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Settings_shows_the_checkout_in_place_of_releases()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(launcher: launcher, dispatcher: new AvaloniaUiDispatcher());
        using var source = await FakeCheckout.StartAsync(h, launcher);
        using var settings = new SettingsViewModel(h.Services, "me@example.com") { SelectedCategory = "General", SourceUpdates = source.Badge };
        var window = new SettingsWindow { DataContext = settings, Width = 900, Height = 700 };
        window.Show();
        await UiText.SettleUntilAsync(window, () => Texts(window).Contains("3 new commits on origin/main."), "the check's result");

        Assert.Contains(Texts(window), t => t?.StartsWith("This is a source build, so instead of releases it checks the branch its checkout tracks", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<CheckBox>(), c => c.IsEffectivelyVisible && c.Content as string == "Include pre-releases");
        var buttons = Buttons(window);
        Assert.Contains("Check now", buttons);
        Assert.Contains("Pull, build and restart", buttons);
        window.Close();
    }
}
