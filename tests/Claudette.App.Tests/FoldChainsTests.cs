using Claudette.App.Conversation;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.Settings;

namespace Claudette.App.Tests;

/// <summary>
/// Folding each chain of steps (DESIGN.md §18, "Conversation cards"): Settings → Conversation says what tabs do, and each
/// tab can fold its steps or show them all for itself, from its menu's <b>Steps</b>, the message box's or Tab settings….
/// </summary>
public class FoldChainsTests
{
    [Fact]
    public async Task A_tab_folds_its_steps_its_own_way_whatever_Settings_say()
    {
        await using var h = new TabTestHarness();
        var tab = await ChainThenReplyAsync(h);
        Assert.False(tab.FoldsChains);
        Assert.Empty(InlineDispatcher.Read(() => tab.Items.OfType<StepsMarkerItem>().ToList()));

        h.OnUiThread(() => tab.FoldStepsCommand.Execute(null));

        Assert.True(tab.State.Overrides.FoldChains);
        Assert.True(tab.FoldsChains);
        Assert.True(tab.HasOverrides);
        Assert.False(h.Services.Settings.Appearance.FoldChains);
        Assert.True(InlineDispatcher.Read(() => tab.Items.OfType<ToolGroupItem>().Single().IsFolded));
        Assert.NotNull(InlineDispatcher.Read(() => tab.Items.OfType<AssistantTextItem>().Single().Steps));
        // Settings → Conversation says one tab has its own.
        Assert.Single(h.Shell.TabsWithOwn(TabOverrideKeys.FoldChains));
        // A copy of the tab keeps it.
        Assert.True(tab.CopyState(CopyPoint.Whole).Overrides.FoldChains);

        // Settings turning folding on and off again leaves the tab's choice.
        h.Services.Settings.Appearance.FoldChains = true;
        h.OnUiThread(tab.OnSettingsChanged);
        Assert.Equal("Default (folded)", tab.StepsDefaultText);
        h.Services.Settings.Appearance.FoldChains = false;
        h.OnUiThread(tab.OnSettingsChanged);
        Assert.True(tab.FoldsChains);
        Assert.Equal("Default (all)", tab.StepsDefaultText);

        h.OnUiThread(() => tab.ShowAllStepsCommand.Execute(null));

        Assert.False(tab.State.Overrides.FoldChains);
        Assert.True(tab.StepsAll);
        Assert.Empty(InlineDispatcher.Read(() => tab.Items.OfType<StepsMarkerItem>().ToList()));
        Assert.False(InlineDispatcher.Read(() => tab.Items.OfType<ToolGroupItem>().Single().IsFolded));

        h.OnUiThread(() => tab.UseDefaultStepsCommand.Execute(null));

        Assert.Null(tab.State.Overrides.FoldChains);
        Assert.True(tab.StepsByDefault);
        Assert.False(tab.HasOverrides);
        Assert.Empty(h.Shell.TabsWithOwn(TabOverrideKeys.FoldChains));
    }

    [Fact]
    public async Task The_message_boxs_Steps_shows_only_while_Settings_fold_and_switches_the_tab_alone()
    {
        await using var h = new TabTestHarness();
        var tab = await ChainThenReplyAsync(h);
        Assert.False(tab.ShowsStepsButton);

        h.Services.Settings.Appearance.FoldChains = true;
        h.OnUiThread(tab.OnSettingsChanged);

        Assert.True(tab.ShowsStepsButton);
        Assert.Equal("Steps: folded", tab.StepsButtonText);
        Assert.True(InlineDispatcher.Read(() => tab.Items.OfType<ToolGroupItem>().Single().IsFolded));

        h.OnUiThread(() => tab.ToggleFoldChainsCommand.Execute(null));

        Assert.Equal("Steps: all", tab.StepsButtonText);
        Assert.False(tab.State.Overrides.FoldChains);
        Assert.True(h.Services.Settings.Appearance.FoldChains);
        Assert.True(tab.ShowsStepsButton);
        Assert.False(InlineDispatcher.Read(() => tab.Items.OfType<ToolGroupItem>().Single().IsFolded));

        // Back to what Settings says: the tab has nothing of its own to keep.
        h.OnUiThread(() => tab.ToggleFoldChainsCommand.Execute(null));

        Assert.Null(tab.State.Overrides.FoldChains);
        Assert.True(tab.FoldsChains);

        h.Services.Settings.Appearance.FoldChains = false;
        h.OnUiThread(tab.OnSettingsChanged);

        Assert.False(tab.ShowsStepsButton);
    }

    [Fact]
    public async Task Show_every_step_in_a_chains_menu_is_for_its_tab()
    {
        await using var h = new TabTestHarness(s => s.Appearance.FoldChains = true);
        var tab = await ChainThenReplyAsync(h);
        Assert.True(InlineDispatcher.Read(() => tab.Items.OfType<ToolGroupItem>().Single().IsFolded));

        h.OnUiThread(() => tab.ShowEveryStepCommand.Execute(null));

        Assert.False(tab.State.Overrides.FoldChains);
        Assert.True(h.Services.Settings.Appearance.FoldChains);
        Assert.False(InlineDispatcher.Read(() => tab.Items.OfType<ToolGroupItem>().Single().IsFolded));
    }

    [Fact]
    public async Task Tab_settings_chooses_the_tabs_steps()
    {
        await using var h = new TabTestHarness();
        var tab = await ChainThenReplyAsync(h);
        var settings = new TabSettingsViewModel(h.Services, tab, () => { });
        Assert.Equal(["Default (all)", "Folded", "All"], settings.StepsChoices.Select(c => c.Label));
        Assert.Same(settings.StepsChoices[0], settings.SelectedSteps);

        h.OnUiThread(() => settings.SelectedSteps = settings.StepsChoices[1]);
        await settings.Applying;

        Assert.True(tab.State.Overrides.FoldChains);
        Assert.True(InlineDispatcher.Read(() => tab.Items.OfType<ToolGroupItem>().Single().IsFolded));
        Assert.True(settings.FindSetting("tab.foldChains")!.IsChanged);

        h.OnUiThread(() => settings.SelectedSteps = settings.StepsChoices[0]);
        await settings.Applying;

        Assert.Null(tab.State.Overrides.FoldChains);
        Assert.False(InlineDispatcher.Read(() => tab.Items.OfType<ToolGroupItem>().Single().IsFolded));
    }

    /// <summary>A turn of one call, then Claude's reply: a chain of steps the reply can carry.</summary>
    private static async Task<TabViewModel> ChainThenReplyAsync(TabTestHarness h)
    {
        var tab = await h.OpenTabAsync();
        tab.ComposerText = "go";
        await tab.SendCommand.ExecuteAsync(null);
        h.Transport.Emit("""{"type":"assistant","message":{"content":[{"type":"tool_use","id":"r1","name":"Read","input":{"file_path":"a.cs"}}]}}""");
        h.Transport.Emit("""{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"r1","content":"text"}]}}""");
        h.Transport.Emit("""{"type":"assistant","message":{"content":[{"type":"text","text":"Done"}]}}""");
        h.Transport.Emit("""{"type":"result","subtype":"success","is_error":false,"session_id":"s1"}""");
        await TabTestHarness.Eventually(
            () => tab.IsSettled && InlineDispatcher.Read(() => tab.Items.OfType<AssistantTextItem>().Any(r => r.Text == "Done")), "the reply");
        return tab;
    }
}
