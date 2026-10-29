using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Claudette.Core;
using Claudette.Core.Docs;
using Claudette.Core.Protocol;
using Claudette.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveMarkdown.Avalonia;

namespace Claudette.App.Conversation;

/// <summary>One row in a tab's conversation (DESIGN.md §5).</summary>
public abstract class ConversationItem : ObservableObject
{
    /// <summary>
    /// A step folded into its chain's line (Settings → Conversation → Fold each chain of steps, DESIGN.md §18): the list
    /// gives it no height, as if it weren't there, though it stays in the conversation for find and export. Set by
    /// <see cref="StepRuns"/>, which tells the list by replacing the chain's line.
    /// </summary>
    public bool IsFolded { get; internal set; }

    /// <summary>What <see cref="IsFolded"/> was when the list last measured it, so it knows to measure it again.</summary>
    internal bool FoldSeen { get; set; }
}

/// <summary>
/// A user message or an assistant reply. It has a Copy button and shows when it was sent, on hover (DESIGN.md §5,
/// "Copy and times").
/// </summary>
public abstract partial class MessageItem : ConversationItem
{
    private TimeProvider? _clock;

    /// <summary>When it was sent: live, as it was built; restored, from its transcript entry. Null when unknown.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    public bool HasTime => SentAt is not null && _clock is not null;

    /// <summary>Short, in the current culture: "14:05" today, "Mon 14:05" in the last week, else the date and time.</summary>
    public string? TimeText => SentAt is { } sent && _clock is { } clock ? MessageTimes.Short(sent, clock) : null;

    /// <summary>The full date and time, for the tooltip.</summary>
    public string? TimeTip => SentAt is { } sent && _clock is { } clock ? MessageTimes.Full(sent, clock) : null;

    /// <summary>What <b>Copy message</b> puts on the clipboard.</summary>
    public abstract string CopyText { get; }

    /// <summary>Just copied: the button says "Copied" for a moment.</summary>
    [ObservableProperty]
    public partial bool IsCopied { get; set; }

    /// <summary>Sets when the message was sent; <paramref name="clock"/> says what "today" is when it's shown.</summary>
    public void Stamp(DateTimeOffset? sentAt, TimeProvider clock)
    {
        SentAt = sentAt;
        _clock = clock;
        RefreshTime();
    }

    /// <summary>"Today" moved on, for example past midnight: the short time may need its day now.</summary>
    public void RefreshTime()
    {
        OnPropertyChanged(nameof(SentAt));
        OnPropertyChanged(nameof(HasTime));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TimeTip));
    }
}

/// <summary>How a message's time reads, in the clock's time zone and the current culture (DESIGN.md §5).</summary>
public static class MessageTimes
{
    /// <summary>"14:05" today, "Mon 14:05" in the six days before, else the short date and time.</summary>
    public static string Short(DateTimeOffset sent, TimeProvider clock)
    {
        var culture = CultureInfo.CurrentCulture;
        var local = TimeZoneInfo.ConvertTime(sent, clock.LocalTimeZone);
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), clock.LocalTimeZone).Date;
        var time = local.ToString("t", culture);
        return local.Date == today ? time
            : local.Date < today && local.Date > today.AddDays(-7) ? $"{local.ToString("ddd", culture)} {time}"
            : local.ToString("g", culture);
    }

    /// <summary>The long date and the time, for the tooltip.</summary>
    public static string Full(DateTimeOffset sent, TimeProvider clock) =>
        TimeZoneInfo.ConvertTime(sent, clock.LocalTimeZone).ToString("f", CultureInfo.CurrentCulture);
}

public sealed partial class UserMessageItem(string text, string? suffixText = null, bool isCheckIn = false, bool isAutoContinue = false) : MessageItem
{
    public string Text { get; } = text;

    /// <summary>
    /// The prompt's transcript id, once Claude Code has echoed it back (or from the transcript): the point its files can
    /// be put back to (DESIGN.md §5, "Rewind and branch").
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestoreFiles))]
    public partial string? Uuid { get; set; }

    /// <summary>The id Claudette sent the prompt with, which Claude Code echoes back (DESIGN.md §13, "Wire format").</summary>
    public string? SentId { get; set; }

    /// <summary>
    /// Sent while Claude worked, and waiting its turn: Claude Code hasn't taken it yet (DESIGN.md §5, "Queued messages").
    /// Its echo ends the wait.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QueuedText), nameof(CancelQueuedTip))]
    public partial bool IsQueued { get; set; }

    /// <summary>
    /// Held by Claudette until the turn ends, with Settings → General set to queue messages sent while Claude works
    /// (DESIGN.md §5, "Queued messages"): Claude Code hasn't been sent it yet. <b>Send now</b> sends it into the turn.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QueuedText), nameof(CancelQueuedTip))]
    public partial bool IsHeld { get; set; }

    /// <summary>
    /// Held until a usage limit resets, by a tab that doesn't use usage credits while they pay (DESIGN.md §6, "Tabs that
    /// don't use usage credits"). <b>Send now</b> sends it anyway, paid for with them.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QueuedText), nameof(SendNowTip))]
    public partial bool IsHeldForCredits { get; set; }

    /// <summary>What the queued line says: when it goes.</summary>
    public string QueuedText => IsHeldForCredits ? "Queued: sent when the usage limit resets"
        : IsHeld ? "Queued: sent when this turn ends"
        : "Queued: sent when Claude can take it";

    public string SendNowTip => IsHeldForCredits
        ? "Sends it now, paid for with usage credits"
        : "Sends it now, into the turn: Claude reads it at its next step";

    public string CancelQueuedTip => IsHeld
        ? "Takes the message back before it's sent, and puts it in the composer"
        : "Takes the message back before Claude reads it, and puts it in the composer";

    /// <summary>How much of a long message the card shows until <b>Show all</b>, such as a large paste (DESIGN.md §5, "Attachments").</summary>
    public const int ShownTextLimit = 4_000;

    /// <summary>How many lines of a long message the card shows until <b>Show all</b>.</summary>
    public const int ShownLineLimit = 40;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownText), nameof(IsTextCut))]
    public partial bool ShowsAllText { get; set; }

    /// <summary>The text as the card shows it: the start of a long one, up to <see cref="ShownTextLimit"/> characters or <see cref="ShownLineLimit"/> lines.</summary>
    public string ShownText => IsTextCut ? Text[..CutAt(Text)] : Text;

    public bool IsTextCut => !ShowsAllText && CutAt(Text) < Text.Length;

    public string ShowAllText => $"Show all ({(System.Text.Encoding.UTF8.GetByteCount(Text) + 1023) / 1024:N0} KB)";

    [RelayCommand]
    private void ShowAll() => ShowsAllText = true;

    /// <summary>Where a long text is cut: at the limit, or at the end of its last shown line when that comes first.</summary>
    private static int CutAt(string text)
    {
        var end = Math.Min(text.Length, ShownTextLimit);
        var lines = 0;
        for (var i = 0; i < end; i++)
        {
            if (text[i] == '\n' && ++lines == ShownLineLimit)
            {
                return i;
            }
        }
        return end;
    }

    /// <summary>
    /// The conversation entry just before this prompt: resuming there leaves the prompt out. Null for the first prompt,
    /// before which there's nothing to keep.
    /// </summary>
    public string? ResumeAt { get; set; }

    /// <summary>Restore files to before this message: it has a checkpoint to go back to.</summary>
    public bool CanRestoreFiles => Uuid is not null;

    /// <summary>Quick suffixes appended to the message, shown in a lighter style (DESIGN.md §5, "Quick suffixes").</summary>
    public string? SuffixText { get; } = suffixText;

    public bool HasSuffix => !string.IsNullOrEmpty(SuffixText);

    /// <summary>The message as it was sent: its text, then its quick suffixes after a blank line.</summary>
    public override string CopyText => !HasSuffix ? Text : Text.Length == 0 ? SuffixText! : $"{Text}\n\n{SuffixText}";

    /// <summary>Sent by Claudette as an automatic check-in (DESIGN.md §5, "Check-ins on long turns").</summary>
    public bool IsCheckIn { get; } = isCheckIn;

    /// <summary>Sent by Claudette once a usage limit reset (DESIGN.md §6, "Continuing after a limit resets").</summary>
    public bool IsAutoContinue { get; } = isAutoContinue;

    /// <summary>
    /// Who a message came from when it wasn't the user (DESIGN.md §18, "Threads"): <i>"From the thread Plan"</i> on a
    /// sub-thread's, <i>"From the sub-threads"</i> on a thread's report, or <i>"From the session api-worker"</i>.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>Claudette passed it on for a thread or its sub-threads: taken back, it doesn't go into the composer.</summary>
    public bool IsFromThread { get; init; }

    /// <summary>The label over a message Claudette sent by itself or passed on, or null for one the user sent.</summary>
    public string? AutomaticLabel => Label ?? (IsCheckIn ? "Automatic check-in" : IsAutoContinue ? "Automatic continue after the usage limit reset" : null);

    /// <summary>Its card's label (DESIGN.md §18, "Conversation cards"): "You", or who sent it when it wasn't the user.</summary>
    public string CardLabel => AutomaticLabel ?? "You";

    /// <summary>
    /// The note on threads Claudette sent after the message (DESIGN.md §18, "Threads"): the card says so in a line, with
    /// the note as its tip.
    /// </summary>
    public string? ThreadNote { get; init; }

    public bool HasThreadNote => ThreadNote is not null;

    /// <summary>The line under a message that carried a note on threads.</summary>
    public string ThreadNoteText => ThreadNote == Core.Threads.ThreadMessages.NoLongerAThread
        ? "Told Claude this tab is no longer a thread"
        : "Told Claude about its sub-threads";

    /// <summary>
    /// The note on subagents sent after the message, after the tab's setting for them changed (DESIGN.md §5,
    /// "Subagents"): the card says so in a line, with the note as its tip.
    /// </summary>
    public string? SubagentNote { get; init; }

    public bool HasSubagentNote => SubagentNote is not null;

    /// <summary>The line under a message that carried a note on subagents: "Told Claude to avoid subagents".</summary>
    public string? SubagentNoteText { get; init; }

    /// <summary>What Claude had been told about subagents before this message's note, for when it's taken back.</summary>
    internal Core.Settings.SubagentUse? SubagentsToldBefore { get; set; }

    /// <summary>
    /// The note on the sessions and agents the message names, sent after it (DESIGN.md §5, "Autocomplete"): the card says
    /// so in a line, with the note as its tip.
    /// </summary>
    public string? MentionNote { get; init; }

    /// <summary>The names of the sessions and agents the message names.</summary>
    public IReadOnlyList<string> Mentioned { get; init; } = [];

    public bool HasMentionNote => MentionNote is not null;

    /// <summary>The line under a message that named sessions or agents.</summary>
    public string MentionNoteText => Mentioned.Count switch
    {
        0 => "Told Claude how to reach who it names",
        1 => $"Told Claude how to reach {Mentioned[0]}",
        2 => $"Told Claude how to reach {Mentioned[0]} and {Mentioned[1]}",
        _ => $"Told Claude how to reach {Mentioned[0]} and {Mentioned.Count - 1} others",
    };

    /// <summary>Attached images, shown as thumbnails (DESIGN.md §5, "Attachments").</summary>
    public IReadOnlyList<MessageImage> Images { get; init; } = [];

    /// <summary>
    /// The large pastes sent after what was typed (DESIGN.md §5, "Attachments"), so a message taken back puts them back as
    /// attachments. Only for a message sent here: a transcript's is one text.
    /// </summary>
    public IReadOnlyList<string> PastedTexts { get; set; } = [];

    /// <summary>With <see cref="PastedTexts"/>, what was typed before them; null when the text is all typed.</summary>
    public string? TypedText { get; set; }

    public bool HasImages => Images.Count > 0;
}

/// <summary>Assistant text, streamed in as Markdown.</summary>
public sealed partial class AssistantTextItem : MessageItem
{
    public ObservableStringBuilder Markdown { get; } = new();

    /// <summary>The <see cref="Markdown"/> its card shows, parsed once (DESIGN.md §5).</summary>
    public KeptMarkdown Parsed => field ??= new(Markdown);

    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = true;

    /// <summary>
    /// Claude carried on working after it, with a tool call in the same turn: a step on the way rather than the turn's
    /// answer, so its card is quieter (DESIGN.md §18, "Conversation cards").
    /// </summary>
    [ObservableProperty]
    public partial bool IsIntermediate { get; set; }

    public string Text => Markdown.ToString();

    /// <summary>The reply's Markdown, as Claude wrote it.</summary>
    public override string CopyText => Text;

    public void Append(string text) => Markdown.Append(text);
}

/// <summary>Claude's thinking, collapsed by default.</summary>
public sealed partial class ThinkingItem : ConversationItem
{
    private readonly System.Text.StringBuilder _text = new();
    private string? _textAsString = "";

    /// <summary>How long <see cref="ShownText"/> was when the view was last told it changed.</summary>
    private int _shownLength;

    /// <summary>
    /// The thinking so far. It streams in small pieces, so it's kept in a builder and made into a string only when read,
    /// rather than copied whole for every piece.
    /// </summary>
    public string Text
    {
        get => _textAsString ??= _text.ToString();
        set
        {
            _text.Clear().Append(value);
            TextChanged(force: true);
        }
    }

    public void Append(string text)
    {
        if (text.Length > 0)
        {
            _text.Append(text);
            TextChanged(force: false);
        }
    }

    /// <summary>
    /// What the view shows: the text while the card is open, else nothing, so collapsed thinking is never made into a
    /// string. While it streams in, the view hears of it as it grows by an eighth (at least 256 characters) rather than
    /// with every piece, and all of it once it's done. Without the blank lines Claude's thinking often starts or ends
    /// with, which would leave empty room in its card.
    /// </summary>
    public string? ShownText => IsExpanded ? Text.Trim() : null;

    private void TextChanged(bool force)
    {
        _textAsString = null;
        if (HasText != _saidHasText)
        {
            _saidHasText = HasText;
            OnPropertyChanged(nameof(HasText));
        }
        if (force || _shownLength == 0 || _text.Length - _shownLength >= Math.Max(256, _shownLength / 8))
        {
            ShowText();
        }
    }

    private bool _saidHasText;

    private void ShowText()
    {
        _shownLength = _text.Length;
        OnPropertyChanged(nameof(Text));
        if (IsExpanded)
        {
            OnPropertyChanged(nameof(ShownText));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    public partial bool IsStreaming { get; set; } = true;

    // All of it, once it's done.
    partial void OnIsStreamingChanged(bool value) => ShowText();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownText))]
    public partial bool IsExpanded { get; set; }

    public bool HasText => _text.Length > 0;

    /// <summary>When the thinking began and ended, by the conversation's clock; null when it isn't known.</summary>
    public DateTimeOffset? StartedAt { get; internal set; }

    public DateTimeOffset? EndedAt { get; internal set; }

    /// <summary>How long Claude thought, when both times are known.</summary>
    public TimeSpan? Duration => EndedAt - StartedAt is { } took && took >= TimeSpan.Zero ? took : null;

    public string Header => IsStreaming ? "Thinking…" : "Thinking";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>
/// A tool call: a tile in its group's card, with its icon, name, target and state, which opens to the full input and
/// output (DESIGN.md §5, §18 "Conversation cards"). Edit and Write show a diff; Bash shows its command and output; Read
/// the file as Claude read it.
/// </summary>
public partial class ToolUseItem : ConversationItem
{
    private TimeProvider? _clock;

    /// <summary>The card of calls it's a tile in; null for a subagent, which is a card of its own.</summary>
    public ToolGroupItem? Group { get; internal set; }

    /// <summary>When Claude made the call: live, as it arrived; restored, from its transcript entry. Null when unknown.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>When its result came back, by the same clock; null while it runs, or when unknown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration), nameof(DurationText), nameof(StateText))]
    public partial DateTimeOffset? CompletedAt { get; set; }

    /// <summary>"14:05" today, as a message's time reads (DESIGN.md §5, "Copy and times").</summary>
    public string? TimeText => StartedAt is { } started && _clock is { } clock ? MessageTimes.Short(started, clock) : null;

    public string? TimeTip => StartedAt is { } started && _clock is { } clock ? MessageTimes.Full(started, clock) : null;

    public bool HasTime => TimeText is not null;

    /// <summary>How long it took, from the call to its result; null while it runs or when either time is unknown.</summary>
    public TimeSpan? Duration => CompletedAt - StartedAt is { } took && took >= TimeSpan.Zero ? took : null;

    /// <summary>"0.4s", "12s", "1m 05s".</summary>
    public string? DurationText => Duration is { } took
        ? took.TotalSeconds < 10 ? string.Create(CultureInfo.CurrentCulture, $"{took.TotalSeconds:0.0}s") : Formats.Elapsed(took)
        : null;

    /// <summary>Sets when the call was made; <paramref name="clock"/> says what "today" is when its time is shown.</summary>
    public void Stamp(DateTimeOffset? startedAt, TimeProvider clock)
    {
        StartedAt = startedAt;
        _clock = clock;
        RefreshTime();
    }

    /// <summary>"Today" moved on: the short time may need its day now.</summary>
    public void RefreshTime()
    {
        OnPropertyChanged(nameof(StartedAt));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TimeTip));
        OnPropertyChanged(nameof(HasTime));
    }

    /// <summary>
    /// The tile's state line: "Running…", "Failed · 2.1s", or what the result says and how long it took ("120 lines ·
    /// 0.3s", "Done · 4.2s"). An Edit's or Write's counts show beside it instead of "Done".
    /// </summary>
    public string StateText
    {
        get
        {
            if (!IsComplete)
            {
                return "Running…";
            }
            var first = IsError ? "Failed" : ResultInfo?.Facts ?? (HasDiff ? null : "Done");
            var parts = new[] { first, DurationText }.Where(p => p is not null).ToArray();
            return parts.Length > 0 ? string.Join(" · ", parts) : "Done";
        }
    }

    /// <summary>The typed view of the result's <c>tool_use_result</c>: lines read, files found, a fetch's status.</summary>
    public ToolOutput? ResultInfo { get; private set; }

    /// <summary>Most of a Read's text kept for its tile and window; a longer file is cut, as a tool's output is shown.</summary>
    public const int ReadTextLimit = 200_000;

    /// <summary>The file as a Read read it, without line numbers (cut at <see cref="ReadTextLimit"/>); null for other tools.</summary>
    public string? ReadText { get; private set; }

    /// <summary>The line <see cref="ReadText"/> starts at.</summary>
    public int ReadStartLine => ResultInfo?.StartLine ?? 1;

    public bool HasReadText => !string.IsNullOrEmpty(ReadText);

    /// <summary>The read text as the opened tile shows it: the start of a long one, as for output.</summary>
    public string? ShownReadText => ReadText is { Length: > ShownOutputLimit } text && !ShowsAllOutput ? text[..ShownOutputLimit] : ReadText;

    public bool IsReadTextCut => !ShowsAllOutput && ReadText is { Length: > ShownOutputLimit };

    public ToolUseItem(string toolUseId, string name, JsonObject input)
    {
        ToolUseId = toolUseId;
        Name = name;
        // A copy without its parent: the input is a node of the whole message, which it would otherwise keep alive.
        Input = input.Parent is null ? input : input.DeepClone().AsObject();
        Summary = Summarize(name, input);
        Command = name == "Bash" ? Str(input, "command") : null;
        Detail = DetailText(name, input);
        if (name == "Edit" && Str(input, "old_string") is { } oldText && Str(input, "new_string") is { } newText)
        {
            Diff = DiffView.FromReplacement(oldText, newText);
        }
        else if (name == "Write" && Str(input, "content") is { } content)
        {
            Diff = DiffView.FromNewFile(content);
        }
    }

    public string ToolUseId { get; }

    public string Name { get; }

    public JsonObject Input { get; }

    /// <summary>
    /// The file a Read, Edit, Write, MultiEdit or NotebookEdit worked on, which its detail's <b>Open</b> opens (DESIGN.md
    /// §18, "Project files", "Opening files in the editor"); null for other tools.
    /// </summary>
    public string? FilePath => Name is "Read" or "Edit" or "Write" or "MultiEdit" or "NotebookEdit"
        ? Str(Input, "file_path") ?? Str(Input, "notebook_path")
        : null;

    public bool CanOpenFile => FilePath is { Length: > 0 };

    /// <summary>The line to open the file at: an edit's first changed line, from its <c>structuredPatch</c>, or where a Read started.</summary>
    public int? FileLine => ChangedLine ?? (Name == "Read" && ResultInfo is { } read ? read.StartLine : null);

    /// <summary>The first line an Edit or Write changed, from its result's <c>structuredPatch</c>.</summary>
    private int? ChangedLine { get; set; }

    public string Summary { get; }

    /// <summary>
    /// What its tile says it worked on (DESIGN.md §18, "Conversation cards"): the summary, but a file by the end of its
    /// path, which says the most in a tile's width: <c>…\Player\StarfallCharacter.cpp</c>.
    /// </summary>
    public string TileSummary
    {
        get
        {
            if ((Str(Input, "file_path") ?? Str(Input, "notebook_path")) is not { Length: > 0 } path)
            {
                return Summary;
            }
            var separator = path.Contains('\\') && !path.Contains('/') ? '\\' : '/';
            var parts = path.Split('/', '\\').Where(p => p.Length > 0).ToArray();
            var tail = parts.Length > 2 ? $"…{separator}{parts[^2]}{separator}{parts[^1]}" : path;
            return Name == "Read" && Input["offset"] is { } offset ? $"{tail} (from line {offset})" : tail;
        }
    }

    /// <summary>What a screen reader calls its tile: the tool and what it worked on, "Edit C:\src\auth.cs".</summary>
    public string TileName => Summary.Length > 0 ? $"{Name} {Summary}" : Name;

    /// <summary>The summary in full, for its tooltip: a command keeps its line breaks.</summary>
    public string FullSummary => Name is "Bash" or "PowerShell" && Str(Input, "command") is { } command
        ? command.Trim()
        : Summarize(Name, Input, int.MaxValue);

    public string Icon => Name switch
    {
        "Read" => "▤",
        "Write" or "Edit" or "NotebookEdit" => "✎",
        "Bash" => "❯",
        "Grep" or "Glob" => "⌕",
        "WebFetch" or "WebSearch" => "◍",
        "Agent" or "Task" => "◈",
        "Skill" => "✦",
        "AskUserQuestion" => "?",
        "ExitPlanMode" => "☰",
        _ => "•",
    };

    /// <summary>The card's icon: the key of a vector icon in App.axaml (DESIGN.md §5).</summary>
    public string IconKey => ToolIcons.KeyFor(Name);

    /// <summary>Edit, Write and the other file tools, which can be opened in the diff view (DESIGN.md §5, §8).</summary>
    public bool IsFileChange => Core.Diffs.ChangedFiles.IsFileTool(Name);

    /// <summary>A file change that went through, so there's something to show in the diff view.</summary>
    public bool CanOpenDiff => IsFileChange && IsComplete && !IsError;

    /// <summary>For Bash: the command, shown in full when expanded.</summary>
    public string? Command { get; }

    public bool IsBash => Command is not null;

    /// <summary>The full input, for tools without a better view.</summary>
    public string? Detail { get; }

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiff), nameof(DiffStats), nameof(StateText))]
    public partial DiffView? Diff { get; set; }

    public bool HasDiff => Diff is { Lines.Count: > 0 };

    public string? DiffStats => Diff?.Stats;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput), nameof(ShownOutput), nameof(IsOutputCut), nameof(ShowAllOutputText))]
    public partial string? Output { get; set; }

    public bool HasOutput => !string.IsNullOrEmpty(Output);

    /// <summary>How much of a long output the card shows until **Show all** (a whole file Read, a big Grep).</summary>
    public const int ShownOutputLimit = 20_000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownOutput), nameof(IsOutputCut), nameof(ShownReadText), nameof(IsReadTextCut))]
    public partial bool ShowsAllOutput { get; private set; }

    /// <summary>
    /// The output as the card shows it: the start of a long one, since laying out a hundred kilobytes of text in one block
    /// is slow even while most of it is scrolled out of sight.
    /// </summary>
    public string? ShownOutput => IsOutputCut ? Output![..ShownOutputLimit] : Output;

    public bool IsOutputCut => !ShowsAllOutput && Output is { Length: > ShownOutputLimit };

    public string ShowAllOutputText => (Output ?? ReadText) is { } output
        ? $"Show all ({(output.Length + 1023) / 1024:N0} KB)"
        : "Show all";

    [RelayCommand]
    private void ShowAllOutput() => ShowsAllOutput = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResultSummary))]
    public partial string? ResultSummary { get; set; }

    public bool HasResultSummary => !string.IsNullOrEmpty(ResultSummary);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenDiff), nameof(StateText))]
    public partial bool IsError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenDiff), nameof(StateText))]
    public partial bool IsComplete { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public bool CanExpand => HasDetail || HasDiff || HasOutput || IsBash || HasReadText;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    /// <summary>Fills in the result, using Claude Code's structured <c>tool_use_result</c> where it has one.</summary>
    public virtual void ApplyResult(string text, bool isError, JsonNode? toolUseResult)
    {
        IsComplete = true;
        IsError = isError;
        ResultInfo = isError ? null : ToolOutput.From(Name, toolUseResult);
        if (ResultInfo?.FileContent is { Length: > 0 } read)
        {
            ReadText = read.Length > ReadTextLimit ? read[..ReadTextLimit] : read;
            OnPropertyChanged(nameof(ReadText));
            OnPropertyChanged(nameof(HasReadText));
            OnPropertyChanged(nameof(ShownReadText));
            OnPropertyChanged(nameof(IsReadTextCut));
        }
        OnPropertyChanged(nameof(ResultInfo));
        OnPropertyChanged(nameof(StateText));
        if (toolUseResult is JsonObject result)
        {
            if (result["structuredPatch"] is JsonArray { Count: > 0 } patch)
            {
                Diff = DiffView.FromStructuredPatch(patch);
                ChangedLine = FirstChangedLine(patch);
            }
            else if (Name == "Write" && Str(result, "type") == "create" && Str(result, "content") is { } content)
            {
                Diff = DiffView.FromNewFile(content);
            }
            if (Name == "Bash")
            {
                // Output that's an image (isImage) is a data URL, which shows as the image instead (Images).
                var stdout = result.GetBool("isImage") == true ? "" : Str(result, "stdout") ?? "";
                var stderr = Str(result, "stderr") ?? "";
                Output = string.Join('\n', new[] { stdout, stderr }.Where(s => s.Length > 0));
                ResultSummary = BashSummary(result) ?? FirstLine(Output) ?? FirstLine(text);
                GitChips = Conversation.GitChips.From(result.GetObject("gitOperation"));
                OnPropertyChanged(nameof(CanExpand));
                return;
            }
        }
        ResultSummary = HasDiff && !isError ? null : FirstLine(text);
        if (!HasDiff && Name is not ("Read" or "Agent" or "Task"))
        {
            Output = text;
        }
        OnPropertyChanged(nameof(CanExpand));
    }

    /// <summary>What git did, read from a Bash result's <c>gitOperation</c>: "Committed 1a2b3c4 on main", "Opened PR #42".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGitChips))]
    public partial IReadOnlyList<GitChip> GitChips { get; private set; } = [];

    public bool HasGitChips => GitChips.Count > 0;

    /// <summary>
    /// The images in the result, under the card (DESIGN.md §18, "Images from Claude's tools"): an image Read read, a
    /// PDF's pages, a notebook's figures, an MCP tool's screenshot. A click opens one full size.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImages))]
    public partial IReadOnlyList<ToolImage> Images { get; internal set; } = [];

    public bool HasImages => Images.Count > 0;

    /// <summary>
    /// The Claude Doc a call to claude.ai's Docs connector made or changed (DESIGN.md §5, "Claude Docs"): a link to it, in
    /// place of the first line of the connector's reply.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDoc))]
    public partial DocLink? Doc { get; private set; }

    public bool HasDoc => Doc is not null;

    internal void ShowDoc(DocLink link)
    {
        Doc = link;
        ResultSummary = null;
    }

    /// <summary>How a Bash command ended, when its result says more than its output's first line (DESIGN.md §5).</summary>
    private static string? BashSummary(JsonObject result)
    {
        if (result.GetDouble("timedOutAfterMs") is { } timedOut)
        {
            return $"Reached its {Formats.Elapsed(TimeSpan.FromMilliseconds(timedOut))} time limit; carries on in the background";
        }
        if (result.GetString("backgroundTaskId") is not null)
        {
            return result.GetBool("backgroundedByUser") == true ? "Moved to the background" : "Running in the background";
        }
        return result.GetBool("interrupted") == true ? "Interrupted" : null;
    }

    /// <summary>The most telling input field, such as the file path or command.</summary>
    public static string Summarize(string name, JsonObject input, int maxLength = 140)
    {
        var summary = name switch
        {
            "Read" when Str(input, "file_path") is { } path && input["offset"] is not null
                => $"{path} (from line {input["offset"]})",
            "Grep" when Str(input, "pattern") is { } pattern
                => Str(input, "path") is { } where ? $"{pattern} in {where}" : pattern,
            "Agent" or "Task" => Str(input, "description"),
            "AskUserQuestion" => (input["questions"] as JsonArray)?.OfType<JsonObject>().Select(q => Str(q, "question")).FirstOrDefault(q => q is not null),
            "ExitPlanMode" => "Plan ready for review",
            _ => null,
        };
        if (summary is null)
        {
            foreach (var key in new[] { "file_path", "notebook_path", "command", "pattern", "path", "url", "query", "skill", "description", "prompt" })
            {
                if (Str(input, key) is { } value)
                {
                    summary = value;
                    break;
                }
            }
        }
        summary = (summary ?? "").ReplaceLineEndings(" ");
        return summary.Length > maxLength ? summary[..(maxLength - 3)] + "…" : summary;
    }

    private static string? DetailText(string name, JsonObject input) => name switch
    {
        "Bash" or "Edit" or "Write" or "Read" => null,
        "Agent" or "Task" => Str(input, "prompt"),
        "ExitPlanMode" => Str(input, "plan"),
        "AskUserQuestion" => string.Join("\n\n", (input["questions"] as JsonArray ?? []).OfType<JsonObject>().Select(q =>
            $"{Str(q, "question")}\n" + string.Join("\n", (q["options"] as JsonArray ?? []).OfType<JsonObject>().Select(o => $"  • {Str(o, "label")}")))),
        "WebFetch" => Str(input, "prompt") is { } prompt ? $"{Str(input, "url")}\n\n{prompt}" : null,
        _ => input.Count > 0 ? input.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) : null,
    };

    protected static string? Str(JsonObject obj, string name) => obj.GetString(name);

    /// <summary>The first line a <c>structuredPatch</c> changed: its first hunk's start, past the unchanged lines leading it.</summary>
    private static int? FirstChangedLine(JsonArray patch)
    {
        if (patch[0] is not JsonObject hunk || hunk["newStart"].AsWholeNumber() is not { } start)
        {
            return null;
        }
        var context = hunk["lines"] is JsonArray lines ? lines.TakeWhile(l => l is JsonValue value && value.TryGetValue<string>(out var line) && line.StartsWith(' ')).Count() : 0;
        return (int)Math.Max(1, start + context);
    }

    protected static string? FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var line = text.AsSpan().Trim();
        var end = line.IndexOfAny('\r', '\n');
        var first = (end >= 0 ? line[..end] : line).ToString();
        return first.Length > 160 ? first[..157] + "…" : first;
    }
}

/// <summary>A subagent: a collapsible group holding that agent's own text and tool calls (DESIGN.md §5).</summary>
public sealed partial class SubagentItem : ToolUseItem
{
    public SubagentItem(string toolUseId, string name, JsonObject input) : base(toolUseId, name, input)
    {
        AgentType = Str(input, "subagent_type") ?? "agent";
    }

    public string AgentType { get; }

    public ObservableCollection<ConversationItem> Items { get; } = [];

    /// <summary>Launched in the background: its call returns at once, and it keeps running (DESIGN.md §18).</summary>
    [ObservableProperty]
    public partial bool IsBackground { get; private set; }

    /// <summary>Stopped, by the user or an interrupt: neither done nor failed, so its dot is muted.</summary>
    public bool IsStopped { get; private set; }

    /// <summary>Finished and returned its report: the green dot.</summary>
    public bool IsSucceeded => IsComplete && !IsError && !IsStopped;

    /// <summary>
    /// Claudette's hook refused its <c>Agent</c> call, so it never started (DESIGN.md §5, "Subagents"): its dot is muted,
    /// it has no cards, and it has no row in the sidebar or the agent map.
    /// </summary>
    public bool IsRefused { get; private set; }

    /// <summary>It started, so it has work to show: every subagent but one Claudette's hook refused.</summary>
    public bool IsStarted => !IsRefused;

    /// <summary>The call came back refused by Claudette's hook, as Settings → Tabs → Subagents says.</summary>
    internal void ShowRefused()
    {
        IsComplete = true;
        IsError = false;
        IsStopped = true;
        IsRefused = true;
        ResultSummary = "Not started: subagents are turned off for this tab";
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsSucceeded));
        OnPropertyChanged(nameof(IsRefused));
        OnPropertyChanged(nameof(IsStarted));
    }

    public override void ApplyResult(string text, bool isError, JsonNode? toolUseResult)
    {
        if (!isError && toolUseResult is JsonObject result && Str(result, "status") is "async_launched" or "remote_launched")
        {
            // Not finished: a task notification says when it is.
            IsBackground = true;
            ResultSummary = "Running in the background";
            return;
        }
        base.ApplyResult(text, isError, toolUseResult);
        if (!isError && FirstLine(Report(text, toolUseResult)) is { } report)
        {
            ResultSummary = report;
        }
        OnPropertyChanged(nameof(IsSucceeded));
    }

    /// <summary>How it ended, as the agent map sees it, so the group's status dot and result line agree with the map.</summary>
    internal void ShowEnded(AgentStatus status, string? report)
    {
        IsComplete = true;
        IsError = status == AgentStatus.Failed;
        IsStopped = status == AgentStatus.Stopped;
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsSucceeded));
        ResultSummary = status switch
        {
            AgentStatus.Stopped => "Stopped",
            _ => FirstLine(report) ?? ResultSummary,
        };
    }

    private const string HandBackMarker = "The report follows:";

    /// <summary>
    /// The report a subagent handed back. Claude Code puts it in <c>tool_use_result.content</c>; nested subagents'
    /// results have no <c>tool_use_result</c>, so their tool result text is read instead, without the frame Claude
    /// Code puts around it ("[Subagent hand-back] … The report follows:", indented, then an <c>agentId</c> and
    /// <c>&lt;usage&gt;</c> trailer). Null when the frame is there but can't be read.
    /// </summary>
    public static string? Report(string text, JsonNode? toolUseResult)
    {
        if (toolUseResult is JsonObject result && result["content"] is JsonArray content)
        {
            var joined = string.Join("\n\n", content.OfType<JsonObject>().Where(b => Str(b, "type") == "text").Select(b => Str(b, "text")).OfType<string>());
            if (joined.Length > 0)
            {
                return joined;
            }
        }
        if (!text.StartsWith("[Subagent hand-back]", StringComparison.Ordinal))
        {
            return text;
        }
        var start = text.IndexOf(HandBackMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }
        var lines = text[(start + HandBackMarker.Length)..].Split('\n').ToList();
        var usage = lines.FindIndex(l => l.StartsWith("<usage>", StringComparison.Ordinal));
        if (usage >= 0)
        {
            lines.RemoveRange(usage, lines.Count - usage);
        }
        if (lines.Count > 0 && lines[^1].StartsWith("agentId: ", StringComparison.Ordinal))
        {
            lines.RemoveAt(lines.Count - 1);
        }
        // The harness indents every line of the report by two spaces.
        return string.Join('\n', lines.Select(l => l.StartsWith("  ", StringComparison.Ordinal) ? l[2..] : l)).Trim();
    }
}

public enum NoteKind
{
    Info,
    Warning,
    Error,
}

/// <summary>A system note: a model change, an error, a retry, the process exiting.</summary>
public sealed partial class NoteItem(string text, NoteKind kind) : ConversationItem
{
    [ObservableProperty]
    public partial string Text { get; set; } = text;

    public NoteKind Kind { get; } = kind;

    /// <summary>An address to open from the note, such as the tab's session in the Claude app (DESIGN.md §18).</summary>
    public string? Link { get; init; }

    public bool HasLink => Link is not null;

    public bool IsError => Kind == NoteKind.Error;

    public bool IsWarning => Kind == NoteKind.Warning;

    public bool IsInfo => Kind == NoteKind.Info;

    /// <summary>Its card's label (DESIGN.md §18, "Conversation cards").</summary>
    public string KindLabel => Kind switch
    {
        NoteKind.Error => "Error",
        NoteKind.Warning => "Warning",
        _ => "Note",
    };

    /// <summary>Its card's icon: the key of one in App.axaml.</summary>
    public string KindIcon => Kind switch
    {
        NoteKind.Error => "IconCardError",
        NoteKind.Warning => "IconCardWarning",
        _ => "IconCardNote",
    };
}

/// <summary>How a hook run ended, or that it hasn't yet.</summary>
public enum HookRunState
{
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>
/// One run of a hook from the user's or project's settings (DESIGN.md §5, "Hook runs"): a compact row, which expands to
/// what the hook printed. Hook names and output are the hook's own: untrusted text.
/// </summary>
public sealed partial class HookRunItem(string hookId, string hookName, string hookEvent) : ConversationItem
{
    /// <summary>Most of a hook's output kept for the row; a hook that prints more is cut, as a tool's output is.</summary>
    public const int OutputLimit = 20_000;

    public string HookId { get; } = hookId;

    /// <summary>The hook's name, such as <c>UserPromptSubmit</c> or the matcher it runs for.</summary>
    public string HookName { get; } = hookName;

    /// <summary>The event that ran it, such as <c>PreToolUse</c>.</summary>
    public string HookEvent { get; } = hookEvent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsFailed), nameof(Title), nameof(StatusText))]
    public partial HookRunState State { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    public partial string Output { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial int? ExitCode { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public bool IsRunning => State == HookRunState.Running;

    public bool IsFailed => State == HookRunState.Failed;

    public bool HasOutput => Output.Length > 0;

    /// <summary>"PreToolUse hook" or "PreToolUse hook (Bash)", for the row.</summary>
    public string Title
    {
        get
        {
            // Claude Code names a hook after its event and matcher, "PreToolUse:Bash": the event is in the title already.
            var name = HookName.StartsWith(HookEvent + ":", StringComparison.Ordinal) ? HookName[(HookEvent.Length + 1)..] : HookName;
            return name.Length > 0 && name != HookEvent ? $"{HookEvent} hook ({name})" : $"{HookEvent} hook";
        }
    }

    public string StatusText => State switch
    {
        HookRunState.Running => "running…",
        HookRunState.Failed => ExitCode is { } code ? $"failed (exit code {code})" : "failed",
        HookRunState.Cancelled => "cancelled",
        _ => "done",
    };

    internal void Append(string text)
    {
        if (text.Length == 0 || Output.Length >= OutputLimit)
        {
            return;
        }
        var joined = Output + text;
        Output = joined.Length > OutputLimit ? joined[..OutputLimit] + "\n…" : joined;
    }
}

/// <summary>
/// A message Claudette doesn't know and skipped, shown only while protocol logging is on (DESIGN.md §16): a collapsed
/// row that expands to the raw JSON.
/// </summary>
public sealed partial class UnsupportedMessageItem(string messageType, string json) : ConversationItem
{
    public string Title => $"Unsupported message from Claude Code: {messageType}";

    public string Json { get; } = json;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
}

/// <summary>A file on a turn's footer, which opens the turn's changes to it (DESIGN.md §8, "Changes per turn").</summary>
public sealed partial class TurnFileRow(TurnSummaryItem turn, Core.Diffs.TurnFileChange change, string displayPath) : ObservableObject
{
    public TurnSummaryItem Turn { get; } = turn;

    public Core.Diffs.TurnFileChange Change { get; } = change;

    public string DisplayPath { get; } = displayPath;

    /// <summary>The file's own name, which the Modified files card puts first.</summary>
    public string FileName => Path.GetFileName(DisplayPath.TrimEnd('/', '\\')) is { Length: > 0 } name ? name : DisplayPath;

    /// <summary>The folder it's in, as the path shows it; null for a file at the top of the tab's folder.</summary>
    public string? Folder => Path.GetDirectoryName(DisplayPath) is { Length: > 0 } folder ? folder.Replace('\\', '/') : null;

    public bool HasFolder => Folder is not null;

    /// <summary>
    /// <c>+12 −3</c> once counted against how the turn left the file, or what changed when no line did (<c>CRLF → LF</c>);
    /// null until then, or when it can't be.
    /// </summary>
    [ObservableProperty]
    public partial string? Stats { get; set; }

    /// <summary>What changed that the lines don't show, once counted (DESIGN.md §8, "What the lines don't show").</summary>
    public Core.Diffs.HiddenChanges? Hidden { get; set; }

    /// <summary>What <see cref="Hidden"/> says, under the path in the tile's tip.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tip))]
    public partial string? Note { get; set; }

    /// <summary>The tile's tip: the file's path, and what changed that the lines don't show.</summary>
    public string Tip => Note is null ? Change.Path : $"{Change.Path}\n{Note}";
}

/// <summary>
/// The footer after each turn (DESIGN.md §5): a Turn statistics card with its duration, tokens and model, and above it a
/// Modified files card with the files the turn changed (DESIGN.md §8, "Changes per turn").
/// </summary>
public sealed partial class TurnSummaryItem(string text) : ConversationItem
{
    private TimeProvider? _clock;

    public string Text { get; } = text;

    /// <summary>The tool calls the turn made, every agent's; 0 when it made none.</summary>
    public int ToolCalls { get; init; }

    /// <summary>The input tokens read from the prompt cache, when the result says.</summary>
    public long? CacheReadTokens { get; init; }

    /// <summary>
    /// What the Turn statistics card says: <see cref="Text"/>, then how much came from the prompt cache and the tool
    /// calls: "1m 04s · 75.2k in · 904 out · Opus 5.5 · 62k from cache · 7 tool calls".
    /// </summary>
    public string StatsText
    {
        get
        {
            var parts = new List<string> { Text };
            if (CacheReadTokens is > 0 and var cached)
            {
                parts.Add($"{Tokens(cached)} from cache");
            }
            if (ToolCalls > 0)
            {
                parts.Add(ToolCalls == 1 ? "1 tool call" : $"{ToolCalls} tool calls");
            }
            return string.Join(" · ", parts.Where(p => p.Length > 0));
        }
    }

    /// <summary>When the turn ended, by the tab's clock.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    public string? TimeText => EndedAt is { } ended && _clock is { } clock ? MessageTimes.Short(ended, clock) : null;

    public string? TimeTip => EndedAt is { } ended && _clock is { } clock ? MessageTimes.Full(ended, clock) : null;

    internal void Stamp(DateTimeOffset? endedAt, TimeProvider clock)
    {
        EndedAt = endedAt;
        _clock = clock;
        RefreshTime();
    }

    /// <summary>"Today" moved on: the short time may need its day now.</summary>
    public void RefreshTime()
    {
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TimeTip));
    }

    /// <summary>The files the turn changed with Edit, Write, MultiEdit or NotebookEdit, in order; empty when it changed none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles), nameof(FilesText))]
    public partial IReadOnlyList<TurnFileRow> Files { get; set; } = [];

    public bool HasFiles => Files.Count > 0;

    public string FilesText => Files.Count == 1 ? "1 file changed" : $"{Files.Count} files changed";

    /// <summary>The footer for a turn's result; null when the result says none of those.</summary>
    /// <param name="spent">What the turn spent, by model: the models it names.</param>
    /// <param name="modelName">Turns a model id into a display name.</param>
    /// <param name="toolCalls">The tool calls the turn made.</param>
    internal static TurnSummaryItem? For(ResultMessage result, ModelUsageTotals spent, Func<string?, string?> modelName, int toolCalls = 0)
    {
        var parts = new List<string>();
        if (result.DurationMs is { } ms)
        {
            parts.Add(ms >= 60_000 ? Formats.Elapsed(TimeSpan.FromMilliseconds(ms)) : $"{ms / 1000:0.#}s");
        }
        if (result.Usage is { } usage)
        {
            var input = (Number(usage["input_tokens"]) ?? 0) + (Number(usage["cache_creation_input_tokens"]) ?? 0) + (Number(usage["cache_read_input_tokens"]) ?? 0);
            var output = Number(usage["output_tokens"]) ?? 0;
            parts.Add($"{Tokens(input)} in · {Tokens(output)} out");
        }
        // Only the models this turn used: the result's own list counts every turn of the process.
        if (!spent.IsEmpty)
        {
            parts.Add(string.Join(", ", spent.Models.Keys.Select(m => modelName(m) ?? m)));
        }
        return parts.Count > 0
            ? new TurnSummaryItem(string.Join(" · ", parts)) { ToolCalls = toolCalls, CacheReadTokens = Number(result.Usage?["cache_read_input_tokens"]) }
            : null;
    }

    private static string Tokens(long count) => count switch
    {
        >= 1_000_000 => $"{count / 1_000_000.0:0.#}M",
        >= 1_000 => $"{count / 1_000.0:0.#}k",
        _ => count.ToString(),
    };

    private static long? Number(JsonNode? node) => node.AsWholeNumber();
}

/// <summary>
/// Where one of Claude's tasks started (DESIGN.md §5, "Tasks"): a slim row in the conversation, which follows the task
/// to done and opens the Tasks page.
/// </summary>
public sealed partial class TaskStartItem : ConversationItem
{
    public TaskStartItem(TodoItem todo)
    {
        Todo = todo;
        StartedAt = todo.StartedAt;
        todo.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TodoItem.CompletedAt))
            {
                OnPropertyChanged(nameof(TimeText));
            }
        };
    }

    public TodoItem Todo { get; }

    /// <summary>When it started, by the tab's clock or its transcript entry; null when not known.</summary>
    public DateTimeOffset? StartedAt { get; }

    /// <summary>"Started 14:05", then "Took 4m" once it's done.</summary>
    public string? TimeText =>
        Todo.CompletedAt is { } done && StartedAt is { } began ? $"Took {Formats.Duration(done - began)}"
        : StartedAt is { } started ? $"Started {started.ToLocalTime():t}"
        : null;
}

/// <summary>
/// Where Claude's task list stands as a turn that changed it ends (DESIGN.md §5, "Tasks"), with <b>Continue</b> while
/// tasks are left.
/// </summary>
public sealed partial class TasksSummaryItem(string text, bool allDone) : ConversationItem
{
    /// <summary>"All 7 tasks done · 23m", or "4 of 7 tasks left · Next: #4 Run the migration".</summary>
    public string Text { get; } = text;

    public bool AllDone { get; } = allDone;

    public string Glyph => AllDone ? "✓" : "◐";

    /// <summary><b>Continue</b> shows: this is the latest such row, tasks are left, and no turn has started since.</summary>
    [ObservableProperty]
    public partial bool IsContinueOffered { get; set; }
}

/// <summary>
/// A Claude Doc a turn made or changed, as it ends (DESIGN.md §5, "Claude Docs"), with <b>Open</b>: Claude often names
/// the doc without its address.
/// </summary>
public sealed class DocSummaryItem(DocRow doc, ClaudeDocChange change) : ConversationItem
{
    public DocRow Doc { get; } = doc;

    public ClaudeDocChange Change { get; } = change;

    /// <summary>"Created “Enemies: your task list”".</summary>
    public string Text => Doc.LinkText(Change);
}
