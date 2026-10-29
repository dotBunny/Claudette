// claudette-demo: demo content for trying Claudette's UI and for its screenshots, with no account and no tokens.
//
//   claudette-demo <folder> [--theme system|light|dark] [--style standard|claude] [--projects <folder>] [--still] [--quiet]
//
// Makes, in <folder> (replacing what an earlier run made there):
//   home/           for CLAUDETTE_HOME: settings with fake-claude as Claude Code and the theme and style asked for
//                   (--still reduces motion, so Claudette on the composer stands still for screenshots, and --quiet
//                   turns the OS's notifications off, which a Claudette kept behind other windows would send),
//                   seven tabs over three game projects (Unreal in C++, Unity in C#, Godot): the first with a long
//                   conversation and changed files, one with every file reviewed, and a thread with two sub-threads,
//                   a plan, a task list part-way done and three subagents; an API-key account, Studio, which
//                   orbit-game's group and one starfall tab run on; the older projects as recent folders; and a usage
//                   history that ends now, with the plan's usage and a month of Studio's spend
//   claude-config/  for CLAUDE_CONFIG_DIR: the tabs' transcripts, with the subagents' beside them, and past sessions
//                   for History: more in each project, one in a worktree of starfall, and four older projects
//   usage.json      for FAKE_CLAUDE_USAGE: the plan usage fake-claude reports, where the history ends
//   projects/       three git repositories, with Claude's changes in their working trees, and the older projects'
//                   empty folders. --projects puts them elsewhere: the conversation shows their paths, so a short one
//                   such as C:\Demo reads better.
// then prints the environment to run Claudette with. A folder it would replace must be empty or one it made.
//
//   claudette-demo --stand-in-editor <uproject>
//
// stands in for the Unreal editor with <uproject> open, run as a copy named UnrealEditor: it writes the project's
// Saved/Logs/<Name>.log and waits, for the guide's picture of an editor's log (StandInEditor.cs).
//
// tools/Claudette.Demo/screenshots.ps1 takes the README's screenshots with it, on Windows.
using Claudette.Core;
using Claudette.Core.Accounts;
using Claudette.Core.Development;
using Claudette.Core.Settings;
using Claudette.Demo;

if (args is ["--stand-in-editor", var standInProject])
{
    return StandInEditor.Run(standInProject);
}

const string Usage = "usage: claudette-demo <folder> [--theme system|light|dark] [--style standard|claude] [--projects <folder>] [--still] [--quiet]";
string? folder = null;
string? projects = null;
var still = false;
var quiet = false;
var theme = ThemeChoice.Dark;
var style = AppStyle.Standard;
try
{
    var rest = new Queue<string>(args);
    while (rest.TryDequeue(out var arg))
    {
        switch (arg)
        {
            case "--theme":
                theme = Enum.Parse<ThemeChoice>(Next(rest), ignoreCase: true);
                break;
            case "--style":
                style = Enum.Parse<AppStyle>(Next(rest), ignoreCase: true);
                break;
            case "--projects":
                projects = Next(rest);
                break;
            case "--still":
                still = true;
                break;
            case "--quiet":
                quiet = true;
                break;
            default:
                folder = folder is null && !arg.StartsWith('-') ? arg : throw new ArgumentException($"Unexpected argument: {arg}");
                break;
        }
    }
    if (folder is null)
    {
        throw new ArgumentException("Which folder?");
    }
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(Usage);
    return 2;
}

folder = Path.GetFullPath(folder);
projects = Path.GetFullPath(projects ?? Path.Combine(folder, "projects"));
try
{
    // Projects inside the folder go with it.
    DemoFolder.Replace(folder);
    DemoFolder.Replace(projects);
}
catch (IOException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var home = Path.Combine(folder, "home");
var paths = AppPaths.Under(home);
var config = Path.Combine(folder, "claude-config");
var transcripts = Path.Combine(config, "projects", "demo");
var usage = Path.Combine(folder, "usage.json");
Directory.CreateDirectory(transcripts);
Directory.CreateDirectory(paths.DataDirectory);
Directory.CreateDirectory(paths.SettingsDirectory);

var tabs = DemoContent.Write(projects, transcripts);
var sessionMarks = DemoHistory.Write(projects, transcripts);
DemoUsage.Write(paths.UsageDatabase, usage, DateTimeOffset.UtcNow, tabs);

var settings = new AppSettings();
settings.General.CheckForAppUpdates = false;
settings.General.ShowServiceStatus = false;
settings.ClaudeCode.ClaudePath = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "fake-claude.exe" : "fake-claude");
settings.ClaudeCode.CheckForUpdates = false;
settings.ClaudeCode.UseLoginShellEnvironment = false;
settings.Appearance.Theme = theme;
settings.Appearance.Style = style;
if (still)
{
    // Screenshots catch her standing on the composer rather than part-way through falling off it.
    settings.Appearance.Motion = MotionSetting.Reduce;
}
if (quiet)
{
    // Kept behind other windows, as the screenshot scripts keep it, Claudette would think the user away and tell them.
    var notifications = settings.Notifications;
    notifications.TurnFinished = notifications.NeedsInput = notifications.ProcessErrors = notifications.UsageAlerts = false;
    notifications.SignIn = notifications.UpdateReady = notifications.ProjectActions = false;
}
settings.Sessions.RestoreUnpinnedTabs = true;
await new JsonFileStore<AppSettings>(paths.SettingsFile).SaveAsync(settings);

await new JsonFileStore<AppState>(paths.StateFile).SaveAsync(new AppState
{
    Tabs = tabs,
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
            .Select((name, i) => new RecentFolder { Path = Path.Combine(projects, name), LastUsed = DateTimeOffset.UtcNow.AddDays(-2 - 3 * i) }),
    ],
    SessionMarks = sessionMarks,
    // The studio's API key, which orbit-game's group uses (DESIGN.md §4, "Accounts"). Its key isn't stored: fake-claude
    // never runs the key helper.
    Accounts = [new ClaudeAccount { Id = DemoContent.StudioAccount, Name = "Studio", Color = "#A66BFF", DailySpendAlert = 20 }],
    FolderAccounts = { [tabs.Single(t => t.Id == "tab-pool").Folder] = DemoContent.StudioAccount },
    Window = new WindowPlacement(60, 40, 1480, 920, IsMaximized: false),
    // Answered, so the sidebar doesn't offer Claude Code's projects over the pictures (DESIGN.md §4, "Claude Code's projects").
    ClaudeProjectsOffered = true,
});

Console.WriteLine($"""
    Demo content in {folder}, with its projects in {projects}.
    Run Claudette with these set (the usage history ends now, so the header's projection reads best soon after):

      CLAUDETTE_HOME={home}
      CLAUDE_CONFIG_DIR={config}
      FAKE_CLAUDE_USAGE={usage}
      CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC=1
    """);
return 0;

static string Next(Queue<string> rest) => rest.TryDequeue(out var value) ? value : throw new ArgumentException("A value is missing.");
