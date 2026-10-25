using Claudette.Core.Updates;

namespace Claudette.Core.LoginItems;

/// <summary>The program and arguments a login entry runs to start a copy of Claudette (DESIGN.md §9, "Starting at login").</summary>
public sealed record LoginCommand(string Program, IReadOnlyList<string> Arguments)
{
    /// <summary>Tells Claudette it was started at login: it opens minimized, or hands over to a better copy.</summary>
    public const string LoginOption = "--login";

    /// <summary>
    /// The command that starts <paramref name="copy"/> at login: <c>open -a</c> for <c>Claudette.app</c>, so macOS starts
    /// it as an app; the program in a build's folder; or <c>dotnet Claudette.dll</c> for a build without one, or whose
    /// program wouldn't find .NET. Null for the MSIX, which only its own startup task starts.
    /// </summary>
    /// <param name="windows">
    /// Whether the program is <c>Claudette.exe</c> rather than <c>Claudette</c>. On Windows a build's program is used
    /// whenever it has one: <c>dotnet.exe</c> is a console program, so it would open a console window at login.
    /// </param>
    /// <param name="dotnetHost">The <c>dotnet</c> that runs a build without a program of its own.</param>
    /// <param name="programFindsDotnet">
    /// Whether a build's program finds the .NET <paramref name="dotnetHost"/> belongs to at login, with none of a
    /// terminal's environment (<see cref="LoginDotnet"/>). On macOS and Linux, a build is started with
    /// <paramref name="dotnetHost"/> when it doesn't.
    /// </param>
    public static LoginCommand? For(ClaudetteCopy copy, bool windows, string dotnetHost, bool programFindsDotnet = true)
    {
        switch (copy.Kind)
        {
            case AppInstallKind.Msix:
                return null;
            case AppInstallKind.MacApp:
                return new LoginCommand("/usr/bin/open", ["-a", copy.Location, "--args", LoginOption]);
        }
        var program = Path.Combine(copy.Location, windows ? ClaudetteCopy.AssemblyName + ".exe" : ClaudetteCopy.AssemblyName);
        return File.Exists(program) && (windows || programFindsDotnet)
            ? new LoginCommand(program, [LoginOption])
            : new LoginCommand(dotnetHost, [Path.Combine(copy.Location, ClaudetteCopy.AssemblyName + ".dll"), LoginOption]);
    }

    /// <summary>
    /// The <c>dotnet</c> to run a build with: the one running this Claudette when it was started that way, otherwise the
    /// one on <c>PATH</c>.
    /// </summary>
    public static string DotnetHost(string? processPath) =>
        processPath is not null && Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? processPath : "dotnet";
}
