using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Claudette.App.Services;
using Claudette.App.ViewModels;
using Claudette.Core;
using Claudette.Core.Processes;
using Claudette.Core.Protocol;
using Claudette.Core.Sessions;
using Claudette.Core.Settings;
using Claudette.Platform.Processes;
using Claudette.Testing;

namespace Claudette.App.Tests.Support;

/// <summary>Plays Claude Code's side of one session for view model tests.</summary>
internal sealed class ScriptedTransport : IClaudeTransport
{
    // Switched as a session starts, off the test's thread, and written to by the test.
    private volatile ScriptedProcess _process;
    private readonly List<JsonObject> _sent = [];
    private readonly List<(ScriptedProcess Process, JsonObject Message)> _sentBy = [];

    /// <summary>Every pretend process so far, in the order they started: the first tab's is 0.</summary>
    private readonly List<ScriptedProcess> _processes = [];

    public ScriptedTransport() => _process = NewProcess();

    private ScriptedProcess NewProcess()
    {
        var process = new ScriptedProcess();
        lock (_processes)
        {
            _processes.Add(process);
        }
        return process;
    }

    private ScriptedProcess Process(int index)
    {
        lock (_processes)
        {
            return _processes[index];
        }
    }

    /// <summary>
    /// The pretend process for the session starting now, which it has to itself, as each <c>claude</c> has its own: the
    /// current one while no session has it, else a new one, so a second tab running alongside the first reads its own
    /// output, and a tab starting again after its process exited (after a sign-in, say) gets a fresh one. What's emitted
    /// from then on goes to it; <see cref="EmitTo(int, string)"/> reaches an earlier one. Stopping it ends only that
    /// process, so a tab that finishes closing after the next one started (a test doesn't wait for all of it) can't end
    /// the next one's. Everything sent so far is kept.
    /// </summary>
    public IClaudeTransport ForSession()
    {
        // Checked and claimed in one step: tabs restored together start at once, each on a thread of its own.
        lock (_processes)
        {
            if (_process.HasExited || _process.IsTaken)
            {
                _process = NewProcess();
            }
            _process.IsTaken = true;
            return new SessionTransport(this, _process);
        }
    }

    /// <summary>
    /// Answers to control requests by subtype; null leaves the request unanswered, an exception answers with an error.
    /// Tests change them while sessions send requests on other threads.
    /// </summary>
    public ConcurrentDictionary<string, Func<JsonObject, JsonObject?>> Answers { get; } = new()
    {
        ["initialize"] = _ => new JsonObject
        {
            ["models"] = new JsonArray(
                new JsonObject { ["value"] = "default", ["resolvedModel"] = "claude-opus-5-5", ["displayName"] = "Default (recommended)", ["supportsEffort"] = true, ["supportedEffortLevels"] = new JsonArray("low", "high"), ["supportsAutoMode"] = true },
                new JsonObject { ["value"] = "opus", ["resolvedModel"] = "claude-opus-5-5", ["displayName"] = "Opus", ["supportsEffort"] = true, ["supportedEffortLevels"] = new JsonArray("low", "high"), ["supportsAutoMode"] = true },
                new JsonObject { ["value"] = "haiku", ["resolvedModel"] = "claude-haiku-4-5", ["displayName"] = "Haiku", ["supportsEffort"] = false }),
            ["current_permission_mode"] = "default",
            ["output_style"] = "default",
            ["available_output_styles"] = new JsonArray("default", "Explanatory", "Learning"),
        },
        ["get_context_usage"] = _ => new JsonObject { ["totalTokens"] = 1000, ["maxTokens"] = 200000, ["percentage"] = 0.5 },
    };

    public IReadOnlyList<JsonObject> Sent
    {
        get
        {
            lock (_sent)
            {
                return _sent.ToArray();
            }
        }
    }

    public IEnumerable<string> SentUserTexts => Sent.Where(m => m["type"]?.GetValue<string>() == "user").Select(m => m["message"]!["content"]!.GetValue<string>());

    public IEnumerable<string> SentControlSubtypes => Sent.Where(m => m["type"]?.GetValue<string>() == "control_request").Select(m => m["request"]!["subtype"]!.GetValue<string>());

    /// <summary>The pretend <c>claude</c> process id, for the process monitor.</summary>
    public int? ProcessId { get; set; } = 4242;

    public ChannelReader<string> Output => _process.Output.Reader;

    public Task<TransportExit> Completion => _process.Completion.Task;

    public ValueTask SendAsync(string line, CancellationToken cancellationToken = default) => Receive(_process, line);

    /// <summary>Records what the session sent, and answers its control requests on its own process's output.</summary>
    private ValueTask Receive(ScriptedProcess process, string line)
    {
        var message = JsonNode.Parse(line)!.AsObject();
        lock (_sent)
        {
            _sent.Add(message);
            _sentBy.Add((process, message));
        }
        if (message["type"]?.GetValue<string>() == "control_request")
        {
            var id = message["request_id"]!.GetValue<string>();
            var request = message["request"]!.AsObject();
            var subtype = request["subtype"]!.GetValue<string>();
            if (Answers.TryGetValue(subtype, out var answer))
            {
                try
                {
                    if (answer(request) is { } response)
                    {
                        process.Write(OutgoingMessages.ControlSuccess(id, response).ToJsonString());
                    }
                }
                catch (Exception ex)
                {
                    process.Write(OutgoingMessages.ControlError(id, ex.Message).ToJsonString());
                }
            }
            else
            {
                process.Write(OutgoingMessages.ControlSuccess(id, []).ToJsonString());
            }
        }
        return ValueTask.CompletedTask;
    }

    public void Emit(JsonObject message) => Emit(message.ToJsonString());

    public void Emit(string line)
    {
        // Claude Code starts its running totals again when the conversation is cleared.
        if (line.Contains("\"conversation_reset\"", StringComparison.Ordinal))
        {
            _process.RunningUsage.Clear();
        }
        _process.Write(line);
    }

    /// <summary>
    /// Emits on the pretend process started <paramref name="index"/>th (0 for the first), for tests with several tabs
    /// running: each session has a process of its own.
    /// </summary>
    public void EmitTo(int index, JsonObject message) => Process(index).Write(message.ToJsonString());

    /// <inheritdoc cref="EmitTo(int, JsonObject)"/>
    public void EmitTo(int index, string line) => Process(index).Write(line);

    /// <summary>What the session on the <paramref name="index"/>th pretend process sent.</summary>
    public IReadOnlyList<JsonObject> SentTo(int index)
    {
        var process = Process(index);
        lock (_sent)
        {
            return [.. _sentBy.Where(s => s.Process == process).Select(s => s.Message)];
        }
    }

    /// <summary>The user messages the <paramref name="index"/>th pretend process was sent, as text.</summary>
    public IEnumerable<string> SentUserTextsTo(int index) => SentTo(index).Where(m => m["type"]?.GetValue<string>() == "user").Select(UserText);

    /// <summary>A user message's text: its string content, or its text blocks joined by new lines.</summary>
    public static string UserText(JsonObject message) => message["message"]!["content"] switch
    {
        JsonValue value => value.GetValue<string>(),
        JsonArray blocks => string.Join("\n", blocks.OfType<JsonObject>().Where(b => b["type"]?.GetValue<string>() == "text").Select(b => b["text"]!.GetValue<string>())),
        _ => "",
    };

    /// <summary>
    /// Starts the current pretend process's running totals from what a transcript saved, as Claude Code does for a resumed
    /// or forked session: its results carry them from then on.
    /// </summary>
    public void RestoreUsage(string model, long input, long output, double cost) => _process.RunningUsage[model] = (input, output, cost);

    /// <summary>
    /// A complete turn: init, a text reply and a result with usage. The turn spends 100 input and 20 output tokens and
    /// $0.01; the result carries the pretend process's running totals, as Claude Code's does.
    /// </summary>
    public void EmitTurn(string reply = "ok", string model = "claude-opus-5-5", string sessionId = "s1")
    {
        var running = _process.RunningUsage;
        var (input, output, cost) = running.GetValueOrDefault(model);
        running[model] = (input + 100, output + 20, cost + 0.01);
        var modelUsage = new JsonObject();
        foreach (var (name, totals) in running)
        {
            modelUsage[name] = new JsonObject
            {
                ["inputTokens"] = totals.Input, ["outputTokens"] = totals.Output, ["cacheReadInputTokens"] = 0, ["cacheCreationInputTokens"] = 0, ["costUSD"] = totals.Cost,
            };
        }
        Emit(new JsonObject { ["type"] = "system", ["subtype"] = "init", ["session_id"] = sessionId, ["model"] = model, ["permissionMode"] = "default" });
        Emit(new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = reply }) },
        });
        Emit(new JsonObject
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["is_error"] = false,
            ["result"] = reply,
            ["session_id"] = sessionId,
            ["modelUsage"] = modelUsage,
        });
    }

    public void CloseInput() => Exit(0);

    public void Terminate() => Exit(-1);

    public void Exit(int code, string standardError = "") => _process.Exit(code, standardError);

    public ValueTask DisposeAsync()
    {
        Exit(0);
        return ValueTask.CompletedTask;
    }

    /// <summary>One pretend <c>claude</c> process: its output, and how it ended.</summary>
    private sealed class ScriptedProcess
    {
        public Channel<string> Output { get; } = Channel.CreateUnbounded<string>();

        public TaskCompletionSource<TransportExit> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HasExited => Completion.Task.IsCompleted;

        /// <summary>A session has it (<see cref="ForSession"/>): the next one to start gets a new process.</summary>
        public bool IsTaken { get; set; }

        /// <summary>What the process has spent by model, which each result it writes reports in <c>modelUsage</c>.</summary>
        public Dictionary<string, (long Input, long Output, double Cost)> RunningUsage { get; } = [];

        public void Write(string line) => Output.Writer.TryWrite(line);

        public void Exit(int code, string standardError)
        {
            Output.Writer.TryComplete();
            Completion.TrySetResult(new TransportExit(code, standardError));
        }
    }

    /// <summary>A session's connection to its own pretend process; what it sends is recorded with everything else.</summary>
    private sealed class SessionTransport(ScriptedTransport transport, ScriptedProcess process) : IClaudeTransport
    {
        public ChannelReader<string> Output => process.Output.Reader;

        public Task<TransportExit> Completion => process.Completion.Task;

        public int? ProcessId => transport.ProcessId;

        public ValueTask SendAsync(string line, CancellationToken cancellationToken = default) => transport.Receive(process, line);

        public void CloseInput() => process.Exit(0, "");

        public void Terminate() => process.Exit(-1, "");

        public ValueTask DisposeAsync()
        {
            process.Exit(0, "");
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class ScriptedSessionFactory(ScriptedTransport transport, TimeProvider time) : IClaudeSessionFactory
{
    // Starts run off the test's thread: the lists are read as copies.
    private readonly List<ClaudeLaunchOptions> _launches = [];
    private readonly List<ClaudeSession> _sessions = [];

    public IReadOnlyList<ClaudeLaunchOptions> Launches
    {
        get
        {
            lock (_launches)
            {
                return [.. _launches];
            }
        }
    }

    /// <summary>While set, starts fail with this, as a <c>claude</c> that couldn't start would.</summary>
    public Exception? StartFailure { get; set; }


    /// <summary>While set, a start waits for it once <c>claude</c> is running, as a slow handshake would.</summary>
    public TaskCompletionSource? StartGate { get; set; }

    /// <summary>Every session started, including ones a start stopped before handing them over.</summary>
    public IReadOnlyList<ClaudeSession> Sessions
    {
        get
        {
            lock (_sessions)
            {
                return [.. _sessions];
            }
        }
    }

    public async Task<ClaudeSession> StartAsync(ClaudeLaunchOptions options, CancellationToken cancellationToken = default)
    {
        if (StartFailure is { } failure)
        {
            Launched(options);
            throw failure;
        }
        // The new session's pretend process is in place before the launch is recorded: a test that waits for the launch
        // and then emits writes to the process this session reads, not the one before.
        var connection = transport.ForSession();
        Launched(options);
        var session = new ClaudeSession(connection, time, showsElicitations: options.ShowsElicitations, restoredUsage: options.RestoredUsage);
        lock (_sessions)
        {
            _sessions.Add(session);
        }
        try
        {
            if (StartGate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }
            await session.InitializeAsync(options.Hooks, cancellationToken);
        }
        catch
        {
            // As the real factory does: a start that fails leaves no claude running.
            await session.DisposeAsync();
            throw;
        }
        return session;
    }

    private void Launched(ClaudeLaunchOptions options)
    {
        lock (_launches)
        {
            _launches.Add(options);
        }
    }
}

internal sealed class InlineDispatcher : IUiDispatcher
{
    // Stands in for the one UI thread: every harness shares it, and tests read view model state under it too.
    private static readonly Lock UiThread = new();

    public void Post(Action action) => RunOnUiThread(action);

    /// <summary>
    /// Serializes like a UI thread would, with a synchronization context so an <c>await</c> in posted work resumes
    /// under the lock too, as it resumes on Avalonia's UI thread in the app.
    /// </summary>
    private static void RunOnUiThread(Action action)
    {
        lock (UiThread)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(UiContext.Instance);
            try
            {
                action();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> as if on the UI thread, as a click would: under the lock, and with the UI thread's
    /// synchronization context, so what it starts resumes there after each await, as it does in the app. Started from
    /// the test's own thread instead, that work resumes on the thread pool, outside the lock, and a wait can see it
    /// half done.
    /// </summary>
    public static void Run(Action action) => RunOnUiThread(action);

    /// <summary><see cref="Run"/> for a command that goes on after it returns: its task, for the test to wait on.</summary>
    public static Task RunAsync(Func<Task> work)
    {
        var task = Task.CompletedTask;
        RunOnUiThread(() => task = work());
        return task;
    }

    /// <inheritdoc cref="RunAsync(Func{Task})"/>
    public static Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        Task<T>? task = null;
        RunOnUiThread(() => task = work());
        return task!;
    }

    /// <summary>Evaluates <paramref name="read"/> as if on the UI thread, so it can't see a collection mid-change.</summary>
    public static T Read<T>(Func<T> read)
    {
        lock (UiThread)
        {
            return read();
        }
    }

    /// <summary>Runs continuations later on a pool thread, one at a time under the lock, like a UI thread's queue.</summary>
    private sealed class UiContext : SynchronizationContext
    {
        public static readonly UiContext Instance = new();

        public override void Post(SendOrPostCallback d, object? state) =>
            ThreadPool.UnsafeQueueUserWorkItem(_ => RunOnUiThread(() => d(state)), null);

        public override void Send(SendOrPostCallback d, object? state) => RunOnUiThread(() => d(state));

        public override SynchronizationContext CreateCopy() => this;
    }
}

internal sealed class NoPlatform : IPlatformServices
{
    /// <summary>As on Windows and macOS, on every system, so the header looks the same in every test run.</summary>
    public bool CanMergeTitleBar => true;

    // Work off the test's thread opens and shows things too: what's recorded is read as copies.
    private readonly List<string> _openedUrls = [];
    private readonly List<string> _revealedFolders = [];
    private readonly List<string> _openedFiles = [];
    private readonly List<IReadOnlyList<string>> _menus = [];

    public IReadOnlyList<string> OpenedUrls => Copy(_openedUrls);

    public string? Clipboard { get; private set; }

    /// <summary><b>Copy image</b>: the clipboard has the image, as a paste would read it back.</summary>
    public Task SetClipboardImageAsync(byte[] data)
    {
        ClipboardImage = data;
        return Task.CompletedTask;
    }

    /// <summary>What the folder picker returns: null is Cancel.</summary>
    public string? FolderToPick { get; set; }

    public Task<string?> PickFolderAsync(string title) => Task.FromResult(FolderToPick);

    /// <summary>What the file picker returns: null is Cancel.</summary>
    public string? FileToPick { get; set; }

    public Task<string?> PickFileAsync(string title) => Task.FromResult(FileToPick);

    /// <summary>What the save dialog returns: null is Cancel.</summary>
    public string? SavePathToPick { get; set; }

    /// <summary>What the last save dialog suggested, and offered.</summary>
    public (string Name, IReadOnlyList<SaveFileType> Types)? LastSaveDialog { get; private set; }

    public Task<string?> PickSaveFileAsync(string title, string suggestedName, IReadOnlyList<SaveFileType> types)
    {
        LastSaveDialog = (suggestedName, types);
        return Task.FromResult(SavePathToPick);
    }

    public Task OpenUrlAsync(string url)
    {
        Record(_openedUrls, url);
        return Task.CompletedTask;
    }

    /// <summary>The folders shown in Explorer or Finder, in order.</summary>
    public IReadOnlyList<string> RevealedFolders => Copy(_revealedFolders);

    public Task RevealFolderAsync(string path)
    {
        Record(_revealedFolders, path);
        return Task.CompletedTask;
    }

    public Task SetClipboardTextAsync(string text)
    {
        Clipboard = text;
        return Task.CompletedTask;
    }

    /// <summary>Files, an image or text "on the clipboard", for paste tests.</summary>
    public List<string> ClipboardFiles { get; } = [];

    public byte[]? ClipboardImage { get; set; }

    public Task<IReadOnlyList<string>> GetClipboardFilesAsync() => Task.FromResult<IReadOnlyList<string>>(ClipboardFiles.ToArray());

    public Task<string?> GetClipboardTextAsync() => Task.FromResult(Clipboard);

    public Task<byte[]?> GetClipboardImageAsync() => Task.FromResult(ClipboardImage);

    public IReadOnlyList<string> OpenedFiles => Copy(_openedFiles);

    public Task OpenFileAsync(string path)
    {
        Record(_openedFiles, path);
        return Task.CompletedTask;
    }

    /// <summary>The menus shown, each with its choices, in order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Menus => Copy(_menus);

    /// <summary>What a menu picks: an index, or null to close it without one.</summary>
    public int? ChoiceToPick { get; set; }

    public Task<int?> ChooseAsync(object? anchor, IReadOnlyList<string> choices)
    {
        Record(_menus, choices);
        return Task.FromResult(ChoiceToPick);
    }

    private static void Record<T>(List<T> list, T item)
    {
        lock (list)
        {
            list.Add(item);
        }
    }

    private static IReadOnlyList<T> Copy<T>(List<T> list)
    {
        lock (list)
        {
            return [.. list];
        }
    }
}

/// <summary>A process tree the test controls: which children are "running", and whether they were killed.</summary>
internal sealed class FakeProcessTree(int rootPid, TimeProvider time) : ProcessTree(rootPid)
{
    public List<(int Pid, string Name)> Children { get; } = [];

    /// <summary>The parent of a child that isn't directly under <c>claude</c>, by its PID.</summary>
    public Dictionary<int, int> Parents { get; } = [];

    public bool Killed { get; private set; }

    public List<int> Stopped { get; } = [];

    public override IReadOnlyList<ProcessSnapshot> Sample(bool includeCommandLines) =>
    [
        new ProcessSnapshot { Pid = RootPid, ParentPid = 1, Name = "claude", IsRoot = true, MemoryBytes = 100 << 20, CpuPercent = 1, FirstSeen = time.GetUtcNow() },
        .. Children.Select(c => new ProcessSnapshot
        {
            Pid = c.Pid, ParentPid = Parents.GetValueOrDefault(c.Pid, RootPid), Name = c.Name, MemoryBytes = 50 << 20, CpuPercent = 20,
            CommandLine = includeCommandLines ? $"{c.Name} --serve" : null, FirstSeen = time.GetUtcNow(),
        }),
    ];

    public override IReadOnlyList<int> DescendantIds() => Children.Select(c => c.Pid).ToArray();

    public override void KillAll()
    {
        Killed = true;
        Children.Clear();
    }

    public override Task StopAsync(int pid, TimeSpan grace, CancellationToken cancellationToken = default)
    {
        Stopped.Add(pid);
        Children.RemoveAll(c => c.Pid == pid);
        return Task.CompletedTask;
    }
}

internal sealed class FakeProcessTreeTracker(TimeProvider time) : IProcessTreeTracker
{
    public Dictionary<int, FakeProcessTree> Trees { get; } = [];

    public ProcessTree Track(int rootPid) => Trees.TryGetValue(rootPid, out var tree) ? tree : Trees[rootPid] = new FakeProcessTree(rootPid, time);

    public ProcessTree? Find(int rootPid) => Trees.GetValueOrDefault(rootPid);
}

/// <summary>A tab wired to a scripted Claude Code, with a fake clock and a temporary data folder.</summary>
internal sealed class TabTestHarness : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"claudette-tabtest-{Guid.NewGuid():N}");

    /// <summary>The version tests run as, unless they pass one.</summary>
    public static readonly Core.Updates.AppVersion TestVersion = new(0, 1, 0);

    /// <param name="updater">Claude Code's installation, for update tests; when given, update checks are set up too.</param>
    /// <param name="launcher">Starts processes other than Claude Code, which the scripted sessions stand in for.</param>
    /// <param name="dispatcher">The UI thread: an inline stand-in for view model tests, Avalonia's own for rendered UI tests.</param>
    /// <param name="appInstaller">Installs Claudette's own updates; by default none can be.</param>
    /// <param name="http">Answers Claudette's own web requests; by default every request fails, so nothing reaches the network.</param>
    /// <param name="appVersion">
    /// Claudette's version; by default <see cref="TestVersion"/> rather than the built one, so the Settings snapshots
    /// don't change when the version does.
    /// </param>
    /// <param name="loginShell">Stands in for the user's login shell (DESIGN.md §13); by default none is read.</param>
    public TabTestHarness(Action<AppSettings>? configure = null, FakeClaudeUpdater? updater = null, IProcessLauncher? launcher = null, IUiDispatcher? dispatcher = null,
        Core.Updates.IAppInstaller? appInstaller = null, HttpMessageHandler? http = null, Core.Updates.AppVersion? appVersion = null, ILoginShell? loginShell = null,
        Demo.DemoMode? demo = null)
    {
        Directory.CreateDirectory(Path.Combine(_root, "work"));
        Directory.CreateDirectory(ProjectsDirectory);
        Trees = new FakeProcessTreeTracker(Time);
        Trash = new FakeTrash(Path.Combine(_root, "trash"));
        // Other programs' folders (the Epic launcher's, Unity Hub's) are empty ones here, never the real ones (DESIGN.md §18).
        var otherPrograms = new Core.ProjectTools.ProjectToolPaths(Path.Combine(_root, "home"), Path.Combine(_root, "appdata"), Path.Combine(_root, "localappdata"),
            Path.Combine(_root, "programdata"), Path.Combine(_root, "programfiles"), Path.Combine(_root, "applications"));
        // Real processes, or the test's, but never p4 unless the test's is FakeP4 (NoRealP4Launcher says why).
        Services = new AppServices(AppPaths.Under(_root), launcher is FakeP4 ? launcher : new NoRealP4Launcher(launcher ?? new ProcessLauncher()), Time, Platform, dispatcher ?? new InlineDispatcher(), processTrees: Trees, notifier: Notifier,
            appInstaller: appInstaller, httpHandler: http ?? new OfflineHandler(), appVersion: appVersion ?? TestVersion, loginShell: loginShell,
            projectToolPaths: otherPrograms, sleepBlocker: SleepBlocker, loginItems: LoginItems, systemMotion: SystemMotion,
            trash: Trash, demo: demo);
        Services.Notifications.UseBadge(Notifier);
        // The machine running the tests doesn't decide whether the Claude app is available (its ANTHROPIC_BASE_URL, say).
        Services.RemoteControl.EnvironmentVariable = _ => null;
        // Nor do its organization's managed Claude Code settings (DESIGN.md §7, "Starting mode").
        Services.ClaudeManagedSettingsDirectory = null;
        // Nor its environment, for what Claude Code's configuration says (its DISABLE_PROMPT_CACHING, say).
        Services.ClaudeEnvironmentFor = (overrides, _) => Task.FromResult(Core.Claude.ClaudeEnvironment.From(ClaudeUserEnvironment, overrides));
        // The Project Files tabs' watching is the test's to say (DESIGN.md §18, "Project files"), and with no UI thread to
        // own AvaloniaEdit's documents, their text is plain.
        Services.FolderWatcher = FolderWatcher;
        if (Services.Dispatcher is InlineDispatcher)
        {
            Services.EditorDocuments = text => new PlainEditorDocument(text);
        }
        configure?.Invoke(Services.Settings);
        // Never the real installer: it would install Claude Code on the machine running the tests.
        Services.InstallerFactory = () => Installer;
        if (updater is not null)
        {
            Services.UpdaterFactory = _ => updater;
            Services.UseInstall(new Core.Installation.ClaudeInstall("claude", updater.Installed));
        }
        Services.ProjectsDirectory = ProjectsDirectory;
        Factory = new ScriptedSessionFactory(Transport, Time);
        Services.UseSessionFactory(Factory);
        Shell = new ShellViewModel(Services, () => OnAuthenticationRequired());
    }

    /// <summary>Claude Code's installer, for the setup screen's Install Claude Code (DESIGN.md §12, "Installing Claude Code").</summary>
    public FakeClaudeInstaller Installer { get; } = new();

    /// <summary>
    /// Stands in for the user environment each tab's <c>claude</c> starts from, as far as Claudette reads it (DESIGN.md
    /// §6, "Prompt cache"): empty unless a test sets variables.
    /// </summary>
    public Dictionary<string, string> ClaudeUserEnvironment { get; } = new(StringComparer.Ordinal);

    /// <summary>What the main window does when a tab finds Claude Code signed out (DESIGN.md §11).</summary>
    public Action OnAuthenticationRequired { get; set; } = () => { };

    public FakeProcessTreeTracker Trees { get; }

    /// <summary>Stands in for the OS's file notifications, for the Project Files tabs (DESIGN.md §18, "Project files").</summary>
    public FakeFolderWatcher FolderWatcher { get; } = new();

    public NoPlatform Platform { get; } = new();

    public FakeNotifier Notifier { get; } = new();

    /// <summary>Stands in for keeping the computer awake while tabs are connected to the Claude app (DESIGN.md §18).</summary>
    public FakeSleepBlocker SleepBlocker { get; } = new();

    /// <summary>Stands in for the OS's reduce-motion setting (DESIGN.md §3, "Accessibility").</summary>
    public FakeSystemMotion SystemMotion { get; } = new();

    /// <summary>Stands in for the OS's trash (DESIGN.md §18, "Project files", "Renaming and deleting"): never the user's own.</summary>
    public FakeTrash Trash { get; private set; } = null!;

    /// <summary>Stands in for the OS's login entry (DESIGN.md §9, "Starting at login"): never the real Run key or LaunchAgents.</summary>
    public Core.Tests.Support.FakeLoginItems LoginItems { get; } = new();

    /// <summary>Stands in for Claude Code's <c>projects</c> folder.</summary>
    public string ProjectsDirectory => Path.Combine(_root, "projects");

    public string Root => _root;

    /// <summary>Writes a transcript where Claude Code would keep it.</summary>
    public string WriteTranscript(string sessionId, params string[] lines)
    {
        var folder = Path.Combine(ProjectsDirectory, "work-project");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{sessionId}.jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    public HarnessClock Time { get; } = new(DateTimeOffset.Parse("2026-09-28T12:00:00Z"));

    public ScriptedTransport Transport { get; } = new();

    public ScriptedSessionFactory Factory { get; }

    public AppServices Services { get; }

    public ShellViewModel Shell { get; }

    public string WorkFolder => Path.Combine(_root, "work");

    /// <summary>The tool window of <paramref name="kind"/> (DESIGN.md §3, "Tool windows").</summary>
    public ViewModels.ToolWindows.ToolWindowViewModel ToolWindow(ViewModels.ToolWindows.ToolWindowKind kind) => Shell.ToolWindows.For(kind);

    /// <summary>The tool window of <paramref name="kind"/> is on screen, beside the strip.</summary>
    public bool IsShowing(ViewModels.ToolWindows.ToolWindowKind kind) => InlineDispatcher.Read(() => ToolWindow(kind).IsShowing);

    /// <summary>
    /// Shows the tool window of <paramref name="kind"/>, as a button or the palette does. Pinned, as a window starts, it stays
    /// as the focus moves, as the side panel did.
    /// </summary>
    public void ShowToolWindow(ViewModels.ToolWindows.ToolWindowKind kind) => OnUiThread(() => Shell.ToolWindows.Show(kind));

    /// <summary>
    /// Runs <paramref name="action"/> as a click does, on the UI thread, so what it starts resumes there after each await,
    /// as in the app: in rendered UI tests Avalonia's own, which the test is on; in view model tests the inline stand-in's
    /// (<see cref="InlineDispatcher.Run"/>). The stand-in's in a UI test would resume on the pool, changing lists a window
    /// shows while the UI thread lays it out.
    /// </summary>
    public void OnUiThread(Action action)
    {
        if (Services.Dispatcher is AvaloniaUiDispatcher)
        {
            Avalonia.Threading.Dispatcher.UIThread.VerifyAccess();
            action();
            return;
        }
        InlineDispatcher.Run(action);
    }

    public async Task<TabViewModel> OpenTabAsync()
    {
        await Shell.OpenFolderAsync(WorkFolder);
        var tab = Shell.SelectedTab!;
        // Idle comes first; the start's own events and context usage request follow, and would undo a status a test sets.
        await Eventually(() => tab.Status == TabStatus.Idle && tab.IsSettled, "the tab to start");
        return tab;
    }

    /// <summary>
    /// Opens History as a click does, on the UI thread (<see cref="OnUiThread"/>): its reading and filtering
    /// finish there one step at a time, as in the app, so the sessions it shows as it reads them can't land between a
    /// filtering's check and what it writes.
    /// </summary>
    public HistoryViewModel OpenHistory()
    {
        OnUiThread(() => Shell.OpenHistoryCommand.Execute(null));
        return Shell.History!;
    }

    /// <summary>
    /// Waits until <paramref name="condition"/> holds, read the way the UI thread would (<see cref="InlineDispatcher.Read"/>),
    /// by the clock (<see cref="Waiting.UntilAsync"/>). Some waits are on real processes (git, for the file index).
    /// </summary>
    public static Task Eventually(Func<bool> condition, string? what = null) =>
        Waiting.UntilAsync(() => InlineDispatcher.Read(condition), what);

    public async ValueTask DisposeAsync()
    {
        // Closed on the UI thread, as the window closes in the app: work still finishing there, such as a Files tab's
        // explorer listing its folder, can't be caught halfway by the tabs being saved and stopped.
        await InlineDispatcher.RunAsync(() => Shell.DisposeAsync().AsTask());
        await Services.DisposeAsync();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (UnauthorizedAccessException)
        {
            // Git makes its objects read-only, and Windows won't delete a read-only file.
            try
            {
                foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
        catch (IOException)
        {
        }
    }
}

internal static class SettingsExtensions
{
    public static QuickSuffix Suffix(this AppServices services, string id) => services.Settings.QuickSuffixes.Single(s => s.Id == id);
}

/// <summary>Fails every request, so a test that doesn't script Claudette's web requests can't reach the network.</summary>
public sealed class OfflineHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("Tests don't reach the network.");
}
