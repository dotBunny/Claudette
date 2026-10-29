using System.Text.RegularExpressions;
using Claudette.Core.ProjectTools.EditorLogs;

namespace Claudette.Core.ProjectTools.Unity;

/// <summary>
/// The logs Unity writes for a project (DESIGN.md §18, "Editor logs"): the editor or a batch mode run with the project's
/// folder after <c>-projectPath</c>. Each writes the machine's <c>Editor.log</c>, which Unity moves to
/// <c>Editor-prev.log</c> as it starts, unless <c>-logFile</c> names another file; <c>-logFile -</c> writes none. From
/// Unity 6.5, once the editor has the project open it moves its log to the project's own, <c>Logs/Editor.log</c>, and
/// says so in the machine's (<see cref="MoveReader"/>), unless <c>-useGlobalLog</c> or the editor's preferences keep it there.
/// </summary>
public static partial class UnityLogs
{
    /// <summary>How the line saying the log moved to the project's starts, wherever it is in the line.</summary>
    public const string MovedLine = "Logs moved to project-relative";

    public static EditorLogSource Source(UnityProject project, ProjectToolPaths paths, ToolOS os) => new()
    {
        ProcessNames = UnityProvider.EditorProcessNames,
        Match = process => Writer(process, project.Root, UnityEditors.EditorLog(paths, os), os),
        Classify = Classify,
        IsCrash = IsCrash,
        NewParser = _ => Parser(),
        NewMoveReader = () => new MoveReader(project.Root, os),
    };

    /// <summary>A reader for one of Unity's logs.</summary>
    public static EditorLogParser Parser() => new LineParser();

    /// <summary>The project's own log, which Unity 6.5 and later write once the editor has it open: <c>Logs/Editor.log</c>.</summary>
    public static string ProjectLog(string root, ToolOS os) => os.Join(root, "Logs", "Editor.log");

    /// <summary>
    /// What <paramref name="process"/> is and the log it writes, when it has <paramref name="root"/> open: the file
    /// <c>-logFile</c> names; else the project's own log when it has been written since the process started, as Unity
    /// 6.5's editor does once it has the project open; else <paramref name="editorLog"/>, the machine's, which can move
    /// to the project's unless <c>-useGlobalLog</c> keeps it there.
    /// </summary>
    public static EditorLogWriter? Writer(SystemProcess process, string root, string? editorLog, ToolOS os)
    {
        var commandLine = process.CommandLine;
        if (!Opens(process, root, os))
        {
            return null;
        }
        string? log = editorLog;
        var canMove = false;
        if (CommandLineOptions.Has(commandLine, "-logFile"))
        {
            var named = CommandLineOptions.ValueAfter(commandLine, "-logFile");
            if (named == "-")
            {
                // To standard output: no file.
                return null;
            }
            if (named is { Length: > 0 } && !named.StartsWith('-'))
            {
                log = os.IsRooted(named) ? named : os.Join(root, named);
            }
        }
        else if (!CommandLineOptions.Has(commandLine, "-useGlobalLog"))
        {
            var own = ProjectLog(root, os);
            if (WrittenSince(own, process.StartedAt))
            {
                log = own;
            }
            else
            {
                canMove = log is not null;
            }
        }
        return log is null ? null : new EditorLogWriter(CommandLineOptions.Has(commandLine, "-batchmode") ? "Unity (batch mode)" : "Unity editor", [log]) { CanMove = canMove };
    }

    /// <summary>
    /// Whether <paramref name="process"/> has <paramref name="root"/> open: its <c>-projectPath</c> is the folder, either
    /// slash. A relative one, such as <c>-projectPath .</c> from a batch run in the project's folder, is read against the
    /// process's working folder; and once Unity has its project open it works in the project's folder, so a working
    /// folder that's the project's counts too. Without the working folder, a relative one can't be told.
    /// </summary>
    public static bool Opens(SystemProcess process, string root, ToolOS os)
    {
        if (CommandLineOptions.ValueAfter(process.CommandLine, "-projectPath") is not { } opened)
        {
            return false;
        }
        if (CommandLineOptions.FullPath(opened, process.WorkingFolder, os) is { } full && SamePath(full, root, os))
        {
            return true;
        }
        return !os.IsRooted(opened) && process.WorkingFolder is { } folder && SamePath(folder, root, os);
    }

    /// <summary>
    /// Whether <paramref name="path"/> has been written since <paramref name="started"/>. Unity before 6.5 never writes
    /// the project's <c>Logs/Editor.log</c>, and 6.5 keeping to the machine's log leaves the project's as it was, so a
    /// file written since the editor started is one it's writing. Without the start time, it can't be told.
    /// </summary>
    private static bool WrittenSince(string path, DateTimeOffset? started)
    {
        if (started is not { } since)
        {
            return false;
        }
        try
        {
            var file = new FileInfo(path);
            return file.Exists && file.LastWriteTimeUtc >= since.UtcDateTime;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads where Unity's machine-wide <c>Editor.log</c> says an editor's log moved to (DESIGN.md §18, "Editor logs", "A
    /// log that moves"): <c>Logs moved to project-relative Editor.log file, and subsequent logs will only be written to
    /// this file.</c>, matched by its start, then the file, <c>F:/Unity/projs/myproj/Logs/Editor.log</c>, on the next line
    /// that isn't blank or after the sentence on its own. Either slash; a relative path is the project's; without one
    /// that ends in <c>.log</c>, the project's <c>Logs/Editor.log</c>, the project being the log's.
    /// </summary>
    public sealed partial class MoveReader(string root, ToolOS os) : LogMoveReader
    {
        /// <summary>What comes before a relative path: a space, a quote, or the end of what <c>-timestamps</c> puts before a line.</summary>
        private static readonly char[] RelativeStarts = [' ', '\t', '"', '\'', '|', ']'];

        /// <summary>The sentence was seen, and its file is still to come: how many reads ago, while it is.</summary>
        private int? _waiting;

        public override IReadOnlyList<LogMoveLine> Read(IReadOnlyList<string> lines, bool fresh, bool final)
        {
            if (fresh)
            {
                _waiting = null;
            }
            List<LogMoveLine>? moves = null;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (_waiting is not null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    _waiting = null;
                    if (PathIn(line) is { } next)
                    {
                        (moves ??= []).Add(new LogMoveLine(i, Resolve(next)));
                        continue;
                    }
                    // Not a file: it moved to the project's own, said on the line before this one.
                    (moves ??= []).Add(new LogMoveLine(i - 1, ProjectLog(root, os)));
                }
                var at = line.IndexOf(MovedLine, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    continue;
                }
                if (PathIn(line[(at + MovedLine.Length)..]) is { } same)
                {
                    (moves ??= []).Add(new LogMoveLine(i, Resolve(same)));
                }
                else
                {
                    _waiting = 0;
                }
            }
            if (_waiting is { } reads)
            {
                if (final || reads > 0)
                {
                    // No file came with it, in this read or the one after it.
                    _waiting = null;
                    (moves ??= []).Add(new LogMoveLine(lines.Count - 1, ProjectLog(root, os)));
                }
                else
                {
                    _waiting = reads + 1;
                }
            }
            return moves ?? (IReadOnlyList<LogMoveLine>)[];
        }

        /// <summary>
        /// The path at the end of <paramref name="text"/>: a full one from where it starts, or else a relative one without
        /// spaces. Null unless it ends in <c>.log</c>.
        /// </summary>
        private static string? PathIn(string text)
        {
            var trimmed = text.Trim().TrimEnd('.', '"', '\'').TrimEnd();
            if (!trimmed.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (FullPath().Match(trimmed) is { Success: true } full)
            {
                return full.Groups["path"].Value;
            }
            var last = trimmed[(trimmed.LastIndexOfAny(RelativeStarts) + 1)..];
            return last.Length > 0 ? last : null;
        }

        /// <summary>A path as the OS writes it, read against the project's folder when it's relative.</summary>
        private string Resolve(string path)
        {
            var native = os == ToolOS.Windows ? path.Replace('/', '\\') : path.Replace('\\', '/');
            return os.IsRooted(native) ? native : os.Join(root, native);
        }

        /// <summary>
        /// A full path, <c>F:/…</c>, <c>C:\…</c>, <c>\\server\…</c> or <c>/…</c>, at the start of the text or after a
        /// space, a quote, or what Unity's <c>-timestamps</c> puts before a line, and running to its end.
        /// </summary>
        [GeneratedRegex(@"(?:^|(?<=[\s""'|\]]))(?<path>[A-Za-z]:[\\/].*|\\\\[^\\/\s].*|/[^/\s].*)$")]
        private static partial Regex FullPath();
    }

    /// <summary>The C# compiler's <c>error CS0103</c> and <c>warning CS0168</c>, exceptions, and lines that start with Error or Warning.</summary>
    public static LogLineLevel Classify(string line) =>
        ErrorLine().IsMatch(line) ? LogLineLevel.Error
        : WarningLine().IsMatch(line) ? LogLineLevel.Warning
        : LogLineLevel.Normal;

    /// <summary>Unity's crash report in the log: <c>Crash!!!</c>, or <c>Native Crash Reporting</c>.</summary>
    public static bool IsCrash(string line) =>
        line.Contains("Crash!!!", StringComparison.Ordinal) || line.Contains("Native Crash Reporting", StringComparison.Ordinal);

    private static bool SamePath(string a, string b, ToolOS os) =>
        string.Equals(Normal(a), Normal(b), os.PathComparison());

    private static string Normal(string path) => path.Replace('\\', '/').TrimEnd('/');

    /// <summary>
    /// Reads Unity's lines as entries (DESIGN.md §18, "Editor logs", "Its lines"): a message and, for one from a script,
    /// its stack trace, <c>UnityEngine.Debug:LogWarning (object)</c> and the frames under it, then
    /// <c>(Filename: Assets/Player.cs Line: 12)</c>, which carry it on, muted, as an exception's frames do. The trace
    /// says what a script's message is: <c>LogWarning</c> a warning, <c>LogError</c>, <c>LogException</c> and
    /// <c>LogAssertion</c> an error. A blank line carries on the entry before it.
    /// </summary>
    private sealed partial class LineParser() : EditorLogParser(Classify, IsCrash)
    {
        protected override LineRead Read(string line, LogVerbosity? open)
        {
            if (open is null)
            {
                return new LineRead(Starts: true);
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                return new LineRead(Starts: false);
            }
            if (DebugCall().Match(line) is { Success: true } call)
            {
                var kind = call.Groups["kind"].ValueSpan;
                LogVerbosity? raises = kind is "Log" ? null : kind is "LogWarning" ? LogVerbosity.Warning : LogVerbosity.Error;
                return new LineRead(Starts: false, IsTrace: true, Raises: raises);
            }
            return Frame().IsMatch(line) ? new LineRead(Starts: false, IsTrace: true) : new LineRead(Starts: true);
        }

        [GeneratedRegex(@"^UnityEngine\.Debug:(?<kind>Log(?:Warning|Error|Exception|Assertion)?)(?:Format)?\s?\(")]
        private static partial Regex DebugCall();

        /// <summary>
        /// <c>Player:Start () (at Assets/Player.cs:12)</c>, <c>Player.Update ()</c>, Mono's <c>  at Player.Update ()</c>
        /// and <c>(Filename: Assets/Player.cs Line: 12)</c>; not a compiler's <c>Assets/Player.cs(12,5): error</c>.
        /// </summary>
        [GeneratedRegex(@"^[^\s()]*[:.][^\s()]*\s?\([^()]*\)(?:\s\(at\s[^()]*\))?\s*$|^\s+at\s|^\(Filename: .* Line: -?\d+\)\s*$")]
        private static partial Regex Frame();
    }

    [GeneratedRegex(@"\berror CS\d+\b|^[A-Za-z_][\w.]*Exception:|^(?:Error|ERROR)\b")]
    private static partial Regex ErrorLine();

    [GeneratedRegex(@"\bwarning CS\d+\b|^(?:Warning|WARNING)\b")]
    private static partial Regex WarningLine();
}
