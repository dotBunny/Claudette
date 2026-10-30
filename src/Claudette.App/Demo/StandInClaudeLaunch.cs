using System.Text;
using Claudette.Core.Demo;

namespace Claudette.App.Demo;

/// <summary>
/// Claudette run as its demo's <c>claude</c> (DESIGN.md §11, "Demo mode"): <c>Program.Main</c> runs this before anything
/// else when <see cref="StandInClaude.Variable"/> is set, so it opens no window and hands nothing to a running Claudette.
/// </summary>
internal static class StandInClaudeLaunch
{
    public static bool IsStandIn => Environment.GetEnvironmentVariable(StandInClaude.Variable) == "1";

    public static int Run(string[] args)
    {
        // UTF-8 both ways, as Claude Code's stream-json is: a console's code page would mangle the replies' dashes.
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var input = new StreamReader(Console.OpenStandardInput(), utf8);
        using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = false };
        var config = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } directory ? directory : null;
        var standIn = new StandInClaude(input, output, TimeProvider.System, config, Environment.CurrentDirectory);
        return standIn.RunAsync(args).GetAwaiter().GetResult();
    }
}
