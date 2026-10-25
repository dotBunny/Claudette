using System.Xml;
using System.Xml.Linq;
using Claudette.Core.LoginItems;
using Claudette.Core.Updates;
using Claudette.Platform.LoginItems.Mac;
using Claudette.Platform.Tests.Support;

namespace Claudette.Platform.Tests.LoginItems;

/// <summary>
/// The LaunchAgent that starts Claudette at login on macOS (DESIGN.md §9, "Starting at login"), written to a temporary
/// folder. Only file work, so it runs on every OS.
/// </summary>
public sealed class LaunchAgentLoginItemsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("claudette-launchagent-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string LaunchAgents => Path.Combine(_root, "LaunchAgents");

    /// <summary>A .NET a build's program finds by itself, so the agent doesn't depend on where this machine's is.</summary>
    private static readonly LoginDotnet UsualDotnet = new("/usr/local/share/dotnet/dotnet", FoundByProgram: true);

    [Fact]
    public void The_agent_opens_the_app_with_login_once_at_login()
    {
        var items = new LaunchAgentLoginItems(new FakeLauncher(), LaunchAgents, UsualDotnet);
        Assert.Equal(LoginEntryState.Missing, items.ReadEntry());

        items.WriteEntry(new ClaudetteCopy(AppInstallKind.MacApp, "/Applications/R&D/Claudette.app", "0.3.0"));

        Assert.Equal(Path.Combine(LaunchAgents, "com.dotbunny.claudette.login.plist"), items.AgentPath);
        Assert.Equal(LoginEntryState.Enabled, items.ReadEntry());
        var agent = ReadPlist(items.AgentPath);
        Assert.Equal("com.dotbunny.claudette.login", agent["Label"].Value);
        Assert.Equal(["/usr/bin/open", "-a", "/Applications/R&D/Claudette.app", "--args", "--login"], agent["ProgramArguments"].Elements("string").Select(s => s.Value));
        Assert.Equal("true", agent["RunAtLoad"].Name.LocalName);
        // What the agent starts outlives it: a source build's copy, for one.
        Assert.Equal("true", agent["AbandonProcessGroup"].Name.LocalName);
        Assert.Equal("Aqua", agent["LimitLoadToSessionType"].Value);

        items.DeleteEntry();

        Assert.Equal(LoginEntryState.Missing, items.ReadEntry());
    }

    [Fact]
    public void A_source_build_is_started_from_its_build_output()
    {
        var build = Directory.CreateDirectory(Path.Combine(_root, "bin")).FullName;
        File.WriteAllText(Path.Combine(build, "Claudette"), "");
        var items = new LaunchAgentLoginItems(new FakeLauncher(), LaunchAgents, UsualDotnet);

        items.WriteEntry(new ClaudetteCopy(AppInstallKind.SourceBuild, build, "0.3.0"));

        var agent = ReadPlist(items.AgentPath);
        Assert.Equal([Path.Combine(build, "Claudette"), "--login"], agent["ProgramArguments"].Elements("string").Select(s => s.Value));
    }

    [Fact]
    public void A_build_on_a_dotnet_its_program_wouldnt_find_is_started_with_that_dotnet()
    {
        var build = Directory.CreateDirectory(Path.Combine(_root, "bin")).FullName;
        File.WriteAllText(Path.Combine(build, "Claudette"), "");
        var items = new LaunchAgentLoginItems(new FakeLauncher(), LaunchAgents, new LoginDotnet("/Users/me/.dotnet/dotnet", FoundByProgram: false));

        items.WriteEntry(new ClaudetteCopy(AppInstallKind.SourceBuild, build, "0.3.0"));

        var agent = ReadPlist(items.AgentPath);
        Assert.Equal(["/Users/me/.dotnet/dotnet", Path.Combine(build, "Claudette.dll"), "--login"], agent["ProgramArguments"].Elements("string").Select(s => s.Value));
    }

    /// <summary>The plist's dictionary, each key's value element by the key's name.</summary>
    private static Dictionary<string, XElement> ReadPlist(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        var dict = XDocument.Load(reader).Root!.Element("dict")!.Elements().ToList();
        var entries = new Dictionary<string, XElement>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < dict.Count; i += 2)
        {
            entries[dict[i].Value] = dict[i + 1];
        }
        return entries;
    }
}
