using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Claudette.MockApi;

/// <summary>A request the mock received, for tests to check what reached the API.</summary>
/// <param name="LastUserImages">The images in the latest user message, in order.</param>
/// <param name="ApiKey">The <c>x-api-key</c> the request came with: which key Claude Code used.</param>
/// <param name="System">The text of the request's system prompt, so tests can see what Claude was told.</param>
public sealed record RecordedRequest(
    string Method,
    string Path,
    string? Model,
    string? Effort,
    int ToolCount,
    int MessageCount,
    string LastUserText,
    string Reply,
    string LastToolResultText = "",
    IReadOnlyList<RecordedImage>? LastUserImages = null,
    string? ApiKey = null,
    string System = "");

/// <summary>An image that reached the API: its media type, size, and for a PNG its dimensions.</summary>
public sealed record RecordedImage(string MediaType, int Bytes, int? Width, int? Height);

/// <summary>
/// A fake Anthropic Messages API, so the real <c>claude</c> CLI can be driven without tokens (DESIGN.md §15,
/// "Real CLI against a fake model"). Point Claude Code at <see cref="BaseAddress"/> with <c>ANTHROPIC_BASE_URL</c>
/// and any <c>ANTHROPIC_API_KEY</c>. Replies are scripted from keywords in the latest prompt:
/// <list type="bullet">
/// <item><c>WRITE_FILE &lt;path&gt;</c>: a Write tool call, then "Done with the tool."</item>
/// <item><c>READ_FILE &lt;path&gt;</c>: a Read tool call, then "Done with the tool." An image file comes back as an image.</item>
/// <item><c>EDIT_FILE &lt;path&gt;</c>: a Read, then an Edit replacing <c>ORIGINAL LINE</c> with <c>EDITED LINE</c>, then done.</item>
/// <item><c>RUN_BASH &lt;command&gt;</c>: a Bash tool call, then done.</item>
/// <item><c>SLOW</c>: text streamed in small chunks over about 20 seconds.</item>
/// <item><c>ASK_QUESTION</c>: an AskUserQuestion tool call ("Which database?": Postgres or SQLite), then done.</item>
/// <item>
/// <c>EXIT_PLAN</c>: a Write of a two-step plan to the plan file Claude Code names, then an ExitPlanMode tool call,
/// then done. Needs plan mode.
/// </item>
/// <item><c>API_ERROR</c>: the first two requests fail with 529 "overloaded", so Claude Code retries; then <c>pong</c>.</item>
/// <item>
/// <c>SUBAGENTS</c>: two subagents in parallel, in the foreground. "Touch a marker file" runs
/// <c>touch agent-marker.txt</c>, which asks for permission from inside the subagent; "Delegate a deeper look" starts
/// a nested Explore subagent that replies "Found 3 matches in src/.". Then done.
/// </item>
/// <item><c>LONG_AGENT</c>: one subagent that runs <c>sleep 30</c>, for stopping it; in the background with <c>BACKGROUND</c>.</item>
/// <item>
/// <c>SUBAGENT_WRITE &lt;path&gt;</c> and <c>SUBAGENT_EDIT &lt;path&gt;</c>: one subagent that does <c>WRITE_FILE</c> or
/// <c>EDIT_FILE</c> on the path, in a worktree of its own (<c>isolation: worktree</c>) with <c>ISOLATED</c> after it.
/// </item>
/// <item>
/// <c>ENTER_WORKTREE &lt;name&gt;</c> and <c>EXIT_WORKTREE keep|remove</c>: an EnterWorktree or ExitWorktree tool call,
/// then done.
/// </item>
/// <item><c>AGENT_REPLY &lt;text&gt;</c>: replies with the text (how the subagents above answer).</item>
/// <item>
/// <c>SEND_MESSAGE &lt;to&gt; | &lt;message&gt;</c>: a SendMessage tool call, then done, for threads (DESIGN.md §18). The
/// message can script the recipient, such as <c>AGENT_REPLY The art page is done.</c>
/// </item>
/// <item>Requests with no tools that mention "title": <c>{"title": "Mock session title"}</c>.</item>
/// <item>
/// Claude Code's request for a prompt suggestion (<c>--prompt-suggestions</c>, its prompt starting
/// <c>[SUGGESTION MODE</c>): <c>run the tests</c>.
/// </item>
/// <item>Anything else: <c>pong</c>.</item>
/// </list>
/// Ported from <c>spikes/mock-server.mjs</c>.
/// </summary>
public sealed partial class MockAnthropicApi : IAsyncDisposable
{
    /// <summary>The plan <c>EXIT_PLAN</c> writes.</summary>
    private const string PlanText = "1. Read the code\n2. Fix the bug";

    /// <summary>How Claude Code's request for a prompt suggestion starts.</summary>
    private const string SuggestionPrompt = "[SUGGESTION MODE";

    /// <summary>The prompt suggestion the mock gives.</summary>
    public const string Suggestion = "run the tests";

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private int _counter;
    private int _apiErrors;

    private MockAnthropicApi(WebApplication app)
    {
        _app = app;
    }

    public Uri BaseAddress { get; private set; } = null!;

    public IReadOnlyList<RecordedRequest> Requests => _requests.ToArray();

    /// <summary>Starts the mock on 127.0.0.1. Port 0 picks a free port.</summary>
    public static async Task<MockAnthropicApi> StartAsync(int port = 0)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();
        var mock = new MockAnthropicApi(app);
        app.Run(mock.HandleAsync);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        mock.BaseAddress = new Uri(address);
        return mock;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private async Task HandleAsync(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? "";
        JsonObject body = [];
        if (request.ContentLength is > 0 || request.Headers.TransferEncoding.Count > 0)
        {
            try
            {
                body = await JsonNode.ParseAsync(request.Body) as JsonObject ?? [];
            }
            catch (JsonException)
            {
                // Not JSON.
            }
        }

        if (path.StartsWith("/v1/messages/count_tokens", StringComparison.Ordinal))
        {
            Record(request, path, body, "count_tokens");
            await context.Response.WriteAsJsonAsync(new { input_tokens = 100 });
            return;
        }
        if (!path.StartsWith("/v1/messages", StringComparison.Ordinal))
        {
            Record(request, path, body, "not_found");
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { type = "error", error = new { type = "not_found_error", message = "mock: not implemented" } });
            return;
        }

        // API_ERROR: overloaded twice, so Claude Code reports its retries (system/api_retry), then a normal reply.
        if (LastUserText(body).Text.Contains("API_ERROR", StringComparison.Ordinal) && Interlocked.Increment(ref _apiErrors) <= 2)
        {
            Record(request, path, body, "overloaded");
            context.Response.StatusCode = 529;
            await context.Response.WriteAsJsonAsync(new { type = "error", error = new { type = "overloaded_error", message = "mock: overloaded" } });
            return;
        }

        var plan = Script(body);
        Record(request, path, body, plan.Kind);
        var id = $"msg_mock_{Interlocked.Increment(ref _counter)}";
        var stopReason = plan.Blocks.Any(b => b["type"]!.GetValue<string>() == "tool_use") ? "tool_use" : "end_turn";
        var usage = new JsonObject { ["input_tokens"] = 1000, ["output_tokens"] = 20, ["cache_creation_input_tokens"] = 0, ["cache_read_input_tokens"] = 0 };

        if (body["stream"]?.GetValue<bool>() != true)
        {
            await context.Response.WriteAsync(new JsonObject
            {
                ["id"] = id, ["type"] = "message", ["role"] = "assistant", ["model"] = body["model"]?.DeepClone(),
                ["content"] = new JsonArray(plan.Blocks.Select(b => (JsonNode?)b.DeepClone()).ToArray()),
                ["stop_reason"] = stopReason, ["stop_sequence"] = null, ["usage"] = usage,
            }.ToJsonString());
            return;
        }

        var response = context.Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        var aborted = context.RequestAborted;
        var startUsage = usage.DeepClone();
        startUsage["output_tokens"] = 1;
        await SseAsync(response, "message_start", new JsonObject
        {
            ["type"] = "message_start",
            ["message"] = new JsonObject
            {
                ["id"] = id, ["type"] = "message", ["role"] = "assistant", ["model"] = body["model"]?.DeepClone(),
                ["content"] = new JsonArray(), ["stop_reason"] = null, ["stop_sequence"] = null, ["usage"] = startUsage,
            },
        }, aborted);
        for (var i = 0; i < plan.Blocks.Count; i++)
        {
            var block = plan.Blocks[i];
            if (block["type"]!.GetValue<string>() == "text")
            {
                await SseAsync(response, "content_block_start", new JsonObject { ["type"] = "content_block_start", ["index"] = i, ["content_block"] = new JsonObject { ["type"] = "text", ["text"] = "" } }, aborted);
                var text = block["text"]!.GetValue<string>();
                var chunks = plan.ChunkDelay is null ? [text] : text.Chunk(5).Select(c => new string(c)).ToArray();
                foreach (var chunk in chunks)
                {
                    if (aborted.IsCancellationRequested)
                    {
                        return;
                    }
                    await SseAsync(response, "content_block_delta", new JsonObject { ["type"] = "content_block_delta", ["index"] = i, ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = chunk } }, aborted);
                    if (plan.ChunkDelay is { } delay)
                    {
                        try
                        {
                            await Task.Delay(delay, aborted);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
            }
            else
            {
                await SseAsync(response, "content_block_start", new JsonObject
                {
                    ["type"] = "content_block_start", ["index"] = i,
                    ["content_block"] = new JsonObject { ["type"] = "tool_use", ["id"] = block["id"]!.DeepClone(), ["name"] = block["name"]!.DeepClone(), ["input"] = new JsonObject() },
                }, aborted);
                await SseAsync(response, "content_block_delta", new JsonObject
                {
                    ["type"] = "content_block_delta", ["index"] = i,
                    ["delta"] = new JsonObject { ["type"] = "input_json_delta", ["partial_json"] = block["input"]!.ToJsonString() },
                }, aborted);
            }
            await SseAsync(response, "content_block_stop", new JsonObject { ["type"] = "content_block_stop", ["index"] = i }, aborted);
        }
        await SseAsync(response, "message_delta", new JsonObject { ["type"] = "message_delta", ["delta"] = new JsonObject { ["stop_reason"] = stopReason, ["stop_sequence"] = null }, ["usage"] = new JsonObject { ["output_tokens"] = 20 } }, aborted);
        await SseAsync(response, "message_stop", new JsonObject { ["type"] = "message_stop" }, aborted);
    }

    private static async Task SseAsync(HttpResponse response, string eventName, JsonObject data, CancellationToken cancellationToken)
    {
        try
        {
            await response.WriteAsync($"event: {eventName}\ndata: {data.ToJsonString()}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The client went away (for example after an interrupt).
        }
    }

    private void Record(HttpRequest request, string path, JsonObject body, string reply)
    {
        _requests.Enqueue(new RecordedRequest(
            request.Method,
            path,
            body["model"]?.GetValue<string>(),
            body["output_config"]?["effort"]?.GetValue<string>(),
            (body["tools"] as JsonArray)?.Count ?? 0,
            (body["messages"] as JsonArray)?.Count ?? 0,
            LastUserText(body).Text,
            reply,
            LastToolResultText(body),
            LastUserImages(body),
            request.Headers["x-api-key"].FirstOrDefault(),
            SystemText(body)));
    }

    /// <summary>The system prompt's text: its blocks' text, one after another, or the string it is.</summary>
    private static string SystemText(JsonObject body) => body["system"] switch
    {
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonArray blocks => string.Join("\n", blocks.OfType<JsonObject>().Select(b => b["text"]?.GetValue<string>())),
        _ => "",
    };

    private sealed record Plan(string Kind, List<JsonObject> Blocks, TimeSpan? ChunkDelay = null);

    private Plan Script(JsonObject body)
    {
        var system = body["system"]?.ToJsonString() ?? "";
        var (text, hasToolResult) = LastUserText(body);
        var tools = (body["tools"] as JsonArray)?.OfType<JsonObject>().Select(t => t["name"]?.GetValue<string>()).ToHashSet() ?? [];

        if (tools.Count == 0 && TitlePattern().IsMatch(system + text))
        {
            return new Plan("title", [TextBlock("""{"title": "Mock session title"}""")]);
        }
        // As Claude Code 2.1.284 asks for one; it drops a suggestion shorter than two words, such as "pong".
        if (text.StartsWith(SuggestionPrompt, StringComparison.Ordinal))
        {
            return new Plan("suggestion", [TextBlock(Suggestion)]);
        }
        // Compacting asks for the summary in a <summary> block. Since 2.1.311 Claude Code asks once more when the reply
        // has none, so a "pong" would cost it a second call.
        if (CompactionPattern().IsMatch(text))
        {
            return new Plan("compaction", [TextBlock("<summary>The user said hello, and Claude answered.</summary>")]);
        }

        // EDIT_FILE needs two tool rounds (Claude Code requires a Read before an Edit), so count the tool rounds
        // since the prompt that asked for it.
        var messages = (body["messages"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var promptIndex = Array.FindLastIndex(messages, m => m["role"]?.GetValue<string>() == "user" && KeywordPattern().IsMatch(MessageText(m)));
        var prompt = promptIndex >= 0 ? MessageText(messages[promptIndex]) : "";
        var toolRounds = promptIndex >= 0 ? messages.Skip(promptIndex + 1).Count(m => m["role"]?.GetValue<string>() == "user") : 0;
        if (EditPattern().Match(prompt) is { Success: true } edit && tools.Contains("Edit"))
        {
            var filePath = edit.Groups[1].Value;
            if (toolRounds == 0)
            {
                return new Plan("tool-read", [ToolUse("Read", new JsonObject { ["file_path"] = filePath })]);
            }
            if (toolRounds == 1)
            {
                return new Plan("tool-edit", [ToolUse("Edit", new JsonObject { ["file_path"] = filePath, ["old_string"] = "ORIGINAL LINE", ["new_string"] = "EDITED LINE" })]);
            }
        }
        // Claude Code 2.1.308 holds some tools back, ExitPlanMode and SendMessage among them: the model loads one with
        // ToolSearch ("select:<name>") before it can call it, and the next request offers it. The mock does the same for
        // a tool it means to call. Each step is taken once, as the tool calls since the step's prompt show.
        var deferred = DeferredTools(SystemText(body) + "\n" + string.Join("\n", messages.Select(MessageText)));
        bool Reachable(string tool) => tools.Contains(tool) || (tools.Contains("ToolSearch") && deferred.Contains(tool));
        Plan? CallOrLoad(string tool, IReadOnlyList<string> called, Func<Plan> call) =>
            tools.Contains(tool) ? call()
            : called.Contains($"ToolSearch:{tool}") ? null
            : new Plan("tool-search", [ToolUse("ToolSearch", new JsonObject { ["query"] = $"select:{tool}", ["max_results"] = 1 })]);

        // EXIT_PLAN writes the plan to the file Claude Code's plan mode reminder names, as a model does, then calls
        // ExitPlanMode, which reads the plan from that file (2.1.285 drops a plan passed as input). With no plan file
        // named, the plan goes in the input.
        if (prompt.Contains("EXIT_PLAN", StringComparison.Ordinal) && Reachable("ExitPlanMode"))
        {
            var called = ToolCallsSince(messages, promptIndex);
            var planFile = PlanFilePattern().Match(string.Join("\n", messages.Select(MessageText)));
            if (planFile.Success && !called.Contains("Write"))
            {
                return new Plan("tool-plan-file", [ToolUse("Write", new JsonObject { ["file_path"] = planFile.Groups[1].Value, ["content"] = PlanText })]);
            }
            if (!called.Contains("ExitPlanMode")
                && CallOrLoad("ExitPlanMode", called, () => new Plan("tool-plan", [ToolUse("ExitPlanMode", planFile.Success ? [] : new JsonObject { ["plan"] = PlanText })])) is { } exit)
            {
                return exit;
            }
        }
        // ENTER_WORKTREE and EXIT_WORKTREE: Claude moving into a worktree of its own mid-session, and out again.
        if (WorktreePattern().Match(prompt) is { Success: true } worktree)
        {
            var (tool, input) = worktree.Groups[1].Value == "ENTER"
                ? ("EnterWorktree", new JsonObject { ["name"] = worktree.Groups[2].Value })
                : ("ExitWorktree", new JsonObject { ["action"] = worktree.Groups[2].Value });
            if (Reachable(tool) && ToolCallsSince(messages, promptIndex) is var called && !called.Contains(tool)
                && CallOrLoad(tool, called, () => new Plan("tool-worktree", [ToolUse(tool, input)])) is { } move)
            {
                return move;
            }
        }
        // SEND_MESSAGE, a thread's SendMessage call (DESIGN.md §18).
        var sendIndex = Array.FindLastIndex(messages, m => m["role"]?.GetValue<string>() == "user" && SendPattern().IsMatch(MessageText(m)));
        if (sendIndex >= 0 && Reachable("SendMessage") && ToolCallsSince(messages, sendIndex) is var sent && !sent.Contains("SendMessage")
            && SendPattern().Match(MessageText(messages[sendIndex])) is var send
            && CallOrLoad("SendMessage", sent, () => new Plan("tool-send", [ToolUse("SendMessage", new JsonObject { ["to"] = send.Groups[1].Value.Trim(), ["message"] = send.Groups[2].Value.Trim() })])) is { } message)
        {
            return message;
        }

        if (hasToolResult)
        {
            return new Plan("after-tool", [TextBlock("Done with the tool.")]);
        }
        // Subagents (DESIGN.md §18): each one's first request carries its prompt, which scripts it in turn.
        var agentTool = tools.Contains("Agent") ? "Agent" : "Task";
        if (tools.Contains(agentTool))
        {
            if (text.Contains("SUBAGENTS", StringComparison.Ordinal))
            {
                return new Plan("agents",
                [
                    TextBlock("I'll split this into two parts."),
                    ToolUse(agentTool, AgentInput("Touch a marker file", "general-purpose", "Create the marker file.\n\nRUN_BASH touch agent-marker.txt")),
                    ToolUse(agentTool, AgentInput("Delegate a deeper look", "general-purpose", "NESTED_AGENT: hand the search to a helper and report back.")),
                ]);
            }
            if (text.Contains("NESTED_AGENT", StringComparison.Ordinal))
            {
                return new Plan("nested-agent", [ToolUse(agentTool, AgentInput("Search deeper", "Explore", "AGENT_REPLY Found 3 matches in src/."))]);
            }
            // SUBAGENT_WRITE and SUBAGENT_EDIT: one subagent that writes or edits the file, in a worktree of its own with ISOLATED.
            if (SubagentFilePattern().Match(text) is { Success: true } file)
            {
                var input = AgentInput("Change a file", "general-purpose", $"{file.Groups[1].Value}_FILE {file.Groups[2].Value}");
                if (file.Groups[3].Success)
                {
                    input["isolation"] = "worktree";
                }
                return new Plan("agent-file", [ToolUse(agentTool, input)]);
            }
            if (text.Contains("LONG_AGENT", StringComparison.Ordinal))
            {
                return new Plan("long-agent", [ToolUse(agentTool, AgentInput("Long job", "general-purpose", "RUN_BASH sleep 30", background: text.Contains("BACKGROUND", StringComparison.Ordinal)))]);
            }
        }
        if (AgentReplyPattern().Match(text) is { Success: true } reply)
        {
            return new Plan("agent-reply", [TextBlock(reply.Groups[1].Value.Trim())]);
        }
        if (ReadPattern().Match(text) is { Success: true } read && tools.Contains("Read"))
        {
            return new Plan("tool", [ToolUse("Read", new JsonObject { ["file_path"] = read.Groups[1].Value })]);
        }
        if (WritePattern().Match(text) is { Success: true } write && tools.Contains("Write"))
        {
            return new Plan("tool", [ToolUse("Write", new JsonObject { ["file_path"] = write.Groups[1].Value, ["content"] = "hello from mock\n" })]);
        }
        if (BashPattern().Match(text) is { Success: true } bash && tools.Contains("Bash"))
        {
            return new Plan("tool", [ToolUse("Bash", new JsonObject { ["command"] = bash.Groups[1].Value.Trim(), ["description"] = "mock command" })]);
        }
        if (text.Contains("ASK_QUESTION", StringComparison.Ordinal) && tools.Contains("AskUserQuestion"))
        {
            var question = new JsonObject
            {
                ["question"] = "Which database?", ["header"] = "Database", ["multiSelect"] = false,
                ["options"] = new JsonArray(
                    new JsonObject { ["label"] = "Postgres", ["description"] = "Relational" },
                    new JsonObject { ["label"] = "SQLite", ["description"] = "A single file" }),
            };
            return new Plan("tool-ask", [ToolUse("AskUserQuestion", new JsonObject { ["questions"] = new JsonArray(question) })]);
        }
        if (text.Contains("SLOW", StringComparison.Ordinal))
        {
            return new Plan("slow", [TextBlock(string.Concat(Enumerable.Repeat("slow ", 40)))], TimeSpan.FromMilliseconds(500));
        }
        return new Plan("text", [TextBlock("pong")]);
    }

    /// <summary>
    /// The tools the model called after <paramref name="index"/>'s message, by name, and each <c>ToolSearch</c> that
    /// loaded one as <c>ToolSearch:&lt;name&gt;</c>.
    /// </summary>
    private static IReadOnlyList<string> ToolCallsSince(JsonObject[] messages, int index) =>
    [
        .. messages.Skip(index + 1)
            .Where(m => m["role"]?.GetValue<string>() == "assistant" && m["content"] is JsonArray)
            .SelectMany(m => ((JsonArray)m["content"]!).OfType<JsonObject>())
            .Where(b => b["type"]?.GetValue<string>() == "tool_use")
            .Select(b => b["name"]?.GetValue<string>() is "ToolSearch" && b["input"]?["query"]?.GetValue<string>() is { } query && query.StartsWith("select:", StringComparison.Ordinal)
                ? $"ToolSearch:{query["select:".Length..]}"
                : b["name"]?.GetValue<string>() ?? ""),
    ];

    /// <summary>
    /// The tools Claude Code holds back for ToolSearch to load, from its reminder: "The following deferred tools are now
    /// available via ToolSearch. … before calling them:" and a name a line, up to a blank line.
    /// </summary>
    private static IReadOnlySet<string> DeferredTools(string text) =>
        DeferredPattern().Match(text) is { Success: true } list
            ? list.Groups[1].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    private JsonObject ToolUse(string name, JsonObject input) =>
        new() { ["type"] = "tool_use", ["id"] = $"toolu_mock_{Interlocked.Increment(ref _counter)}", ["name"] = name, ["input"] = input };

    private static JsonObject TextBlock(string text) => new() { ["type"] = "text", ["text"] = text };

    private static JsonObject AgentInput(string description, string type, string prompt, bool background = false) => new()
    {
        ["description"] = description, ["subagent_type"] = type, ["prompt"] = prompt, ["run_in_background"] = background,
    };

    private static (string Text, bool HasToolResult) LastUserText(JsonObject body)
    {
        var messages = (body["messages"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var last = messages.LastOrDefault(m => m["role"]?.GetValue<string>() == "user");
        var hasToolResult = last?["content"] is JsonArray blocks && blocks.OfType<JsonObject>().Any(b => b["type"]?.GetValue<string>() == "tool_result");
        return (last is null ? "" : MessageText(last), hasToolResult);
    }

    /// <summary>The text of the tool results in the latest user message, so tests can see what a tool returned.</summary>
    private static string LastToolResultText(JsonObject body)
    {
        var messages = (body["messages"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var last = messages.LastOrDefault(m => m["role"]?.GetValue<string>() == "user");
        if (last?["content"] is not JsonArray blocks)
        {
            return "";
        }
        return string.Join("\n", blocks.OfType<JsonObject>().Where(b => b["type"]?.GetValue<string>() == "tool_result").Select(b => b["content"] switch
        {
            JsonValue value when value.TryGetValue<string>(out var s) => s,
            JsonArray parts => string.Join("\n", parts.OfType<JsonObject>().Select(p => p["text"]?.GetValue<string>())),
            _ => "",
        }));
    }

    /// <summary>The base64 image blocks in the latest user message.</summary>
    private static IReadOnlyList<RecordedImage> LastUserImages(JsonObject body)
    {
        var last = (body["messages"] as JsonArray)?.OfType<JsonObject>().LastOrDefault(m => m["role"]?.GetValue<string>() == "user");
        if (last?["content"] is not JsonArray blocks)
        {
            return [];
        }
        return blocks.OfType<JsonObject>()
            .Where(b => b["type"]?.GetValue<string>() == "image")
            .Select(b =>
            {
                var mediaType = b["source"]?["media_type"]?.GetValue<string>() ?? "";
                byte[] data;
                try
                {
                    data = Convert.FromBase64String(b["source"]?["data"]?.GetValue<string>() ?? "");
                }
                catch (FormatException)
                {
                    data = [];
                }
                // A PNG's width and height are the first fields of its IHDR chunk, at bytes 16 and 20.
                var isPng = data.Length >= 24 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G';
                return new RecordedImage(
                    mediaType,
                    data.Length,
                    isPng ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16)) : null,
                    isPng ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20)) : null);
            })
            .ToArray();
    }

    /// <summary>A message's plain text: its string content, or its text blocks joined.</summary>
    private static string MessageText(JsonObject message) => message["content"] switch
    {
        JsonValue value when value.TryGetValue<string>(out var s) => s,
        JsonArray blocks => string.Join("\n", blocks.OfType<JsonObject>().Where(b => b["type"]?.GetValue<string>() == "text").Select(b => b["text"]?.GetValue<string>())),
        _ => "",
    };

    [GeneratedRegex("title", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePattern();

    /// <summary>
    /// Claude Code's request for a summary, which asks for "a &lt;summary&gt; block" or for it "inside
    /// &lt;summary&gt;&lt;/summary&gt; tags". Not any &lt;summary&gt;: a background task's notification has one too.
    /// </summary>
    [GeneratedRegex(@"<summary> block|<summary></summary> tags")]
    private static partial Regex CompactionPattern();

    [GeneratedRegex(@"(ENTER|EXIT)_WORKTREE (\S+)")]
    private static partial Regex WorktreePattern();

    [GeneratedRegex(@"SUBAGENT_(WRITE|EDIT) (\S+)( ISOLATED)?")]
    private static partial Regex SubagentFilePattern();

    [GeneratedRegex("EDIT_FILE|WRITE_FILE|RUN_BASH|SLOW|ASK_QUESTION|EXIT_PLAN|_WORKTREE|hello")]
    private static partial Regex KeywordPattern();

    [GeneratedRegex(@"EDIT_FILE (\S+)")]
    private static partial Regex EditPattern();

    [GeneratedRegex(@"WRITE_FILE (\S+)")]
    private static partial Regex WritePattern();

    [GeneratedRegex(@"READ_FILE (\S+)")]
    private static partial Regex ReadPattern();

    [GeneratedRegex(@"RUN_BASH (.+)")]
    private static partial Regex BashPattern();

    [GeneratedRegex(@"SEND_MESSAGE (.+?) \| (.+)")]
    private static partial Regex SendPattern();

    [GeneratedRegex(@"AGENT_REPLY (.+)")]
    private static partial Regex AgentReplyPattern();

    /// <summary>Where the plan mode reminder asks for the plan: "You should create your plan at &lt;path&gt; using the Write tool."</summary>
    [GeneratedRegex(@"create your plan at (.+?\.md) using the Write tool")]
    private static partial Regex PlanFilePattern();

    /// <summary>Claude Code's list of the tools it holds back for ToolSearch (2.1.308): a name a line, after its header.</summary>
    [GeneratedRegex(@"deferred tools are now available via ToolSearch[^\n]*\n((?:[A-Za-z_][\w-]*\r?\n?)+)")]
    private static partial Regex DeferredPattern();
}
