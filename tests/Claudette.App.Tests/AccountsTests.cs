using System.Text.Json;
using System.Text.Json.Nodes;
using Claudette.App.Conversation;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.Settings;
using Claudette.Core.Accounts;
using Claudette.Usage;

namespace Claudette.App.Tests;

/// <summary>Accounts besides the Default one: API keys, which account a tab runs under, and their spend (DESIGN.md §4, "Accounts").</summary>
public class AccountsTests
{
    private const string Init = """{"type":"system","subtype":"init","session_id":"s1","model":"claude-opus-5-5","permissionMode":"default"}""";
    private const string SignedOutReply = """{"type":"assistant","message":{"model":"<synthetic>","content":[{"type":"text","text":"Invalid API key · Fix external API key"}]},"error":"authentication_failed"}""";

    // ---- Adding one -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_API_key_goes_only_to_the_credential_store_and_its_account_takes_the_next_free_color()
    {
        await using var h = new TabTestHarness();
        var keys = UseKeys(h);

        var work = await h.Services.Accounts.AddApiKeyAccountAsync(" Work ", " sk-ant-work ");
        var side = await h.Services.Accounts.AddApiKeyAccountAsync("Side project", "sk-ant-side");

        Assert.Equal("Work", work.Name);
        Assert.Equal("sk-ant-work", keys.Secrets[ApiKeyHelper.CredentialKey(work.Id)]);
        Assert.Equal("Claudette: Claude API key (Work)", keys.Labels[ApiKeyHelper.CredentialKey(work.Id)]);
        Assert.Equal([work, side], h.Services.Accounts.All);
        Assert.Equal([TabGroupViewModel.Palette[0].Color, TabGroupViewModel.Palette[1].Color], h.Services.Accounts.All.Select(AccountsService.ColorOf));
        // Never in the app state.
        Assert.DoesNotContain("sk-ant", JsonSerializer.Serialize(h.Services.State), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_credential_store_no_account_is_added()
    {
        await using var h = new TabTestHarness();
        UseKeys(h).IsAvailable = false;
        h.Services.Accounts.Credentials = new Core.Credentials.UnavailableCredentialStore();

        await Assert.ThrowsAsync<Core.Credentials.CredentialStoreException>(() => h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work"));
        Assert.Empty(h.Services.Accounts.All);
    }

    // ---- Which account a tab runs under -----------------------------------------------------------------------------

    [Fact]
    public async Task A_groups_new_tabs_run_under_its_account_with_the_key_helper_and_its_open_tabs_keep_theirs()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        var first = await h.OpenTabAsync();
        var group = h.Shell.Groups.Single();

        // Default: Claude Code's own sign-in, started as before.
        Assert.Null(first.Account);
        Assert.Null(h.Factory.Launches[0].Settings);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", h.Factory.Launches[0].EnvironmentOverrides.Keys);
        Assert.Equal(["Default", "Work"], group.AccountMenu.Select(m => m.Name));
        Assert.Equal([true, false], group.AccountMenu.Select(m => m.IsChecked));

        group.ChooseAccountCommand.Execute(work.Id);

        Assert.Same(work, group.Account);
        Assert.Equal("Work", group.LabelAccountTag?.Name);
        Assert.Equal(work.Id, h.Services.State.FolderAccounts.Single().Value);
        // The open tab keeps Default, and its row says so now that it differs from its group.
        Assert.Null(first.Account);
        Assert.True(first.RowAccountTag?.IsDefault);

        var second = await OpenAnotherTabAsync(h);

        Assert.Same(work, second.Account);
        Assert.False(second.ShowsAccountTag);
        var launch = h.Factory.Launches[1];
        Assert.Equal($"\"{HelperPath(h)}\" --api-key-helper {work.Id}", JsonNode.Parse(launch.Settings!)!["apiKeyHelper"]!.GetValue<string>());
        // Nothing in the environment can take over from the key.
        Assert.Null(launch.EnvironmentOverrides["ANTHROPIC_API_KEY"]);
        Assert.Null(launch.EnvironmentOverrides["ANTHROPIC_AUTH_TOKEN"]);
        Assert.Contains(second.InfoRows, r => r.Label == "Account" && r.Value == "Work (API key)");
    }

    [Fact]
    public async Task New_folders_use_the_account_Settings_says()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        h.Services.Accounts.NewFolderAccount = work.Id;

        var tab = await h.OpenTabAsync();

        Assert.Same(work, tab.Account);
        Assert.Same(work, h.Shell.Groups.Single().Account);
    }

    // ---- Moving a tab -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Moving_a_tab_with_a_conversation_asks_first_with_what_it_costs_then_carries_on_under_the_account()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        h.Transport.Answers["get_context_usage"] = _ => new JsonObject { ["totalTokens"] = 150_000, ["maxTokens"] = 1_000_000, ["percentage"] = 15.0 };
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        var tab = await h.OpenTabAsync();
        h.Transport.EmitTurn();
        // The whole turn, and the tab idle after it: the session's ID comes with the turn's first line, and the context
        // with the tab's start. A move confirmed while the tab still says it's working waits for a turn end that's gone by.
        await TabTestHarness.Eventually(() => tab.State.SessionId == "s1" && tab.Context.Tokens == 150_000 && tab.State.Tokens.Turns == 1
            && tab.Status == TabStatus.Idle && tab.IsSettled, "the turn");
        Assert.Equal(["Default", "Work"], tab.AccountMenu.Select(m => m.Name));

        tab.SwitchAccountCommand.Execute(work.Id);

        var confirmation = Assert.IsType<ConfirmationViewModel>(h.Shell.Confirmation);
        Assert.Equal("Move \"work\" to Work?", confirmation.Title);
        Assert.Contains("without the prompt cache, once: about $0.75 at list price.", confirmation.Message, StringComparison.Ordinal);
        Assert.Null(tab.Account);

        await confirmation.ConfirmCommand.ExecuteAsync(null);
        await TabTestHarness.Eventually(() => h.Factory.Launches.Count == 2 && tab.IsSettled, "the restart");

        Assert.Same(work, tab.Account);
        Assert.Equal(work.Id, tab.State.AccountId);
        Assert.Equal("s1", h.Factory.Launches[1].Resume);
        Assert.Contains(work.Id, h.Factory.Launches[1].Settings, StringComparison.Ordinal);
        Assert.Contains(InlineDispatcher.Read(() => tab.Items.OfType<NoteItem>().ToArray()), n => n.Text == "Started Claude Code again under Work.");

        // And back to Default, which asks too, about the plan this time.
        tab.SwitchAccountCommand.Execute(null);
        Assert.EndsWith("which counts toward its plan's limits.", h.Shell.Confirmation!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tab_moved_to_an_api_key_counts_only_what_its_turns_there_spend()
    {
        // The resumed claude starts from the totals the transcript saved, and its first result carries them: without
        // them taken off, the whole conversation's spend so far would count against the key (DESIGN.md §6).
        await using var h = new TabTestHarness();
        UseKeys(h);
        var tracker = new UsageTracker(h.Services, new UsageStore(Path.Combine(h.Root, "usage.db"), h.Time));
        h.Services.UseUsageTracker(tracker);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        var tab = await h.OpenTabAsync();
        h.Transport.EmitTurn();
        h.Transport.EmitTurn();
        // Idle too: the second result is counted before the tab hears it's idle, and a move confirmed in between waits
        // for a turn end that's gone by.
        await TabTestHarness.Eventually(() => tab.State.SessionId == "s1" && tab.IsSettled && tab.State.Tokens.Turns == 2 && tab.Status == TabStatus.Idle,
            "two turns on Default");
        // What Claude Code saves when the process exits: the two turns' totals.
        h.WriteTranscript("s1", """{"type":"cost-state","sessionId":"s1","totalCostUSD":0.02,"modelUsage":{"claude-opus-5-5":{"inputTokens":200,"outputTokens":40,"cacheReadInputTokens":0,"cacheCreationInputTokens":0,"costUSD":0.02}}}""");

        tab.SwitchAccountCommand.Execute(work.Id);
        await Assert.IsType<ConfirmationViewModel>(h.Shell.Confirmation).ConfirmCommand.ExecuteAsync(null);
        await TabTestHarness.Eventually(() => h.Factory.Launches.Count == 2 && tab.IsSettled, "the restart");
        Assert.Equal(240, Assert.Single(h.Factory.Launches[1].RestoredUsage).Total);
        h.Transport.RestoreUsage("claude-opus-5-5", 200, 40, 0.02);
        h.Transport.EmitTurn();

        await TabTestHarness.Eventually(() => tab.State.Tokens.Turns == 3, "the turn under Work");
        Assert.Equal(360, tab.State.Tokens.Total);
        await TabTestHarness.Eventually(() => tracker.Store.GetSpend(work.Id, DateTimeOffset.MinValue) > 0, "the turn's record");
        Assert.Equal(0.01, tracker.Store.GetSpend(work.Id, DateTimeOffset.MinValue), precision: 6);
        Assert.Equal(0.02, tracker.Store.GetSpend(null, DateTimeOffset.MinValue), precision: 6);
    }

    [Fact]
    public async Task A_tab_with_nothing_to_carry_on_moves_without_asking()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        var tab = await h.OpenTabAsync();

        tab.SwitchAccountCommand.Execute(work.Id);
        await TabTestHarness.Eventually(() => h.Factory.Launches.Count == 2 && tab.IsSettled, "the restart");

        Assert.Null(h.Shell.Confirmation);
        Assert.Same(work, tab.Account);
        Assert.NotNull(h.Factory.Launches[1].Settings);
    }

    [Fact]
    public async Task While_Claude_works_the_move_waits_for_the_turn_to_end()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        var tab = await h.OpenTabAsync();
        tab.ComposerText = "hello";
        await tab.SendCommand.ExecuteAsync(null);
        await TabTestHarness.Eventually(() => tab.IsWorking, "the turn to start");

        tab.SwitchAccountCommand.Execute(work.Id);

        Assert.Single(h.Factory.Launches);
        Assert.Contains(InlineDispatcher.Read(() => tab.Items.OfType<NoteItem>().ToArray()), n => n.Text.StartsWith("Moves to Work once this turn ends", StringComparison.Ordinal));
        h.Transport.EmitTurn();
        await TabTestHarness.Eventually(() => h.Factory.Launches.Count == 2 && tab.IsSettled, "the restart after the turn");
        Assert.NotNull(h.Factory.Launches[1].Settings);
    }

    [Fact]
    public async Task A_copy_of_a_tab_stays_on_its_account_whatever_its_groups()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        var tab = await h.OpenTabAsync();
        tab.SwitchAccountCommand.Execute(work.Id);
        await TabTestHarness.Eventually(() => h.Factory.Launches.Count == 2 && tab.IsSettled, "the move");

        await h.Shell.DuplicateTabCommand.ExecuteAsync(tab);
        var copy = h.Shell.SelectedTab!;
        await TabTestHarness.Eventually(() => h.Factory.Launches.Count == 3, "the copy to start");

        Assert.NotSame(tab, copy);
        Assert.Same(work, copy.Account);
        Assert.Null(h.Shell.Groups.Single().Account);
        Assert.NotNull(h.Factory.Launches[2].Settings);
    }

    // ---- What an API key changes -------------------------------------------------------------------------------------

    [Fact]
    public async Task A_refused_key_is_the_tabs_own_error_and_doesnt_ask_Claude_Code_to_sign_in()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var signInAsked = 0;
        h.OnAuthenticationRequired = () => signInAsked++;
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        h.Services.Accounts.NewFolderAccount = work.Id;
        var tab = await h.OpenTabAsync();

        h.Transport.Emit(Init);
        h.Transport.Emit(SignedOutReply);
        await TabTestHarness.Eventually(() => tab.Status == TabStatus.Error, "the refused key");

        Assert.Equal(0, signInAsked);
        Assert.False(h.Shell.NeedsSignIn);
        Assert.False(tab.IsWaitingForSignIn);
        Assert.Contains(InlineDispatcher.Read(() => tab.Items.OfType<NoteItem>().ToArray()),
            n => n.Text.StartsWith("Claude Code couldn't use Work's API key. Check the key in Settings → Accounts", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_API_key_tab_cant_connect_to_the_Claude_app()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        h.Services.Accounts.NewFolderAccount = work.Id;
        var tab = await h.OpenTabAsync();

        Assert.False(tab.RemoteControl.CanToggle);
        Assert.Equal("Work uses an API key, and the Claude app needs a claude.ai sign-in", tab.RemoteControl.UnavailableReason);
        Assert.Equal(tab.RemoteControl.UnavailableReason, tab.RemoteControl.ToggleTip);
    }

    // ---- Spend ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_API_keys_turns_are_its_own_spend_which_the_header_shows_and_warns_about_once_a_day()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var tracker = new UsageTracker(h.Services, new UsageStore(Path.Combine(h.Root, "usage.db"), h.Time));
        h.Services.UseUsageTracker(tracker);
        tracker.SpendRecorded += id => _ = h.Services.Accounts.OnSpendRecordedAsync(id);
        using var spend = new AccountSpendViewModel(h.Services, tracker);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        work.DailySpendAlert = 0.015m;
        h.Services.Accounts.NewFolderAccount = work.Id;
        var tab = await h.OpenTabAsync();
        spend.Show(tab.Account);
        Assert.True(spend.IsShown);

        // Each scripted turn costs $0.01.
        h.Transport.EmitTurn();
        await TabTestHarness.Eventually(() => spend.TodayText == "$0.01", "the first turn's spend");
        Assert.Empty(Warnings());
        h.Transport.EmitTurn();
        await TabTestHarness.Eventually(() => spend.TodayText == "$0.02" && Warnings().Count == 1, "the second turn's spend and its warning");

        Assert.Equal("$0.02", spend.WeekText);
        Assert.Equal("$0.02", spend.MonthText);
        Assert.Equal("Work: $0.02 today", Warnings().Single().Title);
        Assert.All(tracker.Store.GetTurns(DateTimeOffset.MinValue, DateTimeOffset.MaxValue), t => Assert.Equal(work.Id, t.Account));

        // Once a day.
        h.Transport.EmitTurn();
        await TabTestHarness.Eventually(() => spend.TodayText == "$0.03", "the third turn's spend");
        await h.Services.Accounts.OnSpendRecordedAsync(work.Id);
        Assert.Single(Warnings());

        // A Default tab shows the plan's meters instead.
        spend.Show(null);
        Assert.False(spend.IsShown);

        List<Platform.Notifications.OsNotification> Warnings() => [.. h.Notifier.Shown.Where(n => n.Title.StartsWith("Work:", StringComparison.Ordinal))];
    }

    [Fact]
    public async Task The_usage_panel_lists_each_accounts_tokens_and_spend()
    {
        await using var h = new TabTestHarness();
        UseKeys(h);
        var tracker = new UsageTracker(h.Services, new UsageStore(Path.Combine(h.Root, "usage.db"), h.Time));
        h.Services.UseUsageTracker(tracker);
        using var header = new UsageViewModel(h.Services, tracker);
        var work = await h.Services.Accounts.AddApiKeyAccountAsync("Work", "sk-ant-work");
        var now = h.Time.GetUtcNow();
        tracker.Store.AddTurns(
        [
            new TurnRecord(now, "tab-1", "s1", "claude-opus-5-5", 1000, 100, 0, 0, 1.5, Account: work.Id),
            new TurnRecord(now, "tab-2", "s2", "claude-opus-5-5", 100, 10, 0, 0, 0.2),
            new TurnRecord(now, "tab-3", "s3", "claude-opus-5-5", 10, 1, 0, 0, 0.1, Account: "gone"),
        ]);
        var panel = new UsagePanelViewModel(h.Services, tracker, header, _ => null);

        await panel.RefreshAsync();

        Assert.Equal([("Work", "$1.50"), ("Default", "$0.20"), ("A removed account", "$0.10")], panel.Accounts.Select(a => (a.Name, a.Cost)));
    }

    // ---- Settings → Accounts ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Settings_adds_renames_recolors_and_removes_an_account_but_not_while_a_tab_runs_under_it()
    {
        await using var h = new TabTestHarness();
        var keys = UseKeys(h);
        using var page = new AccountsPage(new SettingsContext(h.Services));
        Assert.False(page.HasAccounts);

        page.NewName = "Work";
        await page.AddCommand.ExecuteAsync(null);
        Assert.Equal("Give the account a name, and paste its API key.", page.AddStatus);
        page.NewKey = "sk-ant-work";
        await page.AddCommand.ExecuteAsync(null);

        var editor = Assert.Single(page.Editors);
        var work = editor.Account;
        Assert.Equal("", page.NewKey);
        Assert.Equal(["Default", "Work"], page.NewFolderChoices.Select(c => c.Name));

        editor.Name = "Work (Acme)";
        editor.Name = "  ";
        editor.Color = TabGroupViewModel.Palette[3];
        Assert.Equal("Work (Acme)", work.Name);
        Assert.Equal(TabGroupViewModel.ToHex(TabGroupViewModel.Palette[3].Color), work.Color);

        editor.ChangeKeyCommand.Execute(null);
        editor.NewKey = "sk-ant-new";
        await editor.SaveKeyCommand.ExecuteAsync(null);
        Assert.Equal("sk-ant-new", keys.Secrets[ApiKeyHelper.CredentialKey(work.Id)]);
        Assert.False(editor.IsChangingKey);

        // Not while a tab runs under it.
        h.Services.Accounts.NewFolderAccount = work.Id;
        var tab = await h.OpenTabAsync();
        Assert.False(editor.CanRemove);
        Assert.StartsWith("1 open tab runs under Work (Acme)", editor.RemoveTip, StringComparison.Ordinal);
        await h.Shell.CloseTabCommand.ExecuteAsync(tab);
        await TabTestHarness.Eventually(() => !h.Shell.AllTabs.Any(), "the tab to close");
        Assert.True(editor.CanRemove);

        editor.Removal.Ask();
        await editor.Removal.ConfirmCommand.ExecuteAsync(null);

        Assert.Empty(page.Editors);
        Assert.Empty(h.Services.Accounts.All);
        Assert.Empty(keys.Secrets);
        Assert.Null(h.Services.State.NewFolderAccount);
        Assert.Equal(["Default"], page.NewFolderChoices.Select(c => c.Name));
    }

    // ---- The key helper -----------------------------------------------------------------------------------------------

    [Fact]
    public void The_key_helper_prints_the_accounts_key_and_nothing_else()
    {
        var keys = new FakeCredentialStore();
        keys.Secrets[ApiKeyHelper.CredentialKey("work")] = "sk-ant-work";
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(0, ApiKeyHelperLaunch.Run("work", keys, output, error));
        Assert.Equal("sk-ant-work", output.ToString());
        Assert.Equal("", error.ToString());

        using var missingOutput = new StringWriter();
        Assert.Equal(1, ApiKeyHelperLaunch.Run("gone", keys, missingOutput, error));
        Assert.Equal("", missingOutput.ToString());
        Assert.Contains("no API key for this account in Test Keychain", error.ToString(), StringComparison.Ordinal);
    }

    private static FakeCredentialStore UseKeys(TabTestHarness h)
    {
        var keys = new FakeCredentialStore();
        h.Services.Accounts.Credentials = keys;
        h.Services.Accounts.HelperExecutable = HelperPath(h);
        return keys;
    }

    private static string HelperPath(TabTestHarness h) => Path.Combine(h.Root, "Claudette.exe");

    /// <summary>A second tab in the same folder, on a pretend process of its own.</summary>
    private static async Task<TabViewModel> OpenAnotherTabAsync(TabTestHarness h)
    {
        await h.Shell.OpenFolderAsync(h.WorkFolder);
        var tab = h.Shell.SelectedTab!;
        await TabTestHarness.Eventually(() => tab.Status == TabStatus.Idle && tab.IsSettled, "the second tab to start");
        return tab;
    }
}
