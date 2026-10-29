using System.Text;

namespace Claudette.Core.ProjectTools.EditorLogs;

/// <summary>How bad a line of an editor's log is, for its color and the counts on its row (DESIGN.md §18, "Editor logs").</summary>
public enum LogLineLevel
{
    Normal,
    Warning,
    Error,
}

/// <summary>A process of the project's engine and the log it writes.</summary>
/// <param name="Role">What it is, for the row: "Unreal editor", "Unreal commandlet ResavePackages", "Unity (batch mode)".</param>
/// <param name="LogFiles">
/// Its log: one file when its command line or the engine says which; otherwise the files it may have taken, in the order
/// the engine tries them (Unreal's <c>Name.log</c>, then <c>Name_2.log</c>…), told apart by when each was opened
/// (<see cref="EditorLogSource.OpenedAt"/>).
/// </param>
public sealed record EditorLogWriter(string Role, IReadOnlyList<string> LogFiles)
{
    /// <summary>
    /// Its log may move to another file while it runs, when a line in it says so (<see cref="EditorLogSource.NewMoveReader"/>),
    /// as Unity's editor writes the machine's <c>Editor.log</c> until it has the project open, then the project's own.
    /// </summary>
    public bool CanMove { get; init; }
}

/// <summary>
/// What a provider says about its engine's logs (DESIGN.md §18, "Editor logs"): which processes write them, which of
/// those are this project's and where each one's log is, and how to read its lines. On <see cref="ProjectInfo.Logs"/>.
/// </summary>
public sealed class EditorLogSource
{
    /// <summary>The engine's process names, for <see cref="ISystemProcesses.Find"/>.</summary>
    public required IReadOnlyList<string> ProcessNames { get; init; }

    /// <summary>What a running process is and where its log is; null when it isn't this project's, or writes no file.</summary>
    public required Func<SystemProcess, EditorLogWriter?> Match { get; init; }

    /// <summary>Whether a line is an error, a warning or neither, by the engine's own markers.</summary>
    public required Func<string, LogLineLevel> Classify { get; init; }

    /// <summary>Whether a line says the process crashed, such as Unreal's <c>=== Critical error: ===</c>.</summary>
    public required Func<string, bool> IsCrash { get; init; }

    /// <summary>A reader for one of its logs, as its entries, reading their times in the machine's time zone.</summary>
    public required Func<TimeZoneInfo, EditorLogParser> NewParser { get; init; }

    /// <summary>The levels its lines have, in the order its log's filter shows them.</summary>
    public IReadOnlyList<EditorLogLevel> Levels { get; init; } = MessageLevels;

    /// <summary>Its lines have Unreal's log categories, which the log can be filtered by.</summary>
    public bool HasCategories { get; init; }

    /// <summary>Errors, warnings and the rest, as Unity's and Godot's lines are.</summary>
    public static IReadOnlyList<EditorLogLevel> MessageLevels { get; } =
        [new(LogVerbosity.Error, "Errors"), new(LogVerbosity.Warning, "Warnings"), new(LogVerbosity.Log, "Messages")];

    /// <summary>
    /// When a log was opened, from its first line, read in the machine's time zone; null when it doesn't say. Only needed
    /// when a process's <see cref="EditorLogWriter.LogFiles"/> are several to choose from.
    /// </summary>
    public Func<string, TimeZoneInfo, DateTimeOffset?>? OpenedAt { get; init; }

    /// <summary>
    /// A reader for the lines in one of its logs that say the log moved to another file, as Unity's machine-wide
    /// <c>Editor.log</c> says when the editor moves to the project's own (DESIGN.md §18, "Editor logs", "A log that
    /// moves"); null when its logs don't move.
    /// </summary>
    public Func<LogMoveReader>? NewMoveReader { get; init; }
}

/// <summary>Options on a process's command line, as editors read them: quoted or not, ignoring case.</summary>
public static class CommandLineOptions
{
    /// <summary>The command line's words, a quoted part kept as one with its quotes taken off.</summary>
    public static IReadOnlyList<string> Words(string? commandLine)
    {
        var words = new List<string>();
        if (commandLine is null)
        {
            return words;
        }
        var word = new StringBuilder();
        var quoted = false;
        var any = false;
        foreach (var c in commandLine)
        {
            if (c == '"')
            {
                quoted = !quoted;
                any = true;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (any)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    any = false;
                }
            }
            else
            {
                word.Append(c);
                any = true;
            }
        }
        if (any)
        {
            words.Add(word.ToString());
        }
        return words;
    }

    /// <summary>Whether <paramref name="flag"/> (such as <c>-game</c>) is one of the command line's words.</summary>
    public static bool Has(string? commandLine, params string[] flag) =>
        Words(commandLine).Any(w => flag.Any(f => string.Equals(w, f, StringComparison.OrdinalIgnoreCase)));

    /// <summary>The word after <paramref name="option"/> (such as <c>-logFile</c>); null when it isn't there or is last.</summary>
    public static string? ValueAfter(string? commandLine, string option)
    {
        var words = Words(commandLine);
        for (var i = 0; i < words.Count - 1; i++)
        {
            if (string.Equals(words[i], option, StringComparison.OrdinalIgnoreCase))
            {
                return words[i + 1];
            }
        }
        return null;
    }

    /// <summary>
    /// <paramref name="path"/>, from a process's command line, as a full path: as it is when it's full, else read against
    /// the process's <paramref name="workingFolder"/>, with <c>.</c> and <c>..</c> taken out and either slash. Null when
    /// it's relative and the working folder isn't known.
    /// </summary>
    public static string? FullPath(string path, string? workingFolder, ToolOS os)
    {
        if (os.IsRooted(path))
        {
            return path;
        }
        if (workingFolder is null)
        {
            return null;
        }
        var parts = workingFolder.Replace('\\', '/').TrimEnd('/').Split('/').ToList();
        foreach (var part in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                // Never past the drive or the root.
                if (parts.Count > 1)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }
        return string.Join(os == ToolOS.Windows ? '\\' : '/', parts) is { Length: > 0 } full ? full : "/";
    }

    /// <summary>
    /// The value of a word such as <c>-abslog=C:\Logs\Game.log</c> for <paramref name="key"/> <c>abslog</c>, with or
    /// without the dash, as Unreal reads it; null when there's none.
    /// </summary>
    public static string? ValueOf(string? commandLine, string key)
    {
        foreach (var word in Words(commandLine))
        {
            var bare = word.TrimStart('-');
            if (bare.Length > key.Length && bare[key.Length] == '=' && bare.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                var value = bare[(key.Length + 1)..];
                return value.Length > 0 ? value : null;
            }
        }
        return null;
    }
}
