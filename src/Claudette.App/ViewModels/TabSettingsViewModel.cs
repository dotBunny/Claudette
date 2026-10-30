using System.Collections.ObjectModel;
using Claudette.App.Services;
using Claudette.App.ViewModels.Settings;
using Claudette.Core.Sessions;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>A choice in a dropdown. A null value means "use the default".</summary>
public sealed record Choice(string? Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// "Tab settings…": settings for one tab that replace the defaults from Settings → Tabs (DESIGN.md §14, "Per-tab
/// overrides"), and the tab's own switches. Each change applies as it's made, as in Settings, and each section has
/// <b>Use defaults</b>. The extra folders apply when it closes, since Claude Code starts again to use them. The folder's
/// custom project actions are in Settings, on the tab's Actions page, which a button here opens.
/// </summary>
public sealed partial class TabSettingsViewModel : ViewModelBase, ISettingsSource
{
    private const string On = "on";
    private const string Off = "off";

    private readonly AppServices _services;
    private readonly TabViewModel _tab;
    private readonly Action _close;
    private readonly ShellViewModel? _shell;
    private readonly Dictionary<string, SettingItem> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SettingsGroup> _groups = new(StringComparer.Ordinal);

    /// <summary>While the dialog fills itself in, or resets several at once: nothing applies until it's done.</summary>
    private bool _holding = true;

    public TabSettingsViewModel(AppServices services, TabViewModel tab, Action close, ShellViewModel? shell = null)
    {
        _shell = shell;
        _services = services;
        _tab = tab;
        _close = close;
        var overrides = tab.State.Overrides;
        var defaults = services.Settings;

        // What the tab's Claude Code offers, else what Claude Code last offered on this machine (DESIGN.md §14).
        IReadOnlyList<ModelInfo> models = tab.Models.Count > 0 ? tab.Models : services.State.KnownModels;
        var defaultModel = defaults.NewTabs.DefaultModel is { } modelId ? models.FirstOrDefault(m => m.Value == modelId)?.DisplayName ?? modelId : "Claude Code's default";
        ModelChoices = [new Choice(null, $"Default ({defaultModel})"), .. models.Select(m => new Choice(m.Value, m.DisplayName))];
        if (overrides.Model is { } model && ModelChoices.All(c => c.Value != model))
        {
            ModelChoices = [.. ModelChoices, new Choice(model, model)];
        }
        var levels = models.SelectMany(m => m.SupportedEffortLevels).Distinct(StringComparer.Ordinal).ToList();
        if (levels.Count == 0)
        {
            levels = ["low", "medium", "high", "xhigh", "max"];
        }
        if (overrides.Effort is { } effort && !levels.Contains(effort))
        {
            levels.Add(effort);
        }
        var defaultEffort = defaults.NewTabs.DefaultEffort is { } level ? Core.Formats.Capitalize(level) : "the model's default";
        EffortChoices = [new Choice(null, $"Default ({defaultEffort})"), .. levels.Select(e => new Choice(e, Core.Formats.Capitalize(e)))];
        // Claude Code's default is known once the tab has started and read its settings files (DESIGN.md §7).
        var defaultMode = defaults.NewTabs.DefaultPermissionMode ?? tab.ClaudeCodeStartingMode;
        ModeChoices =
        [
            new Choice(null, $"Default ({(defaultMode is null ? "Claude Code's default" : PermissionModeInfo.Label(defaultMode))})"),
            .. PermissionModeInfo.Choices.Select(m => new Choice(m.Value, m.Label)),
        ];
        MonitorChoices = [new Choice(null, $"Default ({(defaults.Processes.ShowMonitor ? "on" : "off")})"), new Choice(On, "On"), new Choice(Off, "Off")];
        AutoContinueChoices = [new Choice(null, $"Default ({(defaults.Usage.ContinueAfterLimitReset ? "on" : "off")})"), new Choice(On, "On"), new Choice(Off, "Off")];
        UsageCreditsChoices = [new Choice(null, $"Default ({(defaults.Usage.UseUsageCredits ? "on" : "off")})"), new Choice(On, "On"), new Choice(Off, "Off")];
        SubagentChoices =
        [
            new Choice(null, $"Default ({InSentence(SubagentUseChoice.Label(defaults.ClaudeCode.Subagents))})"),
            .. Enum.GetValues<SubagentUse>().Select(use => new Choice(use.ToString(), SubagentUseChoice.Label(use))),
        ];
        StepsChoices = [new Choice(null, $"Default ({(defaults.Appearance.FoldChains ? "folded" : "all")})"), new Choice(On, "Folded"), new Choice(Off, "All")];
        ShowsUsageCredits = tab.HasUsageCreditsMenu;

        SelectedModel = ModelChoices.First(c => c.Value == overrides.Model);
        SelectedEffort = EffortChoices.FirstOrDefault(c => c.Value == overrides.Effort) ?? EffortChoices[0];
        SelectedMode = ModeChoices.FirstOrDefault(c => c.Value == overrides.PermissionMode) ?? ModeChoices[0];
        SelectedMonitor = MonitorChoices.First(c => c.Value == OnOff(overrides.ShowProcessMonitor));
        SelectedAutoContinue = AutoContinueChoices.First(c => c.Value == OnOff(overrides.ContinueAfterLimitReset));
        SelectedUsageCredits = UsageCreditsChoices.First(c => c.Value == OnOff(overrides.UseUsageCredits));
        SelectedSubagents = SubagentChoices.First(c => c.Value == overrides.Subagents?.ToString());
        SelectedSteps = StepsChoices.First(c => c.Value == OnOff(overrides.FoldChains));

        var checkIns = overrides.CheckIns ?? defaults.CheckIns;
        UseCustomCheckIns = overrides.CheckIns is not null;
        CheckInsEnabled = checkIns.Enabled;
        RunTimeMinutes = checkIns.RunTimeMinutes;
        QuietTimeMinutes = checkIns.QuietTimeMinutes;
        CheckInMessage = checkIns.Message;
        NotifyOnCheckIn = checkIns.Notify;
        SyncToLibrary = tab.State.SyncToLibrary;
        RemoteControl = tab.State.RemoteControl;
        ExtraFolders = [.. tab.State.ExtraFolders];
        ExtraFolders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasExtraFolders));

        Section("model", "Model and mode",
        [
            Override("tab.model", "Model", () => SelectedModel.Value is null, () => SelectedModel = ModelChoices[0]),
            Override("tab.effort", "Effort", () => SelectedEffort.Value is null, () => SelectedEffort = EffortChoices[0]),
            Override("tab.permissionMode", "Permission mode", () => SelectedMode.Value is null, () => SelectedMode = ModeChoices[0]),
            Override("tab.subagents", "Subagents", () => SelectedSubagents.Value is null, () => SelectedSubagents = SubagentChoices[0],
                "Applies at once; Claude is told with the next message."),
        ]);
        Section("limits", "At a usage limit",
        [
            Override("tab.continueAfterLimitReset", "Continue tasks when a usage limit resets", () => SelectedAutoContinue.Value is null,
                () => SelectedAutoContinue = AutoContinueChoices[0]),
            Override("tab.useUsageCredits", "Use usage credits", () => SelectedUsageCredits.Value is null, () => SelectedUsageCredits = UsageCreditsChoices[0],
                TabViewModel.UsageCreditsMenuTip),
        ]);
        Section("checkIns", "Check-ins on long turns",
        [
            Override("tab.checkIns", "Use different check-ins for this tab", () => !UseCustomCheckIns, () => UseCustomCheckIns = false),
        ]);
        Section("monitor", "Process monitor",
        [
            Override("tab.processMonitor", "Show the process monitor", () => SelectedMonitor.Value is null, () => SelectedMonitor = MonitorChoices[0],
                "The processes this tab starts, with their CPU and memory."),
        ]);
        Section("conversation", "Conversation",
        [
            Override("tab.foldChains", "Steps", () => SelectedSteps.Value is null, () => SelectedSteps = StepsChoices[0],
                "Folded, each chain of Claude's steps between two messages is one line, such as “7 steps · 18m 43s”. All shows every step."),
        ]);
        // The tab's own switches, not overrides: no defaults to go back to.
        Section("thisTab", "This tab",
        [
            Own("tab.syncToLibrary", "Sync to other machines", "Copies this session to the session library after each turn, so another machine can open it from History."),
            Own("tab.remoteControl", "Connect to the Claude app", "Continue this tab from your phone or claude.ai/code while it runs here."),
            Own("tab.extraFolders", "Extra folders", "Claude can also read and edit files in these. Claude Code starts again with them when you click Done.",
                "Their own settings and CLAUDE.md aren't loaded."),
            Own("tab.projectActions", "Project actions", ProjectActionsText),
        ]);
        _holding = false;
        RefreshItems();
    }

    public string Title => $"Settings for \"{_tab.DisplayName}\"";

    /// <summary>A choice's label inside a sentence, "Default (discourage them)": lowercase, but Claude keeps its capital.</summary>
    private static string InSentence(string label) =>
        label.StartsWith("Claude", StringComparison.Ordinal) ? label : char.ToLowerInvariant(label[0]) + label[1..];

    public IReadOnlyList<Choice> ModelChoices { get; }

    public IReadOnlyList<Choice> EffortChoices { get; }

    public IReadOnlyList<Choice> ModeChoices { get; }

    /// <summary>The process monitor for this tab (DESIGN.md §4, "Process monitor").</summary>
    public IReadOnlyList<Choice> MonitorChoices { get; }

    [ObservableProperty]
    public partial Choice SelectedMonitor { get; set; }

    /// <summary>Continuing a task a usage limit stopped, once it resets (DESIGN.md §6, "Continuing after a limit resets").</summary>
    public IReadOnlyList<Choice> AutoContinueChoices { get; }

    [ObservableProperty]
    public partial Choice SelectedAutoContinue { get; set; }

    /// <summary>
    /// Carrying on with usage credits once a limit is used up (DESIGN.md §6, "Tabs that don't use usage credits"). Only on
    /// the Default account: an API key has none.
    /// </summary>
    public IReadOnlyList<Choice> UsageCreditsChoices { get; }

    public bool ShowsUsageCredits { get; }

    [ObservableProperty]
    public partial Choice SelectedUsageCredits { get; set; }

    /// <summary>Whether Claude may use subagents in this tab (DESIGN.md §5, "Subagents"). It applies at once.</summary>
    public IReadOnlyList<Choice> SubagentChoices { get; }

    [ObservableProperty]
    public partial Choice SelectedSubagents { get; set; }

    /// <summary>Folding each chain of steps in this tab (DESIGN.md §18, "Conversation cards"), which its menu's Steps sets too.</summary>
    public IReadOnlyList<Choice> StepsChoices { get; }

    [ObservableProperty]
    public partial Choice SelectedSteps { get; set; }

    [ObservableProperty]
    public partial Choice SelectedModel { get; set; }

    [ObservableProperty]
    public partial Choice SelectedEffort { get; set; }

    [ObservableProperty]
    public partial Choice SelectedMode { get; set; }

    [ObservableProperty]
    public partial bool UseCustomCheckIns { get; set; }

    [ObservableProperty]
    public partial bool CheckInsEnabled { get; set; }

    [ObservableProperty]
    public partial decimal? RunTimeMinutes { get; set; }

    [ObservableProperty]
    public partial decimal? QuietTimeMinutes { get; set; }

    [ObservableProperty]
    public partial string CheckInMessage { get; set; }

    [ObservableProperty]
    public partial bool NotifyOnCheckIn { get; set; }

    partial void OnSelectedModelChanged(Choice value) => OverridesChanged();

    partial void OnSelectedEffortChanged(Choice value) => OverridesChanged();

    partial void OnSelectedModeChanged(Choice value) => OverridesChanged();

    partial void OnSelectedMonitorChanged(Choice value) => OverridesChanged();

    partial void OnSelectedAutoContinueChanged(Choice value) => OverridesChanged();

    partial void OnSelectedUsageCreditsChanged(Choice value) => OverridesChanged();

    partial void OnSelectedSubagentsChanged(Choice value) => OverridesChanged();

    partial void OnSelectedStepsChanged(Choice value) => OverridesChanged();

    partial void OnUseCustomCheckInsChanged(bool value) => OverridesChanged();

    partial void OnCheckInsEnabledChanged(bool value) => OverridesChanged();

    partial void OnRunTimeMinutesChanged(decimal? value) => OverridesChanged();

    partial void OnQuietTimeMinutesChanged(decimal? value) => OverridesChanged();

    partial void OnCheckInMessageChanged(string value) => OverridesChanged();

    partial void OnNotifyOnCheckInChanged(bool value) => OverridesChanged();

    /// <summary>
    /// <b>Sync to other machines</b> (DESIGN.md §9, "Session library"). The tab's own state rather than an override, so
    /// its section has no <b>Use defaults</b>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SyncSizeText))]
    public partial bool SyncToLibrary { get; set; }

    partial void OnSyncToLibraryChanged(bool value)
    {
        if (!_holding)
        {
            _tab.SetSyncToLibrary(value);
        }
    }

    /// <summary>
    /// What the session takes in the session library, under <b>Sync to other machines</b> while it's on (DESIGN.md §9,
    /// "Writing"); null before the tab's first copy there.
    /// </summary>
    public string? SyncSizeText => SyncToLibrary && _tab.SyncSize is { } size ? $"This session takes {size} in the session library." : null;

    /// <summary><b>Connect to the Claude app</b> (DESIGN.md §18, "Remote Control"). The tab's own state, like <see cref="SyncToLibrary"/>.</summary>
    [ObservableProperty]
    public partial bool RemoteControl { get; set; }

    partial void OnRemoteControlChanged(bool value)
    {
        if (!_holding)
        {
            Applying = After(Applying, () => _tab.RemoteControl.SetAsync(value));
        }
    }

    /// <summary>The account can use Remote Control, or the tab is on and can be turned off.</summary>
    public bool CanChangeRemoteControl => _tab.RemoteControl.CanToggle;

    /// <summary>Why the switch is disabled, or null.</summary>
    public string? RemoteControlUnavailableText => CanChangeRemoteControl ? null : _tab.RemoteControl.UnavailableReason;

    public bool HasRemoteControlUnavailableText => RemoteControlUnavailableText is not null;

    // ---- Applying as each change is made ---------------------------------------------------------------------------

    /// <summary>What applying the changes made so far is doing, for <see cref="DoneAsync"/> and tests to wait on.</summary>
    public Task Applying { get; private set; } = Task.CompletedTask;

    private static string? OnOff(bool? value) => value switch { true => On, false => Off, null => null };

    private static bool? OnOff(string? value) => value switch { On => true, Off => false, _ => null };

    /// <summary>The overrides as the dialog shows them.</summary>
    private TabOverrides Overrides() => new()
    {
        Model = SelectedModel.Value,
        Effort = SelectedEffort.Value,
        PermissionMode = SelectedMode.Value,
        ShowProcessMonitor = OnOff(SelectedMonitor.Value),
        ContinueAfterLimitReset = OnOff(SelectedAutoContinue.Value),
        UseUsageCredits = OnOff(SelectedUsageCredits.Value),
        Subagents = Enum.TryParse<SubagentUse>(SelectedSubagents.Value, out var subagents) ? subagents : null,
        FoldChains = OnOff(SelectedSteps.Value),
        CheckIns = UseCustomCheckIns
            ? new CheckInSettings
            {
                Enabled = CheckInsEnabled,
                RunTimeMinutes = (int)(RunTimeMinutes ?? 0),
                QuietTimeMinutes = (int)(QuietTimeMinutes ?? 0),
                Message = string.IsNullOrWhiteSpace(CheckInMessage) ? CheckInSettings.DefaultMessage : CheckInMessage,
                Notify = NotifyOnCheckIn,
            }
            : null,
    };

    /// <summary>An override changed: the tab takes it now, in the order the changes were made.</summary>
    private void OverridesChanged()
    {
        if (_holding)
        {
            return;
        }
        var previous = _tab.State.Overrides;
        _tab.State.Overrides = Overrides();
        Applying = After(Applying, () => _tab.ApplyOverridesAsync(previous));
        RefreshItems();
    }

    private static async Task After(Task before, Func<Task> then)
    {
        await before;
        await then();
    }

    // ---- Extra folders (DESIGN.md §4, "Extra folders"): the tab's own, like SyncToLibrary --------------------------

    /// <summary>Folders Claude may also read and edit, besides the tab's own (<c>--add-dir</c>). They apply when the dialog closes.</summary>
    public ObservableCollection<string> ExtraFolders { get; }

    public bool HasExtraFolders => ExtraFolders.Count > 0;

    [RelayCommand]
    private async Task AddExtraFolderAsync()
    {
        if (await _services.Platform.PickFolderAsync("A folder Claude may also use") is not { } picked)
        {
            return;
        }
        var folder = FolderHistory.Normalize(picked);
        if (!FolderHistory.SamePath(folder, _tab.Folder) && !ExtraFolders.Any(f => FolderHistory.SamePath(f, folder)))
        {
            ExtraFolders.Add(folder);
        }
    }

    [RelayCommand]
    private void RemoveExtraFolder(string? folder)
    {
        if (folder is not null)
        {
            ExtraFolders.Remove(folder);
        }
    }

    // ---- Project actions (DESIGN.md §18, "Custom actions"): in Settings, on the tab's Actions page --------------------

    /// <summary>"In Settings → NightOwl → Actions": the group is named after the tab's project.</summary>
    public string ProjectActionsText => $"In Settings → {ProjectSettingsViewModel.GroupName(_tab)} → Actions.";

    /// <summary>Opens Settings on the tab's Actions page. This dialog stays open behind it.</summary>
    [RelayCommand]
    private Task OpenProjectActionsAsync() =>
        _shell?.OpenProjectSettingsAsync(_tab, SettingsViewModel.ActionsPage) ?? Task.CompletedTask;

    /// <summary><b>Done</b> (or Esc): the changes have applied already; the extra folders apply now, then it closes.</summary>
    [RelayCommand]
    private async Task DoneAsync()
    {
        _close();
        await Applying;
        await _tab.SetExtraFoldersAsync([.. ExtraFolders]);
    }

    // ---- Its sections and settings, as Settings' pages have them (DESIGN.md §14) --------------------------------------

    public SettingItem? FindSetting(string key) => _items.GetValueOrDefault(key);

    public SettingsGroup? FindGroup(string key) => _groups.GetValueOrDefault(key);

    /// <summary>Each section's <b>Use defaults</b>, and a row's <b>Reset</b>: the tab goes back to Settings' defaults, applied once.</summary>
    public void Reset(IReadOnlyList<SettingItem> items, Action? also = null)
    {
        _holding = true;
        try
        {
            foreach (var item in items)
            {
                item.ResetValue?.Invoke();
            }
            also?.Invoke();
        }
        finally
        {
            _holding = false;
        }
        OverridesChanged();
    }

    /// <summary>Every section's <b>Use defaults</b> at once: for tests.</summary>
    internal void UseAllDefaults() => Reset([.. _items.Values.Where(i => i.CanReset && i.IsChanged)]);

    private void RefreshItems()
    {
        foreach (var item in _items.Values)
        {
            item.Refresh(null);
        }
        foreach (var group in _groups.Values)
        {
            group.Refresh();
        }
    }

    /// <summary>An override: changed while the tab has its own, and reset by going back to the default.</summary>
    private SettingItem Override(string key, string title, Func<bool> isDefault, Action reset, string? description = null) =>
        Add(new SettingItem(key, title, isDefault, reset) { Description = description, IsMachineOnly = false });

    /// <summary>The tab's own switch or list: nothing to go back to.</summary>
    private SettingItem Own(string key, string title, string? description = null, string? more = null) =>
        Add(new SettingItem(key, title) { Description = description, More = more, IsMachineOnly = false });

    private SettingItem Add(SettingItem item)
    {
        _items.Add(item.Key, item);
        item.AttachTo(this);
        return item;
    }

    private void Section(string key, string title, IReadOnlyList<SettingItem> items) =>
        _groups.Add(key, new SettingsGroup(this, key, title, items));
}
