using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.Views;

namespace Claudette.App.UiTests;

/// <summary>
/// The sidebar's menus for a tab and a group, in the full sidebar and the rail (DESIGN.md §4, "Sidebar"): in sections
/// under small titles, as the header's ≡ menu, with an icon before each entry.
/// </summary>
public class SidebarMenusUiTests
{
    /// <summary>Entries that follow the one they belong to, and go without an icon of their own.</summary>
    private static readonly string[] Secondary = ["Reset name", "Clear mark", "Close pane", "Sync now", "Open in the Claude app", "Close other tabs"];

    [AvaloniaFact]
    public async Task A_tabs_menu_is_in_sections_with_an_icon_before_each_entry()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var menu = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("tabrow")).ContextMenu!;

        Assert.Contains("sectioned", menu.Classes);
        Assert.Equal(["Tab", "View", "Session", "Other devices"], Sections(menu));
        Assert.Equal("Tab", SectionOf(menu, "Rename"));
        Assert.Equal("Tab", SectionOf(menu, "Tab settings…"));
        Assert.Equal("View", SectionOf(menu, "Steps"));
        Assert.Equal("View", SectionOf(menu, "Move down"));
        Assert.Equal("Session", SectionOf(menu, "Thread"));
        Assert.Equal("Session", SectionOf(menu, "Copy session ID"));
        Assert.Equal("Other devices", SectionOf(menu, "Connect to the Claude app"));
        Assert.Null(SectionOf(menu, "Close"));
        AssertIcons(menu);

        // The rail's is in the same sections, less what it leaves out.
        h.Shell.Layout.ToggleSidebarCommand.Execute(null);
        UiText.Settle(window);
        var rail = window.GetVisualDescendants().OfType<MarkIcon>().Single(m => m.Name == "RailMark").FindAncestorOfType<Button>()!.ContextMenu!;
        Assert.Equal(["Tab", "View", "Session", "Other devices"], Sections(rail));
        Assert.Null(SectionOf(rail, "Close"));
        AssertIcons(rail);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_groups_menu_is_in_sections_with_an_icon_before_each_entry()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var group = h.Shell.Groups.Single();
        var menu = GroupLabel(window).ContextMenu!;

        Assert.Contains("sectioned", menu.Classes);
        Assert.Equal(["Tabs", "Folder", "Group"], Sections(menu));
        Assert.Equal("Tabs", SectionOf(menu, "New tab here"));
        Assert.Equal("Folder", SectionOf(menu, "History for this folder"));
        Assert.Equal("Group", SectionOf(menu, "Rename…"));
        Assert.Null(SectionOf(menu, "Close group"));
        AssertIcons(menu);
        // Change color's icon is the group's color.
        menu.Open(GroupLabel(window));
        UiText.Settle(window);
        var color = menu.Items.OfType<MenuItem>().Single(m => m.Header as string == "Change color…");
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(group.Brush).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(Assert.IsType<Ellipse>(color.Icon).Fill).Color);
        menu.Close();

        h.Shell.Layout.ToggleSidebarCommand.Execute(null);
        UiText.Settle(window);
        var rail = GroupLabel(window).ContextMenu!;
        Assert.NotSame(menu, rail);
        Assert.Equal(["Tabs", "Folder", "Group"], Sections(rail));
        AssertIcons(rail);
        window.Close();
    }

    private static Button GroupLabel(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("grouplabel") && b.IsEffectivelyVisible);

    private static List<string?> Sections(ContextMenu menu) =>
        [.. menu.Items.OfType<MenuItem>().Where(m => m.Classes.Contains("menusection")).Select(m => m.Header as string)];

    /// <summary>The title of the section the entry is in, or none after the last line.</summary>
    private static string? SectionOf(ContextMenu menu, string header)
    {
        string? section = null;
        foreach (var item in menu.Items)
        {
            switch (item)
            {
                case Separator:
                    section = null;
                    break;
                case MenuItem title when title.Classes.Contains("menusection"):
                    section = title.Header as string;
                    break;
                case MenuItem entry when entry.Header as string == header:
                    return section;
            }
        }
        throw new InvalidOperationException($"The menu has no {header}.");
    }

    /// <summary>Each entry has an icon, check items too (their tick goes before it), but those that follow their main one.</summary>
    private static void AssertIcons(ContextMenu menu)
    {
        var entries = menu.Items.OfType<MenuItem>().Where(m => !m.Classes.Contains("menusection")).ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, m => Assert.True(m.Icon is null == Secondary.Contains(m.Header as string), $"{m.Header} has {(m.Icon is null ? "no icon" : "an icon")}"));
    }
}
