using Claudette.Core.ProjectTools;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Claudette.App.ViewModels;

/// <summary>
/// The Settings window's categories, by the names its sidebar shows (DESIGN.md §14). Use these rather than the names
/// written out, so a page, its search entries and a link to it can't disagree. Each project type's page is named by the
/// type (<see cref="IProjectType.Name"/>), and the selected tab's project pages by the project
/// (<see cref="SettingsViewModel.ProjectPages"/>).
/// </summary>
public static class SettingsCategory
{
    public const string General = "General";

    public const string Appearance = "Appearance";

    public const string Conversation = "Conversation";

    public const string Notifications = "Notifications";

    public const string Keyboard = "Keyboard";


    public const string ClaudeCode = "Claude Code";

    public const string Accounts = "Accounts";

    public const string Tabs = "Tabs";

    public const string Usage = "Usage";

    public const string Sessions = "Sessions";

    public const string Files = "Files and diffs";

    public const string AllTools = "All tools";

    public const string Git = "Git";

    public const string Perforce = "Perforce";

    public const string Advanced = "Advanced";
}

/// <summary>
/// The Settings sidebar's groups (DESIGN.md §14), in order: a heading and its categories. About Claudette is a window of
/// its own (DESIGN.md §14, "About"); Claude ends with Advanced, which is mostly about running Claude Code; Project tools has All tools,
/// then a page for each project type Claudette knows, in its order; Source control has Git, then Perforce.
/// </summary>
public static class SettingsSidebar
{
    public const string ProjectTools = "Project tools";

    public const string SourceControl = "Source control";

    public static IReadOnlyList<(string Heading, IReadOnlyList<string> Categories)> Groups(IEnumerable<IProjectType> types) =>
    [
        ("Claudette", [SettingsCategory.General, SettingsCategory.Appearance, SettingsCategory.Conversation, SettingsCategory.Notifications, SettingsCategory.Keyboard]),
        ("Claude", [SettingsCategory.ClaudeCode, SettingsCategory.Accounts, SettingsCategory.Tabs, SettingsCategory.Usage, SettingsCategory.Advanced]),
        ("Your work", [SettingsCategory.Sessions, SettingsCategory.Files]),
        (ProjectTools, [SettingsCategory.AllTools, .. types.Select(t => t.Name)]),
        (SourceControl, [SettingsCategory.Git, SettingsCategory.Perforce]),
    ];

    /// <summary>Every category, in the sidebar's order.</summary>
    public static IReadOnlyList<string> Categories(IEnumerable<IProjectType> types) => [.. Groups(types).SelectMany(g => g.Categories)];

    /// <summary>
    /// Other words every page of a group is found by, besides its own (DESIGN.md §14, "Search"): Source control's, so
    /// "version control" finds Git and Perforce alike.
    /// </summary>
    public static IReadOnlyList<string> GroupWords(string heading) => heading switch
    {
        SourceControl => [SourceControl, "version control", "vcs"],
        _ => [],
    };
}

/// <summary>An entry in the Settings sidebar's lists: a group's heading, or a page to show.</summary>
public sealed partial class SettingsNavEntry : ObservableObject
{
    private SettingsNavEntry(string title, string? iconKey, bool isHeading)
    {
        Title = title;
        IconKey = iconKey;
        IsHeading = isHeading;
    }

    public string Title { get; }

    /// <summary>A page's icon: the key of one of App.axaml's small vector icons.</summary>
    public string? IconKey { get; }

    public bool IsHeading { get; }

    /// <summary>The page's name, for a heading's group or a page; what <see cref="SettingsViewModel.SelectedCategory"/> holds.</summary>
    public string Category => Title;

    /// <summary>A word after a page's name, such as <i>Off</i> for a project type switched off (<see cref="Settings.SettingsPage.NavTag"/>).</summary>
    [ObservableProperty]
    public partial string? Tag { get; set; }

    public static SettingsNavEntry Heading(string title) => new(title, null, true);

    /// <summary>What a screen reader reads for it: its name.</summary>
    public override string ToString() => Title;

    public static SettingsNavEntry Page(string title, string iconKey, string? tag = null) => new(title, iconKey, false) { Tag = tag };
}
