using System.Globalization;
using Claudette.Core.ProjectTools;
using Claudette.Core.ProjectTools.EditorLogs;
using Claudette.Core.ProjectTools.Godot;
using Claudette.Core.ProjectTools.Unity;
using Claudette.Core.ProjectTools.Unreal;
using Claudette.Core.Tests.Support;

namespace Claudette.Core.Tests.ProjectTools;

/// <summary>
/// Editor logs (DESIGN.md §18, "Editor logs"): which of an engine's processes are a project's and where each writes its
/// log, how bad a line is, and which logs are being written. Every process is a made-up one, and every file a temporary one.
/// </summary>
public sealed class EditorLogTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 15, 1, 20, TimeSpan.Zero);

    private readonly TempFolder _temp = new("claudette-editorlogs");

    public void Dispose() => _temp.Dispose();

    private static SystemProcess Process(int pid, string name, string commandLine, DateTimeOffset? started = null) => new(pid, name, commandLine, started ?? Start);

    // ---- Command lines --------------------------------------------------------------------------------------------------

    [Fact]
    public void Command_line_options_are_read_quoted_or_not_ignoring_case()
    {
        const string line = """
            "C:\Program Files\Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe" "D:\Games\Night Owl\NightOwl.uproject" -ABSLOG="D:\Logs\Night Owl.log" -run=Cook -logFile out.txt
            """;

        Assert.Equal(@"C:\Program Files\Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe", CommandLineOptions.Words(line)[0]);
        Assert.Equal(@"D:\Logs\Night Owl.log", CommandLineOptions.ValueOf(line, "abslog"));
        Assert.Equal("Cook", CommandLineOptions.ValueOf(line, "run"));
        Assert.Null(CommandLineOptions.ValueOf(line, "log"));
        Assert.Equal("out.txt", CommandLineOptions.ValueAfter(line, "-logfile"));
        Assert.Null(CommandLineOptions.ValueAfter(line, "out.txt"));
        Assert.True(CommandLineOptions.Has(line, "-LOGFILE"));
        Assert.False(CommandLineOptions.Has(line, "-game"));
        Assert.Empty(CommandLineOptions.Words(null));
    }

    // ---- Unreal -------------------------------------------------------------------------------------------------------

    private const string UProject = @"D:\Games\NightOwl\NightOwl.uproject";
    private const string UnrealRoot = @"D:\Games\NightOwl";

    private static EditorLogWriter? Unreal(string name, string arguments) =>
        UnrealLogs.Writer(Process(1, name, $@"C:\UE\Engine\Binaries\Win64\{name}.exe {arguments}"), UProject, UnrealRoot, "NightOwl", ToolOS.Windows);

    [Fact]
    public void An_unreal_editor_with_the_project_writes_its_log_or_the_next_one_free()
    {
        var editor = Unreal("UnrealEditor", $"\"{UProject}\"")!;

        Assert.Equal("Unreal editor", editor.Role);
        Assert.Equal(@"D:\Games\NightOwl\Saved\Logs\NightOwl.log", editor.LogFiles[0]);
        Assert.Equal(@"D:\Games\NightOwl\Saved\Logs\NightOwl_2.log", editor.LogFiles[1]);
        Assert.Equal(9, editor.LogFiles.Count);

        // With either slash, ignoring case.
        Assert.NotNull(Unreal("UnrealEditor", "d:/games/nightowl/NIGHTOWL.uproject"));
        // Another project's, or none.
        Assert.Null(Unreal("UnrealEditor", @"D:\Games\Other\Other.uproject"));
        Assert.Null(UnrealLogs.Writer(new SystemProcess(1, "UnrealEditor", null), UProject, UnrealRoot, "NightOwl", ToolOS.Windows));
    }

    [Theory]
    [InlineData("UnrealEditor-Cmd", "-run=ResavePackagesCommandlet", "Unreal commandlet ResavePackages")]
    [InlineData("UnrealEditor-Cmd", "-run=cook -targetplatform=Windows", "Unreal commandlet cook")]
    [InlineData("UnrealEditor-Cmd", "-ExecCmds=\"Automation RunTests Project\"", "Unreal editor (command line)")]
    [InlineData("UnrealEditor", "-game -log", "Unreal game")]
    [InlineData("UnrealEditor", "-server", "Unreal server")]
    [InlineData("UE4Editor", "-skipcompile", "Unreal editor")]
    public void An_unreal_process_says_what_it_is(string name, string arguments, string role) =>
        Assert.Equal(role, Unreal(name, $"\"{UProject}\" {arguments}")!.Role);

    [Fact]
    public void An_unreal_log_named_on_the_command_line_is_the_one_followed()
    {
        Assert.Equal([@"D:\Logs\Owl.log"], Unreal("UnrealEditor", $@"""{UProject}"" -abslog=D:\Logs\Owl.log")!.LogFiles);
        Assert.Equal([@"D:\Games\NightOwl\Saved\Logs\Cook.log"], Unreal("UnrealEditor-Cmd", $"\"{UProject}\" LOG=Cook")!.LogFiles);
        Assert.Equal([@"D:\Games\NightOwl\Saved\Logs\Cook.txt"], Unreal("UnrealEditor-Cmd", $"\"{UProject}\" -log=Cook.txt")!.LogFiles);
        // -log alone opens a log window, and names no file.
        Assert.Equal(9, Unreal("UnrealEditor", $"\"{UProject}\" -log")!.LogFiles.Count);
    }

    [Theory]
    [InlineData("[2026.10.20-15.01.22:123][  0]LogBlueprint: Error: [Compiler] Missing node", LogLineLevel.Error)]
    [InlineData("LogTemp: Warning: Slow frame", LogLineLevel.Warning)]
    [InlineData("[2026.10.20-15.01.22:123][  0]LogWindows: Error: === Critical error: ===", LogLineLevel.Error)]
    [InlineData("Error: Unknown command line option", LogLineLevel.Error)]
    [InlineData(@"D:\Games\NightOwl\Source\Owl.cpp(42): error C2065: 'x': undeclared identifier", LogLineLevel.Error)]
    [InlineData("Source/Owl.cpp:42:7: warning: unused variable 'y'", LogLineLevel.Warning)]
    [InlineData("[2026.10.20-15.01.22:123][  0]LogInit: Display: Errors: none so far", LogLineLevel.Normal)]
    [InlineData("LogInit: Build: ++UE5+Release-5.4", LogLineLevel.Normal)]
    public void An_unreal_line_is_an_error_or_a_warning_by_its_verbosity(string line, LogLineLevel level) =>
        Assert.Equal(level, UnrealLogs.Classify(line));

    [Fact]
    public void An_unreal_crash_is_its_critical_error_or_an_assertion()
    {
        Assert.True(UnrealLogs.IsCrash("[2026.10.20-15.01.22:123][  0]LogWindows: Error: === Critical error: ==="));
        Assert.True(UnrealLogs.IsCrash("Fatal error: [File:D:/Owl/Source/Owl.cpp] [Line: 12]"));
        Assert.False(UnrealLogs.IsCrash("LogTemp: Error: Something went wrong"));
    }

    [Fact]
    public void An_unreal_log_says_when_it_opened_in_the_machines_time()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Owl Time", TimeSpan.FromHours(-4), "Owl Time", "Owl Time");

        Assert.Equal(new DateTimeOffset(2026, 10, 20, 15, 1, 22, TimeSpan.FromHours(-4)), UnrealLogs.OpenedAt("Log file open, 10/20/26 15:01:22", zone));
        Assert.Null(UnrealLogs.OpenedAt("LogInit: Display: Starting", zone));
        Assert.Null(UnrealLogs.OpenedAt(null, zone));
    }

    // ---- Unity --------------------------------------------------------------------------------------------------------

    private const string EditorLog = @"C:\Users\owl\AppData\Local\Unity\Editor\Editor.log";

    private static EditorLogWriter? Unity(string arguments, ToolOS os = ToolOS.Windows, string root = @"D:\Games\Owl") =>
        UnityLogs.Writer(Process(1, "Unity", $@"""C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe"" {arguments}"), root, EditorLog, os);

    [Fact]
    public void A_unity_editor_with_the_project_writes_the_machines_editor_log_or_its_own()
    {
        var editor = Unity(@"-projectPath ""D:\Games\Owl""")!;
        Assert.Equal("Unity editor", editor.Role);
        Assert.Equal([EditorLog], editor.LogFiles);

        Assert.NotNull(Unity(@"-projectpath d:/games/owl/"));
        Assert.Null(Unity(@"-projectPath D:\Games\Owl2"));
        Assert.Null(Unity(@"-projectpath /g/owl", ToolOS.Linux, "/g/Owl"));
        Assert.Null(Unity("-batchmode"));

        var batch = Unity(@"-batchmode -projectPath D:\Games\Owl -logFile Logs\import.log")!;
        Assert.Equal("Unity (batch mode)", batch.Role);
        Assert.Equal([@"D:\Games\Owl\Logs\import.log"], batch.LogFiles);
        Assert.Equal([@"D:\Logs\owl.log"], Unity(@"-projectPath D:\Games\Owl -logFile D:\Logs\owl.log")!.LogFiles);
        // To standard output, as Claudette's own jobs run it: no file.
        Assert.Null(Unity(@"-batchmode -projectPath D:\Games\Owl -runTests -logFile -"));
    }

    [Fact]
    public void A_relative_path_is_read_against_the_processs_working_folder()
    {
        Assert.Equal(@"D:\Games\Owl", CommandLineOptions.FullPath(".", @"D:\Games\Owl", ToolOS.Windows));
        Assert.Equal(@"D:\Games\Owl", CommandLineOptions.FullPath(@"..\Owl\", @"D:\Games\Tools", ToolOS.Windows));
        Assert.Equal(@"D:\Games\Owl\Logs", CommandLineOptions.FullPath("./Owl/Logs", @"D:\Games\", ToolOS.Windows));
        Assert.Equal("/g/Owl", CommandLineOptions.FullPath("Owl", "/g", ToolOS.Linux));
        Assert.Equal("/", CommandLineOptions.FullPath("../..", "/g", ToolOS.MacOS));
        Assert.Equal(@"C:\Owl", CommandLineOptions.FullPath(@"C:\Owl", @"D:\Games", ToolOS.Windows));
        // Without the working folder, a relative one can't be told.
        Assert.Null(CommandLineOptions.FullPath(".", null, ToolOS.Windows));
    }

    [Fact]
    public void A_unity_batch_run_with_a_relative_project_path_is_the_projects_from_its_working_folder()
    {
        // As Claude runs it in the project's folder: -projectPath . and a -logFile in it.
        var run = Process(1, "Unity", @"Unity.exe -batchmode -quit -projectPath . -executeMethod Capture.Run -logFile Logs/hd-capture.log");
        EditorLogWriter? Writer(string? folder) => UnityLogs.Writer(run with { WorkingFolder = folder }, @"D:\Games\Owl", EditorLog, ToolOS.Windows);

        var batch = Writer(@"D:\Games\Owl")!;
        Assert.Equal("Unity (batch mode)", batch.Role);
        Assert.Equal([@"D:\Games\Owl\Logs\hd-capture.log"], batch.LogFiles);
        Assert.NotNull(UnityLogs.Writer(run with { CommandLine = @"Unity.exe -batchmode -projectPath ..\Owl", WorkingFolder = @"d:\games\tools" }, @"D:\Games\Owl", EditorLog, ToolOS.Windows));
        // Another project's folder, or none known: not this project's.
        Assert.Null(Writer(@"D:\Games\Other"));
        Assert.Null(Writer(null));
        // Once Unity has its project open it works in the project's folder, whatever the path was read against.
        Assert.True(UnityLogs.Opens(run with { CommandLine = "Unity -projectPath Owl", WorkingFolder = "/g/Owl" }, "/g/Owl", ToolOS.Linux));
        Assert.False(UnityLogs.Opens(run with { CommandLine = "Unity -projectPath /g/Other", WorkingFolder = "/g/Owl" }, "/g/Owl", ToolOS.Linux));
    }

    [Theory]
    [InlineData("Assets/Player.cs(12,5): error CS0103: The name 'x' does not exist", LogLineLevel.Error)]
    [InlineData("Assets/Player.cs(3,7): warning CS0168: The variable 'e' is declared but never used", LogLineLevel.Warning)]
    [InlineData("NullReferenceException: Object reference not set to an instance of an object", LogLineLevel.Error)]
    [InlineData("Refreshing native plugins compatible for Editor in 1.23 ms", LogLineLevel.Normal)]
    public void A_unity_line_is_an_error_or_a_warning(string line, LogLineLevel level) => Assert.Equal(level, UnityLogs.Classify(line));

    [Fact]
    public void A_unity_crash_is_its_crash_report()
    {
        Assert.True(UnityLogs.IsCrash("Crash!!!"));
        Assert.True(UnityLogs.IsCrash("=================================================================\tNative Crash Reporting"));
        Assert.False(UnityLogs.IsCrash("Crashing is not happening here"));
    }

    // ---- Unity's log moving to the project's (DESIGN.md §18, "Editor logs", "A log that moves") ------------------------

    private const string Sentence = "Logs moved to project-relative Editor.log file, and subsequent logs will only be written to this file.";

    [Fact]
    public void A_unity_editor_writing_the_projects_own_log_since_it_started_is_followed_there()
    {
        var os = ToolOSExtensions.Current;
        var root = _temp.CreateFolder("Owl");
        var own = Path.Combine(root, "Logs", "Editor.log");
        var global = _temp.Combine("Editor.log");
        EditorLogWriter Writer(string arguments = "") =>
            UnityLogs.Writer(Process(1, "Unity", $"Unity -projectPath \"{root}\" {arguments}", Start), root, global, os)!;

        Assert.Equal(own, UnityLogs.ProjectLog(root, os));
        // None of its own yet: the machine's, which can move to the project's.
        Assert.Equal([global], Writer().LogFiles);
        Assert.True(Writer().CanMove);

        // One from before it started, as an earlier session, Unity before 6.5 or Use Global Editor Log leaves it: the machine's still.
        Directory.CreateDirectory(Path.GetDirectoryName(own)!);
        File.WriteAllText(own, "An earlier session\n");
        File.SetLastWriteTimeUtc(own, Start.UtcDateTime.AddHours(-1));
        Assert.Equal([global], Writer().LogFiles);

        // Written since it started: the project's own, which doesn't move again.
        File.SetLastWriteTimeUtc(own, Start.UtcDateTime.AddMinutes(1));
        Assert.Equal([own], Writer().LogFiles);
        Assert.False(Writer().CanMove);
        Assert.Equal("Unity (batch mode)", Writer("-batchmode").Role);

        // -useGlobalLog keeps the machine's, and -logFile the file it names; neither moves.
        Assert.Equal([global], Writer("-useGlobalLog").LogFiles);
        Assert.False(Writer("-useGlobalLog").CanMove);
        Assert.Equal([os.Join(root, "editor.txt")], Writer("-logFile editor.txt").LogFiles);
        Assert.False(Writer("-logFile editor.txt").CanMove);
        // Without the time it started, there's no telling.
        Assert.Equal([global], UnityLogs.Writer(new SystemProcess(1, "Unity", $"Unity -projectPath \"{root}\""), root, global, os)!.LogFiles);
    }

    private static IReadOnlyList<LogMoveLine> Moves(LogMoveReader reader, params string[] lines) => reader.Read(lines, fresh: false, final: false);

    [Fact]
    public void Unitys_line_saying_the_log_moved_has_the_file_on_the_next_line_with_either_slash()
    {
        var windows = new UnityLogs.MoveReader(@"F:\Unity\projs\myproj", ToolOS.Windows);
        Assert.Equal(
            [new LogMoveLine(2, @"F:\Unity\projs\myproj\Logs\Editor.log")],
            Moves(windows, "Loading project", Sentence, "F:/Unity/projs/myproj/Logs/Editor.log", "Another editor: Starting"));

        // The next line that isn't blank, and a backslash where the OS writes a slash.
        var linux = new UnityLogs.MoveReader("/home/owl/Owl", ToolOS.Linux);
        Assert.Equal([new LogMoveLine(2, "/home/owl/Owl/Logs/Editor.log")], Moves(linux, Sentence, "", @"\home\owl\Owl\Logs\Editor.log"));

        // Another project's: it says which, and it's for the tracker to tell whose.
        Assert.Equal([new LogMoveLine(1, @"D:\Lark\Logs\Editor.log")], Moves(windows, Sentence, "D:/Lark/Logs/Editor.log"));

        // A relative path is the project's; Unity's -timestamps before each line, and other words after the start, still match.
        Assert.Equal(
            [new LogMoveLine(1, @"F:\Unity\projs\myproj\Logs\Editor.log")],
            Moves(windows, "2026-10-28T12:00:00.123Z|0x2a1c|Logs moved to project-relative log file:", "2026-10-28T12:00:00.124Z|0x2a1c|Logs/Editor.log"));
        Assert.Equal(
            [new LogMoveLine(1, @"F:\My Projects\Owl\Logs\Editor.log")],
            Moves(windows, Sentence, "2026-10-28T12:00:00.124Z|0x2a1c|F:/My Projects/Owl/Logs/Editor.log"));
        Assert.Empty(Moves(windows, "Logs moved elsewhere", "F:/Unity/projs/myproj/Logs/Editor.log"));
    }

    [Fact]
    public void Unitys_line_saying_the_log_moved_can_have_the_file_after_it_or_none()
    {
        var reader = new UnityLogs.MoveReader(@"F:\Owl", ToolOS.Windows);

        // On the same line, after the sentence.
        Assert.Equal([new LogMoveLine(0, @"F:\Owl\Logs\Editor.log")], Moves(reader, $"{Sentence} F:/Owl/Logs/Editor.log", "Next"));
        Assert.Equal([new LogMoveLine(0, @"F:\Owl\Logs\Editor.log")], Moves(reader, $"{Sentence} \"F:/Owl/Logs/Editor.log\""));

        // The next line isn't a file: the project's own, and the next line isn't this log's.
        Assert.Equal([new LogMoveLine(0, @"F:\Owl\Logs\Editor.log")], Moves(reader, Sentence, "Refreshing native plugins"));

        // The sentence last in a read: its file is in the next.
        Assert.Empty(Moves(reader, "Loading project", Sentence));
        Assert.Equal([new LogMoveLine(0, @"F:\Owl\Logs\Editor.log")], Moves(reader, "F:/Owl/Logs/Editor.log", "Next"));

        // No file in the read after it either: the project's own, said in the read before.
        Assert.Empty(Moves(reader, Sentence));
        Assert.Equal([new LogMoveLine(-1, @"F:\Owl\Logs\Editor.log")], Moves(reader));

        // The last read, as the editor has ended: decided there and then.
        Assert.Equal([new LogMoveLine(0, @"F:\Owl\Logs\Editor.log")], reader.Read([Sentence], fresh: false, final: true));

        // A new file in its place forgets a sentence waiting for its file.
        Assert.Empty(Moves(reader, Sentence));
        Assert.Empty(reader.Read(["Unity Editor version: 6000.5.0f1"], fresh: true, final: false));
        Assert.Empty(Moves(reader));
    }

    private static readonly ProjectToolPaths UnityPaths = new(Path.Combine(Path.GetTempPath(), "owl-home"), LocalAppData: Path.Combine(Path.GetTempPath(), "owl-local"));

    private static string GlobalLog => UnityEditors.EditorLog(UnityPaths, ToolOSExtensions.Current)!;

    private FollowedProject UnityFollowed(string name, out string projectLog)
    {
        var root = _temp.CreateFolder(name);
        projectLog = Path.Combine(root, "Logs", "Editor.log");
        var project = new UnityProject(root, name, "6000.5.0f1", null, null, null, []);
        return new FollowedProject(root, root, UnityLogs.Source(project, UnityPaths, ToolOSExtensions.Current));
    }

    private static SystemProcess UnityEditor(int pid, FollowedProject project, string arguments = "", DateTimeOffset? started = null) =>
        Process(pid, "Unity", $"Unity -projectPath \"{project.Root}\" {arguments}", started ?? Start);

    [Fact]
    public void A_line_saying_a_unity_editors_log_moved_moves_that_projects_editor_and_no_other()
    {
        var owl = UnityFollowed("Owl", out var owlLog);
        var lark = UnityFollowed("Lark", out var larkLog);
        var tracker = new EditorLogTracker(TimeZoneInfo.Utc, ToolOSExtensions.Current);
        var owlEditor = UnityEditor(10, owl);
        var larkEditor = UnityEditor(20, lark, started: Start.AddMinutes(1));

        // Both write the machine's log, which is one log, the first's.
        var shared = Assert.Single(tracker.Update([owl, lark], [owlEditor, larkEditor]));
        Assert.Equal(GlobalLog, shared.Path);
        Assert.Same(owl, shared.Project);

        // Lark's line: Lark's editor writes its project's log from now on, and Owl's the machine's still.
        var move = tracker.Move(GlobalLog, larkLog)!;
        Assert.Same(lark, move.Project);
        Assert.Equal([20], move.Writers.Select(w => w.Process.Pid));
        var logs = tracker.Update([owl, lark], [owlEditor, larkEditor], out var moves);
        Assert.Empty(moves);
        Assert.Equal([GlobalLog, larkLog], logs.Select(l => l.Path));
        Assert.Equal([10], logs[0].Writers.Select(w => w.Process.Pid));

        // A project no tab has, the same file, or one that has moved already: nothing moves.
        Assert.Null(tracker.Move(GlobalLog, _temp.Combine("Elsewhere", "Logs", "Editor.log")));
        Assert.Null(tracker.Move(GlobalLog, GlobalLog));
        Assert.Null(tracker.Move(larkLog, Path.Combine(lark.Root, "Logs", "Other.log")));

        Assert.Equal(owl, tracker.Move(GlobalLog, owlLog)!.Project);
        Assert.Equal([owlLog, larkLog], tracker.Update([owl, lark], [owlEditor, larkEditor]).Select(l => l.Path));
        // A new editor of the project starts on the machine's log again.
        Assert.Equal([GlobalLog], tracker.Update([owl], [UnityEditor(11, owl, started: Start.AddHours(1))]).Select(l => l.Path));
    }

    [Fact]
    public void A_unity_editor_with_useGlobalLog_keeps_the_machines_log_whatever_it_says()
    {
        var owl = UnityFollowed("Owl", out var owlLog);
        var tracker = new EditorLogTracker(TimeZoneInfo.Utc, ToolOSExtensions.Current);
        var editor = UnityEditor(10, owl, "-useGlobalLog");
        Assert.Equal(GlobalLog, Assert.Single(tracker.Update([owl], [editor])).Path);

        Assert.Null(tracker.Move(GlobalLog, owlLog));
        Directory.CreateDirectory(Path.GetDirectoryName(owlLog)!);
        File.WriteAllText(owlLog, "Another session's\n");
        File.SetLastWriteTimeUtc(owlLog, Start.UtcDateTime.AddMinutes(1));

        Assert.Equal(GlobalLog, Assert.Single(tracker.Update([owl], [editor], out var moves)).Path);
        Assert.Empty(moves);
    }

    [Fact]
    public void A_unity_editor_found_writing_its_projects_log_at_a_later_look_has_moved_there_for_good()
    {
        var owl = UnityFollowed("Owl", out var owlLog);
        var tracker = new EditorLogTracker(TimeZoneInfo.Utc, ToolOSExtensions.Current);
        var editor = UnityEditor(10, owl);
        Assert.Equal(GlobalLog, Assert.Single(tracker.Update([owl], [editor], out var none)).Path);
        Assert.Empty(none);

        Directory.CreateDirectory(Path.GetDirectoryName(owlLog)!);
        File.WriteAllText(owlLog, "Unity Editor version: 6000.5.0f1\n");
        File.SetLastWriteTimeUtc(owlLog, Start.UtcDateTime.AddMinutes(1));
        Assert.Equal(owlLog, Assert.Single(tracker.Update([owl], [editor], out var moves)).Path);
        var move = Assert.Single(moves);
        Assert.Equal((GlobalLog, owlLog), (move.From, move.To));
        Assert.Same(owl, move.Project);
        Assert.Equal([10], move.Writers.Select(w => w.Process.Pid));

        // It keeps to it, even with the file gone for now, and says so once.
        File.Delete(owlLog);
        Assert.Equal(owlLog, Assert.Single(tracker.Update([owl], [editor], out moves)).Path);
        Assert.Empty(moves);

        // One that was writing it when first seen has nothing to move from.
        File.WriteAllText(owlLog, "Unity Editor version: 6000.5.0f1\n");
        File.SetLastWriteTimeUtc(owlLog, Start.UtcDateTime.AddMinutes(1));
        var fresh = new EditorLogTracker(TimeZoneInfo.Utc, ToolOSExtensions.Current);
        Assert.Equal(owlLog, Assert.Single(fresh.Update([owl], [editor], out moves)).Path);
        Assert.Empty(moves);
    }

    // ---- Godot --------------------------------------------------------------------------------------------------------

    private static readonly ProjectToolPaths Paths = new(@"C:\Users\owl", AppData: @"C:\Users\owl\AppData\Roaming");

    private static GodotProject Godot(string settings = "") =>
        GodotProject.Parse($"""
            config_version=5

            [application]

            config/name="Night: Owl"
            {settings}
            """, @"D:\Games\Owl\project.godot", @"D:\Games\Owl", hasCSharpFiles: false);

    [Fact]
    public void A_godot_projects_log_is_in_its_user_data_folder_unless_its_settings_move_it()
    {
        Assert.Equal(@"C:\Users\owl\AppData\Roaming\Godot\app_userdata\Night_ Owl\logs\godot.log", GodotLogs.LogFile(Godot(), Paths, ToolOS.Windows));
        var unix = new ProjectToolPaths("/home/owl");
        Assert.Equal("/home/owl/.local/share/godot/app_userdata/Night_ Owl/logs/godot.log", GodotLogs.LogFile(Godot(), unix, ToolOS.Linux));
        Assert.Equal("/home/owl/Library/Application Support/Godot/app_userdata/Night_ Owl/logs/godot.log", GodotLogs.LogFile(Godot(), unix, ToolOS.MacOS));

        var custom = Godot("""
            config/use_custom_user_dir=true
            config/custom_user_dir_name="OwlGame"
            """);
        Assert.Equal(@"C:\Users\owl\AppData\Roaming\OwlGame\logs\godot.log", GodotLogs.LogFile(custom, Paths, ToolOS.Windows));

        var moved = GodotProject.Parse("""
            [debug]

            file_logging/log_path="res://logs/owl.log"
            """, @"D:\Games\Owl\project.godot", @"D:\Games\Owl", hasCSharpFiles: false);
        Assert.Equal(@"D:\Games\Owl\logs\owl.log", GodotLogs.LogFile(moved, Paths, ToolOS.Windows));

        var off = GodotProject.Parse("""
            [debug]

            file_logging/enable_file_logging.pc=false
            """, @"D:\Games\Owl\project.godot", @"D:\Games\Owl", hasCSharpFiles: false);
        Assert.False(off.FileLogging);
        Assert.Null(GodotLogs.LogFile(off, Paths, ToolOS.Windows));
    }

    [Fact]
    public void A_godot_process_with_the_project_says_what_it_is_and_writes_the_projects_log_or_its_own()
    {
        var project = Godot();
        const string log = @"C:\Logs\godot.log";
        EditorLogWriter? Writer(string arguments) => GodotLogs.Writer(Process(1, "Godot_v4.3-stable_win64", $@"C:\Godot\Godot_v4.3-stable_win64.exe {arguments}"), project, log, ToolOS.Windows);

        Assert.Equal("Godot editor", Writer(@"--editor --path D:\Games\Owl")!.Role);
        Assert.Equal("Godot editor", Writer(@"-e --path ""D:/Games/Owl/""")!.Role);
        Assert.Equal("Godot game", Writer(@"--path D:\Games\Owl --remote-debug tcp://127.0.0.1:6007")!.Role);
        Assert.Equal("Godot (headless)", Writer(@"--headless --path D:\Games\Owl --quit")!.Role);
        Assert.Equal([log], Writer(@"--path D:\Games\Owl")!.LogFiles);
        Assert.Equal([@"D:\Logs\run.log"], Writer(@"--path D:\Games\Owl --log-file D:\Logs\run.log")!.LogFiles);
        Assert.Null(Writer(@"--path D:\Games\Other"));
        Assert.Null(Writer(@"--project-manager --path D:\Games\Owl"));
        Assert.Null(GodotLogs.Writer(Process(1, "godot", @"godot --path D:\Games\Owl"), project, null, ToolOS.Windows));

        // A relative --path, from the project's folder or beside it.
        var here = Process(1, "godot", "godot --headless --path . --quit");
        Assert.Equal("Godot (headless)", GodotLogs.Writer(here with { WorkingFolder = @"D:\Games\Owl" }, project, log, ToolOS.Windows)!.Role);
        Assert.NotNull(GodotLogs.Writer(here with { CommandLine = @"godot --path ..\Owl", WorkingFolder = @"D:\Games\Tools" }, project, log, ToolOS.Windows));
        Assert.Null(GodotLogs.Writer(here with { WorkingFolder = @"D:\Games\Other" }, project, log, ToolOS.Windows));
        Assert.Null(GodotLogs.Writer(here, project, log, ToolOS.Windows));
    }

    [Theory]
    [InlineData("ERROR: Condition \"p_node == nullptr\" is true.", LogLineLevel.Error)]
    [InlineData("SCRIPT ERROR: Invalid call. Nonexistent function 'jump'.", LogLineLevel.Error)]
    [InlineData("USER WARNING: Health is low", LogLineLevel.Warning)]
    [InlineData("WARNING: ObjectDB instances leaked at exit", LogLineLevel.Warning)]
    [InlineData("   at: get_node (scene/main/node.cpp:1364)", LogLineLevel.Normal)]
    public void A_godot_line_is_an_error_or_a_warning(string line, LogLineLevel level) => Assert.Equal(level, GodotLogs.Classify(line));

    [Fact]
    public void A_godot_crash_is_its_crash_handler()
    {
        Assert.True(GodotLogs.IsCrash("CrashHandlerException: Program crashed"));
        Assert.True(GodotLogs.IsCrash("handle_crash: Program crashed with signal 11"));
        Assert.False(GodotLogs.IsCrash("ERROR: Something"));
    }

    // ---- Which logs are being written ---------------------------------------------------------------------------------

    private FollowedProject UnrealProject(out string logs)
    {
        logs = _temp.Combine("Saved", "Logs");
        Directory.CreateDirectory(logs);
        var uproject = _temp.Combine("NightOwl.uproject");
        return new FollowedProject(uproject, _temp.Path, UnrealLogs.Source(new UnrealProject(uproject, "NightOwl", _temp.Path, null, "NightOwlEditor", true, [], []), ToolOSExtensions.Current));
    }

    private static void WriteLog(string path, DateTimeOffset opened) =>
        File.WriteAllText(path, $"\uFEFFLog file open, {opened.UtcDateTime.ToString("MM/dd/yy HH:mm:ss", CultureInfo.InvariantCulture)}\nLogInit: Display: Starting\n");

    [Fact]
    public void Each_process_writes_the_log_opened_soonest_after_it_started_that_no_other_has()
    {
        var project = UnrealProject(out var logs);
        var uproject = project.ProjectPath;
        var tracker = new EditorLogTracker(TimeZoneInfo.Utc, ToolOSExtensions.Current);
        var main = Path.Combine(logs, "NightOwl.log");
        var second = Path.Combine(logs, "NightOwl_2.log");
        // A commandlet from yesterday left its log; the editor's opened a moment after it started.
        WriteLog(second, Start.AddDays(-1));
        WriteLog(main, Start.AddSeconds(2));
        var editor = Process(10, "UnrealEditor", $"UnrealEditor.exe \"{uproject}\"", Start);
        var commandlet = Process(20, "UnrealEditor-Cmd", $"UnrealEditor-Cmd.exe \"{uproject}\" -run=Cook", Start.AddMinutes(30));
        var other = Process(30, "UnrealEditor", "UnrealEditor.exe D:/Other/Other.uproject", Start);

        var live = Assert.Single(tracker.Update([project], [editor, other]));
        Assert.Equal(main, live.Path);
        Assert.Same(project, live.Project);
        Assert.Equal("Unreal editor", Assert.Single(live.Writers).Role);

        // A commandlet whose log isn't there yet is looked at again next time; yesterday's isn't its.
        Assert.Equal([main], tracker.Update([project], [editor, commandlet]).Select(l => l.Path));

        WriteLog(second, Start.AddMinutes(30).AddSeconds(1));
        var both = tracker.Update([project], [commandlet, editor]);
        Assert.Equal([main, second], both.Select(l => l.Path));
        Assert.Equal("Unreal commandlet Cook", Assert.Single(both[1].Writers).Role);

        // Each keeps its file while it runs, and one that's gone writes none.
        Assert.Equal([second], tracker.Update([project], [commandlet]).Select(l => l.Path));
        Assert.Empty(tracker.Update([project], []));
        Assert.Empty(tracker.Update([], [editor]));
    }

    [Fact]
    public void Without_a_start_time_to_go_by_a_process_takes_the_first_log_that_is_there()
    {
        var project = UnrealProject(out var logs);
        var tracker = new EditorLogTracker(TimeZoneInfo.Utc, ToolOSExtensions.Current);
        WriteLog(Path.Combine(logs, "NightOwl_2.log"), Start);
        var editor = new SystemProcess(10, "UnrealEditor", $"UnrealEditor.exe \"{project.ProjectPath}\"");

        Assert.Equal(Path.Combine(logs, "NightOwl_2.log"), Assert.Single(tracker.Update([project], [editor])).Path);
    }

    [Fact]
    public void A_log_written_by_two_processes_is_one_log()
    {
        var root = _temp.CreateFolder("owl");
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5\n");
        var project = GodotProject.Read(Path.Combine(root, "project.godot"));
        var followed = new FollowedProject(project.File, root, GodotLogs.Source(project, new ProjectToolPaths(_temp.Combine("home"), _temp.Combine("appdata")), ToolOSExtensions.Current));
        var tracker = new EditorLogTracker(TimeZoneInfo.Utc, ToolOSExtensions.Current);
        var editor = Process(1, "godot", $"godot --editor --path \"{root}\"");
        var game = Process(2, "godot", $"godot --path \"{root}\"", Start.AddMinutes(1));

        var live = Assert.Single(tracker.Update([followed], [editor, game]));

        Assert.Equal(["Godot editor", "Godot game"], live.Writers.Select(w => w.Role));
        Assert.EndsWith(Path.Combine("logs", "godot.log"), live.Path, StringComparison.Ordinal);
    }
}
