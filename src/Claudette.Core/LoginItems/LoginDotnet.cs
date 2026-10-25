using System.Runtime.InteropServices;

namespace Claudette.Core.LoginItems;

/// <summary>
/// The .NET a login entry on macOS or Linux starts a build of Claudette with (DESIGN.md §9, "Starting at login"). A
/// build's own program finds .NET where <c>DOTNET_ROOT</c> says, which nothing sets at login, else in the folder
/// <c>/etc/dotnet/install_location</c> names, else in the OS's usual folder. So when Claudette runs on a .NET elsewhere,
/// such as the one Microsoft's install script puts in <c>~/.dotnet</c>, the entry starts the build with that .NET's own
/// <c>dotnet</c> instead.
/// </summary>
/// <param name="Host">The <c>dotnet</c> to start a build with: the one of the .NET this Claudette runs on, else the one running it, else <c>dotnet</c> on the <c>PATH</c>.</param>
/// <param name="FoundByProgram">Whether a build's own program finds that .NET by itself.</param>
public sealed record LoginDotnet(string Host, bool FoundByProgram)
{
    /// <summary>The file that registers a .NET for programs to find, with <c>_x64</c> or the like for one processor's.</summary>
    public const string InstallLocationFile = "/etc/dotnet/install_location";

    /// <summary>The .NET this Claudette runs on.</summary>
    public static LoginDotnet Current() => Of(
        Environment.ProcessPath,
        RuntimeEnvironment.GetRuntimeDirectory(),
        ProgramRoot(FirstLine, RuntimeInformation.ProcessArchitecture, OperatingSystem.IsMacOS()));

    /// <summary>
    /// The .NET whose runtime is in <paramref name="runtimeDirectory"/>. A build's program finds it when it's in
    /// <paramref name="programRoot"/>, and also when there's no <c>dotnet</c> of its own to start a build with instead.
    /// </summary>
    public static LoginDotnet Of(string? processPath, string runtimeDirectory, string programRoot)
    {
        if (RootOf(runtimeDirectory) is not { } root || !File.Exists(Path.Combine(root, "dotnet")))
        {
            // A self-contained build carries its own .NET, and without a dotnet to start a build with, its program is
            // the best there is.
            return new LoginDotnet(LoginCommand.DotnetHost(processPath), FoundByProgram: true);
        }
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return new LoginDotnet(Path.Combine(root, "dotnet"), string.Equals(Trim(root), Trim(programRoot), comparison));
    }

    /// <summary>
    /// The .NET folder a runtime is in: <c>&lt;root&gt;/shared/Microsoft.NETCore.App/&lt;version&gt;</c>. Null for a
    /// self-contained build, whose runtime is in its own folder.
    /// </summary>
    public static string? RootOf(string runtimeDirectory) =>
        new DirectoryInfo(Trim(runtimeDirectory)).Parent is { Name: "Microsoft.NETCore.App", Parent: { Name: "shared", Parent: { } root } }
            ? root.FullName
            : null;

    /// <summary>
    /// Where a build's own program looks for .NET with nothing in its environment, as .NET's own launcher does: the
    /// folder that <c>/etc/dotnet/install_location_&lt;processor&gt;</c> names, else <c>/etc/dotnet/install_location</c>,
    /// else <c>/usr/local/share/dotnet</c> on macOS and <c>/usr/share/dotnet</c> on Linux.
    /// </summary>
    /// <param name="firstLine">A file's first line, or null when it isn't there or is empty.</param>
    public static string ProgramRoot(Func<string, string?> firstLine, Architecture processor, bool macOS) =>
        firstLine($"{InstallLocationFile}_{processor.ToString().ToLowerInvariant()}")
        ?? firstLine(InstallLocationFile)
        ?? (macOS ? "/usr/local/share/dotnet" : "/usr/share/dotnet");

    private static string? FirstLine(string path)
    {
        try
        {
            return File.Exists(path) && File.ReadLines(path).FirstOrDefault()?.Trim() is { Length: > 0 } line ? line : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Trim(string path) => Path.TrimEndingDirectorySeparator(path);
}
