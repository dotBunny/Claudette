using System.Text.Json.Nodes;
using Claudette.App.Tests.Support;

namespace Claudette.App.Tests;

/// <summary>Pinned groups first in the sidebar (DESIGN.md §4, "Grouped by folder").</summary>
public class PinnedGroupsTests
{
    private static string UserLine(string sessionId, string text, string cwd, string time) => new JsonObject
    {
        ["type"] = "user",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = text },
        ["cwd"] = cwd,
        ["sessionId"] = sessionId,
        ["timestamp"] = time,
    }.ToJsonString();

    /// <summary>Three folders with sessions, the latest used first: c, b, then the work folder.</summary>
    private static (string B, string C) WriteProjects(TabTestHarness h)
    {
        var (b, c) = (Path.Combine(h.Root, "b"), Path.Combine(h.Root, "c"));
        Directory.CreateDirectory(b);
        Directory.CreateDirectory(c);
        h.WriteTranscript("w1", UserLine("w1", "Work", h.WorkFolder, "2026-09-28T09:00:00Z"));
        h.WriteTranscript("b1", UserLine("b1", "B", b, "2026-09-28T10:00:00Z"));
        h.WriteTranscript("c1", UserLine("c1", "C", c, "2026-09-28T11:00:00Z"));
        return (b, c);
    }

    private static string[] Folders(TabTestHarness h) => InlineDispatcher.Read(() => h.Shell.Groups.Select(g => g.Folder).ToArray());

    [Fact]
    public async Task A_pinned_group_goes_first_and_stays_there_however_recently_the_others_were_used()
    {
        await using var h = new TabTestHarness(s => s.Sidebar.ShowClaudeProjects = true);
        h.Services.State.ClaudeProjectsOffered = true;
        var (b, c) = WriteProjects(h);
        h.Shell.Restore(null);
        await TabTestHarness.Eventually(() => InlineDispatcher.Read(() => h.Shell.Groups.Count == 3), "three projects");
        Assert.Equal([c, b, h.WorkFolder], Folders(h));

        var work = InlineDispatcher.Read(() => h.Shell.Groups.Single(g => g.Folder == h.WorkFolder));
        Assert.Equal("Pin to the top", work.PinMenuText);
        InlineDispatcher.Read(() => { h.Shell.TogglePinGroupCommand.Execute(work); return 0; });
        Assert.True(work.IsPinned);
        Assert.Equal("Unpin from the top", work.PinMenuText);
        Assert.Equal([h.WorkFolder, c, b], Folders(h));
        Assert.Equal([h.WorkFolder], h.Services.State.PinnedGroups);

        // A second pin goes after the first, and unpinning lets a group go back to where its use puts it.
        var bGroup = InlineDispatcher.Read(() => h.Shell.Groups.Single(g => g.Folder == b));
        InlineDispatcher.Read(() => { h.Shell.TogglePinGroupCommand.Execute(bGroup); return 0; });
        Assert.Equal([h.WorkFolder, b, c], Folders(h));
        InlineDispatcher.Read(() => { h.Shell.TogglePinGroupCommand.Execute(work); return 0; });
        Assert.False(work.IsPinned);
        Assert.Equal([b, c, h.WorkFolder], Folders(h));
        Assert.Equal([b], h.Services.State.PinnedGroups);
    }

    [Fact]
    public async Task A_pin_is_remembered_and_a_pinned_group_cannot_be_dragged_below_the_others()
    {
        await using var h = new TabTestHarness(s => s.Sidebar.ShowClaudeProjects = true);
        h.Services.State.ClaudeProjectsOffered = true;
        var (b, c) = WriteProjects(h);
        h.Services.State.PinnedGroups = [b];
        h.Shell.Restore(null);
        await TabTestHarness.Eventually(() => InlineDispatcher.Read(() => h.Shell.Groups.Count == 3), "three projects");
        Assert.Equal([b, c, h.WorkFolder], Folders(h));
        var group = InlineDispatcher.Read(() => h.Shell.Groups[0]);
        Assert.True(group.IsPinned);

        // Dragged to the end, it stays among the pinned: it's the only one, so it doesn't move.
        Assert.False(InlineDispatcher.Read(() => h.Shell.MoveGroupTo(group, 2)));
        Assert.Equal([b, c, h.WorkFolder], Folders(h));
    }
}
