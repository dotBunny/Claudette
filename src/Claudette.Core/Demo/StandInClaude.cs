using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claudette.Core.Demo;

/// <summary>
/// Claudette standing in for Claude Code in its demo (DESIGN.md §11, "Demo mode"): what <c>Program.Main</c> runs when
/// started with <see cref="Variable"/> set, which only the demo gives its <c>claude</c>. It answers as Claude Code would,
/// and nothing else: <c>--version</c>, <c>auth status</c> (signed in to a Max plan), a stream-json session's control
/// requests (the plan's usage from the demo's file in <c>CLAUDE_CONFIG_DIR</c>), and its messages, each with the next of
/// a few canned turns: a short answer, one with thinking, one that reads a file and writes another behind a permission,
/// and one that asks a question. It never reaches the network, writes nothing and runs nothing.
/// </summary>
public sealed class StandInClaude
{
    /// <summary>In the environment of the demo's <c>claude</c>: Claudette, started so, stands in for it.</summary>
    public const string Variable = "CLAUDETTE_STAND_IN_CLAUDE";

    /// <summary>The demo's plan usage, a <c>get_usage</c> answer, in <c>CLAUDE_CONFIG_DIR</c>.</summary>
    public const string UsageFile = "demo-usage.json";

    /// <summary>The Claude Code it says it is: one Claudette supports.</summary>
    public const string Version = "2.1.284";

    public const string Email = "demo@example.com";

    private const string Model = "claude-opus-5-5";

    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TimeProvider _time;
    private readonly string? _configDirectory;
    private readonly string _folder;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly Queue<string> _prompts = new();
    private readonly SemaphoreSlim _promptSignal = new(0);
    private string _sessionId = Guid.NewGuid().ToString();
    private string _permissionMode = "default";
    private string _model = Model;
    private CancellationTokenSource _turn = new();
    private int _turns;
    private int _requests;
    private long _spentInput;
    private long _spentOutput;
    private double _spentCost;

    private readonly TimeSpan _delay;

    /// <param name="folder">The folder it runs in, the tab's, which its made-up files are in.</param>
    /// <param name="delay">How long it pauses between the pieces of a reply as it streams; none in tests.</param>
    public StandInClaude(TextReader input, TextWriter output, TimeProvider time, string? configDirectory, string folder, TimeSpan? delay = null)
    {
        _input = input;
        _output = output;
        _time = time;
        _configDirectory = configDirectory;
        _folder = folder;
        _delay = delay ?? TimeSpan.FromMilliseconds(60);
    }

    /// <summary>Does what <c>claude</c> with <paramref name="args"/> would; returns its exit code.</summary>
    public async Task<int> RunAsync(string[] args)
    {
        switch (args)
        {
            case ["--version", ..]:
                await _output.WriteLineAsync($"{Version} (Claude Code)");
                await _output.FlushAsync();
                return 0;
            case ["auth", "status", ..]:
                await _output.WriteLineAsync(AuthStatus().ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                await _output.FlushAsync();
                return 0;
            case ["auth", ..] or ["update", ..] or ["doctor", ..] or ["install", ..]:
                await _output.WriteLineAsync("This is Claudette's demo: Claude Code isn't really here.");
                await _output.FlushAsync();
                return args[0] == "update" ? 0 : 1;
        }
        if (!args.Contains("stream-json"))
        {
            await _output.WriteLineAsync("This is Claudette's demo: Claude Code isn't really here.");
            await _output.FlushAsync();
            return 2;
        }
        _permissionMode = Option(args, "--permission-mode") ?? _permissionMode;
        _model = Option(args, "--model") is { } model ? ModelId(model) : _model;
        // A resumed session keeps its id, as Claude Code's does, unless it's forked.
        if (Option(args, "--resume") is { } resumed && !args.Contains("--fork-session"))
        {
            _sessionId = resumed;
        }
        return await RunSessionAsync();
    }

    private JsonObject AuthStatus() => new()
    {
        ["loggedIn"] = true,
        ["authMethod"] = "claude.ai",
        ["apiProvider"] = "firstParty",
        ["email"] = Email,
        ["subscriptionType"] = "max",
        ["projectsDirectory"] = _configDirectory is null ? null : Path.Combine(_configDirectory, "projects"),
        ["configDirectory"] = _configDirectory,
    };

    private static string? Option(string[] args, string name) => Array.IndexOf(args, name) is var at and >= 0 && at + 1 < args.Length ? args[at + 1] : null;

    private static string ModelId(string model) => model.ToLowerInvariant() switch
    {
        var m when m.Contains("sonnet", StringComparison.Ordinal) => "claude-sonnet-5-5",
        var m when m.Contains("haiku", StringComparison.Ordinal) => "claude-haiku-5-5",
        _ => Model,
    };

    // ---- The session ----------------------------------------------------------------------------------------------

    private async Task<int> RunSessionAsync()
    {
        var turns = Task.Factory.StartNew(RunTurnsAsync, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        while (await _input.ReadLineAsync() is { } line)
        {
            JsonObject message;
            try
            {
                if (JsonNode.Parse(line) is not JsonObject parsed)
                {
                    continue;
                }
                message = parsed;
            }
            catch (JsonException)
            {
                continue;
            }
            switch (message["type"]?.GetValue<string>())
            {
                case "control_request":
                    await AnswerAsync(message);
                    break;
                case "control_response" when message["response"] is JsonObject response
                    && response["request_id"]?.GetValue<string>() is { } id && _pending.TryRemove(id, out var waiter):
                    waiter.TrySetResult(response);
                    break;
                case "user":
                    lock (_prompts)
                    {
                        _prompts.Enqueue(message["message"]?["content"] is JsonValue text ? text.ToString() : "");
                    }
                    _promptSignal.Release();
                    break;
            }
        }
        // Standard input closed: Claudette is done with it.
        await _turn.CancelAsync();
        _prompts.Clear();
        _promptSignal.Release();
        await turns;
        return 0;
    }

    private async Task AnswerAsync(JsonObject message)
    {
        var requestId = message["request_id"]?.GetValue<string>() ?? "";
        var request = message["request"] as JsonObject ?? [];
        var subtype = request["subtype"]?.GetValue<string>();
        JsonObject? answer = subtype switch
        {
            "initialize" => new JsonObject
            {
                ["models"] = new JsonArray(
                    ModelEntry("default", Model, "Default (recommended)"),
                    ModelEntry("opus", Model, "Opus 5.5"),
                    ModelEntry("sonnet", "claude-sonnet-5-5", "Sonnet 5.5"),
                    ModelEntry("haiku", "claude-haiku-5-5", "Haiku 5.5")),
                ["commands"] = new JsonArray(
                    new JsonObject { ["name"] = "compact", ["description"] = "Free up context by summarizing the conversation so far", ["argumentHint"] = "<optional custom summarization instructions>", ["builtin"] = true }),
                ["account"] = new JsonObject { ["email"] = Email, ["subscriptionType"] = "Claude Max" },
                ["current_permission_mode"] = _permissionMode,
            },
            "interrupt" => new JsonObject { ["still_queued"] = new JsonArray() },
            "set_model" => SetModel(request),
            "set_permission_mode" => SetPermissionMode(request),
            "apply_flag_settings" or "set_max_thinking_tokens" => [],
            "get_context_usage" => new JsonObject { ["totalTokens"] = 24600, ["maxTokens"] = 200000, ["percentage"] = 12.3 },
            "get_usage" => Usage(),
            _ => null,
        };
        if (subtype == "interrupt")
        {
            await _turn.CancelAsync();
        }
        await WriteAsync(answer is null
            ? new JsonObject { ["type"] = "control_response", ["response"] = new JsonObject { ["subtype"] = "error", ["request_id"] = requestId, ["error"] = $"Not in Claudette's demo: {subtype}" } }
            : new JsonObject { ["type"] = "control_response", ["response"] = new JsonObject { ["subtype"] = "success", ["request_id"] = requestId, ["response"] = answer } });
    }

    private static JsonObject ModelEntry(string value, string model, string name) => new()
    {
        ["value"] = value,
        ["resolvedModel"] = model,
        ["displayName"] = name,
        ["supportsEffort"] = true,
        ["supportedEffortLevels"] = new JsonArray("low", "medium", "high"),
        ["supportsAutoMode"] = true,
    };

    private JsonObject SetModel(JsonObject request)
    {
        if (request["model"]?.GetValue<string>() is { } model)
        {
            _model = ModelId(model);
        }
        return [];
    }

    private JsonObject SetPermissionMode(JsonObject request)
    {
        _permissionMode = request["mode"]?.GetValue<string>() ?? _permissionMode;
        return [];
    }

    /// <summary>The demo's plan usage, as the demo wrote it; none without it, which Claudette takes as no plan limits.</summary>
    private JsonObject? Usage()
    {
        if (_configDirectory is null)
        {
            return null;
        }
        try
        {
            var path = Path.Combine(_configDirectory, UsageFile);
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ---- Its turns ------------------------------------------------------------------------------------------------

    private async Task RunTurnsAsync()
    {
        while (true)
        {
            await _promptSignal.WaitAsync();
            lock (_prompts)
            {
                if (!_prompts.TryDequeue(out _))
                {
                    // Woken to stop: standard input closed.
                    return;
                }
            }
            _turn = new CancellationTokenSource();
            await WriteAsync(new JsonObject
            {
                ["type"] = "system", ["subtype"] = "init", ["session_id"] = _sessionId, ["model"] = _model, ["permissionMode"] = _permissionMode,
                ["claude_code_version"] = Version, ["cwd"] = _folder, ["capabilities"] = new JsonArray("interrupt_receipt_v1"),
                ["slash_commands"] = new JsonArray("compact"),
            });
            try
            {
                var turn = _turns++ % 4;
                await (turn switch
                {
                    0 => AnswerTurnAsync(),
                    1 => ThinkingTurnAsync(),
                    2 => FilesTurnAsync(),
                    _ => QuestionTurnAsync(),
                });
            }
            catch (OperationCanceledException)
            {
                await WriteAsync(new JsonObject { ["type"] = "result", ["subtype"] = "error_during_execution", ["is_error"] = true, ["terminal_reason"] = "aborted_streaming", ["session_id"] = _sessionId });
            }
        }
    }

    /// <summary>A short answer that says what the demo is.</summary>
    private async Task AnswerTurnAsync()
    {
        const string reply = """
            This is **Claudette's demo**, and I'm a stand-in for Claude: I can't read your projects or run anything, and nothing you send leaves this computer.

            Each message gets the next of a few example turns, so you can see how Claudette shows Claude's work: a reply like this one, one with thinking, one that works with files and asks your permission first, and one that asks you a question. Send another message to see the next.
            """;
        await ReplyAsync(reply);
    }

    /// <summary>Thinking first, then an answer with some Markdown in it.</summary>
    private async Task ThinkingTurnAsync()
    {
        await AssistantAsync(new JsonObject
        {
            ["type"] = "thinking",
            ["thinking"] = "The demo's next turn shows thinking. Claudette folds it into a quiet row above the reply, which opens to show it. I'll follow it with a reply that has a list and a code block, as a real one would.",
            ["signature"] = "",
        });
        const string reply = """
            Here's how a turn like this reads in Claudette:

            1. **Thinking** sits in a quiet row above the reply. Click it to open it.
            2. **The reply** is Markdown: lists, tables, links and code blocks, each block with **Copy**.
            3. **The footer** under the turn says how long it took and what it cost in tokens.

            ```csharp
            // Code blocks keep their language's colors.
            var greeting = "Hello from Claudette's demo";
            Console.WriteLine(greeting);
            ```
            """;
        await ReplyAsync(reply);
    }

    /// <summary>Reads a file, then asks to write another; neither happens: the files are made up.</summary>
    private async Task FilesTurnAsync()
    {
        var readme = Path.Combine(_folder, "README.md");
        var notes = Path.Combine(_folder, "NOTES.md");
        await AssistantAsync(new JsonObject { ["type"] = "text", ["text"] = "I'll look at the README first, then write down what I found." });
        await AssistantAsync(new JsonObject { ["type"] = "tool_use", ["id"] = "toolu_demo_read", ["name"] = "Read", ["input"] = new JsonObject { ["file_path"] = readme } });
        await PauseAsync();
        await ToolResultAsync("toolu_demo_read", "     1\t# A demo project\n     2\t\n     3\tMade up for Claudette's demo: there are no files here really.\n");
        var content = "# Notes\n\nThe README says this project is made up for Claudette's demo.\n";
        var input = new JsonObject { ["file_path"] = notes, ["content"] = content };
        await AssistantAsync(new JsonObject { ["type"] = "tool_use", ["id"] = "toolu_demo_write", ["name"] = "Write", ["input"] = input.DeepClone() });
        var answer = await AskAsync(new JsonObject { ["subtype"] = "can_use_tool", ["tool_name"] = "Write", ["input"] = input, ["tool_use_id"] = "toolu_demo_write" });
        var allowed = answer["response"]?["behavior"]?.GetValue<string>() == "allow";
        await ToolResultAsync("toolu_demo_write", allowed
            ? $"File created successfully at: {notes}"
            : "The user doesn't want to proceed with this tool use. The tool use was rejected.", isError: !allowed);
        await ReplyAsync(allowed
            ? "Done: in a real session, `NOTES.md` would now hold what I found. Here nothing was written: the demo's files are made up."
            : "All right, I won't write it. In a real session, I'd ask what you'd like instead.");
    }

    /// <summary>Asks a question, as Claude does when it needs a choice made, and goes by the answer.</summary>
    private async Task QuestionTurnAsync()
    {
        var input = new JsonObject
        {
            ["questions"] = new JsonArray(new JsonObject
            {
                ["question"] = "Which part of Claudette would you like to look at next?",
                ["header"] = "Next",
                ["multiSelect"] = false,
                ["options"] = new JsonArray(
                    new JsonObject { ["label"] = "Usage", ["description"] = "The meters in the header, and the Usage window they open" },
                    new JsonObject { ["label"] = "History", ["description"] = "Past sessions, by project, to pick up again" },
                    new JsonObject { ["label"] = "Threads", ["description"] = "A tab that hands work to others in its folder" }),
            }),
        };
        await AssistantAsync(new JsonObject { ["type"] = "tool_use", ["id"] = "toolu_demo_ask", ["name"] = "AskUserQuestion", ["input"] = input.DeepClone() });
        var answer = await AskAsync(new JsonObject { ["subtype"] = "can_use_tool", ["tool_name"] = "AskUserQuestion", ["input"] = input, ["tool_use_id"] = "toolu_demo_ask" });
        var choice = (answer["response"]?["updatedInput"]?["answers"] as JsonObject)?.Select(a => a.Value?.ToString()).FirstOrDefault(a => a is { Length: > 0 });
        await ToolResultAsync("toolu_demo_ask", choice is null ? "The user didn't answer." : $"User has answered your questions: \"Which part of Claudette would you like to look at next?\"=\"{choice}\".", isError: choice is null);
        await ReplyAsync(choice switch
        {
            "Usage" => "The usage header across the top shows your plan's session and weekly limits as you go, and how fast you're using them. Click it for the Usage window.",
            "History" => "History, the clock at the top of the sidebar, lists past sessions by project. Pick one up and it opens in a tab where it left off.",
            "Threads" => "**Move to the Input System** in the sidebar is a thread: its Claude handed work to the two tabs under it, and gets their results back.",
            null => "No answer, no problem: in a real session I'd carry on with my best judgment.",
            var other => $"You chose {other}. In a real session I'd carry on from there.",
        });
    }

    // ---- Writing what Claude Code writes --------------------------------------------------------------------------

    /// <summary>Streams <paramref name="reply"/> in a few pieces, then sends it whole and ends the turn.</summary>
    private async Task ReplyAsync(string reply)
    {
        reply = reply.ReplaceLineEndings("\n");
        const int pieces = 6;
        var size = Math.Max(1, (reply.Length + pieces - 1) / pieces);
        for (var at = 0; at < reply.Length; at += size)
        {
            await WriteAsync(new JsonObject
            {
                ["type"] = "stream_event",
                ["event"] = new JsonObject { ["type"] = "content_block_delta", ["index"] = 0, ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = reply.Substring(at, Math.Min(size, reply.Length - at)) } },
                ["parent_tool_use_id"] = null,
            });
            await PauseAsync();
        }
        await AssistantAsync(new JsonObject { ["type"] = "text", ["text"] = reply });
        await ResultAsync(reply);
    }

    private async Task PauseAsync()
    {
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, _time, _turn.Token);
        }
    }

    private Task AssistantAsync(JsonObject block) => WriteAsync(new JsonObject
    {
        ["type"] = "assistant",
        ["message"] = new JsonObject { ["id"] = $"msg_demo_{_turns}", ["model"] = _model, ["role"] = "assistant", ["content"] = new JsonArray(block) },
        ["parent_tool_use_id"] = null,
        ["session_id"] = _sessionId,
    });

    private Task ToolResultAsync(string toolUseId, string text, bool isError = false) => WriteAsync(new JsonObject
    {
        ["type"] = "user",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = toolUseId, ["content"] = text, ["is_error"] = isError }) },
        ["parent_tool_use_id"] = null,
        ["session_id"] = _sessionId,
    });

    /// <summary>A turn's result: a small turn's tokens, and what the session has spent so far, as Claude Code reports it.</summary>
    private Task ResultAsync(string text)
    {
        const long input = 1800, output = 240, cacheRead = 22000;
        _spentInput += input;
        _spentOutput += output;
        _spentCost += 0.04;
        return WriteAsync(new JsonObject
        {
            ["type"] = "result", ["subtype"] = "success", ["is_error"] = false, ["result"] = text, ["terminal_reason"] = "completed",
            ["session_id"] = _sessionId, ["duration_ms"] = 2400, ["num_turns"] = 1,
            ["usage"] = new JsonObject { ["input_tokens"] = input, ["output_tokens"] = output, ["cache_read_input_tokens"] = cacheRead, ["cache_creation_input_tokens"] = 0 },
            ["total_cost_usd"] = _spentCost,
            ["modelUsage"] = new JsonObject
            {
                [_model] = new JsonObject
                {
                    ["inputTokens"] = _spentInput, ["outputTokens"] = _spentOutput, ["cacheReadInputTokens"] = cacheRead * _turns, ["cacheCreationInputTokens"] = 0,
                    ["costUSD"] = _spentCost, ["contextWindow"] = 200000,
                },
            },
        });
    }

    /// <summary>A control request to Claudette, such as a permission: its answer.</summary>
    private async Task<JsonObject> AskAsync(JsonObject request)
    {
        var requestId = $"demo_{Interlocked.Increment(ref _requests)}";
        var waiter = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = waiter;
        await WriteAsync(new JsonObject { ["type"] = "control_request", ["request_id"] = requestId, ["request"] = request });
        return await waiter.Task.WaitAsync(_turn.Token);
    }

    private async Task WriteAsync(JsonObject message)
    {
        await _writeLock.WaitAsync();
        try
        {
            await _output.WriteLineAsync(message.ToJsonString());
            await _output.FlushAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
