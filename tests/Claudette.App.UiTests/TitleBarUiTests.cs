using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Claudette.App.Controls;

namespace Claudette.App.UiTests;

/// <summary>
/// The title bar of a window's own (DESIGN.md §3, "Window frame"), at the top of each window but the main one: while
/// windows draw their own, the window draws into it, with its title unless what's in it names what the window shows, and
/// each control in it takes its own clicks; with the system's title bar, it's only what's in it, and with nothing, gone.
/// </summary>
public class TitleBarUiTests
{
    private static (Window Window, TitleBar Bar, Button Button) Open(bool merged, bool withContent, bool showsTitle = true)
    {
        var button = new Button { Content = "Reviewed" };
        var bar = new TitleBar { Merges = merged, ShowsTitle = showsTitle, Content = withContent ? button : null };
        var window = new Window { Width = 500, Height = 300, Title = "Usage", Content = new DockPanel { Children = { bar, new Border() } } };
        DockPanel.SetDock(bar, Dock.Top);
        window.Show();
        UiText.Settle(window);
        return (window, bar, button);
    }

    private static TextBlock Title(TitleBar bar) => bar.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Title");

    [AvaloniaFact]
    public void Drawn_by_the_window_it_has_the_windows_title_and_its_controls_take_their_own_clicks()
    {
        var (window, bar, button) = Open(merged: true, withContent: true);

        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.True(bar.IsVisible);
        Assert.Equal("Usage", Title(bar).Text);
        Assert.True(Title(bar).IsEffectivelyVisible);
        Assert.Equal(WindowDecorationsElementRole.User, WindowDecorationProperties.GetElementRole(button));
        // Renamed, it follows.
        window.Title = "Usage, this week";
        UiText.Settle(window);
        Assert.Equal("Usage, this week", Title(bar).Text);
        window.Close();
    }

    [AvaloniaFact]
    public void Naming_what_the_window_shows_itself_it_leaves_the_title_out()
    {
        var (window, bar, _) = Open(merged: true, withContent: true, showsTitle: false);

        Assert.True(bar.IsVisible);
        Assert.False(Title(bar).IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Under_the_systems_title_bar_it_is_only_what_is_in_it_and_gone_with_nothing()
    {
        var (window, bar, button) = Open(merged: false, withContent: true);
        Assert.False(window.ExtendClientAreaToDecorationsHint);
        Assert.True(bar.IsVisible);
        Assert.False(Title(bar).IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyVisible);
        window.Close();

        var (empty, emptyBar, _) = Open(merged: false, withContent: false);
        Assert.False(emptyBar.IsVisible);
        // Turned on again, as Settings → Appearance does, it comes back with the title.
        emptyBar.Merges = true;
        UiText.Settle(empty);
        Assert.True(emptyBar.IsVisible);
        Assert.True(empty.ExtendClientAreaToDecorationsHint);
        Assert.True(Title(emptyBar).IsEffectivelyVisible);
        empty.Close();
    }
}
