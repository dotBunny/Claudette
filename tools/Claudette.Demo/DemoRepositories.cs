using System.Diagnostics;
using Claudette.Core.Demo;

namespace Claudette.Demo;

/// <summary>
/// The demo's projects as git repositories, for the screenshots: Changed files, diffs and the Files tab show Claude's
/// changes in their working trees. The app's demo has none (<see cref="DemoContent.EmptyFolder"/>).
/// </summary>
internal static class DemoRepositories
{
    /// <summary>
    /// A git repository with the project's files committed on main, then its branch checked out with Claude's changes
    /// written over it and left uncommitted, as Claude left it.
    /// </summary>
    public static string Make(string projects, DemoProject project)
    {
        var root = Path.Combine(projects, project.Name);
        Directory.CreateDirectory(root);
        Git(root, "init", "-q", "-b", "main");
        WriteFiles(root, project.Before);
        Git(root, "add", "-A");
        Git(root, "-c", "user.name=Demo", "-c", "user.email=demo@example.com", "commit", "-q", "-m", "Initial commit");
        if (project.Branch != "main")
        {
            Git(root, "checkout", "-q", "-b", project.Branch);
        }
        WriteFiles(root, project.After);
        return root;
    }

    private static void WriteFiles(string root, IReadOnlyDictionary<string, string> files)
    {
        foreach (var (name, text) in files)
        {
            var path = Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text.ReplaceLineEndings("\n"));
        }
    }

    private static void Git(string folder, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true };
        // Line endings as written: the files' "before" in the transcripts has to match what's on disk.
        foreach (var arg in (string[])["-c", "core.autocrlf=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }
        using var git = Process.Start(start) ?? throw new InvalidOperationException("git didn't start.");
        var error = git.StandardError.ReadToEnd();
        git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {error.Trim()}");
        }
    }
}
