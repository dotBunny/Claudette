using Claudette.Core.Accounts;
using Claudette.Core.Development;
using Claudette.Core.Settings;

namespace Claudette.Core.Demo;

/// <summary>How a demo is written: which <c>claude</c> its tabs run, and how it looks.</summary>
public sealed record DemoOptions
{
    /// <summary>
    /// The demo's Claude Code: <c>fake-claude</c> for the screenshots, Claudette itself in the app's demo, which stands in
    /// for it (DESIGN.md §11, "Demo mode").
    /// </summary>
    public required string ClaudePath { get; init; }

    public ThemeChoice Theme { get; init; } = ThemeChoice.System;

    public AppStyle Style { get; init; } = AppStyle.Standard;

    /// <summary>Motion reduced, so Claudette on the composer stands still for a screenshot.</summary>
    public bool Still { get; init; }

    /// <summary>The OS's notifications off, which a Claudette kept behind other windows would send.</summary>
    public bool Quiet { get; init; }

    /// <summary>Where the window opens; where the OS puts it when null.</summary>
    public WindowPlacement? Window { get; init; }
}

/// <summary>What a demo was written as: Claudette's folders, Claude Code's, and the tabs.</summary>
public sealed record DemoWritten(AppPaths Paths, string ClaudeConfig, IReadOnlyList<TabState> Tabs);

/// <summary>
/// A demo's folder, but for its usage history, which <c>Claudette.Usage</c>'s <c>DemoUsage</c> writes: Claudette's settings
/// and state under <c>home</c> (as <c>CLAUDETTE_HOME</c>), the tabs' and History's transcripts under <c>claude-config</c>
/// (as <c>CLAUDE_CONFIG_DIR</c>), and the projects. <c>tools/Claudette.Demo</c> writes it for the screenshots, with git
/// repositories and <c>fake-claude</c>; the app's demo with empty folders and Claudette standing in for Claude Code
/// (DESIGN.md §11, "Demo mode").
/// </summary>
public static class DemoHome
{
    /// <summary>
    /// Writes the demo into <paramref name="folder"/>, with its projects under <paramref name="projects"/>, made by
    /// <paramref name="make"/>, as of <paramref name="now"/>.
    /// </summary>
    public static async Task<DemoWritten> WriteAsync(string folder, string projects, DateTimeOffset now, DemoOptions options, Func<string, DemoProject, string> make)
    {
        var paths = AppPaths.Under(Path.Combine(folder, "home"));
        var config = Path.Combine(folder, "claude-config");
        var transcripts = Path.Combine(config, "projects", "demo");
        Directory.CreateDirectory(transcripts);
        Directory.CreateDirectory(paths.DataDirectory);
        Directory.CreateDirectory(paths.SettingsDirectory);

        var tabs = DemoContent.Write(projects, transcripts, now, make);
        var sessionMarks = DemoHistory.Write(projects, transcripts, now);

        var settings = new AppSettings();
        settings.General.CheckForAppUpdates = false;
        settings.General.ShowServiceStatus = false;
        settings.ClaudeCode.ClaudePath = options.ClaudePath;
        settings.ClaudeCode.CheckForUpdates = false;
        settings.ClaudeCode.UseLoginShellEnvironment = false;
        settings.Appearance.Theme = options.Theme;
        settings.Appearance.Style = options.Style;
        if (options.Still)
        {
            // Screenshots catch her standing on the composer rather than part-way through falling off it.
            settings.Appearance.Motion = MotionSetting.Reduce;
        }
        if (options.Quiet)
        {
            var notifications = settings.Notifications;
            notifications.TurnFinished = notifications.NeedsInput = notifications.ProcessErrors = notifications.UsageAlerts = false;
            notifications.SignIn = notifications.UpdateReady = notifications.ProjectActions = false;
        }
        settings.Sessions.RestoreUnpinnedTabs = true;
        await new JsonFileStore<AppSettings>(paths.SettingsFile).SaveAsync(settings);

        await new JsonFileStore<AppState>(paths.StateFile).SaveAsync(new AppState
        {
            Tabs = [.. tabs],
            SelectedTabId = tabs[0].Id,
            SidebarWidth = 300,
            // Wide enough for the plan on the Tasks page, with Tasks beside Agents so both pages' tabs show.
            SidePanelWidth = 420,
            SidePanelPages = ["Files", "Agents", "Tasks", "Project", "Processes", "Mcp", "ScratchPad"],
            TrustedFolders = [.. tabs.Select(t => t.Folder).Distinct()],
            // The older projects, which the new tab picker lists as recent folders.
            RecentFolders =
            [
                .. new[] { "shader-lab", "build-farm", "lighthouse-launcher", "pixel-tools" }
                    .Select((name, i) => new RecentFolder { Path = Path.Combine(projects, name), LastUsed = now.AddDays(-2 - 3 * i) }),
            ],
            SessionMarks = sessionMarks,
            // The studio's API key, which orbit-game's group uses (DESIGN.md §4, "Accounts"). Its key isn't stored: the
            // demo's Claude Code never runs the key helper.
            Accounts = [new ClaudeAccount { Id = DemoContent.StudioAccount, Name = "Studio", Color = "#A66BFF", DailySpendAlert = 20 }],
            FolderAccounts = { [tabs.Single(t => t.Id == "tab-pool").Folder] = DemoContent.StudioAccount },
            Window = options.Window,
            // Answered, so the sidebar doesn't offer Claude Code's projects over the demo (DESIGN.md §4, "Claude Code's projects").
            ClaudeProjectsOffered = true,
        });
        return new DemoWritten(paths, config, tabs);
    }
}
