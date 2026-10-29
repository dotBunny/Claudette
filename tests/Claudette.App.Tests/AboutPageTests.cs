using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.Settings;

namespace Claudette.App.Tests;

/// <summary>
/// Settings → About (DESIGN.md §14, "About"): which Claudette this is and its license, the trademarks it names, and the
/// licenses of what it ships, which the build writes from the packages (ThirdPartyNotices.targets).
/// </summary>
public class AboutPageTests
{
    [Fact]
    public void The_build_lists_every_package_Claudette_ships_with_its_license()
    {
        var libraries = ThirdPartyNotices.Load();
        var byName = libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        // The app's own packages and what they bring, Core's and Usage's included.
        Assert.All(["Avalonia", "Avalonia.AvaloniaEdit", "CommunityToolkit.Mvvm", "LiveMarkdown.Avalonia", "Markdig", "TextMateSharp", "SkiaSharp",
            "Microsoft.Data.Sqlite.Core", "SQLitePCLRaw.core"], name => Assert.True(byName.ContainsKey(name), $"{name} is listed"));
        // Only in Debug builds, and never shipped.
        Assert.False(byName.ContainsKey("AvaloniaUI.DiagnosticsSupport"));
        Assert.Equal(libraries.Count, byName.Count);
        Assert.Equal(libraries.Select(l => l.Name).Order(StringComparer.OrdinalIgnoreCase), libraries.Select(l => l.Name));

        // Every package says its version and license, and every library has its license's text, or a link for one that
        // asks only for that.
        Assert.All(libraries.Where(l => l.Note.Length == 0), l => Assert.NotEmpty(l.Version));
        Assert.All(libraries, l => Assert.NotEmpty(l.License));
        Assert.All(libraries, l => Assert.True(l.LicenseText is { Length: > 0 } || l.LicenseUrl is not null, $"{l.Name} has its license"));

        var avalonia = byName["Avalonia"];
        Assert.Equal("MIT", avalonia.License);
        Assert.Contains("Permission is hereby granted", avalonia.LicenseText, StringComparison.Ordinal);
        Assert.Contains("AvaloniaUI", avalonia.Copyright, StringComparison.Ordinal);
        Assert.StartsWith("https://", avalonia.Url, StringComparison.Ordinal);
        // A package's own license file names its holders; the notices it carries come with it, once for all who share them.
        Assert.Contains("Copyright", byName["SkiaSharp"].LicenseText, StringComparison.Ordinal);
        Assert.NotNull(byName["SkiaSharp.NativeAssets.Win32"].NoticesText);
        Assert.Equal(byName["SkiaSharp.NativeAssets.Win32"].NoticesText, byName["HarfBuzzSharp.NativeAssets.Win32"].NoticesText);
    }

    [Fact]
    public void What_isnt_a_package_is_listed_too_the_runtime_the_font_and_Godots_logo()
    {
        var byName = ThirdPartyNotices.Load().ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("MIT", byName[".NET"].License);
        var inter = byName["Inter"];
        Assert.Equal("OFL-1.1", inter.License);
        Assert.Contains("SIL Open Font License", inter.LicenseText, StringComparison.Ordinal);
        Assert.NotEmpty(inter.Note);
        // CC BY asks for a link to the license, the creator and what was changed.
        var logo = byName["Godot Engine logo"];
        Assert.Equal("CC-BY-4.0", logo.License);
        Assert.Null(logo.LicenseText);
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", logo.LicenseUrl);
        Assert.Contains("Andrea Calabró", logo.Copyright, StringComparison.Ordinal);
        Assert.Contains("redrawn", logo.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task About_says_which_Claudette_this_is_its_license_and_the_trademarks_it_names()
    {
        await using var h = new TabTestHarness();
        var about = new AboutPage(new SettingsContext(h.Services));

        Assert.Equal("About Claudette", about.Title);
        Assert.Equal(h.Services.Build.Label, about.FindSetting("about.version")!.Title);
        Assert.Equal("Copyright (c) 2026 dotBunny Inc. Claudette is free and open source.", about.FindSetting("about.license")!.Description);
        Assert.StartsWith("MIT License", about.LicenseText, StringComparison.Ordinal);
        Assert.Equal(AboutPage.TrademarkNotices.Select(t => t.Names), about.Trademarks.Select(t => t.Title));
        Assert.Contains("Godot Foundation", about.FindSetting("about.trademark.godot")!.Description, StringComparison.Ordinal);
        // Epic's notice is worded as Epic requires.
        Assert.StartsWith("Unreal®, Unreal Engine®, and the Unreal Engine® logo are trademarks", about.FindSetting("about.trademark.unreal")!.Description, StringComparison.Ordinal);
        // Perforce's logo, on its page, is shown with Perforce's permission.
        Assert.Contains("Perforce's permission", about.FindSetting("about.trademark.perforce")!.Description, StringComparison.Ordinal);
        Assert.Contains("isn't sponsored by, affiliated with or endorsed by", about.FindGroup("trademarks")!.Description, StringComparison.Ordinal);
        // Nothing on it to change, so nothing to reset.
        Assert.All(about.Groups, g => Assert.False(g.CanReset));
    }

    /// <summary>About is a window of its own (DESIGN.md §14, "About"): not a page of Settings, whose Help asks for the window.</summary>
    [Fact]
    public async Task About_isnt_a_page_of_Settings_and_Helps_About_Claudette_asks_for_its_window()
    {
        await using var h = new TabTestHarness();
        using var settings = new SettingsViewModel(h.Services, null);
        var asked = 0;
        settings.AboutRequested += () => asked++;

        Assert.DoesNotContain("About", settings.Categories);
        settings.SearchText = "trademark";
        Assert.Empty(settings.SearchResults);
        settings.ShowAboutCommand.Execute(null);

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task Its_links_open_in_the_browser()
    {
        await using var h = new TabTestHarness();
        var about = new AboutPage(new SettingsContext(h.Services));
        var avalonia = about.Libraries.Single(r => r.Library.Name == "Avalonia");
        var logo = about.Libraries.Single(r => r.Library.Name == "Godot Engine logo");

        await about.OpenWebsiteCommand.ExecuteAsync(null);
        await about.OpenSourceCommand.ExecuteAsync(null);
        await about.OpenChangelogCommand.ExecuteAsync(null);
        await about.OpenPrivacyPolicyCommand.ExecuteAsync(null);
        await avalonia.OpenWebsiteCommand.ExecuteAsync(null);
        await logo.OpenLicenseCommand.ExecuteAsync(null);

        Assert.Equal(
            ["https://claudette.dotbunny.com/", "https://github.com/dotBunny/Claudette", "https://github.com/dotBunny/Claudette/blob/main/CHANGELOG.md",
                "https://claudette.dotbunny.com/privacy-policy/", avalonia.Library.Url, "https://creativecommons.org/licenses/by/4.0/"],
            h.Platform.OpenedUrls);
        // A license to read is shown under its row; one that asks for a link is opened.
        Assert.True(avalonia.HasLicenseText);
        Assert.False(avalonia.HasLicenseLink);
        Assert.False(logo.HasLicenseText);
        Assert.True(logo.HasLicenseLink);
        Assert.Equal($"Avalonia {avalonia.Library.Version}", avalonia.Title);
        Assert.StartsWith("MIT · ", avalonia.Description, StringComparison.Ordinal);
        Assert.StartsWith(logo.Library.Note, logo.Description, StringComparison.Ordinal);
    }
}
