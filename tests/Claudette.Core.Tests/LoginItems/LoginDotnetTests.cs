using System.Runtime.InteropServices;
using Claudette.Core.LoginItems;

namespace Claudette.Core.Tests.LoginItems;

/// <summary>
/// The .NET a login entry on macOS or Linux starts a build with, when the build's own program wouldn't find it at login
/// (DESIGN.md §9, "Starting at login").
/// </summary>
public sealed class LoginDotnetTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("claudette-dotnet-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_runtime_is_in_the_dotnet_folder_three_folders_up()
    {
        var dotnet = Path.Combine(_root, ".dotnet");
        var runtime = Directory.CreateDirectory(Path.Combine(dotnet, "shared", "Microsoft.NETCore.App", "10.0.11")).FullName;

        Assert.Equal(dotnet, LoginDotnet.RootOf(runtime + Path.DirectorySeparatorChar));
        Assert.Equal(dotnet, LoginDotnet.RootOf(runtime));
    }

    [Fact]
    public void A_self_contained_build_has_no_dotnet_folder()
    {
        Assert.Null(LoginDotnet.RootOf(Path.Combine(_root, "Claudette")));
        Assert.Null(LoginDotnet.RootOf(Path.Combine(_root, "shared", "Other.App", "10.0.11")));
    }

    [Fact]
    public void A_dotnet_where_the_program_looks_is_found_by_it()
    {
        var runtime = Runtime(Path.Combine(_root, "share", "dotnet"));

        var dotnet = LoginDotnet.Of(null, runtime, Path.Combine(_root, "share", "dotnet") + Path.DirectorySeparatorChar);

        Assert.Equal(new LoginDotnet(Path.Combine(_root, "share", "dotnet", "dotnet"), FoundByProgram: true), dotnet);
    }

    [Fact]
    public void A_dotnet_in_the_home_folder_is_started_with_its_own_dotnet()
    {
        var runtime = Runtime(Path.Combine(_root, ".dotnet"));

        var dotnet = LoginDotnet.Of(null, runtime, "/usr/share/dotnet");

        Assert.Equal(new LoginDotnet(Path.Combine(_root, ".dotnet", "dotnet"), FoundByProgram: false), dotnet);
    }

    [Fact]
    public void Without_a_dotnet_to_start_a_build_with_the_program_is_the_best_there_is()
    {
        var runtime = Directory.CreateDirectory(Path.Combine(_root, ".dotnet", "shared", "Microsoft.NETCore.App", "10.0.11")).FullName;

        Assert.Equal(new LoginDotnet("dotnet", FoundByProgram: true), LoginDotnet.Of(null, runtime, "/usr/share/dotnet"));
        Assert.Equal(new LoginDotnet("dotnet", FoundByProgram: true), LoginDotnet.Of(null, Path.Combine(_root, "Claudette"), "/usr/share/dotnet"));
    }

    [Theory]
    [InlineData(false, "/usr/share/dotnet")]
    [InlineData(true, "/usr/local/share/dotnet")]
    public void The_program_looks_in_the_usual_folder_when_nothing_is_registered(bool macOS, string root)
    {
        Assert.Equal(root, LoginDotnet.ProgramRoot(_ => null, Architecture.X64, macOS));
    }

    [Fact]
    public void A_registered_dotnet_comes_first_and_the_processor_s_own_before_the_rest()
    {
        var files = new Dictionary<string, string>
        {
            ["/etc/dotnet/install_location"] = "/opt/dotnet",
            ["/etc/dotnet/install_location_arm64"] = "/opt/dotnet-arm64",
        };

        Assert.Equal("/opt/dotnet-arm64", LoginDotnet.ProgramRoot(files.GetValueOrDefault, Architecture.Arm64, macOS: false));
        Assert.Equal("/opt/dotnet", LoginDotnet.ProgramRoot(files.GetValueOrDefault, Architecture.X64, macOS: false));
    }

    [Fact]
    public void The_dotnet_running_the_tests_is_found()
    {
        var root = LoginDotnet.RootOf(RuntimeEnvironment.GetRuntimeDirectory());

        Assert.NotNull(root);
        Assert.True(File.Exists(Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")), root);
    }

    /// <summary>A .NET's runtime folder in <paramref name="root"/>, with the <c>dotnet</c> beside it.</summary>
    private static string Runtime(string root)
    {
        var runtime = Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.NETCore.App", "10.0.11")).FullName;
        File.WriteAllText(Path.Combine(root, "dotnet"), "");
        return runtime;
    }
}
