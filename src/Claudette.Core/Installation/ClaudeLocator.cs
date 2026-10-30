using System.Text.RegularExpressions;
using Claudette.Core.Claude;
using Claudette.Core.Diffs;
using Claudette.Core.Processes;

namespace Claudette.Core.Installation;

/// <summary>A usable Claude Code installation.</summary>
public sealed record ClaudeInstall(string Path, Version Version);

public enum ClaudeInstallProblem
{
    NotFound,
    /// <summary>Only an npm <c>claude.cmd</c> launcher was found, which can't be started without a shell.</summary>
    UnsupportedLauncher,
    VersionUnreadable,
    TooOld,
    /// <summary>The path is the Claude desktop app's, or its own copy of Claude Code, which Claudette doesn't run.</summary>
    DesktopApp,
}

/// <param name="DesktopInstalled">
/// Claude Code wasn't found, but the Claude desktop app is installed: the user may have taken it for Claude Code
/// (DESIGN.md §12, "Installing Claude Code").
/// </param>
public sealed record ClaudeLocateResult(ClaudeInstall? Install, ClaudeInstallProblem? Problem, string? Detail, string? FoundPath = null, bool DesktopInstalled = false)
{
    public bool IsUsable => Install is not null && Problem is null;
}

/// <summary>Finds the <c>claude</c> executable and checks its version (DESIGN.md §2, "Dependency").</summary>
/// <param name="environment">
/// The user environment: its <c>PATH</c> is searched and <c>claude --version</c> runs with it. Waiting for it is how
/// the first <c>claude</c> start waits for the login shell (DESIGN.md §13, "Login shell environment").
/// </param>
/// <param name="environmentOverrides">Applied on top of the cleaned environment of <c>claude --version</c>, as for every <c>claude</c>.</param>
/// <param name="desktop">
/// Recognizes the Claude desktop app's files, which are never run, and says whether it's installed. Null recognizes none.
/// </param>
public sealed partial class ClaudeLocator(
    IProcessLauncher launcher,
    TimeProvider timeProvider,
    UserEnvironment? environment = null,
    IReadOnlyDictionary<string, string?>? environmentOverrides = null,
    ClaudeDesktop? desktop = null)
{
    /// <summary>The oldest Claude Code version Claudette supports (DESIGN.md §12, §16). Kept in step with compat/surface.yaml.</summary>
    public static readonly Version MinimumVersion = new(2, 1, 284);

    /// <summary>
    /// The newest Claude Code version Claudette has been checked against (DESIGN.md §16, "Tested versions"). Newer
    /// versions are allowed. Kept in step with compat/surface.yaml.
    /// </summary>
    public static readonly Version LastTestedVersion = new(2, 1, 312);

    private static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(20);

    public async Task<ClaudeLocateResult> LocateAsync(string? configuredPath, CancellationToken cancellationToken = default)
    {
        var userEnvironment = environment is null ? null : await environment.GetAsync(cancellationToken).ConfigureAwait(false);
        var searchPath = userEnvironment?.GetValueOrDefault("PATH") ?? Environment.GetEnvironmentVariable("PATH");
        var path = string.IsNullOrWhiteSpace(configuredPath) ? FindOnSystem(searchPath) : configuredPath;
        if (path is null)
        {
            return OperatingSystem.IsWindows() && FileProbe.FindIn(searchPath, "claude.cmd") is { } cmd
                ? new ClaudeLocateResult(null, ClaudeInstallProblem.UnsupportedLauncher,
                    "Claude Code was installed with npm, which Claudette can't start directly yet. Use Install Claude Code to install the native build, or Choose claude… to pick a claude.exe.", cmd)
                : new ClaudeLocateResult(null, ClaudeInstallProblem.NotFound, "Claude Code wasn't found.", DesktopInstalled: desktop?.IsInstalled(searchPath) == true);
        }
        // Never run, not even for its version: the app itself would open a window. Checked before the file is, since the
        // app deletes an old version's copy.
        switch (desktop?.Identify(path))
        {
            case ClaudeDesktopFile.App:
                return new ClaudeLocateResult(null, ClaudeInstallProblem.DesktopApp, "That's the Claude desktop app, not Claude Code.", path, DesktopInstalled: true);
            case ClaudeDesktopFile.ClaudeCodeCopy:
                return new ClaudeLocateResult(null, ClaudeInstallProblem.DesktopApp,
                    "That's the copy of Claude Code the Claude desktop app keeps for its own use. The app replaces it as it updates, so Claudette doesn't run it.",
                    path, DesktopInstalled: true);
        }
        if (!File.Exists(path))
        {
            return new ClaudeLocateResult(null, ClaudeInstallProblem.NotFound, $"'{path}' doesn't exist.", path);
        }

        ProcessResult result;
        try
        {
            var spec = new ProcessStartSpec(path, ["--version"]) { Environment = ClaudeEnvironment.From(userEnvironment, environmentOverrides) };
            result = await ProcessRunner.RunAsync(launcher, spec, VersionTimeout, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ClaudeLocateResult(null, ClaudeInstallProblem.VersionUnreadable, $"Running '{path} --version' failed: {ex.Message}", path);
        }

        if (!TryParseVersion(result.StandardOutput, out var version))
        {
            return new ClaudeLocateResult(null, ClaudeInstallProblem.VersionUnreadable,
                result.StandardOutput.Trim() is { Length: > 0 } output
                    ? $"Couldn't read the version from '{output}'."
                    : $"'{path} --version' printed no version.", path);
        }
        var install = new ClaudeInstall(path, version);
        return version < MinimumVersion
            ? new ClaudeLocateResult(install, ClaudeInstallProblem.TooOld,
                $"Claude Code {version} is installed. Claudette needs {MinimumVersion} or later.", path)
            : new ClaudeLocateResult(install, null, null, path);
    }

    /// <summary>Reads a version like <c>2.1.284 (Claude Code)</c>.</summary>
    public static bool TryParseVersion(string output, out Version version)
    {
        var match = VersionPattern().Match(output);
        if (match.Success && Version.TryParse(match.Value, out var parsed))
        {
            version = parsed;
            return true;
        }
        version = new Version();
        return false;
    }

    /// <summary>
    /// Looks on <paramref name="searchPath"/>, then in the standard install locations: the native installer's
    /// <c>~/.local/bin</c> first. An app started from the Dock or a desktop launcher doesn't get the shell's <c>PATH</c>;
    /// the login shell's usually fixes that (DESIGN.md §13), and the known locations are there for when it doesn't, and for
    /// a <c>PATH</c> from before Claude Code was installed. The Claude desktop app's files are passed over.
    /// </summary>
    private string? FindOnSystem(string? searchPath)
    {
        var name = OperatingSystem.IsWindows() ? "claude.exe" : "claude";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] known = OperatingSystem.IsWindows()
            ? [Path.Combine(home, ".local", "bin", "claude.exe")]
            : [Path.Combine(home, ".local", "bin", "claude"), "/opt/homebrew/bin/claude", "/usr/local/bin/claude", "/usr/bin/claude"];
        return OnPath(searchPath, name).Concat(known)
            .FirstOrDefault(path => File.Exists(path) && desktop?.Identify(path) is null or ClaudeDesktopFile.None);
    }

    private static IEnumerable<string> OnPath(string? searchPath, string name)
    {
        foreach (var directory in (searchPath ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(directory.Trim().Trim('"'), name);
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry.
                continue;
            }
            yield return candidate;
        }
    }

    [GeneratedRegex(@"\d+\.\d+\.\d+")]
    private static partial Regex VersionPattern();
}
