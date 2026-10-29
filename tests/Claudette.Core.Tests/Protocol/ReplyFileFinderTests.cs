using Claudette.Core.Composer;
using Claudette.Core.Git;
using Claudette.Core.Protocol;
using Claudette.Core.Tests.Support;

namespace Claudette.Core.Tests.Protocol;

/// <summary>Where the file a reply names is (DESIGN.md §18, "Files named in Claude's replies").</summary>
public sealed class ReplyFileFinderTests : IDisposable
{
    private readonly TempFolder _root = new("claudette-replyfiles");
    private readonly List<string> _extraFolders = [];

    public void Dispose() => _root.Dispose();

    private string Folder => _root.CreateFolder("project");

    private string Home => _root.CreateFolder("home");

    private ReplyFileFinder Finder(ProjectFileIndex? index = null) => new(() => Folder, () => _extraFolders, Home, () => index);

    /// <summary>The project's files as the walk lists them, without git.</summary>
    private ProjectFileIndex Index() =>
        new(Folder, new GitWorkingTree(new IsolatedGitLauncher(_root.Path), TimeProvider.System, $"claudette-no-such-git-{Guid.NewGuid():N}"), TimeProvider.System);

    private static ReplyFileReference Code(string code) => ReplyFileReference.FromCode(code)!;

    private Task<IReadOnlyList<string>> FindAsync(ReplyFileFinder finder, string code, IReadOnlyList<string>? sessionFiles = null, bool clicked = false) =>
        finder.FindAsync(Code(code), sessionFiles ?? [], clicked, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_path_is_read_against_the_tabs_folder_then_its_extra_folders()
    {
        var inFolder = _root.Write("project/docs/specs/weapons.md", "");
        var inExtra = _root.Write("shared/docs/art.md", "");
        _extraFolders.Add(_root.Combine("shared"));

        Assert.Equal([inFolder], await FindAsync(Finder(), "docs/specs/weapons.md:42"));
        Assert.Equal([inFolder], await FindAsync(Finder(), @"docs\specs\weapons.md"));
        Assert.Equal([inExtra], await FindAsync(Finder(), "docs/art.md"));
        Assert.Equal([_root.Combine("project", "docs")], await FindAsync(Finder(), "docs/"));
        Assert.Empty(await FindAsync(Finder(), "docs/missing.md"));
    }

    [Fact]
    public async Task A_name_is_the_file_the_session_worked_on_last_before_the_projects()
    {
        var spec = _root.Write("project/docs/specs/weapons.md", "");
        var older = _root.Write("project/Plugins/Combat/weapons.md", "");
        _root.Write("project/weapons.md.bak", "");

        Assert.Equal([spec, older], await FindAsync(Finder(Index()), "weapons.md", [older, spec]));
        Assert.Equal([spec], await FindAsync(Finder(Index()), "specs/weapons.md", [older, spec]));
        // A file the session named that's gone isn't offered.
        File.Delete(older);
        Assert.Equal([spec], await FindAsync(Finder(Index()), "weapons.md", [older, spec]));
    }

    [Fact]
    public async Task A_name_the_session_didnt_work_on_is_every_file_in_the_project_with_it_the_shallowest_first()
    {
        var deep = _root.Write("project/Plugins/Combat/Docs/weapons.md", "");
        var spec = _root.Write("project/docs/weapons.md", "");
        _root.Write("project/docs/old-weapons.md", "");

        Assert.Equal([spec, deep], await FindAsync(Finder(Index()), "weapons.md"));
        Assert.Equal([spec], await FindAsync(Finder(Index()), "docs/weapons.md"));
        Assert.Empty(await FindAsync(Finder(Index()), "shots.md"));
        // Without a listing, only the session's files are looked in.
        Assert.Empty(await FindAsync(Finder(), "weapons.md"));
    }

    [Fact]
    public async Task A_path_from_home_is_the_users()
    {
        var settings = _root.Write("home/.claude/settings.json", "{}");

        Assert.Equal([settings], await FindAsync(Finder(), "~/.claude/settings.json"));
    }

    [Fact]
    public async Task A_path_from_an_environment_variable_is_read_with_the_users()
    {
        var shot = _root.Write("home/temp/shot.png", "");
        var temp = _root.Combine("home", "temp");
        var finder = new ReplyFileFinder(() => Folder, () => _extraFolders, Home, () => null, name => name == "SHOTS" ? temp : null);

        Assert.Equal([shot], await FindAsync(finder, @"%SHOTS%\shot.png"));
        Assert.Equal([shot], await FindAsync(finder, @"$env:SHOTS\shot.png"));
        Assert.Equal([shot], await FindAsync(finder, "${SHOTS}/shot.png"));
        Assert.Equal([shot], await FindAsync(finder, "$SHOTS/shot.png"));
        // $HOME is the user's home, where it isn't set, as on Windows.
        Assert.Equal([shot], await FindAsync(finder, "$HOME/temp/shot.png"));
        // One that isn't set stays as it's written, which no file here is.
        Assert.Empty(await FindAsync(finder, "%NOT_SET%/shot.png"));
    }

    [Fact]
    public void Variables_are_filled_in_in_each_shells_way_and_one_that_isnt_set_stays()
    {
        static string? Variable(string name) => name switch
        {
            "TEMP" => @"C:\Temp",
            "ProgramFiles(x86)" => @"C:\Program Files (x86)",
            _ => null,
        };

        Assert.Equal(@"C:\Temp\shot.png", ReplyFileFinder.ExpandVariables(@"%TEMP%\shot.png", Variable));
        Assert.Equal(@"C:\Temp\shot.png", ReplyFileFinder.ExpandVariables(@"$env:TEMP\shot.png", Variable));
        Assert.Equal(@"C:\Temp\shot.png", ReplyFileFinder.ExpandVariables(@"$Env:TEMP\shot.png", Variable));
        Assert.Equal(@"C:\Temp/shot.png", ReplyFileFinder.ExpandVariables("${TEMP}/shot.png", Variable));
        Assert.Equal(@"C:\Temp/shot.png", ReplyFileFinder.ExpandVariables("$TEMP/shot.png", Variable));
        Assert.Equal(@"C:\Program Files (x86)\Epic Games", ReplyFileFinder.ExpandVariables(@"%ProgramFiles(x86)%\Epic Games", Variable));
        Assert.Equal(@"%NOT_SET%\a.txt", ReplyFileFinder.ExpandVariables(@"%NOT_SET%\a.txt", Variable));
        Assert.Equal("100% off/$5 deal.txt", ReplyFileFinder.ExpandVariables("100% off/$5 deal.txt", Variable));
    }

    [Fact]
    public void Git_Bashs_paths_are_Windows_ones()
    {
        Assert.Equal(@"C:\Work/Game/shot.png", ReplyFileFinder.FromGitBash("/c/Work/Game/shot.png", @"C:\Temp"));
        Assert.Equal(@"D:\", ReplyFileFinder.FromGitBash("/d", @"C:\Temp"));
        Assert.Equal(@"C:\Temp/shot.png", ReplyFileFinder.FromGitBash("/tmp/shot.png", @"C:\Temp"));
        // Anything else is as it was.
        Assert.Equal("/usr/share/doc", ReplyFileFinder.FromGitBash("/usr/share/doc", @"C:\Temp"));
        Assert.Equal("/tmpfiles/a.txt", ReplyFileFinder.FromGitBash("/tmpfiles/a.txt", @"C:\Temp"));
        Assert.Equal("src/a.cs", ReplyFileFinder.FromGitBash("src/a.cs", @"C:\Temp"));
    }

    [Fact]
    public async Task Before_a_click_a_path_elsewhere_is_looked_at_only_if_the_session_named_it()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "On Windows a path on the computer's own drives may be looked at.");
        var elsewhere = _root.Write("elsewhere/notes.md", "");

        Assert.Empty(await FindAsync(Finder(), elsewhere));
        Assert.Equal([elsewhere], await FindAsync(Finder(), elsewhere, [elsewhere]));
        Assert.Equal([elsewhere], await FindAsync(Finder(), elsewhere, clicked: true));
    }

    [Fact]
    public async Task A_network_path_is_never_looked_at()
    {
        var reference = new ReplyFileReference(@"\\server\share\weapons.md", @"\\server\share\weapons.md");

        Assert.Empty(await Finder().FindAsync(reference, [@"\\server\share\weapons.md"], clicked: true, TestContext.Current.CancellationToken));
    }
}
