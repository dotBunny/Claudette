using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Claudette.App.Controls;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.Views;
using Claudette.Core.Settings;

namespace Claudette.App.UiTests;

/// <summary>
/// A setting's <b>Reset</b> and its section's <b>Reset to defaults</b> (DESIGN.md §14) keep their places whether they show
/// or not, so pointing at a row, moving the focus into it, or changing it moves nothing: not the row's height, not where
/// its title and line wrap.
/// </summary>
public class SettingResetUiTests
{
    /// <summary>
    /// Reset shows while a changed row is pointed at or has the focus, in room the row keeps for it. A nested setting
    /// shows its own Reset, not the one above's, and Tab reaches Reset once the focus is in its row.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(AppStyle.Standard)]
    [InlineData(AppStyle.Claude)]
    public async Task Pointing_at_or_focusing_a_changed_setting_moves_nothing(AppStyle style)
    {
        await using var h = new TabTestHarness(s =>
        {
            s.Appearance.Style = style;
            s.General.KeepAwake = true;
            s.General.KeepAwakeWhen = KeepAwakeWhen.ClaudetteRuns;
        }, dispatcher: new AvaloniaUiDispatcher());
        using var settings = new SettingsViewModel(h.Services, null, host: h.Shell) { SelectedCategory = SettingsCategory.General };
        var window = new SettingsWindow { DataContext = settings, Width = 900, Height = 700 };
        window.Show();
        UiText.Settle(window);
        Assert.Equal(style == AppStyle.Claude, window.GetVisualDescendants().OfType<AppearanceScope>().First().Classes.Contains("claude"));
        var parent = Row(window, "general.keepAwake");
        var nested = Row(window, "general.keepAwakeWhen");
        Assert.True(parent.IsChanged && nested.IsChanged);
        var layout = Layout(parent, nested);
        Assert.False(Shows(parent));
        Assert.False(Shows(nested));

        PointAt(window, parent);
        Assert.True(Shows(parent));
        Assert.False(Shows(nested));
        Assert.Equal(layout, Layout(parent, nested));

        // The pointer is over the row above too, but it's the nested row's Reset that shows.
        PointAt(window, nested);
        Assert.False(Shows(parent));
        Assert.True(Shows(nested));
        Assert.Equal(layout, Layout(parent, nested));

        window.MouseMove(new Point(2, 2));
        UiText.Settle(window);
        Assert.False(Shows(parent));
        Assert.False(Shows(nested));

        parent.GetVisualDescendants().OfType<ToggleSwitch>().First().Focus(NavigationMethod.Tab);
        UiText.Settle(window);
        Assert.True(Shows(parent));
        Assert.Equal(layout, Layout(parent, nested));

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        UiText.Settle(window);
        Assert.Same(Reset(parent), window.FocusManager?.GetFocusedElement());
        Assert.True(Shows(parent));
        Assert.Equal(layout, Layout(parent, nested));
        window.Close();
    }

    /// <summary>
    /// Changing a setting doesn't move its row either: Reset's room was there while it was at its default. Its section's
    /// Reset to defaults is there, disabled, until something in the section changes.
    /// </summary>
    [AvaloniaFact]
    public async Task Changing_a_setting_moves_neither_its_row_nor_its_sections_reset()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        using var settings = new SettingsViewModel(h.Services, null, host: h.Shell) { SelectedCategory = SettingsCategory.General };
        var window = new SettingsWindow { DataContext = settings, Width = 900, Height = 700 };
        window.Show();
        UiText.Settle(window);
        var row = Row(window, "general.confirmCloseWorkingTab");
        var section = window.GetVisualDescendants().OfType<SettingsSection>().Single(s => s.Key == "starting");
        var sectionReset = section.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Reset" && b.TemplatedParent == section);
        var layout = Layout(row);
        var place = sectionReset.Bounds;
        Assert.False(row.IsChanged);
        Assert.True(sectionReset.IsEffectivelyVisible);
        Assert.False(sectionReset.IsEffectivelyEnabled);
        Assert.True(Reset(row).IsVisible);
        Assert.False(Shows(row));

        settings.General.ConfirmCloseWorkingTab = false;
        UiText.Settle(window);

        Assert.True(row.IsChanged);
        Assert.Equal(layout, Layout(row));
        Assert.True(sectionReset.IsEffectivelyEnabled);
        Assert.Equal(place, sectionReset.Bounds);
        window.Close();
    }

    /// <summary>
    /// Tab settings… in the main window, which the Claude style and Compact density both reach: pointing at a tab's own
    /// setting doesn't move it.
    /// </summary>
    [AvaloniaFact]
    public async Task Pointing_at_a_tabs_own_setting_in_a_compact_Claude_style_window_moves_nothing()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var model = new TabSettingsViewModel(h.Services, tab, () => { });
        model.SelectedMode = model.ModeChoices[1];
        var view = new TabSettingsView { DataContext = model };
        var window = UiText.Show(new AppearanceScope { Classes = { "compact", "claude" }, Children = { view } });
        var row = Row(window, "tab.permissionMode");
        Assert.True(row.IsChanged);
        var layout = Layout(row);
        Assert.False(Shows(row));

        PointAt(window, row);

        Assert.True(Shows(row));
        Assert.Equal(layout, Layout(row));
        window.Close();
    }

    private static SettingRow Row(Window window, string key) => window.GetVisualDescendants().OfType<SettingRow>().Single(r => r.Key == key);

    private static Button Reset(SettingRow row) => Part<Button>(row, "PART_Reset");

    private static T Part<T>(SettingRow row, string name) where T : Control =>
        row.GetVisualDescendants().OfType<T>().Single(c => c.Name == name && c.TemplatedParent == row);

    /// <summary>Drawn, and there to click and to reach with Tab.</summary>
    private static bool Shows(SettingRow row)
    {
        var reset = Reset(row);
        return reset.IsEffectivelyVisible && reset.Opacity == 1 && reset.IsHitTestVisible && KeyboardNavigation.GetIsTabStop(reset);
    }

    /// <summary>Where each row's parts are: its size, its title's line (with its marks), its title and its line.</summary>
    private static string Layout(params SettingRow[] rows) => string.Join("\n", rows.Select(row =>
        $"{row.Key}: {row.Bounds.Size} title line {Part<WrapPanel>(row, "PART_TitleLine").Bounds} title {Part<TextBlock>(row, "PART_Title").Bounds} line {Part<TextBlock>(row, "PART_Description").Bounds}"));

    /// <summary>Moves the pointer onto the row's title, rendering first so the pointer finds what's drawn there.</summary>
    private static void PointAt(Window window, SettingRow row)
    {
        var title = Part<TextBlock>(row, "PART_Title");
        var at = title.TranslatePoint(new Point(4, title.Bounds.Height / 2), window)!.Value;
        Assert.Same(row, (UiText.HitTest(window, at) as Visual)?.FindAncestorOfType<SettingRow>(includeSelf: true));
        window.MouseMove(at);
        UiText.Settle(window);
    }
}
