using Claudette.Core.Development;
using Claudette.Core.Git;
using Claudette.Core.Processes;
using Claudette.Core.Tests.Support;

namespace Claudette.Core.Tests.Development;

/// <summary>
/// A source build's checkout against the branch it tracks (DESIGN.md §9, "New commits upstream"): against the real git,
/// isolated from the user's and system config, with a bare repository in a temporary folder as the remote.
/// </summary>
public sealed class SourceCheckoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"claudette-checkout-{Guid.NewGuid():N}");
    private readonly string _upstream;
    private readonly string _author;
    private readonly string _checkout;
    private readonly IsolatedGitLauncher _launcher;
    private readonly SourceCheckout _source;

    public SourceCheckoutTests()
    {
        Directory.CreateDirectory(_root);
        _upstream = Path.Combine(_root, "upstream.git");
        _author = Path.Combine(_root, "author");
        _checkout = Path.Combine(_root, "checkout");
        _launcher = new IsolatedGitLauncher(_root);
        _source = new SourceCheckout(new GitWorkingTree(_launcher, TimeProvider.System), _launcher, TimeProvider.System);
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(_root, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The remote with one commit, a clone that pushes to it, and the checkout, cloned from it.</summary>
    private async Task CloneAsync()
    {
        await _launcher.RunAsync(_root, "init", "-q", "--bare", "--initial-branch=main", _upstream);
        await _launcher.RunAsync(_root, "clone", "-q", _upstream, _author);
        await File.WriteAllTextAsync(Path.Combine(_author, "README.md"), "Claudette\n", Token);
        await _launcher.RunAsync(_author, "add", "README.md");
        await CommitAsync(_author, "First");
        await _launcher.RunAsync(_author, "push", "-q", "origin", "HEAD:main");
        await _launcher.RunAsync(_root, "clone", "-q", _upstream, _checkout);
    }

    private Task CommitAsync(string folder, string subject) =>
        _launcher.RunAsync(folder, "-c", "user.email=ada@example.com", "-c", "user.name=Ada", "commit", "-q", "--allow-empty", "-m", subject);

    private async Task PushAsync(params string[] subjects)
    {
        foreach (var subject in subjects)
        {
            await CommitAsync(_author, subject);
        }
        await _launcher.RunAsync(_author, "push", "-q", "origin", "HEAD:main");
    }

    private async Task<string> HeadAsync(string folder)
    {
        var result = await ProcessRunner.RunAsync(_launcher, new ProcessStartSpec("git", ["rev-parse", "HEAD"]) { WorkingDirectory = folder }, Waiting.Timeout,
            TimeProvider.System, Token);
        return result.StandardOutput.Trim();
    }

    [Fact]
    public async Task New_commits_upstream_are_listed_newest_first_and_pulled_as_a_fast_forward()
    {
        Assert.SkipWhen(!IsolatedGitLauncher.GitInstalled, "git isn't on PATH.");
        await CloneAsync();
        await PushAsync("Second", "Third");

        var status = Assert.IsType<UpstreamStatus>((await _source.CheckAsync(_checkout, Token)).Status);

        Assert.Equal(("main", "origin/main", 2, 0, false), (status.Branch, status.Upstream, status.Behind, status.Ahead, status.HasChanges));
        Assert.True(status.CanPull);
        Assert.Null(status.WhyNotPull);
        Assert.Equal(["Third", "Second"], status.Commits.Select(c => c.Subject));
        Assert.All(status.Commits, c => Assert.Equal("Ada", c.Author));
        Assert.Equal(await HeadAsync(_author), status.UpstreamHead);

        Assert.Null(await _source.PullAsync(_checkout, Token));

        Assert.Equal(await HeadAsync(_author), await HeadAsync(_checkout));
        var after = Assert.IsType<UpstreamStatus>((await _source.CheckAsync(_checkout, Token)).Status);
        Assert.Equal((0, false), (after.Behind, after.CanPull));
        Assert.Empty(after.Commits);
    }

    [Fact]
    public async Task Changes_or_commits_of_the_checkouts_own_keep_it_from_pulling()
    {
        Assert.SkipWhen(!IsolatedGitLauncher.GitInstalled, "git isn't on PATH.");
        await CloneAsync();
        await PushAsync("Second");
        var before = await HeadAsync(_checkout);

        await File.WriteAllTextAsync(Path.Combine(_checkout, "README.md"), "Changed\n", Token);
        var changed = Assert.IsType<UpstreamStatus>((await _source.CheckAsync(_checkout, Token)).Status);
        Assert.False(changed.CanPull);
        Assert.Equal("The checkout has uncommitted changes. Commit or stash them to pull.", changed.WhyNotPull);
        Assert.Equal(changed.WhyNotPull, await _source.PullAsync(_checkout, Token));
        Assert.Equal(before, await HeadAsync(_checkout));
        Assert.Equal("Changed\n", await File.ReadAllTextAsync(Path.Combine(_checkout, "README.md"), Token));

        await _launcher.RunAsync(_checkout, "checkout", "-q", "--", "README.md");
        await CommitAsync(_checkout, "Mine");
        var ahead = Assert.IsType<UpstreamStatus>((await _source.CheckAsync(_checkout, Token)).Status);
        Assert.Equal((1, 1), (ahead.Ahead, ahead.Behind));
        Assert.Equal("The checkout has 1 commit that origin/main doesn't. Pull it yourself.", ahead.WhyNotPull);
        Assert.Equal(ahead.WhyNotPull, await _source.PullAsync(_checkout, Token));
    }

    [Fact]
    public async Task Untracked_files_dont_keep_it_from_pulling()
    {
        Assert.SkipWhen(!IsolatedGitLauncher.GitInstalled, "git isn't on PATH.");
        await CloneAsync();
        await PushAsync("Second");
        await File.WriteAllTextAsync(Path.Combine(_checkout, "notes.md"), "mine\n", Token);

        var status = Assert.IsType<UpstreamStatus>((await _source.CheckAsync(_checkout, Token)).Status);

        Assert.True(status.CanPull);
        Assert.Null(await _source.PullAsync(_checkout, Token));
        Assert.True(File.Exists(Path.Combine(_checkout, "notes.md")));
    }

    [Fact]
    public async Task A_checkout_on_no_branch_or_one_without_an_upstream_isnt_checked()
    {
        Assert.SkipWhen(!IsolatedGitLauncher.GitInstalled, "git isn't on PATH.");
        await CloneAsync();

        await _launcher.RunAsync(_checkout, "checkout", "-q", "--detach");
        var detached = await _source.CheckAsync(_checkout, Token);
        Assert.Null(detached.Status);
        Assert.Equal("The checkout isn't on a branch, so there's no upstream to check.", detached.Problem);

        await _launcher.RunAsync(_checkout, "checkout", "-q", "-b", "experiment");
        var local = await _source.CheckAsync(_checkout, Token);
        Assert.Null(local.Status);
        Assert.Equal("The checkout's branch experiment doesn't track a remote branch, so there's nothing to check.", local.Problem);
    }

    [Fact]
    public async Task A_fetch_that_fails_says_what_git_said()
    {
        Assert.SkipWhen(!IsolatedGitLauncher.GitInstalled, "git isn't on PATH.");
        await CloneAsync();
        await _launcher.RunAsync(_checkout, "remote", "set-url", "origin", Path.Combine(_root, "gone.git"));

        var check = await _source.CheckAsync(_checkout, Token);

        Assert.Null(check.Status);
        var problem = Assert.IsType<string>(check.Problem);
        Assert.StartsWith("Couldn't fetch origin/main: ", problem, StringComparison.Ordinal);
        Assert.True(problem.Length > "Couldn't fetch origin/main: ".Length, "Git's own words are missing.");
    }

    [Fact]
    public void The_log_reads_each_commit_and_skips_what_it_cant()
    {
        var log = "abc1234def\u001fFix the badge\u001fAda\u001f2026-10-22T14:03:00+02:00\r\n"
            + "not a commit\n"
            + "0123456789\u001fSubject with \u001f in it\u001fGrace\u001fnot a date\n";

        var commits = SourceCheckout.ParseLog(log);

        var commit = Assert.Single(commits);
        Assert.Equal(("abc1234def", "abc1234", "Fix the badge", "Ada"), (commit.Hash, commit.ShortHash, commit.Subject, commit.Author));
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 14, 3, 0, TimeSpan.FromHours(2)), commit.When);
    }

    // ---- Building ------------------------------------------------------------------------------------------------

    private static SourceBuild Build(string root) => new(Path.Combine(root, "src", "Claudette.App", "bin", "Release", "net10.0"), root);

    [Fact]
    public void The_project_and_configuration_come_from_the_build_output()
    {
        var root = Path.GetFullPath(Path.Combine(_root, "repo"));
        var build = Build(root);

        Assert.Equal(Path.Combine(root, "src", "Claudette.App"), build.ProjectDirectory);
        Assert.Equal("Release", build.Configuration);
        Assert.Null(new SourceBuild(Path.Combine(root, "out"), root).Configuration);
    }

    [Fact]
    public async Task The_app_is_built_in_its_configuration_from_the_checkouts_top()
    {
        var root = Path.GetFullPath(Path.Combine(_root, "repo"));
        var launcher = new FakeProcessLauncher { Respond = _ => new ProcessResult(0, "Restore complete\nClaudette -> bin/Release/net10.0/Claudette.dll", "") };
        var source = new SourceCheckout(new GitWorkingTree(launcher, TimeProvider.System), launcher, TimeProvider.System);
        var seen = new List<string>();

        var result = await source.BuildAsync(Build(root), seen.Add, Token);

        Assert.True(result.Succeeded);
        var spec = Assert.Single(launcher.Started);
        // The test host sets DOTNET_ROOT itself.
        Assert.Equal(SourceCheckout.Dotnet(UserEnvironment.Snapshot()), spec.FileName);
        Assert.Equal(["build", Path.Combine(root, "src", "Claudette.App"), "-c", "Release", "--nologo", "-tl:off", "--disable-build-servers"], spec.Arguments);
        Assert.Equal(root, spec.WorkingDirectory);
        Assert.Contains("Restore complete", seen);
    }

    [Fact]
    public async Task A_failed_build_keeps_its_last_lines()
    {
        var root = Path.GetFullPath(Path.Combine(_root, "repo"));
        // dotnet build writes its errors to its standard output, after the rest.
        var output = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"line {i}").Append("error CS1002: ; expected"));
        var launcher = new FakeProcessLauncher { Respond = _ => new ProcessResult(1, output, "") };
        var source = new SourceCheckout(new GitWorkingTree(launcher, TimeProvider.System), launcher, TimeProvider.System);

        var result = await source.BuildAsync(Build(root), cancellationToken: Token);

        Assert.False(result.Succeeded);
        Assert.Equal("The build failed (exit code 1).", result.Error);
        Assert.Equal(SourceCheckout.BuildLines, result.LastLines.Count);
        Assert.Contains("error CS1002: ; expected", result.LastLines);
        Assert.DoesNotContain("line 1", result.LastLines);
    }

    [Fact]
    public void The_dotnet_in_DOTNET_ROOT_comes_first()
    {
        var root = Path.Combine(_root, "dotnet");
        Directory.CreateDirectory(root);
        var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        Assert.Equal("dotnet", SourceCheckout.Dotnet(new Dictionary<string, string> { ["DOTNET_ROOT"] = root }));
        File.WriteAllText(Path.Combine(root, name), "");
        Assert.Equal(Path.Combine(root, name), SourceCheckout.Dotnet(new Dictionary<string, string> { ["DOTNET_ROOT"] = root }));
        Assert.Equal("dotnet", SourceCheckout.Dotnet(new Dictionary<string, string>()));
    }
}
