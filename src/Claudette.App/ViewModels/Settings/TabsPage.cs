using System.Collections.ObjectModel;
using Claudette.Core;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels.Settings;

/// <summary>A favorite folder in Settings → Tabs, in the order the new tab picker lists them.</summary>
public sealed record FavoriteFolderRow(string Path, string Name);

/// <summary>Settings → Tabs → Subagents' choices, and the tab's own in Tab settings… (DESIGN.md §5, "Subagents").</summary>
public sealed record SubagentUseChoice(SubagentUse Use)
{
    public static string Label(SubagentUse use) => use switch
    {
        SubagentUse.Discourage => "Discourage them",
        SubagentUse.DontAllow => "Don't allow them",
        _ => "Claude decides",
    };

    public override string ToString() => Label(Use);
}

/// <summary>
/// The keys Tab settings… overrides go by (DESIGN.md §14, "Per-tab overrides"), for the settings that say "2 tabs have
/// their own".
/// </summary>
public static class TabOverrideKeys
{
    public const string Model = "model";
    public const string Effort = "effort";
    public const string PermissionMode = "permissionMode";
    public const string Subagents = "subagents";
    public const string ContinueAfterLimitReset = "continueAfterLimitReset";
    public const string UseUsageCredits = "useUsageCredits";
    public const string CheckIns = "checkIns";
    public const string ProcessMonitor = "processMonitor";
    public const string FoldChains = "foldChains";

    /// <summary>Whether <paramref name="overrides"/> has its own value for the setting <paramref name="key"/> names.</summary>
    public static bool Has(TabOverrides overrides, string key) => key switch
    {
        Model => overrides.Model is not null,
        Effort => overrides.Effort is not null,
        PermissionMode => overrides.PermissionMode is not null,
        Subagents => overrides.Subagents is not null,
        ContinueAfterLimitReset => overrides.ContinueAfterLimitReset is not null,
        UseUsageCredits => overrides.UseUsageCredits is not null,
        CheckIns => overrides.CheckIns is not null,
        ProcessMonitor => overrides.ShowProcessMonitor is not null,
        FoldChains => overrides.FoldChains is not null,
        _ => false,
    };
}

/// <summary>
/// Settings → Tabs (DESIGN.md §14): what each new tab starts with, which its Tab settings… can change for it: the model,
/// effort, permission mode and subagents, what happens at a usage limit, check-ins on long turns and the process
/// monitor; and the new tab picker's recent and favorite folders (DESIGN.md §4, "Opening a tab").
/// </summary>
public sealed partial class TabsPage : SettingsPage
{
    /// <summary>Used until a session has told Claudette which models the installed Claude Code offers.</summary>
    private static readonly IReadOnlyList<Choice> FallbackModels =
        [new("opus", "Opus"), new("sonnet", "Sonnet"), new("haiku", "Haiku"), new("fable", "Fable")];

    private static readonly IReadOnlyList<string> FallbackEffortLevels = ["low", "medium", "high", "xhigh", "max"];

    public TabsPage(SettingsContext context) : base(context, SettingsCategory.Tabs)
    {
        Section("newTabs", "New tabs start with",
        [
            Setting("newTabs.defaultModel", "Model", s => s.NewTabs.DefaultModel, (s, v) => s.NewTabs.DefaultModel = v,
                keywords: ["default model", "opus", "sonnet", "haiku"], overrideKey: TabOverrideKeys.Model),
            Setting("newTabs.defaultEffort", "Effort", s => s.NewTabs.DefaultEffort, (s, v) => s.NewTabs.DefaultEffort = v,
                "How hard the model thinks before it answers.", keywords: ["default effort", "thinking"], overrideKey: TabOverrideKeys.Effort),
            Setting("newTabs.defaultPermissionMode", "Permission mode", s => s.NewTabs.DefaultPermissionMode, (s, v) => s.NewTabs.DefaultPermissionMode = v,
                "What Claude may do without asking first.",
                "Claude Code's default is what your settings and your organization's make it, usually Auto. A project's own settings can still change it for its tabs.",
                ["default permission mode", "auto", "plan", "accept edits", "bypass"], TabOverrideKeys.PermissionMode),
            Setting("claudeCode.subagents", "Subagents", s => s.ClaudeCode.Subagents, (s, v) => s.ClaudeCode.Subagents = v,
                "Whether Claude hands parts of its work to subagents.",
                "Discourage asks it to do the work itself, unless you ask for a subagent. Don't allow tells it it can't, and Claudette turns down any subagent it starts anyway, in tabs already open too.",
                ["agents", "task tool"], TabOverrideKeys.Subagents),
        ], description: "Each tab can change these for itself, from Tab settings… in its menu.");
        Section("usageLimits", "At a usage limit",
        [
            Setting("usage.continueAfterLimitReset", "Continue tasks when a usage limit resets", s => s.Usage.ContinueAfterLimitReset, (s, v) => s.Usage.ContinueAfterLimitReset = v,
                "The tab waits for the limit to reset, then asks Claude to carry on.",
                "When a usage limit stops Claude mid-task, the tab waits for the limit to reset, then asks Claude to continue from where it left off.",
                ["auto-continue", "resume", "limit"], TabOverrideKeys.ContinueAfterLimitReset),
            Setting("usage.useUsageCredits", "Use usage credits once a limit is used up", s => s.Usage.UseUsageCredits, (s, v) => s.Usage.UseUsageCredits = v,
                "Off, tabs stop at the limit instead, and wait for it to reset.",
                "When your plan has usage credits turned on, they pay for Claude once your session or weekly limit is used up. Each tab can change this from its menu too, so only the work that matters spends them.",
                ["extra usage", "credits", "overage", "limit"], TabOverrideKeys.UseUsageCredits),
        ]);
        Section("checkIns", "Check-ins on long turns",
        [
            Setting("checkIns.enabled", "Check in on long turns", s => s.CheckIns.Enabled, (s, v) => s.CheckIns.Enabled = v,
                "Claudette asks Claude how it's going, so a stuck tab doesn't burn usage unnoticed.", keywords: ["stuck", "long turn"],
                overrideKey: TabOverrideKeys.CheckIns),
            Setting("checkIns.runTimeMinutes", "After the turn has run (minutes)", s => s.CheckIns.RunTimeMinutes, (s, v) => s.CheckIns.RunTimeMinutes = v,
                "0 turns this off.", keywords: ["check-in"]),
            Setting("checkIns.quietTimeMinutes", "After no output for (minutes)", s => s.CheckIns.QuietTimeMinutes, (s, v) => s.CheckIns.QuietTimeMinutes = v,
                "0 turns this off.", keywords: ["check-in", "quiet"]),
            Setting("checkIns.message", "Message", s => s.CheckIns.Message, (s, v) => s.CheckIns.Message = v,
                "What Claudette sends Claude.", keywords: ["check-in message"]),
            Setting("checkIns.notify", "Notify me when a check-in is sent", s => s.CheckIns.Notify, (s, v) => s.CheckIns.Notify = v,
                keywords: ["check-in", "notification"]),
        ]);
        Section("processMonitor", "Process monitor",
        [
            Setting("processes.showMonitor", "Show the process monitor", s => s.Processes.ShowMonitor, (s, v) => s.Processes.ShowMonitor = v,
                "The processes each tab starts, with their CPU and memory.",
                "Test runs, dev servers, builds and MCP servers, in the message box's bar and the Processes tool window. Closing a tab stops the processes it started, unless you choose to leave them running.",
                ["processes", "cpu", "memory"], TabOverrideKeys.ProcessMonitor),
            Setting("processes.refreshSeconds", "Refresh every (seconds)", s => s.Processes.RefreshSeconds, (s, v) => s.Processes.RefreshSeconds = v,
                keywords: ["process monitor", "interval"]),
            Setting("processes.showCommandLines", "Show command lines", s => s.Processes.ShowCommandLines, (s, v) => s.Processes.ShowCommandLines = v,
                "They sometimes hold tokens or passwords: turn this off if others can see your screen.", keywords: ["process monitor", "privacy"]),
        ]);
        Section("picker", "New tab picker",
        [
            Setting("newTabs.recentFolderLimit", "Recent folders to keep", s => s.NewTabs.RecentFolderLimit, (s, v) => s.NewTabs.RecentFolderLimit = v,
                keywords: ["history", "recent"]),
            Info("state.clearRecentFolders", "Clear recent folders", "Favorites are kept.", keywords: ["recent", "history"]),
            // This machine's folders, not a setting: never reset.
            Setting("state.favoriteFolders", "Favorite folders", null, null, "Always at the top of the new tab picker.", keywords: ["pinned folders", "favourites"]),
        ]);
        Section("claudeProjects", "Claude Code's projects",
        [
            Setting("sidebar.showClaudeProjects", "Show Claude Code's projects in the sidebar", s => s.Sidebar.ShowClaudeProjects, (s, v) => s.Sidebar.ShowClaudeProjects = v,
                "Every folder Claude Code has sessions in gets a group, with its past sessions under it, tab or no tab.",
                "The sessions are the ones History lists: Claude Code's own on this machine, including ones started in a terminal, and the session library's. A group's past sessions fold away under its tabs; click one to pick it up in a new tab. Turning this off closes the groups that have nothing open.",
                ["claude projects", "past sessions", "folders", "history", "sidebar"]),
        ], description: "Work in the folders Claude Code already knows, straight from the sidebar.");
        LoadFavorites();
    }

    public override string IconKey => "IconSettingsTabs";

    public override string Description => "What each new tab starts with, and the folders you open them in.";

    // ---- Models and effort levels from Claude Code (DESIGN.md §14, "Tabs") ---------------------------------------

    public IReadOnlyList<Choice> ModelChoices => field ??= BuildModelChoices();

    public IReadOnlyList<Choice> EffortChoices => field ??= BuildEffortChoices();

    /// <summary>
    /// "Claude Code's default" names the mode it comes to from the user's and managed settings, usually Auto (DESIGN.md
    /// §7, "Starting mode"). A project's settings can still change it for its tabs.
    /// </summary>
    public IReadOnlyList<Choice> ModeChoices => field ??=
    [
        new(null, $"Claude Code's default ({PermissionModeInfo.Label(Services.ReadStartingPermissionMode(null).Expected)})"),
        .. PermissionModeInfo.Choices.Select(m => new Choice(m.Value, m.Label)),
    ];

    public Choice DefaultModel
    {
        get => ModelChoices.FirstOrDefault(c => c.Value == Settings.NewTabs.DefaultModel) ?? ModelChoices[0];
        set => Set(value?.Value, v => Settings.NewTabs.DefaultModel = v);
    }

    public Choice DefaultEffort
    {
        get => EffortChoices.FirstOrDefault(c => c.Value == Settings.NewTabs.DefaultEffort) ?? EffortChoices[0];
        set => Set(value?.Value, v => Settings.NewTabs.DefaultEffort = v);
    }

    public Choice DefaultMode
    {
        get => ModeChoices.FirstOrDefault(c => c.Value == Settings.NewTabs.DefaultPermissionMode) ?? ModeChoices[0];
        set => Set(value?.Value, v => Settings.NewTabs.DefaultPermissionMode = v);
    }

    /// <summary>What Claude Code offered in its last <c>initialize</c> reply, plus the current default if it's not among them.</summary>
    private IReadOnlyList<Choice> BuildModelChoices()
    {
        var known = Services.State.KnownModels;
        IReadOnlyList<Choice> models = known.Count > 0 ? [.. known.Select(m => new Choice(m.Value, m.DisplayName))] : FallbackModels;
        var choices = new List<Choice> { new(null, "Claude Code's default") };
        choices.AddRange(models);
        if (Settings.NewTabs.DefaultModel is { } current && choices.All(c => c.Value != current))
        {
            choices.Add(new Choice(current, current));
        }
        return choices;
    }

    /// <summary>Every effort level a model offers, in Claude Code's order.</summary>
    private IReadOnlyList<Choice> BuildEffortChoices()
    {
        var levels = Services.State.KnownModels.SelectMany(m => m.SupportedEffortLevels).Distinct(StringComparer.Ordinal).ToList();
        if (levels.Count == 0)
        {
            levels = [.. FallbackEffortLevels];
        }
        var choices = new List<Choice> { new(null, "The model's default") };
        choices.AddRange(levels.Select(l => new Choice(l, Formats.Capitalize(l))));
        if (Settings.NewTabs.DefaultEffort is { } current && choices.All(c => c.Value != current))
        {
            choices.Add(new Choice(current, Formats.Capitalize(current)));
        }
        return choices;
    }

    public IReadOnlyList<SubagentUseChoice> SubagentChoices { get; } =
        [new(SubagentUse.ClaudeDecides), new(SubagentUse.Discourage), new(SubagentUse.DontAllow)];

    /// <summary>
    /// <b>Subagents</b> (DESIGN.md §5, "Subagents"): whether Claude may hand work to them. Applies at once, in tabs already
    /// open too; each tab can override it in Tab settings….
    /// </summary>
    public SubagentUseChoice Subagents
    {
        get => SubagentChoices.FirstOrDefault(c => c.Use == Settings.ClaudeCode.Subagents) ?? SubagentChoices[0];
        set => Set(value?.Use ?? SubagentUse.ClaudeDecides, v => Settings.ClaudeCode.Subagents = v);
    }

    // ---- At a usage limit (DESIGN.md §6) --------------------------------------------------------------------------

    /// <summary>DESIGN.md §6, "Continuing after a limit resets". Each tab can override it in Tab settings.</summary>
    public bool ContinueAfterLimitReset
    {
        get => Settings.Usage.ContinueAfterLimitReset;
        set => Set(value, v => Settings.Usage.ContinueAfterLimitReset = v);
    }

    /// <summary>DESIGN.md §6, "Tabs that don't use usage credits". Each tab can override it from its menu or Tab settings.</summary>
    public bool UseUsageCredits
    {
        get => Settings.Usage.UseUsageCredits;
        set => Set(value, v => Settings.Usage.UseUsageCredits = v);
    }

    // ---- Check-ins (DESIGN.md §5, "Check-ins on long turns") ---------------------------------------------------------

    public bool CheckInsEnabled
    {
        get => Settings.CheckIns.Enabled;
        set => Set(value, v => Settings.CheckIns.Enabled = v);
    }

    public decimal? CheckInRunTime
    {
        get => Settings.CheckIns.RunTimeMinutes;
        set => Set(value, v => Settings.CheckIns.RunTimeMinutes = Math.Max(0, (int)(v ?? 0)));
    }

    public decimal? CheckInQuietTime
    {
        get => Settings.CheckIns.QuietTimeMinutes;
        set => Set(value, v => Settings.CheckIns.QuietTimeMinutes = Math.Max(0, (int)(v ?? 0)));
    }

    public string CheckInMessage
    {
        get => Settings.CheckIns.Message;
        set => Set(value, v => Settings.CheckIns.Message = string.IsNullOrWhiteSpace(v) ? CheckInSettings.DefaultMessage : v);
    }

    public bool NotifyOnCheckIn
    {
        get => Settings.CheckIns.Notify;
        set => Set(value, v => Settings.CheckIns.Notify = v);
    }

    // ---- The process monitor (DESIGN.md §4, "Process monitor") ------------------------------------------------------

    public bool ShowProcessMonitor
    {
        get => Settings.Processes.ShowMonitor;
        set => Set(value, v => Settings.Processes.ShowMonitor = v);
    }

    // ---- Claude Code's projects (DESIGN.md §4, "Claude Code's projects") -----------------------------------------

    public bool ShowClaudeProjects
    {
        get => Settings.Sidebar.ShowClaudeProjects;
        set => Set(value, v => Settings.Sidebar.ShowClaudeProjects = v);
    }

    public decimal? ProcessRefreshSeconds
    {
        get => Settings.Processes.RefreshSeconds;
        set => Set(value, v => Settings.Processes.RefreshSeconds = Math.Clamp((int)(v ?? 2), 1, 60));
    }

    public bool ShowCommandLines
    {
        get => Settings.Processes.ShowCommandLines;
        set => Set(value, v => Settings.Processes.ShowCommandLines = v);
    }

    // ---- Recent folders --------------------------------------------------------------------------------------------

    public decimal? RecentFolderLimit
    {
        get => Settings.NewTabs.RecentFolderLimit;
        set => Set(value, v => Settings.NewTabs.RecentFolderLimit = Math.Clamp((int)(v ?? 20), 1, 100));
    }

    [RelayCommand]
    private void ClearRecentFolders()
    {
        Services.State.RecentFolders.RemoveAll(r => !FolderHistory.IsFavorite(Services.State, r.Path));
        Services.SaveState();
    }

    // ---- Favorite folders (DESIGN.md §4, "Opening a tab") -----------------------------------------------------------

    public ObservableCollection<FavoriteFolderRow> Favorites { get; } = [];

    public bool HasFavorites => Favorites.Count > 0;

    private void LoadFavorites()
    {
        Favorites.Clear();
        foreach (var path in Services.State.FavoriteFolders)
        {
            Favorites.Add(new FavoriteFolderRow(path, Formats.FolderName(path)));
        }
        OnPropertyChanged(nameof(HasFavorites));
    }

    [RelayCommand]
    private async Task AddFavoriteAsync()
    {
        if (await Services.Platform.PickFolderAsync("Add a favorite folder") is { } folder)
        {
            FolderHistory.SetFavorite(Services.State, folder, true);
            Services.SaveState();
            LoadFavorites();
        }
    }

    [RelayCommand]
    private void RemoveFavorite(FavoriteFolderRow? row)
    {
        if (row is not null)
        {
            FolderHistory.SetFavorite(Services.State, row.Path, false);
            Services.SaveState();
            LoadFavorites();
        }
    }

    [RelayCommand]
    private void MoveFavoriteUp(FavoriteFolderRow? row) => MoveFavorite(row, -1);

    [RelayCommand]
    private void MoveFavoriteDown(FavoriteFolderRow? row) => MoveFavorite(row, 1);

    private void MoveFavorite(FavoriteFolderRow? row, int by)
    {
        var favorites = Services.State.FavoriteFolders;
        var index = row is null ? -1 : favorites.IndexOf(row.Path);
        var target = index + by;
        if (index < 0 || target < 0 || target >= favorites.Count)
        {
            return;
        }
        (favorites[index], favorites[target]) = (favorites[target], favorites[index]);
        Services.SaveState();
        LoadFavorites();
    }
}
