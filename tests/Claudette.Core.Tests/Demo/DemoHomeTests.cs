using Claudette.Core.Demo;
using Claudette.Core.Settings;

namespace Claudette.Core.Tests.Demo;

/// <summary>
/// The demo's folder (DESIGN.md §11, "Demo mode"): Claudette's settings and tabs, the transcripts, and in the app's demo
/// the projects' folders, empty.
/// </summary>
public sealed class DemoHomeTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("claudette-demo-home").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task The_apps_demo_has_its_tabs_and_transcripts_and_empty_project_folders()
    {
        var now = new DateTimeOffset(2026, 10, 30, 15, 0, 0, TimeSpan.FromHours(-4));
        var projects = Path.Combine(_root, "projects");

        var written = await DemoHome.WriteAsync(_root, projects, now, new DemoOptions { ClaudePath = "claudette-itself", Style = AppStyle.Claude }, DemoContent.EmptyFolder);

        var settings = new JsonFileStore<AppSettings>(written.Paths.SettingsFile).Load();
        Assert.Equal(("claudette-itself", AppStyle.Claude), (settings.ClaudeCode.ClaudePath, settings.Appearance.Style));
        // Nothing that would reach the network.
        Assert.False(settings.General.CheckForAppUpdates || settings.General.ShowServiceStatus || settings.ClaudeCode.CheckForUpdates);
        var state = new JsonFileStore<AppState>(written.Paths.StateFile).Load();
        Assert.Equal(7, state.Tabs.Count);
        Assert.Equal("tab-grapple", state.SelectedTabId);
        Assert.All(state.Tabs, t => Assert.True(File.Exists(t.TranscriptPath), t.TranscriptPath));
        // Earlier the same day.
        Assert.All(state.Tabs, t => Assert.Contains("\"timestamp\":\"2026-10-30T", File.ReadAllText(t.TranscriptPath!), StringComparison.Ordinal));
        // The projects' folders, with nothing in them.
        foreach (var project in new[] { "starfall", "tidepool", "orbit-game" })
        {
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(projects, project)));
        }
        Assert.Equal(Path.Combine(_root, "claude-config"), written.ClaudeConfig);
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(written.ClaudeConfig, "projects", "demo"), "*.jsonl"));
    }
}
