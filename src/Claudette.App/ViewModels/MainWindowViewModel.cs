using Claudette.App.Services;
using Claudette.Core.Auth;
using Claudette.Core.Development;
using Claudette.Core.Installation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// Runs the startup checks (Claude Code installed, then signed in) and shows the matching page (DESIGN.md §2, §11).
/// </summary>
public sealed partial class MainWindowViewModel(AppServices services, string? initialFolder = null) : ViewModelBase, IAsyncDisposable, IRestartHost
{
    private ShellViewModel? _shell;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; } = new BusyViewModel("Starting…");

    /// <summary>The account menu, the sign-in banner and its dialog, and signing out (DESIGN.md §11).</summary>
    public AccountViewModel Account { get; } = new(services);

    /// <summary>Claude's service status: the header's dot and the banner (DESIGN.md §18, "Service status").</summary>
    public ServiceStatusViewModel ServiceStatus { get; } = new(services);

    /// <summary>The header's cup: whether the computer is kept awake, and the switch for it (DESIGN.md §9, "Keeping the computer awake").</summary>
    public KeepAwakeService KeepAwake { get; } = services.KeepAwake;

    /// <summary>The signed-in email and plan, or null when signed out.</summary>
    public string? AccountText => Account.IsSignedIn ? Account.Summary : null;

    /// <summary>Why the usage header is missing, when usage tracking couldn't start.</summary>
    [ObservableProperty]
    public partial string? UsageNote { get; set; }

    /// <summary>The usage header (DESIGN.md §6), once signed in.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsage), nameof(ShowsTitle), nameof(ShowsUsageDetails))]
    public partial UsageViewModel? Usage { get; set; }

    partial void OnUsageChanged(UsageViewModel? oldValue, UsageViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnUsageHeaderChanged;
        }
        if (newValue is not null)
        {
            newValue.PropertyChanged += OnUsageHeaderChanged;
        }
    }

    /// <summary>Plan usage arrived, or the detailed header was turned on or off: what the header shows follows.</summary>
    private void OnUsageHeaderChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UsageViewModel.HasData))
        {
            OnPropertyChanged(nameof(HasUsage));
            OnPropertyChanged(nameof(ShowsTitle));
        }
        if (e.PropertyName is nameof(UsageViewModel.ShowDetails) or nameof(UsageViewModel.HasData))
        {
            OnPropertyChanged(nameof(ShowsUsageDetails));
        }
    }

    /// <summary>
    /// True once plan usage has arrived, and the selected tab runs under the Default account, whose plan it is. API-key
    /// accounts have no plan limits: the header shows their spend instead (<see cref="Spend"/>), or stays plain.
    /// </summary>
    public bool HasUsage => Usage is { HasData: true } && Spend is not { IsShown: true };

    /// <summary>The header while the selected tab runs under an API key: its estimated spend (DESIGN.md §4, "Accounts").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsage), nameof(ShowsSpend), nameof(ShowsTitle), nameof(ShowsUsageDetails))]
    public partial AccountSpendViewModel? Spend { get; set; }

    public bool ShowsSpend => Spend is { IsShown: true };

    /// <summary>The detailed header's charts, which are the Default plan's: not while an API key's spend shows.</summary>
    public bool ShowsUsageDetails => Usage is { ShowDetails: true } && !ShowsSpend;

    /// <summary>The header just says Claudette: no plan usage to show, and no API key's spend.</summary>
    public bool ShowsTitle => !HasUsage && !ShowsSpend;

    /// <summary>This is a demo (DESIGN.md §11, "Demo mode"): its banner shows, with <b>Leave demo</b>.</summary>
    public bool IsDemo => Services.Demo is not null;

    /// <summary><b>Leave demo</b>: closes it, as closing its window does, and its folder goes as it closes.</summary>
    [RelayCommand]
    private void LeaveDemo() => ExitRequested?.Invoke();

    /// <summary>
    /// Set by the window: it's too narrow for the session's meter beside everything else in the header, so the plan and
    /// the processes' totals make room, and the account is its icon (DESIGN.md §6, "Header meters").
    /// </summary>
    [ObservableProperty]
    public partial bool IsHeaderNarrow { get; set; }

    /// <summary>
    /// The header is the window's title bar, as in JetBrains Rider (DESIGN.md §3, "Window frame"), with Claudette's logo
    /// first: where the platform can (<see cref="IPlatformServices.CanMergeTitleBar"/>), unless Settings → Appearance → Use
    /// the system's title bar is on.
    /// </summary>
    public bool MergesTitleBar => services.Platform.CanMergeTitleBar && !services.Settings.Appearance.SystemTitleBar;

    /// <summary>Settings changed: Use the system's title bar applies at once.</summary>
    public void TitleBarSettingChanged() => OnPropertyChanged(nameof(MergesTitleBar));

    /// <summary>The header says Claudette when there's nothing else to show and no logo saying so.</summary>
    public bool ShowsHeaderTitle => ShowsTitle && !MergesTitleBar;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(ShowsTitle) or nameof(MergesTitleBar))
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(ShowsHeaderTitle)));
        }
    }

    /// <summary>The sidebar's "Claude Code … is ready" badge and Settings' update section (DESIGN.md §12).</summary>
    [ObservableProperty]
    public partial ClaudeUpdateViewModel? Updates { get; set; }

    private ClaudeUpdateResult? _launchUpdate;

    /// <summary>The main UI, once Claude Code is installed and signed in.</summary>
    public ShellViewModel? Shell => _shell;

    /// <summary>For tests: <paramref name="shell"/> is the main UI, as after signing in.</summary>
    internal void UseShell(ShellViewModel shell)
    {
        _shell = shell;
        CurrentPage = shell;
        OnPropertyChanged(nameof(Shell));
    }

    public AppServices Services => services;

    public Task StartAsync()
    {
        services.Notifications.Activated += OnNotificationActivated;
        // From launch, before Claude Code is found or signed in: an incident can be why those fail.
        services.ServiceStatus.Start();
        Account.CheckSignIn = CheckSignInAsync;
        // Signed out from the account menu or Settings: the tabs wait for the next sign-in (DESIGN.md §11), and the
        // utility session starts again when next needed, without the old account.
        Account.SignedOut += () =>
        {
            _shell?.OnSignedOut();
            _ = services.StopUtilitySessionAsync();
        };
        Account.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountViewModel.Status))
            {
                OnPropertyChanged(nameof(AccountText));
            }
        };
        return CheckInstallAsync(services.Settings.ClaudeCode.ClaudePath);
    }

    private async Task CheckInstallAsync(string? path)
    {
        CurrentPage = new BusyViewModel("Looking for Claude Code…");
        var result = await services.Locator.LocateAsync(path);
        if (result.Install is { } found && services.State.UpdateClaudeOnNextLaunch)
        {
            // Update on next launch (DESIGN.md §12): before any tab or the utility session starts a claude process.
            services.State.UpdateClaudeOnNextLaunch = false;
            services.SaveState();
            CurrentPage = new BusyViewModel("Updating Claude Code…");
            _launchUpdate = await UpdateClaudeAsync(found.Path, null);
            result = await services.Locator.LocateAsync(path);
        }
        if (!result.IsUsable)
        {
            // Too old: the setup screen offers the same Update now as the header (DESIGN.md §12, "Minimum version").
            var update = result is { Problem: ClaudeInstallProblem.TooOld, Install: { } old }
                ? output => UpdateClaudeAsync(old.Path, output)
                : (Func<Action<string>, Task<ClaudeUpdateResult>>?)null;
            // Missing, or only the desktop app's or npm's: Install Claude Code (DESIGN.md §12, "Installing Claude Code").
            var install = result.Problem is ClaudeInstallProblem.NotFound or ClaudeInstallProblem.DesktopApp or ClaudeInstallProblem.UnsupportedLauncher
                ? new SetupInstall(services.CreateInstaller().CommandText, InstallClaudeAsync)
                : null;
            CurrentPage = new SetupViewModel(result, services.Platform, CheckInstallAsync, update, () => CheckInstallAsync(path), install, new Demo.DemoStarter(services));
            return;
        }
        if (path != services.Settings.ClaudeCode.ClaudePath)
        {
            // The user pointed Claudette at claude from the setup screen: remember it. Or it was found by looking, from
            // the setup screen after the path in Settings didn't work: forget that path, so the next launch looks too.
            services.Settings.ClaudeCode.ClaudePath = path;
            services.SaveSettings();
        }
        services.UseInstall(result.Install!);
        if (_launchUpdate is { } launchUpdate)
        {
            services.ClaudeUpdates!.RecordLaunchUpdate(launchUpdate);
            _launchUpdate = null;
        }
        await CheckSignInAsync();
    }

    /// <summary>
    /// Runs Claude Code's installer, then makes sure Claudette can find what it installed, so the setup screen can say
    /// why not before looking again.
    /// </summary>
    private async Task<ClaudeInstallResult> InstallClaudeAsync(Action<string> onOutput)
    {
        try
        {
            var installer = services.CreateInstaller();
            var result = await Task.Run(() => installer.InstallAsync(line => services.Dispatcher.Post(() => onOutput(line))));
            if (!result.Succeeded)
            {
                return result;
            }
            var located = await services.Locator.LocateAsync(null);
            return located.IsUsable
                ? result
                : new ClaudeInstallResult(false, $"The installer finished, but Claudette still can't use Claude Code: {located.Detail} Restart Claudette, or use Choose claude… to point Claudette at the new claude.");
        }
        catch (Exception ex)
        {
            return new ClaudeInstallResult(false, $"The install failed: {ex.Message}");
        }
    }

    /// <summary>Checks how <paramref name="claudePath"/> was installed, then runs its update command.</summary>
    private async Task<ClaudeUpdateResult> UpdateClaudeAsync(string claudePath, Action<string>? onOutput)
    {
        try
        {
            var updater = services.CreateUpdater(claudePath);
            var check = await Task.Run(() => updater.CheckAsync());
            if (!check.Plan.CanRun)
            {
                return new ClaudeUpdateResult(false, check.InstalledVersion,
                    check.Plan.ManualCommand is { } manual ? $"Run this to update Claude Code: {manual}" : check.Plan.Note ?? "Claudette can't update this installation.");
            }
            return await Task.Run(() => updater.UpdateAsync(check.Plan, onOutput is null ? null : line => services.Dispatcher.Post(() => onOutput(line))));
        }
        catch (Exception ex)
        {
            return new ClaudeUpdateResult(false, null, $"The update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs <c>claude auth status</c> (DESIGN.md §11, "Detecting"). At launch, signed out shows the sign-in screen
    /// instead of the tabs; later, it keeps the banner and the dialog that's open.
    /// </summary>
    private async Task CheckSignInAsync()
    {
        var midSession = _shell is not null;
        if (!midSession && CurrentPage is not SignInViewModel)
        {
            CurrentPage = new BusyViewModel("Checking your sign-in…");
        }
        AuthStatus status;
        try
        {
            status = await services.Auth!.GetStatusAsync();
        }
        catch (Exception ex)
        {
            var message = $"Couldn't check your sign-in: {ex.Message}";
            if (midSession)
            {
                Account.SignIn?.ShowCheckFailed(message);
            }
            else if (CurrentPage is SignInViewModel page)
            {
                page.ShowCheckFailed(message);
            }
            else
            {
                CurrentPage = new SignInViewModel(services, CheckSignInAsync) { Error = message };
            }
            return;
        }

        Account.Status = status;
        if (!status.LoggedIn)
        {
            if (midSession)
            {
                Account.SignIn?.ShowStillSignedOut();
            }
            else if (CurrentPage is SignInViewModel page)
            {
                page.ShowStillSignedOut();
            }
            else
            {
                CurrentPage = new SignInViewModel(services, CheckSignInAsync);
            }
            return;
        }

        Account.OnSignedIn(status);
        services.ProjectsDirectory = status.ProjectsDirectory
            ?? (status.ConfigDirectory is { } config ? Path.Combine(config, "projects") : null);
        services.ClaudeConfigDirectory = status.ConfigDirectory
            ?? (status.ProjectsDirectory is { } projects ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(projects)) : null);
        // Claude Code's prefersReducedMotion is in its config folder, now known (DESIGN.md §3, "Accessibility").
        _ = services.ReadMotionPreferencesAsync();
        if (_shell is null)
        {
            _shell = new ShellViewModel(services, ShowSignIn);
            _shell.TabStatusChanged += () => _tabsChanged?.Invoke();
            // The header follows the selected tab's account (DESIGN.md §4, "Accounts").
            _shell.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.SelectedAccount))
                {
                    Spend?.Show(_shell.SelectedAccount);
                }
            };
            OnPropertyChanged(nameof(Shell));
            CurrentPage = _shell;
            var restored = _restore;
            _restore = null;
            if (restored is not null)
            {
                // Restarted into this build or release: the tabs the last one had (DESIGN.md §9, "Working on
                // Claudette"). The window deletes the snapshot once this build is up.
                _shell.Restore(null, restored);
            }
            else
            {
                _shell.Restore(initialFolder);
            }
            StartUsage();
            StartUpdateChecks(_shell);
            StartRestarts(_shell);
            StartAppUpdates(_shell, restored?.Update);
        }
        else
        {
            CurrentPage = _shell;
            // Like the tabs, the utility session (plan usage) starts again, with the new sign-in.
            await services.StopUtilitySessionAsync();
            await _shell.OnSignedInAgainAsync();
        }
    }

    /// <summary>Starts plan usage tracking and the header. A failure here never stops the tabs from working.</summary>
    private void StartUsage()
    {
        try
        {
            var tracker = services.StartUsageTracking();
            var usage = new UsageViewModel(services, tracker)
            {
                // The detailed header's busiest tabs, by name (DESIGN.md §6, "Detailed header").
                TabName = id => _shell?.AllTabs.FirstOrDefault(t => t.Id == id)?.DisplayName,
            };
            tracker.TurnRecorded += id => _shell?.OnTurnRecorded(id);
            // An API key's spend warnings (DESIGN.md §4, "Accounts").
            tracker.SpendRecorded += id => _ = services.Accounts.OnSpendRecordedAsync(id);
            // Other machines' turns under an account of the same name count toward its warnings (DESIGN.md §6).
            tracker.TurnsImported += names => _ = services.Accounts.OnTurnsImportedAsync(names);
            Usage = usage;
            Spend = new AccountSpendViewModel(services, tracker);
            Spend.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AccountSpendViewModel.IsShown))
                {
                    OnPropertyChanged(nameof(HasUsage));
                    OnPropertyChanged(nameof(ShowsSpend));
                    OnPropertyChanged(nameof(ShowsTitle));
                    OnPropertyChanged(nameof(ShowsUsageDetails));
                }
            };
            Spend.Show(_shell?.SelectedAccount);
        }
        catch (Exception ex)
        {
            UsageNote = $"Usage unavailable ({ex.Message})";
        }
    }

    /// <summary>Checks for Claude Code updates at launch and every few hours, and shows the sidebar's badge (DESIGN.md §12).</summary>
    private void StartUpdateChecks(ShellViewModel shell)
    {
        if (services.ClaudeUpdates is not { } updates)
        {
            return;
        }
        var viewModel = new ClaudeUpdateViewModel(services, updates, () => shell.RunningVersions);
        shell.RunningVersionsChanged += viewModel.Refresh;
        Updates = viewModel;
        shell.Updates = viewModel;
        updates.Start();
    }

    /// <summary>
    /// A tab reported that Claude Code needs a sign-in (DESIGN.md §11): a banner across all tabs, which stay open, and
    /// an OS notification if Claudette isn't in front. The account menu catches up with <c>claude auth status</c>.
    /// </summary>
    private void ShowSignIn()
    {
        if (!Account.NeedsSignIn)
        {
            Account.RequireSignIn();
            _ = Account.RefreshAsync();
        }
    }

    /// <summary>Asks the window to come to the front, for a clicked notification or a second launch.</summary>
    public event Action? BringToFrontRequested;

    /// <summary>
    /// Claudette was launched again, for example from the jump list or Open Recent (DESIGN.md §4, "Other ways in"):
    /// come to the front, and open a tab in the folder it names.
    /// </summary>
    public void OnLaunchedAgain(IReadOnlyList<string> args)
    {
        // A login entry starting Claudette when it's already running: nothing to do (DESIGN.md §9, "Starting at login").
        if (LaunchArguments.IsLogin(args))
        {
            return;
        }
        BringToFrontRequested?.Invoke();
        if (LaunchArguments.Folder(args) is { } folder && _shell is not null && CurrentPage == _shell)
        {
            _ = _shell.OpenFolderAsync(folder);
        }
    }

    /// <summary>
    /// A notification was clicked (DESIGN.md §10): bring Claudette to the front and go to the tab or screen it was
    /// about.
    /// </summary>
    internal void OnNotificationActivated(NotificationTarget target)
    {
        BringToFrontRequested?.Invoke();
        switch (target.Kind)
        {
            case NotificationKind.UsageAlert:
                Usage?.OpenPanelCommand.Execute(null);
                break;
            case NotificationKind.UpdateReady:
                Updates?.RequestOpen();
                break;
            case NotificationKind.SignIn:
                // At launch the sign-in screen is already showing; later, the banner's dialog, ready to start.
                if (Account.NeedsSignIn)
                {
                    Account.ShowSignIn();
                }
                break;
            case NotificationKind.ProjectAction:
                // The log of the run that finished, in the main area (DESIGN.md §18, "Project tools").
                if (target.TabId is { } jobTab && CurrentPage == _shell && _shell?.SelectTab(jobTab) == true)
                {
                    _shell.SelectedTab?.ProjectTools.Runs.OpenNotifiedRun();
                }
                break;
            default:
                if (target.TabId is { } tabId && CurrentPage == _shell)
                {
                    _shell?.SelectTab(tabId);
                }
                break;
        }
    }

    // ---- Restarting into a new build (DESIGN.md §9, "Working on Claudette") -----------------------------------

    private DevelopmentBuild? _development;
    private Action? _stopListening;
    private Action? _resumeListening;
    private RestartSnapshot? _restore;
    private Action? _tabsChanged;

    /// <summary>Watches for new builds and restarts into them, when running from a source build.</summary>
    public RestartService? Restarts { get; private set; }

    /// <summary>Set by the window: where it is and how big, for the build this one restarts into.</summary>
    public Func<WindowPlacement?>? GetWindowPlacement { get; set; }

    /// <summary>Raised when the new build is up and this one should close.</summary>
    public event Action? ExitRequested;

    /// <summary>This is a source build: offer its new builds once the tabs are up.</summary>
    /// <param name="stopListening">Stops taking later launches, so the new build can.</param>
    /// <param name="resumeListening">Takes them again if the new build doesn't start.</param>
    public void UseDevelopmentBuild(DevelopmentBuild build, Action? stopListening = null, Action? resumeListening = null)
    {
        _development = build;
        services.IsSourceBuild = true;
        UseSingleInstance(stopListening, resumeListening);
    }

    /// <summary>How a restart hands later launches to the new build or version, and takes them back if it fails.</summary>
    public void UseSingleInstance(Action? stopListening, Action? resumeListening)
    {
        _stopListening = stopListening;
        _resumeListening = resumeListening;
    }

    // ---- Claudette's own updates (DESIGN.md §2, "Updating Claudette") --------------------------------------------

    /// <summary>Release checks, downloads and installs.</summary>
    public AppUpdateService? AppUpdates { get; private set; }

    /// <summary>The sidebar's update badge and Settings' Claudette updates section.</summary>
    [ObservableProperty]
    public partial AppUpdateViewModel? AppUpdate { get; private set; }

    private void StartAppUpdates(ShellViewModel shell, AppUpdateHandover? handover)
    {
        var updates = AppUpdates = new AppUpdateService(services, this, _development is not null, _stopListening, _resumeListening);
        AppUpdate = shell.AppUpdate = new AppUpdateViewModel(updates);
        services.SettingsChanged += (_, _) => updates.OnSettingsChanged();
        if (handover is not null)
        {
            updates.OnRestoredAfterUpdate(handover);
        }
        updates.Start();
    }

    /// <summary>The tabs to open instead of the saved ones: this build was restarted into from an earlier one.</summary>
    public void RestoreOnStart(RestartSnapshot snapshot) => _restore = snapshot;

    private void StartRestarts(ShellViewModel shell)
    {
        if (_development is not { } build)
        {
            return;
        }
        Restarts = new RestartService(services, build, this, _stopListening, _resumeListening);
        shell.NewBuild = new NewBuildViewModel(Restarts);
        _ = Task.Run(() => new BuildCopies(services.Paths.BuildCopiesDirectory).CleanUp(build.RunningDirectory));
        Restarts.Start();
        // New commits on the branch the checkout tracks, as an installed Claudette's releases (DESIGN.md §9, "New commits upstream").
        var updates = SourceUpdates = new SourceUpdateService(services, Restarts);
        SourceUpdate = shell.SourceUpdate = new SourceUpdateViewModel(updates, services.Time);
        services.SettingsChanged += (_, _) => updates.OnSettingsChanged();
        updates.Start();
    }

    /// <summary>A source build's checks of its checkout for new commits, and their pulls and builds.</summary>
    public SourceUpdateService? SourceUpdates { get; private set; }

    /// <summary>The sidebar's badge for new commits, and Settings' section for them, for a source build.</summary>
    public SourceUpdateViewModel? SourceUpdate { get; private set; }

    bool IRestartHost.AnyTabWorking => _shell?.AnyTabWorking == true;

    event Action? IRestartHost.TabsChanged
    {
        add => _tabsChanged += value;
        remove => _tabsChanged -= value;
    }

    RestartSnapshot IRestartHost.Capture()
    {
        var snapshot = _shell?.CaptureForRestart() ?? new RestartSnapshot();
        snapshot.Window = GetWindowPlacement?.Invoke();
        return snapshot;
    }

    async Task IRestartHost.CloseTabsAsync(string message)
    {
        CurrentPage = new BusyViewModel(message);
        if (_shell is not null)
        {
            await _shell.CloseTabsForRestartAsync();
        }
    }

    void IRestartHost.Recover(RestartSnapshot snapshot)
    {
        if (_shell is not null)
        {
            _shell.Restore(null, snapshot);
            CurrentPage = _shell;
        }
    }

    void IRestartHost.Exit() => ExitRequested?.Invoke();

    public async ValueTask DisposeAsync()
    {
        services.Notifications.Activated -= OnNotificationActivated;
        ServiceStatus.Dispose();
        Usage?.Dispose();
        Spend?.Dispose();
        Updates?.Dispose();
        _shell?.NewBuild?.Dispose();
        SourceUpdate?.Dispose();
        SourceUpdates?.Dispose();
        Restarts?.Dispose();
        AppUpdate?.Dispose();
        AppUpdates?.Dispose();
        if (_shell is not null)
        {
            await _shell.DisposeAsync();
        }
    }
}
