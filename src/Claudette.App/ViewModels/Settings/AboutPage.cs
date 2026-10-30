using Claudette.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels.Settings;

/// <summary>
/// About Claudette (DESIGN.md §14, "About"), shown in its own window (Views/AboutWindow): which Claudette this is, where it is on the web and its license; the
/// trademarks it names or shows, with each owner's notice and that none of them makes or endorses it; and the licenses
/// of what it's built with, which the build reads from the packages (<see cref="ThirdPartyNotices"/>).
/// </summary>
public sealed partial class AboutPage : SettingsPage
{
    public const string WebsiteUrl = "https://claudette.dotbunny.com/";

    public const string PrivacyPolicyUrl = "https://claudette.dotbunny.com/privacy-policy/";

    public static readonly string SourceUrl = $"https://github.com/{ReleaseFeed.DefaultRepository}";

    public static readonly string ChangelogUrl = $"{SourceUrl}/blob/main/CHANGELOG.md";

    /// <summary>
    /// The trademarks Claudette names or shows, each with its owner's notice: a row each under Trademarks. A logo shown
    /// with its owner's permission says so here, as Godot's and Perforce's do (<see cref="Logos"/>; DESIGN.md §14,
    /// "About").
    /// </summary>
    public static IReadOnlyList<(string Key, string Names, string Notice)> TrademarkNotices { get; } =
    [
        ("about.trademark.anthropic", "Claude and Claude Code", "Trademarks of Anthropic, PBC."),
        ("about.trademark.godot", "Godot and the Godot logo",
            "Trademarks of the Godot Foundation, used under its trademark policy. The logo is by Andrea Calabró, under CC BY 4.0."),
        ("about.trademark.unity", "Unity", "A trademark or registered trademark of Unity Technologies or its affiliates in the U.S. and elsewhere."),
        // Epic's own notice, which its guidelines require as it's worded.
        ("about.trademark.unreal", "Unreal, Unreal Engine and the Unreal Engine logo",
            "Unreal®, Unreal Engine®, and the Unreal Engine® logo are trademarks or registered trademarks of Epic Games, Inc. in the United States of America and elsewhere. Other brands or product names are the trademarks of their respective owners. The logo is used with Epic's permission."),
        ("about.trademark.perforce", "Perforce, P4V and the Perforce logo",
            "Trademarks of Perforce Software, Inc. or one of its affiliates. The logo is used with Perforce's permission, as its brand guidelines say."),
    ];

    /// <summary>
    /// The people outside dotBunny whose work is in Claudette, in the order they first contributed, each with what they
    /// brought: a row each under Contributors (DESIGN.md §14, "About"). Merging a pull request from someone new adds
    /// them here, in the same change or the one after.
    /// </summary>
    public static IReadOnlyList<Contributor> ContributorList { get; } =
    [
        new("Steklorez", "Steklorez",
            "Claude Code's projects in the sidebar, with their past sessions; your messages set apart from Claude's; collapsing tool calls."),
    ];

    /// <summary><b>Try the demo</b>, at the window's foot (DESIGN.md §11, "Demo mode").</summary>
    public Demo.DemoStarter Demo => field ??= new Demo.DemoStarter(Services);

    public AboutPage(SettingsContext context) : base(context, "About Claudette")
    {
        Section("claudette", "Claudette",
        [
            Info("about.version", Services.Build.Label, "A desktop app for Claude Code, made by dotBunny.", keywords: ["version", "dotBunny"]),
            Info("about.links", "On the web", "The website and user guide, the source code, what changed in each version, and the privacy policy.",
                keywords: ["website", "github", "source code", "changelog", "privacy policy"]),
            Info("about.license", "MIT License", $"{ThirdPartyNotices.ClaudetteCopyright.TrimEnd('.')}. Claudette is free and open source.",
                keywords: ["license", "open source", "copyright"]),
        ]);
        Contributors = [.. ContributorList.Select(c => new ContributorRow(c, Services.Platform.OpenUrlAsync))];
        Section("contributors", "Contributors",
            [.. ContributorList.Select(c => Info(c.Key, c.Name, c.Contributed, keywords: ["contributor", "thanks", "pull request", c.GitHub]))],
            "The people outside dotBunny whose work is in Claudette, and what each brought. Thank you.");
        Trademarks = [.. TrademarkNotices.Select(t => Info(t.Key, t.Names, t.Notice, keywords: ["trademark", "logo"]))];
        Section("trademarks", "Trademarks", Trademarks,
            "Claudette is made by dotBunny. It isn't sponsored by, affiliated with or endorsed by Anthropic or the makers of the engines and tools it "
            + "works with, and names them only to say what it works with. Other names are trademarks of their owners.");
        Section("licenses", "Open-source licenses", [],
            "What Claudette is built with that others made, with their licenses and the third-party notices they carry.");
    }

    public override string IconKey => "IconCardNote";

    /// <summary>Its window has the Claude style's looks when that's the style.</summary>
    public bool IsClaudeStyle => Services.Settings.Appearance.Style == Claudette.Core.Settings.AppStyle.Claude;

    public override string Description => "Which Claudette this is, whose names it uses, and what it's built with.";

    /// <summary>The contributors' rows, in <see cref="ContributorList"/>'s order.</summary>
    public IReadOnlyList<ContributorRow> Contributors { get; }

    /// <summary>The trademark rows, in <see cref="TrademarkNotices"/>' order.</summary>
    public IReadOnlyList<SettingItem> Trademarks { get; }

    /// <summary>A row for each library, read when the page first shows them.</summary>
    public IReadOnlyList<ThirdPartyLibraryRow> Libraries => field ??= [.. ThirdPartyNotices.Load().Select(l => new ThirdPartyLibraryRow(l, Services.Platform.OpenUrlAsync))];

    /// <summary>Claudette's own license, shown under its row.</summary>
    [ObservableProperty]
    public partial bool IsLicenseOpen { get; set; }

    public string LicenseText => field ??= ThirdPartyNotices.ClaudetteLicense();

    [RelayCommand]
    private Task OpenWebsiteAsync() => Services.Platform.OpenUrlAsync(WebsiteUrl);

    [RelayCommand]
    private Task OpenSourceAsync() => Services.Platform.OpenUrlAsync(SourceUrl);

    [RelayCommand]
    private Task OpenChangelogAsync() => Services.Platform.OpenUrlAsync(ChangelogUrl);

    [RelayCommand]
    private Task OpenPrivacyPolicyAsync() => Services.Platform.OpenUrlAsync(PrivacyPolicyUrl);
}

/// <summary>Someone outside dotBunny whose work is in Claudette: their name, their GitHub account and what they brought.</summary>
public sealed record Contributor(string Name, string GitHub, string Contributed)
{
    /// <summary>Their row's key in About.</summary>
    public string Key => $"about.contributor.{GitHub.ToLowerInvariant()}";

    public string ProfileUrl => $"https://github.com/{GitHub}";

    /// <summary>Their pull requests to Claudette, on GitHub.</summary>
    public string PullRequestsUrl => $"{AboutPage.SourceUrl}/pulls?q=is%3Apr+author%3A{GitHub}";
}

/// <summary>One of About's contributors: their row, with their GitHub profile and pull requests a click away.</summary>
public sealed partial class ContributorRow(Contributor contributor, Func<string, Task> openUrl)
{
    public Contributor Contributor => contributor;

    /// <summary>The row's setting, which gives it its name and what they brought.</summary>
    public string Key => contributor.Key;

    [RelayCommand]
    private Task OpenProfileAsync() => openUrl(contributor.ProfileUrl);

    [RelayCommand]
    private Task OpenPullRequestsAsync() => openUrl(contributor.PullRequestsUrl);
}

/// <summary>One of About's open-source licenses: a library, with its license and notices shown under it on request.</summary>
public sealed partial class ThirdPartyLibraryRow(ThirdPartyLibrary library, Func<string, Task> openUrl) : ObservableObject
{
    public ThirdPartyLibrary Library => library;

    /// <summary>"Avalonia 12.1.4", or "Inter" for what isn't a package.</summary>
    public string Title => library.Version.Length > 0 ? $"{library.Name} {library.Version}" : library.Name;

    /// <summary>What it is to Claudette, if that needs saying, then its license and whose it is: "MIT · Copyright 2013-2026 © The AvaloniaUI Project".</summary>
    public string Description => string.Join(Environment.NewLine,
        new[] { library.Note, string.Join(" · ", new[] { library.License, library.Copyright }.Where(s => s.Length > 0)) }.Where(s => s.Length > 0));

    public bool HasWebsite => library.Url.Length > 0;

    /// <summary>Its license is a text to show; otherwise it's a link (<see cref="HasLicenseLink"/>).</summary>
    public bool HasLicenseText => library.LicenseText is not null;

    public bool HasLicenseLink => library.LicenseText is null && library.LicenseUrl is not null;

    public bool HasNotices => library.NoticesText is not null;

    [ObservableProperty]
    public partial bool IsLicenseOpen { get; set; }

    [ObservableProperty]
    public partial bool IsNoticesOpen { get; set; }

    [RelayCommand]
    private Task OpenWebsiteAsync() => openUrl(library.Url);

    [RelayCommand]
    private Task OpenLicenseAsync() => openUrl(library.LicenseUrl!);
}
