using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.Conversation;

/// <summary>
/// A run of tool calls with nothing else between them: one Tool calls card, with a tile for each call (DESIGN.md §18,
/// "Conversation cards"). Anything else joining the conversation ends the run, and the next call starts a new card. One
/// tile at a time is open, its detail under the tiles.
/// </summary>
public sealed partial class ToolGroupItem : ConversationItem
{
    private TimeProvider? _clock;

    public ObservableCollection<ToolUseItem> Calls { get; } = [];

    /// <summary>The call whose detail shows under the tiles; null while none is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExpanded))]
    public partial ToolUseItem? Expanded { get; private set; }

    public bool HasExpanded => Expanded is not null;

    /// <summary>
    /// Only the header line shows, not the tiles (Settings → Conversation → Collapse tool calls, or the chevron's own
    /// choice). Opening a call, as find does, opens the card.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCollapsed { get; set; }

    /// <summary>The chevron: folds the tiles away, or brings them back.</summary>
    [RelayCommand]
    private void Toggle() => IsCollapsed = !IsCollapsed;

    /// <summary>When its first call was made.</summary>
    public DateTimeOffset? StartedAt => Calls.Count > 0 ? Calls[0].StartedAt : null;

    public string? TimeText => StartedAt is { } started && _clock is { } clock ? MessageTimes.Short(started, clock) : null;

    public string? TimeTip => StartedAt is { } started && _clock is { } clock ? MessageTimes.Full(started, clock) : null;

    /// <summary>"1 call", "4 calls · 1 running", "4 calls · 1 failed".</summary>
    public string CountText
    {
        get
        {
            var count = Calls.Count == 1 ? "1 call" : $"{Calls.Count} calls";
            var running = Calls.Count(c => !c.IsComplete);
            var failed = Calls.Count(c => c.IsComplete && c.IsError);
            return running > 0 ? $"{count} · {running} running"
                : failed > 0 ? $"{count} · {failed} failed"
                : count;
        }
    }

    /// <summary>A call is still running.</summary>
    public bool IsRunning => Calls.Any(c => !c.IsComplete);

    /// <summary>A call failed.</summary>
    public bool HasFailed => Calls.Any(c => c.IsComplete && c.IsError);

    /// <summary>Says what "today" is when its time is shown.</summary>
    internal void UseClock(TimeProvider clock)
    {
        _clock = clock;
        RefreshTime();
    }

    /// <summary>"Today" moved on: the short times may need their day now.</summary>
    public void RefreshTime()
    {
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TimeTip));
        foreach (var call in Calls)
        {
            call.RefreshTime();
        }
    }

    internal void Add(ToolUseItem call)
    {
        call.Group = this;
        call.PropertyChanged += OnCallChanged;
        Calls.Add(call);
        if (call.IsExpanded)
        {
            Open(call);
        }
        Changed();
    }

    /// <summary>Takes a call's tile away, as a question or plan does when its own card replaces it.</summary>
    internal void Remove(ToolUseItem call)
    {
        if (!Calls.Remove(call))
        {
            return;
        }
        call.PropertyChanged -= OnCallChanged;
        call.Group = null;
        if (ReferenceEquals(Expanded, call))
        {
            Expanded = null;
        }
        Changed();
    }

    private void OnCallChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ToolUseItem call)
        {
            return;
        }
        switch (e.PropertyName)
        {
            case nameof(ToolUseItem.IsExpanded) when call.IsExpanded:
                Open(call);
                break;
            case nameof(ToolUseItem.IsExpanded) when ReferenceEquals(Expanded, call):
                Expanded = null;
                break;
            case nameof(ToolUseItem.IsComplete) or nameof(ToolUseItem.IsError):
                Changed();
                break;
        }
    }

    /// <summary>One tile open at a time: opening one closes the one that was.</summary>
    private void Open(ToolUseItem call)
    {
        if (Expanded is { } open && !ReferenceEquals(open, call))
        {
            open.IsExpanded = false;
        }
        Expanded = call;
        IsCollapsed = false;
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasFailed));
        OnPropertyChanged(nameof(StartedAt));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TimeTip));
    }
}
