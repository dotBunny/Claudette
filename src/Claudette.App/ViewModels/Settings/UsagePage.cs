using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels.Settings;

/// <summary>
/// Settings → Usage (DESIGN.md §6): the warning levels and the burn rate, what the usage header shows, the usage history
/// and sharing usage with other machines. What a tab does at a usage limit is in Tabs, since each tab can change it.
/// </summary>
public sealed partial class UsagePage : SettingsPage
{
    public UsagePage(SettingsContext context) : base(context, SettingsCategory.Usage)
    {
        Section("warnings", "Warnings",
        [
            Setting("usage.warnPercent", "Warn at (% of the session used)", s => s.Usage.WarnPercent, (s, v) => s.Usage.WarnPercent = v,
                "The header's meter turns amber.", keywords: ["threshold", "alert", "limit"]),
            Setting("usage.criticalPercent", "Alert at (% of the session used)", s => s.Usage.CriticalPercent, (s, v) => s.Usage.CriticalPercent = v,
                "The meter turns red, and a notification says so.", keywords: ["threshold", "warning", "limit"]),
            Setting("usage.burnRateWindowMinutes", "Burn rate window (minutes)", s => s.Usage.BurnRateWindowMinutes, (s, v) => s.Usage.BurnRateWindowMinutes = v,
                "How many minutes of recent usage the pace is worked out from.", keywords: ["projection", "pace"]),
        ]);
        Section("header", "The usage header",
        [
            Setting("usage.compactHeader", "Compact usage header", s => s.Usage.CompactHeader, (s, v) => s.Usage.CompactHeader = v,
                "Each limit on one line: its first letter, a small bar and its percent, such as S 61%.",
                "Hover over a meter for its name and when it resets.", ["one line", "short", "small", "chip"]),
            Setting("usage.showModelMeters", "Show model-specific weekly limits", s => s.Usage.ShowModelMeters, (s, v) => s.Usage.ShowModelMeters = v,
                "A meter for each model with a weekly limit of its own.", keywords: ["opus", "fable", "weekly"]),
            Setting("usage.useUsageCommandFallback", "If Claude Code stops reporting them, read them from /usage", s => s.Usage.UseUsageCommandFallback,
                (s, v) => s.Usage.UseUsageCommandFallback = v,
                "Free, but can break when Claude Code changes its wording.", keywords: ["model limits", "fallback"]),
            Setting("state.detailedUsageHeader", "Detailed usage header", () => !Services.State.DetailedUsageHeader, () => Services.State.DetailedUsageHeader = false,
                "Taller, with charts of the session and the week, the burn rate and the busiest tabs.",
                "The chevron after the meters does the same.", ["charts", "sparkline"]),
        ]);
        Section("history", "History",
        [
            // Going back to a month would delete what's older: only its own Reset does that, never the section's.
            Setting("usage.keepHistory", "Keep usage history", s => s.Usage.KeepHistory, (s, v) => s.Usage.KeepHistory = v,
                "Usage percentages and token counts, never your prompts, replies or code.", keywords: ["retention", "clear usage history"],
                resetsWithSection: false),
            Setting("usage.shareThroughLibrary", "Share usage with my other machines", s => s.Usage.ShareThroughLibrary, (s, v) => s.Usage.ShareThroughLibrary = v,
                "Through the session library, so the charts add up every machine's.",
                "Each machine that shares keeps its last week of session and weekly readings in the session library (Settings → Sessions), and the charts take in the readings of the others signed in to the same account. Tabs that sync to the library also keep each turn's token counts, estimated cost, project and tab name there, so the Usage window adds up every machine's. Tabs that don't sync keep theirs on this machine.",
                ["sync", "library", "other computers"]),
            Info("usage.clearEverywhere", "Clear usage history on every machine", keywords: ["shared usage", "reset"]),
        ]);
    }

    public override string IconKey => "IconCardStats";

    public override string Description => "Warnings, the usage header, and the history the charts are drawn from.";

    /// <summary>
    /// The usage header drawn taller with charts (DESIGN.md §6, "Detailed header"), the same switch as its chevron. It's
    /// this machine's state rather than a setting, like the sidebar's collapsed state, so it doesn't sync.
    /// </summary>
    public bool DetailedUsageHeader
    {
        get => Services.State.DetailedUsageHeader;
        set
        {
            if (Services.State.DetailedUsageHeader != value)
            {
                Services.State.DetailedUsageHeader = value;
                SaveState();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The detailed header is kept with the state, so that's saved too; sharing may have gone back to off.</summary>
    protected override void OnReset()
    {
        Services.SaveState();
        ClearEverywhereCommand.NotifyCanExecuteChanged();
    }

    public decimal? WarnPercent
    {
        get => (decimal)Settings.Usage.WarnPercent;
        set => Set(value, v => Settings.Usage.WarnPercent = Math.Clamp((double)(v ?? 75), 1, 100));
    }

    public decimal? CriticalPercent
    {
        get => (decimal)Settings.Usage.CriticalPercent;
        set => Set(value, v => Settings.Usage.CriticalPercent = Math.Clamp((double)(v ?? 90), 1, 100));
    }

    public decimal? BurnRateWindowMinutes
    {
        get => Settings.Usage.BurnRateWindowMinutes;
        set => Set(value, v => Settings.Usage.BurnRateWindowMinutes = Math.Clamp((int)(v ?? 30), 5, 300));
    }

    /// <summary>DESIGN.md §6, "Compact header".</summary>
    public bool CompactHeader
    {
        get => Settings.Usage.CompactHeader;
        set => Set(value, v => Settings.Usage.CompactHeader = v);
    }

    public bool ShowModelMeters
    {
        get => Settings.Usage.ShowModelMeters;
        set => Set(value, v => Settings.Usage.ShowModelMeters = v);
    }

    public bool UseUsageCommandFallback
    {
        get => Settings.Usage.UseUsageCommandFallback;
        set => Set(value, v => Settings.Usage.UseUsageCommandFallback = v);
    }

    /// <summary>DESIGN.md §6, "Sharing across machines".</summary>
    public bool ShareUsageThroughLibrary
    {
        get => Settings.Usage.ShareThroughLibrary;
        set
        {
            Set(value, v => Settings.Usage.ShareThroughLibrary = v);
            ClearEverywhereCommand.NotifyCanExecuteChanged();
        }
    }

    // ---- Usage history (DESIGN.md §6, "Usage history") ----------------------------------------------------------

    public IReadOnlyList<RetentionChoice> HistoryRetentionChoices { get; } =
        [.. Enum.GetValues<RetentionPeriod>().Select(p => new RetentionChoice(p))];

    public RetentionChoice KeepUsageHistory
    {
        get => HistoryRetentionChoices.FirstOrDefault(c => c.Period == Settings.Usage.KeepHistory) ?? HistoryRetentionChoices[2];
        set => Set(value?.Period ?? RetentionPeriod.OneMonth, v => Settings.Usage.KeepHistory = v);
    }

    /// <summary>"Clear usage history" asks first.</summary>
    public InlineConfirmation ClearUsageConfirmation => field ??= new(ClearUsageAsync);

    /// <summary>The confirmation's checkbox: also reset the token totals saved with each tab.</summary>
    [ObservableProperty]
    public partial bool AlsoResetTabTotals { get; set; }

    [ObservableProperty]
    public partial string? UsageClearedText { get; set; }

    [RelayCommand]
    private void ClearUsageHistory()
    {
        AlsoResetTabTotals = false;
        UsageClearedText = null;
        ClearUsageConfirmation.Ask();
    }

    private async Task ClearUsageAsync()
    {
        await Services.ClearUsageHistoryAsync(AlsoResetTabTotals);
        UsageClearedText = AlsoResetTabTotals ? "Usage history and tab token totals cleared." : "Usage history cleared.";
    }

    // ---- Clear on every machine (DESIGN.md §6, "Clear usage history") -------------------------------------------------

    /// <summary>"Clear on every machine" asks first.</summary>
    public InlineConfirmation ClearEverywhereConfirmation => field ??= new(ClearEverywhereAsync);

    /// <summary>The confirmation's checkbox: every machine resets the token totals saved with its tabs too.</summary>
    [ObservableProperty]
    public partial bool AlsoResetTabTotalsEverywhere { get; set; }

    [ObservableProperty]
    public partial string? ClearedEverywhereText { get; set; }

    /// <summary>Only while usage is shared: the machines that don't share have nothing in the library to clear.</summary>
    private bool CanClearEverywhere() => Settings.Usage.ShareThroughLibrary;

    [RelayCommand(CanExecute = nameof(CanClearEverywhere))]
    private void ClearEverywhere()
    {
        AlsoResetTabTotalsEverywhere = false;
        ClearedEverywhereText = null;
        ClearEverywhereConfirmation.Ask();
    }

    private async Task ClearEverywhereAsync()
    {
        var reset = AlsoResetTabTotalsEverywhere;
        var shared = await Services.ClearUsageHistoryEverywhereAsync(reset);
        ClearedEverywhereText = shared
            ? $"Usage history{(reset ? " and tab token totals" : "")} cleared here and in the session library. Your other machines clear theirs the next time they check, within a minute if they're running."
            : "Usage history cleared here, but the session library couldn't be reached to clear the shared usage. Try again once it's available.";
    }

}
