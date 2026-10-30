using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.Settings;
using Claudette.Core.Settings;

namespace Claudette.App.Tests;

/// <summary>
/// The Settings window's sections and settings (DESIGN.md §14): each section's own Reset to defaults, a setting changed
/// from its default and its Reset, what's this computer's alone, tabs that have their own, search, and the window
/// following the main one: its selected tab, where it was left, and links from features to their settings.
/// </summary>
public class SettingsSectionsTests
{
    [Fact]
    public async Task Each_section_resets_only_its_own_settings()
    {
        await using var h = new TabTestHarness();
        h.Services.Settings.Appearance.Theme = ThemeChoice.Dark;
        h.Services.Settings.Appearance.ConversationFont = "Fira Sans";
        using var settings = new SettingsViewModel(h.Services, null);
        var look = settings.Appearance.FindGroup("look")!;
        var text = settings.Appearance.FindGroup("text")!;
        Assert.True(look.HasChanges);
        Assert.True(text.HasChanges);
        Assert.False(settings.Appearance.FindGroup("accessibility")!.HasChanges);

        look.ResetCommand.Execute(null);

        Assert.Equal(ThemeChoice.System, h.Services.Settings.Appearance.Theme);
        Assert.Equal(ThemeChoice.System, settings.Appearance.Theme);
        Assert.Equal("Fira Sans", h.Services.Settings.Appearance.ConversationFont);
        Assert.False(look.HasChanges);
        Assert.True(text.HasChanges);
        Assert.False(look.ResetCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_setting_changed_from_its_default_is_marked_and_its_own_reset_puts_it_back()
    {
        await using var h = new TabTestHarness();
        using var settings = new SettingsViewModel(h.Services, null);
        var density = settings.Appearance.FindSetting("appearance.density")!;
        Assert.False(density.IsChanged);

        settings.Appearance.Density = Density.Compact;
        Assert.True(density.IsChanged);
        Assert.True(settings.Appearance.FindGroup("look")!.HasChanges);

        density.ResetCommand.Execute(null);
        Assert.Equal(Density.Comfortable, h.Services.Settings.Appearance.Density);
        Assert.Equal(Density.Comfortable, settings.Appearance.Density);
        Assert.False(density.IsChanged);
    }

    /// <summary>
    /// A setting settings sync leaves as it is says "This computer" (DESIGN.md §14): once by a page's title when all of it
    /// is, once by a section's heading when all of that is, else by the setting itself, as the zoom in Appearance.
    /// </summary>
    [Fact]
    public async Task What_settings_sync_leaves_is_marked_this_computer_once()
    {
        await using var h = new TabTestHarness();
        using var settings = new SettingsViewModel(h.Services, null);

        Assert.True(settings.General.IsMachineOnly);
        Assert.All(settings.General.Groups, g => Assert.False(g.ShowsMachineMark));
        Assert.False(settings.Appearance.IsMachineOnly);
        Assert.True(settings.Appearance.FindSetting("appearance.zoom")!.ShowsMachineMark);
        Assert.False(settings.Appearance.FindSetting("appearance.theme")!.ShowsMachineMark);
        Assert.False(settings.Appearance.FindGroup("accessibility")!.ShowsMachineMark);
        // Subagents is Claude Code's, which stays here, among what new tabs start with, which syncs.
        Assert.True(settings.Tabs.FindSetting("claudeCode.subagents")!.ShowsMachineMark);
        Assert.False(settings.Tabs.FindSetting("newTabs.defaultModel")!.ShowsMachineMark);
        // The marks follow what settings sync copies.
        foreach (var item in settings.Pages.SelectMany(p => p.Items).Where(i => i.IsSetting && !i.Key.StartsWith("state.", StringComparison.Ordinal)))
        {
            Assert.Equal(!SyncedSettings.Syncs(item.Key), item.IsMachineOnly);
        }
    }

    [Fact]
    public void Sessions_says_what_settings_sync_copies_from_what_it_does()
    {
        Assert.True(SessionsPage.NamesEverySyncedSection);
        Assert.Contains("keyboard shortcuts", SessionsPage.SyncedText, StringComparison.Ordinal);
        Assert.Contains("quick suffixes", SessionsPage.SyncedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_setting_tabs_override_says_which_tabs_have_their_own()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        tab.State.Overrides = new TabOverrides { Model = "opus" };
        using var settings = new SettingsViewModel(h.Services, null, host: h.Shell);

        var model = settings.Tabs.FindSetting("newTabs.defaultModel")!;
        Assert.Equal("1 tab has its own", model.TabsNote);
        Assert.Contains(tab.DisplayName, model.TabsTip, StringComparison.Ordinal);
        Assert.Null(settings.Tabs.FindSetting("newTabs.defaultEffort")!.TabsNote);
    }

    [Fact]
    public async Task Search_finds_a_setting_by_its_name_what_it_does_or_other_words_for_it()
    {
        await using var h = new TabTestHarness();
        using var settings = new SettingsViewModel(h.Services, null);

        settings.SearchText = "dark mode";
        Assert.Equal("Theme", settings.SearchResults.First().Label);
        Assert.Equal("Appearance → Look", settings.SearchResults.First().Where);

        settings.SearchText = "caffeinate";
        Assert.Contains(settings.SearchResults, r => r.Key == "general.keepAwake");

        // By what it does: the line under its title.
        settings.SearchText = "stuck tab";
        Assert.Contains(settings.SearchResults, r => r.Key == "checkIns.enabled");

        // Named by the words first.
        settings.SearchText = "zoom";
        Assert.Equal("Zoom", settings.SearchResults.First().Label);
    }

    [Fact]
    public async Task Picking_a_result_shows_its_page_and_picks_its_setting_out_for_a_moment()
    {
        await using var h = new TabTestHarness();
        using var settings = new SettingsViewModel(h.Services, null);
        settings.SearchText = "burn rate";

        settings.SelectedSearchResult = settings.SearchResults.Single(r => r.Key == "usage.burnRateWindowMinutes");

        Assert.Same(settings.Usage, settings.CurrentPage);
        Assert.Equal("usage.burnRateWindowMinutes", settings.ScrollToKey);
        var setting = settings.Usage.FindSetting("usage.burnRateWindowMinutes")!;
        Assert.True(setting.IsHighlighted);
        h.Time.Advance(SettingsViewModel.HighlightFor);
        Assert.False(setting.IsHighlighted);
    }

    /// <summary>The window isn't modal (DESIGN.md §14): its project pages follow the tab selected in the main window.</summary>
    [Fact]
    public async Task The_project_pages_follow_the_selected_tab_while_settings_is_open()
    {
        await using var h = new TabTestHarness();
        var first = await h.OpenTabAsync();
        var other = Directory.CreateDirectory(Path.Combine(h.Root, "other")).FullName;
        await h.Shell.OpenFolderAsync(other);
        var second = h.Shell.SelectedTab!;
        h.Shell.SelectedTab = first;
        using var settings = new SettingsViewModel(h.Services, null, host: h.Shell) { SelectedCategory = SettingsViewModel.LinksPage };
        Assert.Same(first, settings.Project!.Tab);
        Assert.Equal("work", settings.Project.Heading);

        h.Shell.SelectedTab = second;

        Assert.Same(second, settings.Project!.Tab);
        Assert.Equal("other", settings.Project.Heading);
        Assert.Equal(SettingsViewModel.LinksPage, settings.SelectedProjectNavEntry?.Title);
        var links = Assert.IsType<ProjectLinksPage>(settings.CurrentPage);
        Assert.Same(settings.Project, links.Project);
    }

    [Fact]
    public async Task Settings_opens_where_it_was_last_left()
    {
        await using var h = new TabTestHarness();
        using (var settings = new SettingsViewModel(h.Services, null, host: h.Shell))
        {
            Assert.Equal(SettingsCategory.General, settings.SelectedCategory);
            settings.SelectedCategory = SettingsCategory.Usage;
        }

        using var again = new SettingsViewModel(h.Services, null, host: h.Shell);
        Assert.Same(again.Usage, again.CurrentPage);
    }

    [Fact]
    public async Task Asked_for_while_open_it_goes_to_the_page_and_setting_asked_for()
    {
        await using var h = new TabTestHarness();
        using var settings = new SettingsViewModel(h.Services, null);

        settings.Open(new SettingsOpening(SettingsCategory.Keyboard, Key: "quickSuffixes"));

        Assert.Same(settings.Keyboard, settings.CurrentPage);
        Assert.True(settings.Keyboard.FindSetting("quickSuffixes")!.IsHighlighted);
    }

    /// <summary>A feature's own "…settings" entry, such as the cup's <b>Keep-awake settings…</b>, opens Settings at its setting.</summary>
    [Fact]
    public async Task A_features_settings_entry_opens_settings_at_its_setting()
    {
        await using var h = new TabTestHarness();
        SettingsOpening? opening = null;
        h.Shell.ShowSettingsWindow = o =>
        {
            opening = o;
            return Task.CompletedTask;
        };

        await h.Shell.OpenSettingCommand.ExecuteAsync("General#keepAwake");

        Assert.Equal(SettingsCategory.General, opening!.Category);
        Assert.Equal("keepAwake", opening.Key);
        using var settings = new SettingsViewModel(h.Services, null, opening: opening);
        Assert.True(settings.General.FindGroup("keepAwake")!.IsHighlighted);
    }

    /// <summary>Putting the built-in quick suffixes back deletes the user's own, so the section asks first, and so does the row.</summary>
    [Fact]
    public async Task Resetting_the_quick_suffixes_asks_first()
    {
        await using var h = new TabTestHarness();
        using var settings = new SettingsViewModel(h.Services, null);
        settings.Keyboard.AddSuffixCommand.Execute(null);
        var mine = h.Services.Settings.QuickSuffixes.Count;
        var section = settings.Keyboard.FindGroup("quickSuffixes")!;

        section.ResetCommand.Execute(null);
        Assert.True(section.Confirmation.IsOpen);
        Assert.Equal(mine, h.Services.Settings.QuickSuffixes.Count);
        section.Confirmation.CancelCommand.Execute(null);

        settings.Keyboard.FindSetting("quickSuffixes")!.ResetCommand.Execute(null);
        Assert.True(section.Confirmation.IsOpen);
        await section.Confirmation.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(QuickSuffix.Defaults().Count, h.Services.Settings.QuickSuffixes.Count);
        Assert.Equal(QuickSuffix.Defaults().Count, settings.Keyboard.Suffixes.Count);
    }
}
