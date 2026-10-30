using Claudette.App.Demo;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.Core;
using Claudette.Core.Demo;
using Claudette.Core.Settings;
using Claudette.Platform;

namespace Claudette.App.Tests;

/// <summary>
/// Demo mode (DESIGN.md §11, "Demo mode"): Try the demo writes one into a folder of its own and starts a second Claudette
/// on it; the demo's claude is Claudette standing in for Claude Code; Leave demo closes it.
/// </summary>
public class DemoModeTests
{
    [Fact]
    public async Task Try_the_demo_writes_one_and_starts_a_Claudette_on_its_own_folders()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(launcher: launcher);
        h.Services.DemoFolderRoot = Path.Combine(h.Root, "temp");
        var starter = new DemoStarter(h.Services);
        Assert.True(starter.IsOffered);

        await starter.TryCommand.ExecuteAsync(null);

        var spec = Assert.Single(launcher.Started);
        Assert.True(spec.Detached);
        var environment = spec.Environment!;
        var folder = environment[DemoMode.Variable];
        Assert.StartsWith(Path.Combine(h.Root, "temp", DemoLaunch.FolderPrefix), folder, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(folder, "home"), environment["CLAUDETTE_HOME"]);
        Assert.Equal(Path.Combine(folder, "claude-config"), environment["CLAUDE_CONFIG_DIR"]);
        Assert.Equal("1", environment["CLAUDETTE_RUN_IN_PLACE"]);
        // Not the stand-in itself: only the demo's claude is.
        Assert.False(environment.ContainsKey(StandInClaude.Variable));
        // Its claude is the Claudette it started, and the stand-in has the demo's plan usage to answer with.
        var settings = new JsonFileStore<AppSettings>(AppPaths.Under(Path.Combine(folder, "home")).SettingsFile).Load();
        Assert.Equal(spec.FileName, settings.ClaudeCode.ClaudePath);
        Assert.True(File.Exists(Path.Combine(folder, "claude-config", StandInClaude.UsageFile)));
        Assert.True(File.Exists(AppPaths.Under(Path.Combine(folder, "home")).UsageDatabase));
        Assert.Equal((false, false), (starter.IsStarting, starter.IsProblem));
        Assert.Contains("window of its own", starter.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_demo_left_behind_goes_when_the_next_starts_and_one_still_running_stays()
    {
        var launcher = new FakeLauncher();
        await using var h = new TabTestHarness(launcher: launcher);
        var temp = h.Services.DemoFolderRoot = Path.Combine(h.Root, "temp");
        string Leftover(string name)
        {
            var folder = Path.Combine(temp, DemoLaunch.FolderPrefix + name);
            var data = AppPaths.Under(Path.Combine(folder, "home")).DataDirectory;
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, SingleInstance.ClaimFileName), "");
            return folder;
        }
        var crashed = Leftover("crashed");
        var running = Leftover("running");
        // A running Claudette holds its data folder's claim.
        using (new FileStream(Path.Combine(AppPaths.Under(Path.Combine(running, "home")).DataDirectory, SingleInstance.ClaimFileName), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await h.Services.StartDemoAsync();
        }

        Assert.False(Directory.Exists(crashed));
        Assert.True(Directory.Exists(running));
    }

    [Fact]
    public async Task Only_a_demos_claude_stands_in_and_Leave_demo_closes_it()
    {
        await using (var h = new TabTestHarness())
        {
            Assert.DoesNotContain(StandInClaude.Variable, h.Services.ClaudeVariables.Keys);
            Assert.False(new MainWindowViewModel(h.Services).IsDemo);
        }

        await using var demo = new TabTestHarness(demo: new DemoMode(Path.Combine(Path.GetTempPath(), "claudette-demo-test")));
        Assert.Equal("1", demo.Services.ClaudeVariables[StandInClaude.Variable]);
        // There's no demo of the demo.
        Assert.False(new DemoStarter(demo.Services).IsOffered);
        var main = new MainWindowViewModel(demo.Services);
        Assert.True(main.IsDemo);
        var left = false;
        main.ExitRequested += () => left = true;
        main.LeaveDemoCommand.Execute(null);
        Assert.True(left);
    }
}
