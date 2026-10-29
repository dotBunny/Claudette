using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Claudette.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.Conversation;

/// <summary>
/// One step in the list a folded chain opens into (DESIGN.md §18, "Conversation cards"): a line such as <i>Thought for
/// 7s</i> or <i>Ran read tail of UE4SS.log</i>, how long it took at its right, and a chevron when there's more to see,
/// which opens it under the line: what Claude thought, or the call's input and output.
/// </summary>
public sealed partial class StepRowItem : ObservableObject
{
    internal StepRowItem(ConversationItem source)
    {
        Source = source;
        Tool = source as ToolUseItem;
        Thought = source as ThinkingItem;
    }

    /// <summary>The step this row stands for: a thinking row or a call.</summary>
    internal ConversationItem Source { get; }

    public ToolUseItem? Tool { get; }

    public ThinkingItem? Thought { get; }

    public bool IsThought => Thought is not null;

    public bool IsTool => Tool is not null;

    /// <summary>"Thought for 7s", "Ran check for UE4SS.pdb", "Created analyze_dump.py", "Edited disasm.py".</summary>
    public string Title => Thought is { } thought ? ThoughtTitle(thought) : ToolTitle(Tool!);

    /// <summary>How long it took, and "failed" for a call that did.</summary>
    public string? TimeText
    {
        get
        {
            if (Tool is not { } tool)
            {
                return null;
            }
            var parts = new[] { tool.IsComplete ? tool.DurationText : "running…", tool.IsError ? "failed" : null }.Where(p => p is not null);
            return string.Join(" · ", parts);
        }
    }

    /// <summary>There's something under the line to open: thinking that wrote something, or a call.</summary>
    public bool HasDetail => Tool is not null || Thought is { HasText: true };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollapsed), nameof(ThoughtText), nameof(ShowsThought), nameof(ShowsTool))]
    public partial bool IsExpanded { get; set; }

    public bool IsCollapsed => !IsExpanded;

    /// <summary>What Claude thought, while the row is open.</summary>
    public string? ThoughtText => IsExpanded && Thought is { } thought ? thought.Text.Trim() : null;

    public bool ShowsThought => IsExpanded && Thought is { HasText: true };

    public bool ShowsTool => IsExpanded && Tool is not null;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    /// <summary>The step changed, as a call finished or thinking went on: the line says so.</summary>
    internal void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(HasDetail));
        if (IsExpanded)
        {
            OnPropertyChanged(nameof(ThoughtText));
        }
    }

    private static string ThoughtTitle(ThinkingItem thought) =>
        thought.IsStreaming ? "Thinking…"
        : thought.Duration is { TotalSeconds: >= 1 } took ? $"Thought for {Formats.Elapsed(took)}"
        : "Thought";

    /// <summary>The call as a line: what it did, in words, by the tool.</summary>
    internal static string ToolTitle(ToolUseItem tool)
    {
        var description = Text(tool.Input, "description");
        var file = Text(tool.Input, "file_path") ?? Text(tool.Input, "notebook_path");
        var name = file is null ? null : file[(Math.Max(file.LastIndexOf('/'), file.LastIndexOf('\\')) + 1)..];
        if (tool is SubagentItem)
        {
            return tool.Summary.Length > 0 ? $"Agent · {tool.Summary}" : "Agent";
        }
        return tool.Name switch
        {
            "Bash" => "Ran " + (description ?? Short(tool.Command ?? tool.Summary)),
            "Read" when name is not null => "Read " + name,
            "Write" when name is not null => "Created " + name,
            "Edit" or "MultiEdit" or "NotebookEdit" when name is not null => "Edited " + name,
            "Grep" => "Searched for " + Short(tool.Summary),
            "Glob" => "Listed " + Short(tool.Summary),
            "WebFetch" => "Fetched " + Short(tool.Summary),
            "WebSearch" => "Searched the web for " + Short(tool.Summary),
            _ => tool.Summary.Length > 0 ? $"{tool.Name} {Short(tool.Summary)}" : tool.Name,
        };
    }

    private static string Short(string text)
    {
        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length > 90 ? text[..87] + "…" : text;
    }

    private static string? Text(JsonObject? input, string key) =>
        input?[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}

/// <summary>
/// The line a run of steps folds into (DESIGN.md §18, "Conversation cards"): <i>Worked for 18m</i>, with what the steps
/// were beside it. A click opens it into a list of the steps, a line each, which scrolls once it's long and whose lines
/// open further. While Claude is on the run its last step stays under the line.
/// </summary>
public sealed partial class StepsMarkerItem : ConversationItem
{
    private readonly StepRuns _runs;

    internal StepsMarkerItem(StepRuns runs) => _runs = runs;

    /// <summary>The run's steps, in order: thinking, Tool calls cards, subagents.</summary>
    internal List<ConversationItem> Steps { get; } = [];

    /// <summary>
    /// The run is still the end of the conversation, so its last step stays under the line: it's the one Claude is on.
    /// Once something else follows it, the whole run is folded into the line.
    /// </summary>
    internal bool KeepsLast { get; set; }

    /// <summary>The list is open, and the chevron points down.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoldedAway))]
    public partial bool IsOpen { get; internal set; }

    public bool IsFoldedAway => !IsOpen;

    /// <summary>"Worked for 18m" once the run is done; "Working · 6 earlier steps" while Claude is on it.</summary>
    [ObservableProperty]
    public partial string Title { get; internal set; } = "";

    /// <summary>"9 tool calls · 3 thinking": what the steps were.</summary>
    [ObservableProperty]
    public partial string Summary { get; internal set; } = "";

    /// <summary>The steps, a line each, while the list is open; empty while it isn't, so a folded run costs nothing.</summary>
    public ObservableCollection<StepRowItem> Rows { get; } = [];

    /// <summary>The chevron, or a click on the line: opens the list, or folds it.</summary>
    [RelayCommand]
    private void Toggle() => _runs.Toggle(this);

    /// <summary>The steps the line stands for: all of them, or all but the one Claude is on.</summary>
    private IEnumerable<ConversationItem> Folded => KeepsLast ? Steps.Take(Steps.Count - 1) : Steps;

    /// <summary>Opens the row of <paramref name="step"/> too, as find does for a match inside it.</summary>
    internal void OpenRowOf(ConversationItem step)
    {
        if (Rows.FirstOrDefault(r => ReferenceEquals(r.Source, step) || (r.Tool is { } tool && ReferenceEquals(tool, step))) is { } row)
        {
            row.IsExpanded = true;
        }
    }

    /// <summary>Says what the run is, and keeps the open list as the run is now.</summary>
    internal void Describe()
    {
        var shown = Folded.ToList();
        var calls = shown.Sum(s => s switch { ToolGroupItem group => group.Calls.Count, ToolUseItem => 1, _ => 0 });
        var thinking = shown.Count(s => s is ThinkingItem);
        var agents = shown.Count(s => s is SubagentItem);
        var parts = new List<string>();
        if (calls > 0)
        {
            parts.Add(calls == 1 ? "1 tool call" : $"{calls} tool calls");
        }
        if (thinking > 0)
        {
            parts.Add($"{thinking} thinking");
        }
        if (agents > 0)
        {
            parts.Add(agents == 1 ? "1 agent" : $"{agents} agents");
        }
        Summary = string.Join(" · ", parts);
        Title = KeepsLast ? $"Working · {shown.Count} earlier {(shown.Count == 1 ? "step" : "steps")}"
            : Took() is { } took ? $"Worked for {Formats.Elapsed(took)}"
            : $"{shown.Count} {(shown.Count == 1 ? "step" : "steps")}";
        Fill();
    }

    /// <summary>From the first step's start to the last one's end; null when the times aren't known, or it took under a second.</summary>
    private TimeSpan? Took()
    {
        var start = Steps.Select(StartOf).FirstOrDefault(t => t is not null);
        var end = Steps.Select(EndOf).Where(t => t is not null).DefaultIfEmpty(null).Max();
        return start is { } from && end is { } to && to - from is { TotalSeconds: >= 1 } took ? took : null;
    }

    private static DateTimeOffset? StartOf(ConversationItem step) => step switch
    {
        ThinkingItem thinking => thinking.StartedAt,
        ToolGroupItem group => group.StartedAt,
        ToolUseItem tool => tool.StartedAt,
        _ => null,
    };

    private static DateTimeOffset? EndOf(ConversationItem step) => step switch
    {
        ThinkingItem thinking => thinking.EndedAt,
        ToolGroupItem group => group.Calls.Select(c => c.CompletedAt).Where(t => t is not null).DefaultIfEmpty(null).Max(),
        ToolUseItem tool => tool.CompletedAt,
        _ => null,
    };

    /// <summary>The rows, while the list is open: one for each thinking row and each call, added as the run grows.</summary>
    private void Fill()
    {
        if (!IsOpen)
        {
            if (Rows.Count > 0)
            {
                Rows.Clear();
            }
            return;
        }
        var sources = Folded.SelectMany(s => s is ToolGroupItem group ? group.Calls.Cast<ConversationItem>() : [s]).ToList();
        foreach (var source in sources.Where(s => !Rows.Any(r => ReferenceEquals(r.Source, s))))
        {
            Rows.Add(new StepRowItem(source));
        }
        foreach (var row in Rows)
        {
            row.Refresh();
        }
    }
}

/// <summary>
/// Folds each run of steps on the way (thinking, Tool calls cards, subagents, with nothing else between them) into one
/// <see cref="StepsMarkerItem"/> line, <i>Worked for 18m</i>, with the run's last step under it while Claude is on it
/// (Settings → Conversation → Fold each chain of steps, DESIGN.md §18). The folded steps stay in the conversation, so
/// find, export and the agent map still see them; they are flagged <see cref="ConversationItem.IsFolded"/> and the list
/// gives them no height, and the line opens a list of them of its own. A run of one step Claude is still on is left as
/// it is. Without chain folding there are no lines at all.
/// </summary>
public sealed class StepRuns(ObservableCollection<ConversationItem> items)
{
    /// <summary>Whether runs fold (Settings → Conversation → Fold each chain of steps).</summary>
    public bool Chain { get; set; }

    private static bool IsStep(ConversationItem item) => item is ThinkingItem or ToolGroupItem or ToolUseItem;

    /// <summary>
    /// After a message was handled: the last run, which is the one that can have changed. It's at the end of the
    /// conversation while Claude is on it, or a few items back once a reply or the turn's end has followed it.
    /// </summary>
    public void Update()
    {
        if (!Chain)
        {
            return;
        }
        // Back over what followed the run (a reply, a note, the turn's statistics) to its last step.
        var last = items.Count - 1;
        var skipped = 0;
        while (last >= 0 && !IsStep(items[last]) && items[last] is not StepsMarkerItem && skipped < 16)
        {
            last--;
            skipped++;
        }
        if (last < 0 || (!IsStep(items[last]) && items[last] is not StepsMarkerItem))
        {
            return;
        }
        var start = last + 1;
        while (start > 0 && (IsStep(items[start - 1]) || items[start - 1] is StepsMarkerItem))
        {
            start--;
        }
        Fold(start);
    }

    /// <summary>Every run again: the setting changed, or an earlier conversation was read back.</summary>
    public void Refresh()
    {
        if (!Chain)
        {
            foreach (var marker in items.OfType<StepsMarkerItem>().ToList())
            {
                items.Remove(marker);
            }
            foreach (var item in items)
            {
                item.IsFolded = false;
            }
            return;
        }
        var i = 0;
        while (i < items.Count)
        {
            if (!IsStep(items[i]) && items[i] is not StepsMarkerItem)
            {
                i++;
                continue;
            }
            i = Fold(i);
        }
    }

    /// <summary>The line's chevron, or a click on it.</summary>
    internal void Toggle(StepsMarkerItem marker)
    {
        marker.IsOpen = !marker.IsOpen;
        marker.Describe();
    }

    /// <summary>The line a folded step is in, or null for one that isn't folded.</summary>
    public StepsMarkerItem? MarkerOf(ConversationItem? step) =>
        step is { IsFolded: true } ? items.OfType<StepsMarkerItem>().FirstOrDefault(m => m.Steps.Contains(step)) : null;

    /// <summary>A step the user is being taken to (find, the agent map): opens the list it is in, at its line, and returns the line.</summary>
    public StepsMarkerItem? Reveal(ConversationItem? step)
    {
        if (MarkerOf(step) is not { } marker)
        {
            return null;
        }
        marker.IsOpen = true;
        marker.Describe();
        marker.OpenRowOf(step!);
        return marker;
    }

    /// <summary>Folds the run starting at <paramref name="start"/>, which may begin with its marker; returns where it ends.</summary>
    private int Fold(int start)
    {
        var marker = items[start] as StepsMarkerItem;
        var end = marker is null ? start : start + 1;
        var steps = new List<ConversationItem>();
        while (end < items.Count && IsStep(items[end]))
        {
            steps.Add(items[end]);
            end++;
        }
        if (steps.Count == 0 || (steps.Count == 1 && end == items.Count))
        {
            // None, or one that Claude is still on: nothing to fold, and a line from before has nothing to say.
            foreach (var step in steps)
            {
                step.IsFolded = false;
            }
            if (marker is not null)
            {
                items.RemoveAt(start);
                end--;
            }
            return end;
        }
        var inserted = marker is null;
        if (marker is null)
        {
            marker = new StepsMarkerItem(this);
        }
        // At the end of the conversation Claude may still be adding to the run; once something follows, it's done.
        marker.KeepsLast = end == items.Count;
        marker.Steps.Clear();
        marker.Steps.AddRange(steps);
        // Flags first, then the marker: the list measures again for either, and sees both.
        var changed = false;
        for (var k = 0; k < steps.Count; k++)
        {
            var folded = k < steps.Count - 1 || !marker.KeepsLast;
            if (steps[k].IsFolded != folded)
            {
                steps[k].IsFolded = folded;
                changed = true;
            }
        }
        marker.Describe();
        if (inserted)
        {
            items.Insert(start, marker);
            end++;
        }
        else if (changed)
        {
            // The same item put back in its place tells the list to measure its neighbours again.
            items[start] = marker;
        }
        return end;
    }
}
