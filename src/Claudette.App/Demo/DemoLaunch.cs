using System.Collections;
using Claudette.App.Services;
using Claudette.Core;
using Claudette.Core.Demo;
using Claudette.Core.Processes;
using Claudette.Platform;
using Claudette.Usage.Demo;

namespace Claudette.App.Demo;

/// <summary>
/// This Claudette is a demo (DESIGN.md §11, "Demo mode"), started by another on <see cref="Folder"/>, which goes when it
/// closes: its banner shows, and its <c>claude</c> is Claudette standing in for Claude Code.
/// </summary>
public sealed record DemoMode(string Folder)
{
    /// <summary>In a demo's environment: the folder it runs on.</summary>
    public const string Variable = "CLAUDETTE_DEMO";

    /// <summary>The demo this is, from the environment it was started with; null for any other Claudette.</summary>
    public static DemoMode? FromEnvironment() =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } folder ? new DemoMode(folder) : null;
}

/// <summary>
/// <b>Try the demo</b> (DESIGN.md §11, "Demo mode"): writes a demo into a folder of its own in the temporary folder and
/// starts a second Claudette on it, which leaves this one as it was.
/// </summary>
public static class DemoLaunch
{
    /// <summary>A demo's folder in the temporary folder starts so, which is how one left behind is found.</summary>
    public const string FolderPrefix = "claudette-demo-";

    /// <summary>
    /// What a Claudette passes on to another that it shouldn't: where its own data is, and the screenshot scripts' ways
    /// of driving it.
    /// </summary>
    private static readonly string[] NotPassedOn =
        ["CLAUDETTE_HOME", "CLAUDE_CONFIG_DIR", "CLAUDETTE_AUTOMATION_PIPE", "CLAUDETTE_UNFOCUSED", StandInClaude.Variable, DemoMode.Variable];

    /// <summary>Writes a demo under <paramref name="temporary"/> and starts <paramref name="app"/> on it; returns its folder.</summary>
    public static async Task<string> StartAsync(AppServices services, string app, string temporary)
    {
        RemoveLeftovers(temporary);
        var folder = Path.Combine(temporary, FolderPrefix + Guid.NewGuid().ToString("N")[..8]);
        var now = services.Time.GetLocalNow();
        var options = new DemoOptions { ClaudePath = app, Theme = services.Settings.Appearance.Theme, Style = services.Settings.Appearance.Style };
        // Off the UI thread: it writes a few hundred files.
        var written = await Task.Run(async () =>
        {
            var demo = await DemoHome.WriteAsync(folder, Path.Combine(folder, "projects"), now, options, DemoContent.EmptyFolder).ConfigureAwait(false);
            DemoUsage.Write(demo.Paths.UsageDatabase, Path.Combine(demo.ClaudeConfig, StandInClaude.UsageFile), now.ToUniversalTime(), demo.Tabs);
            return demo;
        }).ConfigureAwait(false);
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            if (variable.Key is string name && variable.Value is string value && !NotPassedOn.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                environment[name] = value;
            }
        }
        environment["CLAUDETTE_HOME"] = Path.GetDirectoryName(written.Paths.DataDirectory)!;
        environment["CLAUDE_CONFIG_DIR"] = written.ClaudeConfig;
        environment[DemoMode.Variable] = folder;
        // A source build would copy itself into the demo's folder first (DESIGN.md §9, "Working on Claudette").
        environment["CLAUDETTE_RUN_IN_PLACE"] = "1";
        _ = services.Launcher.Start(new ProcessStartSpec(app, []) { Environment = environment, Detached = true });
        return folder;
    }

    /// <summary>Deletes what it can of a demo's folder, as the demo closes; what's still open goes with the next demo's leftovers.</summary>
    public static void Remove(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file still open, such as the log: the next demo takes it.
        }
    }

    /// <summary>
    /// The folders of demos that crashed or were killed: the ones no Claudette has open any more, as its lock on its data
    /// folder says, which the OS lets go of when the process ends. A demo still running keeps its folder.
    /// </summary>
    private static void RemoveLeftovers(string temporary)
    {
        if (!Directory.Exists(temporary))
        {
            return;
        }
        foreach (var leftover in Directory.EnumerateDirectories(temporary, FolderPrefix + "*"))
        {
            if (!IsRunning(Path.Combine(AppPaths.Under(Path.Combine(leftover, "home")).DataDirectory, SingleInstance.ClaimFileName)))
            {
                Remove(leftover);
            }
        }
    }

    private static bool IsRunning(string claim)
    {
        if (!File.Exists(claim))
        {
            return false;
        }
        try
        {
            using var free = new FileStream(claim, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
