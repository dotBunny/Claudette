using System.Text.Json.Nodes;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.Core.Files;
using Claudette.Core.Library;
using Claudette.Usage;

namespace Claudette.App.Tests;

/// <summary>
/// Turns machines run in synced sessions, shared through the session library (DESIGN.md §6, "Sharing across
/// machines"). Two harnesses on one library folder stand in for two machines.
/// </summary>
public class SharedTurnsTests
{
    private const string Remote = "git@github.com:studio/dethol.git";

    [Fact]
    public async Task A_synced_tabs_turns_count_on_the_other_machine_in_the_same_project_with_each_machines_share()
    {
        await using var desk = Machine("Desktop", "me@example.com", out var library);
        await using var lap = Machine("Laptop", "me@example.com", out _, library);
        var deskTab = await desk.Harness.OpenTabAsync();
        deskTab.SetSyncToLibrary(true);
        desk.Harness.WriteTranscript("s1", UserLine("s1", desk.Harness.WorkFolder));

        desk.Harness.Transport.EmitTurn();
        await LibraryHasAsync(desk, "s1", turns: 1);
        desk.Harness.Transport.EmitTurn();
        await LibraryHasAsync(desk, "s1", turns: 2);
        var lapTab = await lap.Harness.OpenTabAsync();
        lap.Harness.Transport.EmitTurn();
        await TabTestHarness.Eventually(() => lap.Tracker.Store.GetTurns(DateTimeOffset.MinValue, DateTimeOffset.MaxValue).Count == 1, "the laptop's own turn");
        var imported = new TaskCompletionSource();
        lap.Tracker.TurnsImported += _ => imported.TrySetResult();

        await lap.Tracker.ShareAsync();
        await imported.Task.WaitAsync(Waiting.Timeout, TestContext.Current.CancellationToken);
        using var header = new UsageViewModel(lap.Harness.Services, lap.Tracker);
        var panel = new UsagePanelViewModel(lap.Harness.Services, lap.Tracker, header, id => id == lapTab.Id ? "Laptop tab" : null);
        await panel.RefreshAsync();

        // One project: a clone of the same remote on each machine, three turns in all.
        var project = Assert.Single(panel.Projects);
        Assert.Equal("3 turns", project.Turns);
        Assert.Equal("$0.03", project.Cost);
        Assert.Equal(lap.Harness.WorkFolder, project.Folder);
        Assert.Contains("Desktop: $0.02 · 2 turns", project.Tooltip, StringComparison.Ordinal);
        Assert.Contains("This machine: $0.01 · 1 turn", project.Tooltip, StringComparison.Ordinal);
        Assert.Equal(["Laptop tab", $"{deskTab.DisplayName} · Desktop"], panel.Tabs.Select(t => t.Name).Order());
        Assert.Equal("On Desktop", panel.Tabs.Single(t => t.Name.EndsWith("Desktop", StringComparison.Ordinal)).Tooltip);
        // Read once: nothing new is added the next time.
        await lap.Tracker.ShareAsync();
        Assert.Equal(3, lap.Tracker.Store.GetTokensByTab(DateTimeOffset.MinValue).Sum(t => t.Turns));
    }

    [Fact]
    public async Task A_tab_that_doesnt_sync_shares_nothing()
    {
        await using var desk = Machine("Desktop", "me@example.com", out var library);
        await using var lap = Machine("Laptop", "me@example.com", out _, library);
        var deskTab = await desk.Harness.OpenTabAsync();
        desk.Harness.WriteTranscript("s1", UserLine("s1", desk.Harness.WorkFolder));

        desk.Harness.Transport.EmitTurn();
        await TabTestHarness.Eventually(() => desk.Tracker.Store.GetTurns(DateTimeOffset.MinValue, DateTimeOffset.MaxValue).Count == 1, "the desktop's turn");
        await lap.Tracker.ShareAsync();

        Assert.False(deskTab.State.SyncToLibrary);
        Assert.Empty(lap.Tracker.Store.GetTokensByTab(DateTimeOffset.MinValue));
        Assert.False(Directory.Exists(Path.Combine(library, "sessions")));
    }

    [Fact]
    public async Task Another_plan_accounts_turns_arent_counted()
    {
        await using var desk = Machine("Desktop", "me@example.com", out var library);
        await using var lap = Machine("Laptop", "someone.else@example.com", out _, library);
        var deskTab = await desk.Harness.OpenTabAsync();
        deskTab.SetSyncToLibrary(true);
        desk.Harness.WriteTranscript("s1", UserLine("s1", desk.Harness.WorkFolder));
        desk.Harness.Transport.EmitTurn();
        await LibraryHasAsync(desk, "s1", turns: 1);

        await lap.Tracker.ShareAsync();

        Assert.Empty(lap.Tracker.Store.GetTokensByTab(DateTimeOffset.MinValue));
    }

    [Fact]
    public async Task A_session_continued_on_another_machine_keeps_both_machines_turns_in_the_library()
    {
        await using var desk = Machine("Desktop", "me@example.com", out var library);
        var plan = UsageSharing.AccountKey("me@example.com", "Acme");
        // The laptop ran a turn in the session first.
        var file = Path.Combine(library, "sessions", "s1", SessionLibrary.UsageFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, SessionUsageFile.Write(new SessionUsage(SessionUsageFile.FileVersion,
            new Dictionary<string, string> { ["lap"] = "Laptop" }, new Dictionary<string, string> { ["lap-tab"] = "Review" },
            [new SharedTurn("lap-turn", desk.Harness.Time.GetUtcNow().AddMinutes(-5), "lap", "lap-tab", "claude-opus-5-5", 100, 20, 0, 0, 0.01, Plan: plan)])));
        var tab = await desk.Harness.OpenTabAsync();
        tab.SetSyncToLibrary(true);
        desk.Harness.WriteTranscript("s1", UserLine("s1", desk.Harness.WorkFolder));

        desk.Harness.Transport.EmitTurn();

        var usage = await LibraryHasAsync(desk, "s1", turns: 2);
        Assert.Contains(usage.Turns, t => t.Id == "lap-turn");
        Assert.Contains(usage.Turns, t => t.Machine == desk.Harness.Services.State.MachineId && t.Plan == plan);
        Assert.Equal("Laptop", usage.Machines["lap"]);
        Assert.Equal("Desktop", usage.Machines[desk.Harness.Services.State.MachineId!]);
    }

    [Fact]
    public async Task Another_machines_turns_under_an_api_key_of_the_same_name_count_toward_its_spend_and_warnings()
    {
        await using var lap = Machine("Laptop", "me@example.com", out var library);
        lap.Harness.Services.Accounts.Credentials = new FakeCredentialStore();
        lap.Harness.Services.Accounts.HelperExecutable = Path.Combine(lap.Harness.Root, "Claudette.exe");
        lap.Tracker.TurnsImported += names => _ = lap.Harness.Services.Accounts.OnTurnsImportedAsync(names);
        var studio = await lap.Harness.Services.Accounts.AddApiKeyAccountAsync("Studio", "sk-ant-studio");
        studio.DailySpendAlert = 1m;
        using var spend = new AccountSpendViewModel(lap.Harness.Services, lap.Tracker);
        spend.Show(studio);
        var file = Path.Combine(library, "sessions", "s1", SessionLibrary.UsageFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, SessionUsageFile.Write(new SessionUsage(SessionUsageFile.FileVersion,
            new Dictionary<string, string> { ["desk"] = "Desktop" }, new Dictionary<string, string>(),
            [new SharedTurn("desk-turn", lap.Harness.Time.GetUtcNow(), "desk", "desk-tab", "claude-opus-5-5", 100, 20, 0, 0, 1.25, AccountId: "desk-studio", Account: "studio")])));

        await lap.Tracker.ShareAsync();

        await TabTestHarness.Eventually(() => spend.TodayText == "$1.25", "the other machine's spend in the header");
        await TabTestHarness.Eventually(() => lap.Harness.Notifier.Shown.Any(n => n.Title == "Studio: $1.25 today"), "the spend warning");
        Assert.Contains("other machines' tabs", spend.Tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clearing_on_every_machine_empties_the_library_and_each_machine_clears_its_own_when_it_checks()
    {
        await using var desk = Machine("Desktop", "me@example.com", out var library);
        await using var lap = Machine("Laptop", "me@example.com", out _, library);
        var deskTab = await desk.Harness.OpenTabAsync();
        deskTab.SetSyncToLibrary(true);
        desk.Harness.WriteTranscript("s1", UserLine("s1", desk.Harness.WorkFolder));
        desk.Harness.Transport.EmitTurn();
        await LibraryHasAsync(desk, "s1", turns: 1);
        await lap.Tracker.ShareAsync();
        Assert.Single(lap.Tracker.Store.GetTokensByTab(DateTimeOffset.MinValue));

        // On the laptop, a minute on: Settings → Usage, resetting the tabs' totals too.
        lap.Harness.Time.Advance(TimeSpan.FromMinutes(1));
        var settings = new SettingsViewModel(lap.Harness.Services, null).Usage;
        Assert.True(settings.ClearEverywhereCommand.CanExecute(null));
        settings.ClearEverywhereCommand.Execute(null);
        settings.AlsoResetTabTotalsEverywhere = true;
        await settings.ClearEverywhereConfirmation.ConfirmCommand.ExecuteAsync(null);

        Assert.StartsWith("Usage history and tab token totals cleared here and in the session library.", settings.ClearedEverywhereText, StringComparison.Ordinal);
        Assert.Empty(lap.Tracker.Store.GetTokensByTab(DateTimeOffset.MinValue));
        Assert.False(File.Exists(Path.Combine(library, "sessions", "s1", SessionLibrary.UsageFileName)));
        Assert.True(File.Exists(Path.Combine(library, "usage", SessionLibrary.UsageClearedFileName)));

        // The desktop clears its own, and its tabs' totals, when it next checks.
        Assert.Equal(120, deskTab.State.Tokens.Total);
        await desk.Tracker.ShareAsync();
        await TabTestHarness.Eventually(() => deskTab.State.Tokens.Total == 0, "the desktop's tab totals reset");
        Assert.Empty(desk.Tracker.Store.GetTokensByTab(DateTimeOffset.MinValue));

        // What comes after is shared as before, without what was cleared.
        desk.Harness.Time.Advance(TimeSpan.FromMinutes(2));
        desk.Harness.Transport.EmitTurn();
        var usage = await LibraryHasAsync(desk, "s1", turns: 1);
        Assert.True(usage.Turns[0].At > lap.Tracker.Store.ClearedAt);
        await lap.Tracker.ShareAsync();
        Assert.Equal(1, lap.Tracker.Store.GetTokensByTab(DateTimeOffset.MinValue).Sum(t => t.Turns));
    }

    [Fact]
    public async Task Clearing_on_every_machine_is_only_offered_while_usage_is_shared()
    {
        await using var lap = Machine("Laptop", "me@example.com", out _);
        var settings = new SettingsViewModel(lap.Harness.Services, null).Usage;
        Assert.True(settings.ClearEverywhereCommand.CanExecute(null));

        settings.ShareUsageThroughLibrary = false;

        Assert.False(settings.ClearEverywhereCommand.CanExecute(null));
    }

    /// <summary>Waits for the session's usage file in the library to hold this many turns, letting the copy's settle delay pass.</summary>
    private static async Task<SessionUsage> LibraryHasAsync(SharingMachine machine, string sessionId, int turns)
    {
        var file = Path.Combine(machine.Harness.Services.Library.Library.GetSessionFolder(sessionId), SessionLibrary.UsageFileName);
        SessionUsage? usage = null;
        await TabTestHarness.Eventually(() =>
        {
            machine.Harness.Time.Advance(TimeSpan.FromSeconds(1));
            usage = File.Exists(file) ? SessionUsageFile.Read(AtomicFile.ReadAllText(file)) : null;
            return usage?.Turns.Count == turns;
        }, $"{turns} turns in the library");
        return usage!;
    }

    /// <summary>
    /// A machine that shares its usage through <paramref name="library"/>, signed in as <paramref name="email"/>, with
    /// its work folder a clone of <see cref="Remote"/>.
    /// </summary>
    private static SharingMachine Machine(string name, string email, out string library, string? sharedLibrary = null)
    {
        var h = new TabTestHarness();
        library = sharedLibrary ?? Path.Combine(h.Root, "shared-library");
        h.Services.Settings.Sessions.LibraryFolder = library;
        h.Services.Settings.Sessions.MachineName = name;
        h.Services.Settings.Usage.ShareThroughLibrary = true;
        h.Services.Library.OnSettingsChanged();
        h.Services.RemoteControl.UseAccount(new Core.Auth.AuthStatus(true, "claude.ai", null, email, "Acme", "max", null, null));
        var git = Path.Combine(h.WorkFolder, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(git, "config"), $"[remote \"origin\"]\n\turl = {Remote}\n");
        var tracker = new UsageTracker(h.Services, new UsageStore(Path.Combine(h.Root, "usage.db"), h.Time));
        h.Services.UseUsageTracker(tracker);
        return new SharingMachine(h, tracker);
    }

    private static string UserLine(string sessionId, string cwd) => new JsonObject
    {
        ["type"] = "user",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = "Fix the loader" },
        ["cwd"] = cwd,
        ["sessionId"] = sessionId,
        ["timestamp"] = "2026-09-28T10:00:00Z",
    }.ToJsonString();

    private sealed record SharingMachine(TabTestHarness Harness, UsageTracker Tracker) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Tracker.DisposeAsync();
            await Harness.DisposeAsync();
        }
    }
}
