using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Claudette.Core.Demo;

namespace Claudette.Core.Tests.Demo;

/// <summary>
/// Claudette standing in for Claude Code in its demo (DESIGN.md §11, "Demo mode"): it says it's Claude Code, signed in,
/// and answers a stream-json session as Claude Code would, with canned turns, writing nothing and running nothing.
/// </summary>
public sealed class StandInClaudeTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("claudette-stand-in").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Lines the test writes, read as standard input.</summary>
    private sealed class Input : TextReader
    {
        private readonly Channel<string?> _lines = Channel.CreateUnbounded<string?>();

        public void Send(JsonObject message) => _lines.Writer.TryWrite(message.ToJsonString());

        public void End() => _lines.Writer.TryWrite(null);

        public override async Task<string?> ReadLineAsync() => await _lines.Reader.ReadAsync();
    }

    /// <summary>Its standard output, a message at a time.</summary>
    private sealed class Output : TextWriter
    {
        public Channel<JsonObject> Messages { get; } = Channel.CreateUnbounded<JsonObject>();

        public override Encoding Encoding => Encoding.UTF8;

        public string Text { get; private set; } = "";

        public override Task WriteLineAsync(string? value)
        {
            Text += value + "\n";
            if (value is { Length: > 0 } && value.StartsWith('{') && JsonNode.Parse(value) is JsonObject message)
            {
                Messages.Writer.TryWrite(message);
            }
            return Task.CompletedTask;
        }

        public async Task<JsonObject> NextAsync(Func<JsonObject, bool> wanted)
        {
            using var timeout = new CancellationTokenSource(Waiting.Timeout);
            while (true)
            {
                var message = await Messages.Reader.ReadAsync(timeout.Token);
                if (wanted(message))
                {
                    return message;
                }
            }
        }

        /// <summary>Every message up to the turn's result, which is last.</summary>
        public async Task<List<JsonObject>> TurnAsync()
        {
            var turn = new List<JsonObject>();
            while (turn.LastOrDefault()?["type"]?.GetValue<string>() != "result")
            {
                turn.Add(await NextAsync(_ => true));
            }
            return turn;
        }
    }

    private static string? Type(JsonObject message) => message["type"]?.GetValue<string>();

    private static IEnumerable<JsonObject> Blocks(IEnumerable<JsonObject> turn, string type) =>
        turn.Where(m => Type(m) is "assistant" or "user").SelectMany(m => (m["message"]?["content"] as JsonArray ?? []).OfType<JsonObject>())
            .Where(b => b["type"]?.GetValue<string>() == type);

    private static JsonObject User(string text) => new() { ["type"] = "user", ["message"] = new JsonObject { ["role"] = "user", ["content"] = text } };

    private static JsonObject Answer(string requestId, JsonObject response) => new()
    {
        ["type"] = "control_response",
        ["response"] = new JsonObject { ["subtype"] = "success", ["request_id"] = requestId, ["response"] = response },
    };

    [Fact]
    public async Task It_says_it_is_Claude_Code_signed_in_to_a_Max_plan()
    {
        var output = new Output();
        var standIn = new StandInClaude(new Input(), output, TimeProvider.System, _root, _root, TimeSpan.Zero);
        Assert.Equal(0, await standIn.RunAsync(["--version"]));
        Assert.Equal($"{StandInClaude.Version} (Claude Code)\n", output.Text);

        output = new Output();
        standIn = new StandInClaude(new Input(), output, TimeProvider.System, _root, _root, TimeSpan.Zero);
        Assert.Equal(0, await standIn.RunAsync(["auth", "status", "--json"]));
        var status = JsonNode.Parse(output.Text)!.AsObject();
        Assert.True(status["loggedIn"]!.GetValue<bool>());
        Assert.Equal((StandInClaude.Email, "max", _root), (status["email"]!.GetValue<string>(), status["subscriptionType"]!.GetValue<string>(), status["configDirectory"]!.GetValue<string>()));

        // Signing in or out, updating and the rest aren't for the demo.
        Assert.Equal(1, await new StandInClaude(new Input(), new Output(), TimeProvider.System, _root, _root, TimeSpan.Zero).RunAsync(["auth", "login"]));
    }

    [Fact]
    public async Task Each_message_gets_the_next_turn_and_permissions_and_questions_go_by_the_answer()
    {
        File.WriteAllText(Path.Combine(_root, StandInClaude.UsageFile), """{"subscription_type":"max","rate_limits_available":true}""");
        var (input, output) = (new Input(), new Output());
        var standIn = new StandInClaude(input, output, TimeProvider.System, _root, _root, TimeSpan.Zero);
        var running = standIn.RunAsync(["-p", "--input-format", "stream-json", "--output-format", "stream-json", "--resume", "session-1", "--model", "sonnet"]);

        // The control requests Claudette sends, the plan's usage from the demo's file.
        input.Send(new JsonObject { ["type"] = "control_request", ["request_id"] = "r1", ["request"] = new JsonObject { ["subtype"] = "initialize" } });
        var initialized = await output.NextAsync(m => Type(m) == "control_response");
        Assert.Contains("Sonnet 5.5", initialized.ToJsonString(), StringComparison.Ordinal);
        input.Send(new JsonObject { ["type"] = "control_request", ["request_id"] = "r2", ["request"] = new JsonObject { ["subtype"] = "get_usage" } });
        var usage = await output.NextAsync(m => Type(m) == "control_response");
        Assert.Equal("max", usage["response"]?["response"]?["subscription_type"]?.GetValue<string>());

        // A short answer, in the session it resumed, on the model it was started with.
        input.Send(User("Hello"));
        var first = await output.TurnAsync();
        var init = first.First(m => Type(m) == "system");
        Assert.Equal(("session-1", "claude-sonnet-5-5"), (init["session_id"]!.GetValue<string>(), init["model"]!.GetValue<string>()));
        Assert.Contains(first, m => Type(m) == "stream_event");
        Assert.Contains("Claudette's demo", Blocks(first, "text").Single()["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(first[^1]["is_error"]!.GetValue<bool>());

        // Thinking, then an answer.
        input.Send(User("And?"));
        var second = await output.TurnAsync();
        Assert.Single(Blocks(second, "thinking"));
        Assert.Single(Blocks(second, "text"));

        // A file read, and a write behind a permission, which writes nothing.
        input.Send(User("Do something"));
        var permission = await output.NextAsync(m => Type(m) == "control_request");
        Assert.Equal(("can_use_tool", "Write"), (permission["request"]!["subtype"]!.GetValue<string>(), permission["request"]!["tool_name"]!.GetValue<string>()));
        input.Send(Answer(permission["request_id"]!.GetValue<string>(), new JsonObject { ["behavior"] = "allow", ["updatedInput"] = permission["request"]!["input"]!.DeepClone() }));
        var third = await output.TurnAsync();
        Assert.Contains(Blocks(third, "tool_result"), r => r["content"]!.GetValue<string>().StartsWith("File created successfully", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(_root, "NOTES.md")));

        // A question, and a reply that goes by the answer.
        input.Send(User("Next"));
        var question = await output.NextAsync(m => Type(m) == "control_request");
        Assert.Equal("AskUserQuestion", question["request"]!["tool_name"]!.GetValue<string>());
        var answered = question["request"]!["input"]!.DeepClone().AsObject();
        answered["answers"] = new JsonObject { ["Which part of Claudette would you like to look at next?"] = "History" };
        input.Send(Answer(question["request_id"]!.GetValue<string>(), new JsonObject { ["behavior"] = "allow", ["updatedInput"] = answered }));
        var fourth = await output.TurnAsync();
        Assert.Contains("History", Blocks(fourth, "text").Last()["text"]!.GetValue<string>(), StringComparison.Ordinal);

        // And round again.
        input.Send(User("Again"));
        Assert.Contains("Claudette's demo", Blocks(await output.TurnAsync(), "text").Single()["text"]!.GetValue<string>(), StringComparison.Ordinal);

        // Claudette closing its input ends it.
        input.End();
        Assert.Equal(0, await running.WaitAsync(Waiting.Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_denied_write_is_a_refusal_it_goes_along_with()
    {
        var (input, output) = (new Input(), new Output());
        var running = new StandInClaude(input, output, TimeProvider.System, _root, _root, TimeSpan.Zero).RunAsync(["--input-format", "stream-json"]);
        input.Send(User("one"));
        await output.TurnAsync();
        input.Send(User("two"));
        await output.TurnAsync();
        input.Send(User("three"));
        var permission = await output.NextAsync(m => Type(m) == "control_request");
        input.Send(Answer(permission["request_id"]!.GetValue<string>(), new JsonObject { ["behavior"] = "deny", ["message"] = "No" }));
        var turn = await output.TurnAsync();
        Assert.Contains(Blocks(turn, "tool_result"), r => r["is_error"]!.GetValue<bool>());
        Assert.Contains("won't write it", Blocks(turn, "text").Last()["text"]!.GetValue<string>(), StringComparison.Ordinal);
        input.End();
        await running.WaitAsync(Waiting.Timeout, TestContext.Current.CancellationToken);
    }
}
