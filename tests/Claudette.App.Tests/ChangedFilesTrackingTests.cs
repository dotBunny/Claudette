using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.Core.Settings;

namespace Claudette.App.Tests;

/// <summary>
/// Changed files keeping track as the session goes on (DESIGN.md §8): a new conversation, changes read back, what
/// changes the files besides Claude's edits, and the tab moving.
/// </summary>
public class ChangedFilesTrackingTests
{
    private const string Result = """{"type":"result","subtype":"success","is_error":false,"session_id":"s1","duration_ms":1200}""";

    [Fact]
    public async Task Clear_starts_the_list_and_its_marks_again()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        EmitEdit(h, "e1", path);
        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files.Count == 1, "the changed file");
        tab.ChangedFiles.ToggleFileReviewedCommand.Execute(tab.ChangedFiles.Files[0]);
        Assert.NotEmpty(tab.State.ReviewedFiles);

        h.Transport.Emit("""{"type":"conversation_reset","trigger":"clear"}""");

        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files.Count == 0 && tab.ChangedFiles.Count == 0, "the list emptied");
        Assert.Equal("No changes yet", tab.ChangedFiles.Summary);
        Assert.Empty(tab.State.ReviewedFiles);
        Assert.False(tab.ChangedFiles.AllReviewed);
    }

    [Fact]
    public async Task A_change_read_back_keeps_the_time_it_was_made()
    {
        await using var h = new TabTestHarness();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        h.WriteTranscript("s1",
            Wire.Entry("assistant", "2026-09-27T11:00:00Z", Wire.Message(EditUse("e1", path))),
            Wire.Entry("user", "2026-09-27T11:00:01Z", Wire.ResultMessage("e1", "The file has been updated."), EditResult(path)));
        h.Services.State.Tabs = [new TabState { Folder = h.WorkFolder, IsPinned = true, SessionId = "s1" }];

        h.Shell.Restore(null);
        var tab = h.Shell.AllTabs.Single();

        await TabTestHarness.Eventually(() => tab.Status == TabStatus.Idle && tab.ChangedFiles.Files.Count == 1, "the changed file");
        // The time it was written, not the time it was read: the project's Files tab takes the earliest "before".
        Assert.Equal(DateTimeOffset.Parse("2026-09-27T11:00:01Z"), Assert.Single(tab.ChangedFiles.ClaudeChanges).FirstChanged);
    }

    [Fact]
    public async Task A_turns_end_looks_at_the_disk_again_for_what_its_commands_did()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        tab.ComposerText = "change it";
        await tab.SendCommand.ExecuteAsync(null);
        h.Transport.Emit(Wire.Init());
        EmitEdit(h, "e1", path);
        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ Stats: "+1 −1" }], "the edit");

        // A command in the same turn adds a line, which no Edit reports.
        await File.WriteAllTextAsync(path, "b\nc\n", TestContext.Current.CancellationToken);
        h.Transport.Emit(Result);

        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ Stats: "+1 −1", Since: "Changed since: +1 −0" }], "the line the command added");
    }

    [Fact]
    public async Task Coming_back_to_Claudette_looks_at_the_disk_again_for_what_you_changed()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        EmitEdit(h, "e1", path);
        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ Stats: "+1 −1" }], "the edit");

        await File.WriteAllTextAsync(path, "a\n", TestContext.Current.CancellationToken);
        h.Shell.OnAppActivated();

        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ Stats: "+1 −1", Since: "Changed since: +1 −1" }], "the file put back since");
    }

    [Fact]
    public async Task Moving_the_tab_out_of_a_repository_turns_the_working_tree_view_off()
    {
        await using var h = new TabTestHarness(launcher: GitAt(out _));
        Directory.CreateDirectory(Path.Combine(h.WorkFolder, ".git"));
        await File.WriteAllTextAsync(Path.Combine(h.WorkFolder, ".git", "HEAD"), "ref: refs/heads/main\n", TestContext.Current.CancellationToken);
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        EmitEdit(h, "e1", path);
        tab.ChangedFiles.ShowGitChanges = true;
        await TabTestHarness.Eventually(() => tab.ChangedFiles.Summary == "No changes in the working tree", "the working tree");
        var elsewhere = Path.Combine(h.Root, "elsewhere");
        Directory.CreateDirectory(elsewhere);

        await tab.MoveToFolderAsync(elsewhere);

        // Its switch is hidden outside a repository, so it can't be left on, listing nothing.
        Assert.False(tab.ChangedFiles.IsGitRepository);
        Assert.False(tab.ChangedFiles.ShowGitChanges);
        // Claude's change is in the repository's working tree, which isn't the tab's now: it's headed by it.
        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ Group: "work · main", DisplayPath: "a.cs" }], "Claude's change, under its working tree");
    }

    [Fact]
    public async Task A_folder_whose_name_starts_with_two_dots_is_inside_the_tabs()
    {
        await using var h = new TabTestHarness(launcher: GitAt(out var untracked));
        var tab = await h.OpenTabAsync();
        untracked.Value = "..notes/a.txt";

        tab.ChangedFiles.ShowGitChanges = true;

        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ DisplayPath: "..notes/a.txt" }], "the untracked file, relative");
    }

    [Fact]
    public async Task Another_tabs_later_edit_is_changed_since_not_this_tabs_change()
    {
        await using var h = new TabTestHarness();
        var alpha = await ThreadScript.OpenAsync(h, "Alpha");
        var beta = await ThreadScript.OpenAsync(h, "Beta");
        var path = Path.Combine(h.WorkFolder, "shared.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        h.Shell.SelectedTab = alpha;
        EmitEditTo(h, 0, "e1", path, "a\n", "a", "b");
        await TabTestHarness.Eventually(() => alpha.ChangedFiles.Files is [{ Since: null }], "Alpha's change");
        h.Time.Advance(TimeSpan.FromSeconds(5));

        // Beta, in the background, adds a line: Alpha, on screen, looks again.
        await File.WriteAllTextAsync(path, "b\nc\n", TestContext.Current.CancellationToken);
        EmitEditTo(h, 1, "e2", path, "b\n", "b\n", "b\nc\n");

        // Alpha's change is its own, a to b; Beta's line came after it.
        await TabTestHarness.Eventually(() => alpha.ChangedFiles.Files is [{ Stats: "+1 −1", Since: "Changed since: +1 −0, by Claude in Beta" }], "Beta's line, since");
        h.Shell.SelectedTab = beta;
        await TabTestHarness.Eventually(() => beta.ChangedFiles.Files is [{ Stats: "+1 −0", Since: null }], "Beta's own change");
    }

    [Fact]
    public async Task The_diff_of_a_file_changed_since_ends_at_the_sessions_change_and_switches_to_the_file_now()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        EmitEditTo(h, 0, "e1", path, "a\n", "a", "b");
        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files.Count == 1, "the change");
        await File.WriteAllTextAsync(path, "b\nyours\n", TestContext.Current.CancellationToken);
        await tab.ChangedFiles.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Changed since: +1 −0", tab.ChangedFiles.Files[0].Since);
        Diffs.DiffSource? requested = null;
        tab.ChangedFiles.DiffRequested += source => requested = source;

        await tab.ChangedFiles.OpenFileDiffCommand.ExecuteAsync(tab.ChangedFiles.Files[0]);

        var view = new Diffs.DiffWindowViewModel(requested!, dark: false);
        await TabTestHarness.Eventually(() => !view.IsLoading, "the diff");
        Assert.Equal(("+1 −1", true, false), (view.Stats, view.HasLaterChanges, view.CanRevert));
        view.ShowLaterChanges = true;
        await TabTestHarness.Eventually(() => !view.IsLoading && view.Stats == "+2 −1", "the file now");
        Assert.True(view.CanRevert);
        Assert.Contains("with the changes since", view.BeforeLabel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_turns_footer_counts_its_own_change_though_the_file_changed_after()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        tab.ComposerText = "change it";
        await tab.SendCommand.ExecuteAsync(null);
        h.Transport.Emit(Wire.Init());
        // Another tab's line is already on disk as this turn ends.
        await File.WriteAllTextAsync(path, "b\nanother tab's\n", TestContext.Current.CancellationToken);
        EmitEditTo(h, 0, "e1", path, "a\n", "a", "b");
        h.Transport.Emit(Result);

        await TabTestHarness.Eventually(() => tab.Items.LastOrDefault() is Conversation.TurnSummaryItem { Files: [{ Stats: "+1 −1" }] }, "the footer's counts");
    }

    [Fact]
    public async Task A_change_on_disk_to_a_changed_file_shows_once_the_writing_pauses()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        EmitEditTo(h, 0, "e1", path, "a\n", "a", "b");
        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files.Count == 1 && h.FolderWatcher.Folders.Contains(h.WorkFolder), "the folder watched");

        // You save it in your editor.
        await File.WriteAllTextAsync(path, "b\nyours\n", TestContext.Current.CancellationToken);
        h.FolderWatcher.Changed(path);
        Assert.Null(tab.ChangedFiles.Files[0].Since);
        h.Time.Advance(ChangedFilesViewModel.DiskSettleDelay);

        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ Since: "Changed since: +1 −0" }], "your line");
    }

    [Fact]
    public async Task A_tab_off_screen_watches_nothing_and_catches_up_once_shown()
    {
        await using var h = new TabTestHarness();
        var alpha = await ThreadScript.OpenAsync(h, "Alpha");
        var beta = await ThreadScript.OpenAsync(h, "Beta");
        h.Shell.SelectedTab = alpha;
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        EmitEditTo(h, 0, "e1", path, "a\n", "a", "b");
        await TabTestHarness.Eventually(() => alpha.ChangedFiles.Files.Count == 1 && h.FolderWatcher.Folders.Count == 1, "the folder watched");

        h.Shell.SelectedTab = beta;
        Assert.Empty(h.FolderWatcher.Folders);
        await File.WriteAllTextAsync(path, "b\nyours\n", TestContext.Current.CancellationToken);
        h.Shell.SelectedTab = alpha;

        await TabTestHarness.Eventually(() => alpha.ChangedFiles.Files is [{ Since: "Changed since: +1 −0" }], "your line, once shown");
    }

    [Fact]
    public async Task The_working_tree_view_watches_the_git_folder_for_commits_and_staging()
    {
        var statuses = 0;
        var git = new FakeLauncher
        {
            OnStart = (spec, process) =>
            {
                if (spec.Arguments.Contains("--show-toplevel"))
                {
                    process.WriteOutput(spec.WorkingDirectory!);
                    process.WriteOutput("");
                }
                else if (spec.Arguments.Contains("status"))
                {
                    Interlocked.Increment(ref statuses);
                }
                process.Exit(0);
            },
        };
        await using var h = new TabTestHarness(launcher: git);
        var gitFolder = Path.Combine(h.WorkFolder, ".git");
        Directory.CreateDirectory(gitFolder);
        await File.WriteAllTextAsync(Path.Combine(gitFolder, "HEAD"), "ref: refs/heads/main\n", TestContext.Current.CancellationToken);
        var tab = await h.OpenTabAsync();
        tab.ChangedFiles.ShowGitChanges = true;
        await TabTestHarness.Eventually(() => Volatile.Read(ref statuses) == 1 && h.FolderWatcher.Folders.Contains(gitFolder), "the first listing");

        // A commit from a terminal writes the index.
        h.FolderWatcher.Changed(Path.Combine(gitFolder, "index"));

        await TabTestHarness.Eventually(() =>
        {
            h.Time.Advance(ChangedFilesViewModel.DiskSettleDelay);
            h.Time.Advance(ChangedFilesViewModel.GitRefreshDelay);
            return Volatile.Read(ref statuses) == 2;
        }, "git asked again");
    }

    [Fact]
    public async Task A_subagents_edit_is_counted_from_what_the_hook_read_before_it_ran()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "a\n", TestContext.Current.CancellationToken);

        // Claude Code calls the file tools' hook before the subagent's Edit runs (no Perforce: the threads' and the
        // subagents' hooks come first).
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "control_request",
            ["request_id"] = "r1",
            ["request"] = new JsonObject
            {
                ["subtype"] = "hook_callback",
                ["callback_id"] = "hook_2",
                ["input"] = new JsonObject
                {
                    ["session_id"] = "s1", ["agent_id"] = "a1", ["hook_event_name"] = "PreToolUse", ["tool_name"] = "Edit", ["tool_use_id"] = "toolu_s1",
                    ["tool_input"] = new JsonObject { ["file_path"] = path, ["old_string"] = "a", ["new_string"] = "b" },
                },
                ["tool_use_id"] = "toolu_s1",
            },
        });
        await TabTestHarness.Eventually(() => h.Transport.Sent.Any(m => m["type"]?.GetValue<string>() == "control_response"
            && m["response"]?["request_id"]?.GetValue<string>() == "r1"), "the hook's answer");
        await File.WriteAllTextAsync(path, "b\n", TestContext.Current.CancellationToken);
        // The subagent's Edit and its result, which has no tool_use_result.
        h.Transport.Emit(Wire.Tool("toolu_agent", "toolu_s1", "Edit", new JsonObject { ["file_path"] = path, ["old_string"] = "a", ["new_string"] = "b" }));
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "toolu_s1", ["content"] = "The file has been updated." }) },
            ["parent_tool_use_id"] = "toolu_agent",
        });

        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ Stats: "+1 −1", BeforeKnown: true, Since: null }], "the subagent's change, counted");
        Assert.True(File.Exists(Path.Combine(h.Services.Paths.BeforeContentDirectory, "toolu_s1.txt.gz")));
    }

    /// <summary>
    /// A Write that makes a file with Windows line endings Unix ones, as Claude Code's does, says so on its row and in
    /// its diff: the hook read the line endings before the call ran (DESIGN.md §8, "What the lines don't show").
    /// </summary>
    [Fact]
    public async Task A_write_that_changes_only_line_endings_says_so_on_its_row_and_in_its_diff()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Path.Combine(h.WorkFolder, "a.cs");
        await File.WriteAllTextAsync(path, "one\r\ntwo\r\n", TestContext.Current.CancellationToken);

        // The session's own call: the hook reads the file's line endings, and answers.
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "control_request",
            ["request_id"] = "r1",
            ["request"] = new JsonObject
            {
                ["subtype"] = "hook_callback",
                ["callback_id"] = "hook_2",
                ["input"] = new JsonObject
                {
                    ["session_id"] = "s1", ["hook_event_name"] = "PreToolUse", ["tool_name"] = "Write", ["tool_use_id"] = "w1",
                    ["tool_input"] = new JsonObject { ["file_path"] = path, ["content"] = "one\ntwo\n" },
                },
                ["tool_use_id"] = "w1",
            },
        });
        await TabTestHarness.Eventually(() => h.Transport.Sent.Any(m => m["type"]?.GetValue<string>() == "control_response"
            && m["response"]?["request_id"]?.GetValue<string>() == "r1"), "the hook's answer");
        // Claude Code reports the file with \n, and writes the content as the model gave it.
        await File.WriteAllTextAsync(path, "one\ntwo\n", TestContext.Current.CancellationToken);
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "tool_use", ["id"] = "w1", ["name"] = "Write",
                ["input"] = new JsonObject { ["file_path"] = path, ["content"] = "one\ntwo\n" },
            }) },
        });
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "w1", ["content"] = "The file has been updated." }) },
            ["tool_use_result"] = new JsonObject { ["type"] = "update", ["filePath"] = path, ["content"] = "one\ntwo\n", ["originalFile"] = "one\ntwo\n", ["structuredPatch"] = new JsonArray() },
        });

        await TabTestHarness.Eventually(() => tab.ChangedFiles.Files is [{ Status: "M", Stats: null, Note: "Only its line endings changed: CRLF to LF" }], "the row");
        Assert.Equal("1 file changed", tab.ChangedFiles.Summary);
        Diffs.DiffSource? requested = null;
        tab.ChangedFiles.DiffRequested += source => requested = source;
        await tab.ChangedFiles.OpenFileDiffCommand.ExecuteAsync(tab.ChangedFiles.Files[0]);
        var view = new Diffs.DiffWindowViewModel(requested!, dark: false);
        await TabTestHarness.Eventually(() => !view.IsLoading, "the diff");
        Assert.Equal("CRLF → LF", view.Stats);
        Assert.Equal("Only its line endings changed: CRLF to LF. The lines are the same on both sides.", view.Message);
        Assert.Empty(view.InlineRows);
    }

    /// <summary>Git, faked: the tab's folder is the top of its working tree, which has <paramref name="untracked"/> as its one untracked file, if any.</summary>
    private static FakeLauncher GitAt(out StrongBox<string?> untracked)
    {
        var box = untracked = new StrongBox<string?>();
        return new FakeLauncher
        {
            OnStart = (spec, process) =>
            {
                if (spec.Arguments.Contains("--show-toplevel"))
                {
                    process.WriteOutput(spec.WorkingDirectory!);
                    process.WriteOutput("");
                }
                else if (spec.Arguments.Contains("status") && box.Value is { } file)
                {
                    process.WriteOutput($"?? {file}\0");
                }
                process.Exit(0);
            },
        };
    }

    /// <summary>A successful Edit of <paramref name="path"/>, live, on the tab whose process is <paramref name="process"/>.</summary>
    private static void EmitEditTo(TabTestHarness h, int process, string id, string path, string originalFile, string from, string to)
    {
        h.Transport.EmitTo(process, new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "tool_use", ["id"] = id, ["name"] = "Edit",
                ["input"] = new JsonObject { ["file_path"] = path, ["old_string"] = from, ["new_string"] = to },
            }) },
        });
        h.Transport.EmitTo(process, new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = "The file has been updated." }) },
            ["tool_use_result"] = new JsonObject { ["filePath"] = path, ["oldString"] = from, ["newString"] = to, ["originalFile"] = originalFile },
        });
    }

    /// <summary>A successful Edit of <paramref name="path"/> from <c>a</c> to its <c>b</c>, live.</summary>
    private static void EmitEdit(TabTestHarness h, string id, string path)
    {
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject { ["content"] = new JsonArray(EditUse(id, path)) },
        });
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = "The file has been updated." }) },
            ["tool_use_result"] = EditResult(path),
        });
    }

    private static JsonObject EditUse(string id, string path) => new()
    {
        ["type"] = "tool_use",
        ["id"] = id,
        ["name"] = "Edit",
        ["input"] = new JsonObject { ["file_path"] = path, ["old_string"] = "a", ["new_string"] = "b" },
    };

    private static JsonObject EditResult(string path) => new() { ["filePath"] = path, ["oldString"] = "a", ["newString"] = "b", ["originalFile"] = "a\n" };
}
