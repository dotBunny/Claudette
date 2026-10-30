using System.Text;
using Claudette.App.Services;
using Claudette.Core.Installation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary><b>Install Claude Code</b> on the setup screen (DESIGN.md §12, "Installing Claude Code").</summary>
/// <param name="Command">The installer's command as Claude Code's setup page gives it, shown before it runs.</param>
/// <param name="Run">Runs it, passing each line of output, and says whether Claude Code can now be found.</param>
public sealed record SetupInstall(string Command, Func<Action<string>, Task<ClaudeInstallResult>> Run);

/// <summary>Shown when Claude Code is missing, too old or unusable (DESIGN.md §2, "Dependency").</summary>
/// <param name="update">
/// <b>Update now</b> for a version below the minimum (DESIGN.md §12): runs the install method's update command,
/// passing each line of output. Null when there's nothing to update.
/// </param>
/// <param name="recheck">Checks the same <c>claude</c> again after a successful update.</param>
/// <param name="install">
/// <b>Install Claude Code</b>, when it's missing (DESIGN.md §12, "Installing Claude Code"); after it works, Claudette looks
/// for it again. Null when it isn't offered.
/// </param>
/// <param name="demo"><b>Try the demo</b>, which needs no Claude Code (DESIGN.md §11, "Demo mode"); null when it isn't offered.</param>
public sealed partial class SetupViewModel(
    ClaudeLocateResult result,
    IPlatformServices platform,
    Func<string?, Task> checkAgain,
    Func<Action<string>, Task<ClaudeUpdateResult>>? update = null,
    Func<Task>? recheck = null,
    SetupInstall? install = null,
    Demo.DemoStarter? demo = null) : ViewModelBase
{
    public Demo.DemoStarter? Demo { get; } = demo;

    public const string InstallDocsUrl = "https://code.claude.com/docs/en/setup";

    private readonly StringBuilder _output = new();

    public string Title { get; } = result.Problem switch
    {
        ClaudeInstallProblem.TooOld => "Claude Code needs an update",
        ClaudeInstallProblem.UnsupportedLauncher => "This Claude Code install isn't supported yet",
        ClaudeInstallProblem.VersionUnreadable => "Claude Code didn't respond",
        _ => "Claude Code isn't installed",
    };

    public string Detail { get; } = result.Detail ?? "";

    public string? FoundPath { get; } = result.FoundPath;

    public bool HasFoundPath => FoundPath is not null;

    /// <summary>
    /// Claude Code wasn't found but the Claude desktop app is installed, which people take for it; said only when the
    /// detail doesn't already say so.
    /// </summary>
    public bool ShowsDesktopNote { get; } = result is { DesktopInstalled: true, Problem: not ClaudeInstallProblem.DesktopApp };

    public bool CanUpdate => update is not null;

    public bool CanInstall => install is not null;

    /// <summary>What <b>Install Claude Code</b> runs, as the user would type it.</summary>
    public string? InstallCommandText => install?.Command;

    /// <summary><b>Check again</b> is the main action when there's nothing to update or install.</summary>
    public bool ChecksFirst => !CanUpdate && !CanInstall;

    /// <summary>Update now or Install Claude Code is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateNowCommand), nameof(InstallCommand))]
    public partial bool IsRunning { get; private set; }

    /// <summary>Install Claude Code is running. The installer prints nothing while it downloads, so the screen says what's happening.</summary>
    [ObservableProperty]
    public partial bool IsInstalling { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    public partial string Output { get; private set; } = "";

    public bool HasOutput => Output.Length > 0;

    /// <summary>How Update now or Install Claude Code went.</summary>
    [ObservableProperty]
    public partial string? ResultText { get; private set; }

    [RelayCommand]
    private Task CheckAgainAsync() => checkAgain(null);

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await platform.PickFileAsync("Choose the claude executable") is { } path)
        {
            await checkAgain(path);
        }
    }

    [RelayCommand]
    private Task OpenInstallDocsAsync() => platform.OpenUrlAsync(InstallDocsUrl);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task UpdateNowAsync()
    {
        if (update is null)
        {
            return;
        }
        var outcome = await RunAsync(update);
        ResultText = outcome.Message;
        if (outcome.Succeeded)
        {
            await (recheck?.Invoke() ?? checkAgain(null));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task InstallAsync()
    {
        if (install is null)
        {
            return;
        }
        IsInstalling = true;
        ClaudeInstallResult outcome;
        try
        {
            outcome = await RunAsync(install.Run);
        }
        finally
        {
            IsInstalling = false;
        }
        ResultText = outcome.Message;
        if (outcome.Succeeded)
        {
            await checkAgain(null);
        }
    }

    private async Task<T> RunAsync<T>(Func<Action<string>, Task<T>> run)
    {
        IsRunning = true;
        _output.Clear();
        Output = "";
        ResultText = null;
        try
        {
            return await run(line =>
            {
                _output.AppendLine(line);
                Output = _output.ToString();
            });
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanRun() => !IsRunning;
}
