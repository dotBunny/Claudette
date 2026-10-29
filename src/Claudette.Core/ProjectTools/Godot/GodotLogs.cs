using System.Text.RegularExpressions;
using Claudette.Core.ProjectTools.EditorLogs;

namespace Claudette.Core.ProjectTools.Godot;

/// <summary>
/// The log Godot writes for a project (DESIGN.md §18, "Editor logs"): the editor, the game or a headless run with the
/// project's folder after <c>--path</c>. Each writes <c>file_logging/log_path</c>, <c>user://logs/godot.log</c> by
/// default, in the project's user data folder, unless <c>--log-file</c> names another file; with file logging off, none.
/// Godot renames the old log, with the time in its name, as it starts.
/// </summary>
public static partial class GodotLogs
{
    public static EditorLogSource Source(GodotProject project, ProjectToolPaths paths, ToolOS os) => new()
    {
        ProcessNames = GodotProvider.EditorProcessNames,
        Match = process => Writer(process, project, LogFile(project, paths, os), os),
        Classify = Classify,
        IsCrash = IsCrash,
        NewParser = _ => Parser(),
    };

    /// <summary>A reader for one of Godot's logs.</summary>
    public static EditorLogParser Parser() => new LineParser();

    /// <summary>What <paramref name="process"/> is and the log it writes, when it has <paramref name="project"/> open.</summary>
    public static EditorLogWriter? Writer(SystemProcess process, GodotProject project, string? projectLog, ToolOS os)
    {
        var commandLine = process.CommandLine;
        // A relative --path, such as `--path .` from the project's folder, is read against the process's working folder.
        var opened = CommandLineOptions.ValueAfter(commandLine, "--path") is { } path
            ? CommandLineOptions.FullPath(path, process.WorkingFolder, os) is { } full && (SamePath(full, project.Root, os) || SamePath(full, project.File, os))
            : SystemProcessNames.CommandLineMentions(process, project.File);
        if (!opened || CommandLineOptions.Has(commandLine, "-p", "--project-manager"))
        {
            return null;
        }
        var log = CommandLineOptions.ValueAfter(commandLine, "--log-file") is { Length: > 0 } named && !named.StartsWith('-')
            ? os.IsRooted(named) ? named : os.Join(project.Root, named)
            : projectLog;
        var role = CommandLineOptions.Has(commandLine, "-e", "--editor") ? "Godot editor"
            : CommandLineOptions.Has(commandLine, "--headless") ? "Godot (headless)"
            : "Godot game";
        return log is null ? null : new EditorLogWriter(role, [log]);
    }

    /// <summary>
    /// Where the project's log is: <c>file_logging/log_path</c> with <c>user://</c> as the project's user data folder
    /// (<c>%APPDATA%\Godot\app_userdata\&lt;name&gt;</c>, <c>~/Library/Application Support/Godot/app_userdata/&lt;name&gt;</c>
    /// or <c>~/.local/share/godot/app_userdata/&lt;name&gt;</c>, or the custom folder's name in place of
    /// <c>Godot/app_userdata/&lt;name&gt;</c>) and <c>res://</c> as its own. Null with file logging off.
    /// </summary>
    public static string? LogFile(GodotProject project, ProjectToolPaths paths, ToolOS os)
    {
        if (!project.FileLogging)
        {
            return null;
        }
        var log = project.LogPath;
        if (log.StartsWith("res://", StringComparison.Ordinal))
        {
            return os.Join(project.Root, log["res://".Length..]);
        }
        if (!log.StartsWith("user://", StringComparison.Ordinal))
        {
            return log;
        }
        var data = os switch
        {
            ToolOS.Windows => paths.AppData,
            ToolOS.MacOS => os.Join(paths.Home, "Library", "Application Support"),
            _ => os.Join(paths.Home, ".local", "share"),
        };
        if (data is null)
        {
            return null;
        }
        var user = project.CustomUserDir is { } custom
            ? os.Join(data, SafeName(custom))
            : os.Join(data, os == ToolOS.Linux ? "godot" : "Godot", "app_userdata", SafeName(project.Name));
        return os.Join(user, log["user://".Length..]);
    }

    /// <summary><c>ERROR:</c>, <c>SCRIPT ERROR:</c>, <c>USER ERROR:</c>, and the warnings' <c>WARNING:</c> and the like.</summary>
    public static LogLineLevel Classify(string line) =>
        ErrorLine().IsMatch(line) ? LogLineLevel.Error
        : WarningLine().IsMatch(line) ? LogLineLevel.Warning
        : LogLineLevel.Normal;

    /// <summary>Godot's crash report: <c>CrashHandlerException</c> on Windows, <c>Program crashed with signal</c> elsewhere.</summary>
    public static bool IsCrash(string line) =>
        line.Contains("CrashHandlerException", StringComparison.Ordinal) || line.Contains("Program crashed with signal", StringComparison.Ordinal);

    /// <summary>A folder name as Godot makes one from a project's: the characters a file name can't have become <c>_</c>.</summary>
    private static string SafeName(string name) => UnsafeCharacters().Replace(name.Trim(), "_");

    private static bool SamePath(string a, string b, ToolOS os) =>
        string.Equals(a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'), os.PathComparison());

    /// <summary>
    /// Reads Godot's lines as entries (DESIGN.md §18, "Editor logs", "Its lines"): an error's or a warning's indented
    /// lines under it, <c>   at: get_node (scene/main/node.cpp:1364)</c> and a GDScript backtrace's, carry it on, muted.
    /// Every other line, what <c>print</c> writes among them, is one of its own.
    /// </summary>
    private sealed class LineParser() : EditorLogParser(Classify, IsCrash)
    {
        protected override LineRead Read(string line, LogVerbosity? open) =>
            open is LogVerbosity.Error or LogVerbosity.Warning && line.Length > 0 && char.IsWhiteSpace(line[0])
                ? new LineRead(Starts: false, IsTrace: true)
                : new LineRead(Starts: true);
    }

    [GeneratedRegex(@"^\s*(?:ERROR|SCRIPT ERROR|USER ERROR|SHADER ERROR|FATAL):")]
    private static partial Regex ErrorLine();

    [GeneratedRegex(@"^\s*(?:WARNING|SCRIPT WARNING|USER WARNING|SHADER WARNING):")]
    private static partial Regex WarningLine();

    [GeneratedRegex(@"[\\/:*?""<>|]")]
    private static partial Regex UnsafeCharacters();
}
