using Claudette.Core;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// The offer at the top of the sidebar to show Claude Code's projects (DESIGN.md §4, "Claude Code's projects"): how many
/// folders Claude Code has sessions in on this machine.
/// </summary>
public sealed record ClaudeProjectsOffer(int FolderCount)
{
    public string Text => $"Claude Code has sessions in {FolderCount} folder{(FolderCount == 1 ? "" : "s")} on this machine.";
}

/// <summary>
/// Claude Code's projects in the sidebar (DESIGN.md §4, "Claude Code's projects"): with Settings → Tabs → <b>Show Claude
/// Code's projects in the sidebar</b> on, every folder Claude Code has sessions in has a group, tab or no tab, and every
/// group ends with its past sessions, read as History reads them. Until the user has chosen, the sidebar offers once.
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>The sessions last read, every project's, which the groups are laid out from.</summary>
    private IReadOnlyList<HistoryEntry> _claudeSessions = [];

    /// <summary>A read that a later one has overtaken drops its result instead of replacing the newer one's.</summary>
    private int _claudeSessionsRead;

    private bool _showsClaudeProjects;

    private bool ShowsClaudeProjects => _services.Settings.Sidebar.ShowClaudeProjects;

    /// <summary>The offer to show them, until the user has chosen; null otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClaudeProjectsOffer))]
    public partial ClaudeProjectsOffer? ClaudeProjectsOffer { get; private set; }

    public bool HasClaudeProjectsOffer => ClaudeProjectsOffer is not null;

    /// <summary>At launch, once the tabs are back: the projects, or the one-time offer.</summary>
    private void StartClaudeProjects()
    {
        _showsClaudeProjects = ShowsClaudeProjects;
        if (_showsClaudeProjects)
        {
            RefreshClaudeProjects();
        }
        else if (!_services.State.ClaudeProjectsOffered)
        {
            _ = OfferClaudeProjectsAsync();
        }
    }

    /// <summary>Settings → Tabs changed: the groups come, or the ones with nothing open go.</summary>
    private void OnClaudeProjectsSettingChanged()
    {
        var shows = ShowsClaudeProjects;
        if (shows == _showsClaudeProjects)
        {
            return;
        }
        _showsClaudeProjects = shows;
        // Choosing in Settings is choosing: the offer isn't made after it.
        if (!_services.State.ClaudeProjectsOffered)
        {
            _services.State.ClaudeProjectsOffered = true;
            _services.SaveState();
        }
        ClaudeProjectsOffer = null;
        if (shows)
        {
            RefreshClaudeProjects();
        }
        else
        {
            _claudeSessions = [];
            ApplyClaudeProjects();
        }
    }

    /// <summary>
    /// Reads Claude Code's sessions again, off the UI thread, and lays the groups out from them. Nothing happens while
    /// the setting is off. The index caches each transcript by size and date, so a read after the first is quick.
    /// </summary>
    internal void RefreshClaudeProjects()
    {
        if (!ShowsClaudeProjects)
        {
            return;
        }
        _ = RefreshClaudeProjectsAsync();
    }

    private async Task RefreshClaudeProjectsAsync()
    {
        var read = ++_claudeSessionsRead;
        IReadOnlyList<HistoryEntry> sessions;
        try
        {
            sessions = await HistorySessions.ReadAsync(_services, OpenSessionIds());
        }
        catch (Exception)
        {
            // History says what went wrong when it's opened; the sidebar just keeps what it has.
            return;
        }
        if (read != _claudeSessionsRead || !ShowsClaudeProjects)
        {
            return;
        }
        _claudeSessions = sessions;
        ApplyClaudeProjects();
    }

    private HashSet<string> OpenSessionIds() => AllTabs.Select(t => t.State.SessionId).OfType<string>().ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Lays the sidebar out from the sessions last read: a group for each project, with its past sessions, the ones
    /// with nothing open after the ones with tabs, the most recently used first. With the setting off, every group
    /// loses its past sessions, and one with nothing open leaves.
    /// </summary>
    private void ApplyClaudeProjects()
    {
        var shows = ShowsClaudeProjects;
        var open = OpenSessionIds();
        var now = _services.Time.GetUtcNow();
        var projects = shows
            ? _claudeSessions
                .Where(e => e.ProjectFolder is { } folder && !e.IsConflictCopy && Directory.Exists(folder))
                .GroupBy(e => e.ProjectKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.LastActivity).ToArray(), StringComparer.OrdinalIgnoreCase)
            : [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in Groups.ToArray())
        {
            var key = HistorySessions.ProjectKey(group.Folder);
            if (projects.TryGetValue(key, out var sessions))
            {
                seen.Add(key);
                LayOutProject(group, sessions, open, now);
            }
            else
            {
                group.IsClaudeProject = false;
                group.PastSessions = [];
                group.LastSessionActivity = null;
                if (group.IsEmpty)
                {
                    Groups.Remove(group);
                }
            }
        }
        foreach (var (key, sessions) in projects)
        {
            if (seen.Contains(key))
            {
                continue;
            }
            var group = GroupFor(sessions[0].ProjectFolder!);
            LayOutProject(group, sessions, open, now);
        }
        OrderProjectGroups();
        UpdateGroupLabels();
        OnPropertyChanged(nameof(HasTabs));
    }

    private void LayOutProject(TabGroupViewModel group, HistoryEntry[] sessions, HashSet<string> open, DateTimeOffset now)
    {
        group.IsClaudeProject = true;
        group.LastSessionActivity = sessions[0].LastActivity;
        group.IsPastSessionsExpanded = _services.State.ExpandedPastSessions.Any(f => FolderHistory.SamePath(f, group.Folder));
        var rows = sessions.Where(e => !open.Contains(e.SessionId)).Select(e => new PastSessionRow(e, PastSessionDetail(e, now))).ToArray();
        // Replaced only when they change, so the rows aren't built again on every tab opened elsewhere.
        if (!rows.SequenceEqual(group.PastSessions))
        {
            group.PastSessions = rows;
        }
    }

    /// <summary>The line under a past session's name: when it was last used, and how many messages it has.</summary>
    internal static string PastSessionDetail(HistoryEntry entry, DateTimeOffset now)
    {
        var parts = new List<string> { Formats.Ago(now - entry.LastActivity) };
        if (entry.MessageCount > 0)
        {
            parts.Add($"{entry.MessageCount} message{(entry.MessageCount == 1 ? "" : "s")}");
        }
        if (entry.Machine != "This machine")
        {
            parts.Add(entry.Machine);
        }
        return string.Join(" · ", parts);
    }

    /// <summary>The groups with nothing open go after the rest, the most recently used first; the rest keep their order.</summary>
    private void OrderProjectGroups()
    {
        var ordered = Groups.Where(g => !g.IsProjectOnly).Concat(Groups.Where(g => g.IsProjectOnly).OrderByDescending(g => g.LastSessionActivity)).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var from = Groups.IndexOf(ordered[i]);
            if (from != i)
            {
                Groups.Move(from, i);
            }
        }
    }

    // ---- The offer ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The first time, with the setting never chosen: reads Claude Code's sessions, and offers when they're in folders
    /// that exist here. Made only once, however it's answered.
    /// </summary>
    private async Task OfferClaudeProjectsAsync()
    {
        IReadOnlyList<HistoryEntry> sessions;
        try
        {
            sessions = await HistorySessions.ReadAsync(_services, OpenSessionIds());
        }
        catch (Exception)
        {
            return;
        }
        if (_services.State.ClaudeProjectsOffered || ShowsClaudeProjects)
        {
            return;
        }
        var folders = sessions.Where(e => e.ProjectFolder is { } folder && !e.IsConflictCopy && Directory.Exists(folder))
            .Select(e => e.ProjectKey).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (folders > 0)
        {
            ClaudeProjectsOffer = new ClaudeProjectsOffer(folders);
        }
    }

    /// <summary><b>Show them in the sidebar</b>: turns the setting on, which brings the groups.</summary>
    [RelayCommand]
    private void AcceptClaudeProjects()
    {
        ClaudeProjectsOffer = null;
        _services.State.ClaudeProjectsOffered = true;
        _services.SaveState();
        _services.Settings.Sidebar.ShowClaudeProjects = true;
        _services.SaveSettings();
    }

    /// <summary><b>Not now</b>: the setting stays off, and Settings → Tabs has it.</summary>
    [RelayCommand]
    private void DeclineClaudeProjects()
    {
        ClaudeProjectsOffer = null;
        _services.State.ClaudeProjectsOffered = true;
        _services.SaveState();
    }

    // ---- The rows ----------------------------------------------------------------------------------------------

    /// <summary>Unfolds a group's past sessions, or folds them away; remembered for the folder on this machine.</summary>
    [RelayCommand]
    private void TogglePastSessions(TabGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }
        group.IsPastSessionsExpanded = !group.IsPastSessionsExpanded;
        var expanded = _services.State.ExpandedPastSessions;
        expanded.RemoveAll(f => FolderHistory.SamePath(f, group.Folder));
        if (group.IsPastSessionsExpanded)
        {
            expanded.Add(group.Folder);
        }
        _services.SaveState();
    }

    /// <summary>Resumes a past session in a new tab in its group, as History does (<see cref="SessionOpener"/>).</summary>
    [RelayCommand]
    private Task OpenPastSessionAsync(PastSessionRow? row) => row is null ? Task.CompletedTask : _opener.OpenAsync(row.Entry);
}
