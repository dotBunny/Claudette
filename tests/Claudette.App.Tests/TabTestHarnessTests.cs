using Claudette.App.Conversation;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;

namespace Claudette.App.Tests;

/// <summary>The harness itself: what tests with more than one tab count on, with no setup of their own.</summary>
public class TabTestHarnessTests
{
    [Fact]
    public async Task Tabs_running_side_by_side_each_have_a_pretend_claude_of_their_own()
    {
        await using var h = new TabTestHarness();
        var first = await h.OpenTabAsync();
        var second = await h.OpenTabAsync();
        Assert.NotSame(first, second);

        // What's emitted goes to the newest process; an earlier one is reached by its index.
        h.Transport.EmitTurn("to the second");
        h.Transport.EmitTo(0, """{"type":"assistant","message":{"content":[{"type":"text","text":"to the first"}]}}""");

        await TabTestHarness.Eventually(() => Replies(first).Contains("to the first") && Replies(second).Contains("to the second"), "each reply in its own tab");
        Assert.DoesNotContain("to the second", Replies(first));
        Assert.DoesNotContain("to the first", Replies(second));
        // Each session started on its own process.
        Assert.Contains(h.Transport.SentTo(0), m => m["request"]?["subtype"]?.GetValue<string>() == "initialize");
        Assert.Contains(h.Transport.SentTo(1), m => m["request"]?["subtype"]?.GetValue<string>() == "initialize");
    }

    [Fact]
    public async Task A_tab_that_starts_again_after_its_claude_exited_gets_a_new_one()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        h.Transport.Exit(0);
        await TabTestHarness.Eventually(() => tab.Status == TabStatus.Exited, "the exit");

        await tab.RestartCommand.ExecuteAsync(null);
        await TabTestHarness.Eventually(() => tab.Status == TabStatus.Idle && tab.IsSettled, "the start again");
        h.Transport.EmitTurn("again");

        await TabTestHarness.Eventually(() => Replies(tab).Contains("again"), "the reply");
    }

    private static List<string> Replies(TabViewModel tab) =>
        InlineDispatcher.Read(() => tab.Items.OfType<AssistantTextItem>().Select(r => r.Text).ToList());
}
