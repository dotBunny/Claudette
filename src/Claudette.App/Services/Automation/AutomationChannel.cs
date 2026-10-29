using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Claudette.App.Services.Automation;

/// <summary>
/// A command to Claudette from the screenshot scripts (DESIGN.md §9, "Taking screenshots without the focus"): what to
/// do, and to which control, named as UI Automation saw it. <see cref="AutomationCommands"/> carries it out.
/// </summary>
public sealed record AutomationRequest
{
    /// <summary>
    /// What to do: <c>ping</c>, <c>click</c>, <c>rightclick</c>, <c>toggle</c>, <c>settext</c>, <c>show</c>,
    /// <c>scroll</c>, <c>hover</c>, <c>unhover</c>, <c>key</c>, <c>capture</c> or <c>close</c>.
    /// </summary>
    public string Verb { get; init; } = "";

    /// <summary>
    /// The native handle of the window, or of the popup (a menu, a tip), the control is in. Zero for the window called
    /// <see cref="Title"/>, or without one, the main window: what a script has on macOS and Linux, without UI Automation.
    /// </summary>
    public long Window { get; init; }

    /// <summary>With no <see cref="Window"/> handle, the title of the window, such as <c>Settings</c>.</summary>
    public string? Title { get; init; }

    /// <summary>For <c>capture</c>: where to save the window's picture, a PNG.</summary>
    public string? Path { get; init; }

    /// <summary>The control's name, as UI Automation has it (its <c>AutomationProperties.Name</c>, or its text).</summary>
    public string? Name { get; init; }

    /// <summary>The control's automation id, its <c>x:Name</c> unless it has one of its own.</summary>
    public string? Id { get; init; }

    /// <summary>The control's type, as UI Automation names it: <c>ListItem</c>, <c>Button</c>, <c>Edit</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Where UI Automation saw the middle of the control, in screen pixels: which one, of several that match.</summary>
    public double? X { get; init; }

    /// <inheritdoc cref="X"/>
    public double? Y { get; init; }

    /// <summary>For <c>settext</c>: the text to put in the box.</summary>
    public string? Text { get; init; }

    /// <summary>For <c>toggle</c>: on or off.</summary>
    public bool? On { get; init; }

    /// <summary>For <c>key</c>: the keys pressed together, such as <c>Ctrl+Shift+P</c> or <c>Esc</c>.</summary>
    public string? Keys { get; init; }

    /// <summary>For <c>scroll</c>: how many lines, down; up when it's less than zero.</summary>
    public int Lines { get; init; }
}

/// <summary>What became of a command: done, or why not; and for <c>toggle</c>, whether the switch is on.</summary>
public sealed record AutomationResponse(bool Ok, string? Error = null, string? Value = null)
{
    public static AutomationResponse Done(string? value = null) => new(true, Value: value);

    public static AutomationResponse Failed(string error) => new(false, error);
}

/// <summary>
/// The demo's command channel (DESIGN.md §9, "Taking screenshots without the focus"): with
/// <c>CLAUDETTE_AUTOMATION_PIPE</c> set, Claudette takes commands from the screenshot scripts on a named pipe of that
/// name (a Unix domain socket on macOS and Linux), one JSON line each, and answers each with one. The scripts drive
/// Claudette this way rather than with the mouse, keys or UI Automation's actions, any of which Windows lets bring a
/// window to the front. Only this user's processes can connect, one at a time.
/// </summary>
public sealed class AutomationChannel : IDisposable
{
    public const string Variable = "CLAUDETTE_AUTOMATION_PIPE";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _pipeName;
    private readonly Func<AutomationRequest, Task<AutomationResponse>> _run;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _listening;

    /// <param name="run">Carries out a command, on the UI thread.</param>
    public AutomationChannel(string pipeName, Func<AutomationRequest, Task<AutomationResponse>> run, ILogger logger)
    {
        _pipeName = pipeName;
        _run = run;
        _logger = logger;
        _listening = Task.Run(() => ListenAsync(_stop.Token));
    }

    /// <summary>The pipe's name from <c>CLAUDETTE_AUTOMATION_PIPE</c>; null, and no channel, without it.</summary>
    public static string? PipeName => Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } name ? name : null;

    /// <summary>Stops listening; a command being answered finishes first.</summary>
    public void Dispose() => _stop.Cancel();

    /// <summary>Ends once the channel has stopped listening, for tests.</summary>
    internal Task Listening => _listening;

    private async Task ListenAsync(CancellationToken token)
    {
        NamedPipeServerStream? next = null;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var pipe = next ?? NewInstance();
                next = null;
                try
                {
                    await using (pipe.ConfigureAwait(false))
                    {
                        await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                        // The next script's instance before this one closes: on macOS and Linux a pipe's instances share
                        // one listening socket, which closes with the last of them, resetting a script already waiting on it.
                        next = NewInstance();
                        using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
                        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                        while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                        {
                            var response = await AnswerAsync(line).ConfigureAwait(false);
                            await writer.WriteLineAsync(JsonSerializer.Serialize(response, Json).AsMemory(), token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (IOException ex)
                {
                    // The script went away mid-command; the next one connects afresh.
                    _logger.LogDebug(ex, "The automation pipe's client went away.");
                }
            }
        }
        finally
        {
            if (next is not null)
            {
                await next.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// One of the pipe's instances: two at most, the one a script is on and the next script's, which waits its turn.
    /// </summary>
    private NamedPipeServerStream NewInstance() => new(_pipeName, PipeDirection.InOut, 2, PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private async Task<AutomationResponse> AnswerAsync(string line)
    {
        AutomationRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AutomationRequest>(line, Json);
        }
        catch (JsonException ex)
        {
            return AutomationResponse.Failed($"That isn't a command: {ex.Message}");
        }
        if (request is null)
        {
            return AutomationResponse.Failed("That isn't a command.");
        }
        try
        {
            return await _run(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The automation command {Verb} failed.", request.Verb);
            return AutomationResponse.Failed(ex.Message);
        }
    }
}
