using Claudette.App.Services;
using Claudette.Core;
using Claudette.Core.Git;
using Claudette.Core.History;
using Claudette.Core.Library;
using Claudette.Core.Settings;

namespace Claudette.App.ViewModels;

/// <summary>
/// The past sessions History lists (DESIGN.md §9, "History"), read for it and for the sidebar's Claude Code projects
/// (DESIGN.md §4, "Claude Code's projects"): Claude Code's own on this machine, merged with the session library's
/// records, off the UI thread.
/// </summary>
internal static class HistorySessions
{
    /// <summary>
    /// Reads every session, newest first. With <paramref name="found"/>, each of Claude Code's own is handed over on the
    /// reading thread as the scan finds it, so a list can show them before the rest is read.
    /// </summary>
    /// <param name="open">The sessions open in tabs, which the entries say.</param>
    public static async Task<IReadOnlyList<HistoryEntry>> ReadAsync(AppServices services, HashSet<string> open, Action<HistoryEntry>? found = null)
    {
        var library = services.Library;
        var index = library.History;
        var context = new EntryContext(library.MachineName, services.Time.GetUtcNow(), open,
            // A copy: the list is built off the UI thread, which owns the state.
            new Dictionary<string, SessionMark>(services.State.SessionMarks));
        var sessionLibrary = library.Library;
        return await Task.Run(async () =>
        {
            // Before the scan: the records name the sessions it finds, and say when another machine used one since.
            var stored = ListLibrary(sessionLibrary);
            var records = Records(stored);
            Action<SessionSummary>? each = found is null
                ? null
                : summary => found(LocalEntry(summary, records.GetValueOrDefault(summary.SessionId), context));
            var local = index is null ? [] : await index.ScanAsync(each).ConfigureAwait(false);
            // Sessions only in the library are summarized from its copies, cached as Claude Code's own are.
            var localIds = local.Select(s => s.SessionId).ToHashSet(StringComparer.Ordinal);
            var libraryOnly = stored.Where(e => e.IsConflictCopy || !localIds.Contains(e.Record.SessionId)).Select(e => e.TranscriptPath).ToArray();
            IReadOnlyDictionary<string, SessionSummary?> summaries = index is null
                ? libraryOnly.ToDictionary(p => p, HistoryIndex.ReadSummary, StringComparer.Ordinal)
                : await index.SummarizeAsync(libraryOnly).ConfigureAwait(false);
            return (IReadOnlyList<HistoryEntry>)Merge(local, stored, records, summaries, context);
        });
    }

    /// <summary>
    /// Claude Code's projects (DESIGN.md §4, "Claude Code's projects"): the sessions History lists, by project, newest
    /// first, for the projects whose folders are still here. Each folder is checked once, off the UI thread, as a network
    /// drive that's gone can take seconds to say so; the sidebar lays its groups out from the result without touching the disk.
    /// </summary>
    /// <param name="open">The sessions open in tabs, which the entries say.</param>
    public static async Task<IReadOnlyDictionary<string, HistoryEntry[]>> ReadProjectsAsync(AppServices services, HashSet<string> open)
    {
        var sessions = await ReadAsync(services, open);
        return await Task.Run(() => Projects(sessions, Directory.Exists));
    }

    /// <summary>The sessions by project, newest first, leaving out conflict copies and the projects whose folder <paramref name="exists"/> says is gone.</summary>
    internal static IReadOnlyDictionary<string, HistoryEntry[]> Projects(IEnumerable<HistoryEntry> sessions, Func<string, bool> exists)
    {
        var projects = new Dictionary<string, HistoryEntry[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in sessions.Where(e => e.ProjectFolder is not null && !e.IsConflictCopy).GroupBy(e => e.ProjectKey, StringComparer.OrdinalIgnoreCase))
        {
            var entries = project.OrderByDescending(e => e.LastActivity).ToArray();
            if (exists(entries[0].ProjectFolder!))
            {
                projects[project.Key] = entries;
            }
        }
        return projects;
    }

    /// <summary>The project a folder is in: the folder itself, or for a worktree Claude Code made, its main checkout; normalized.</summary>
    public static string ProjectKey(string? folder) =>
        folder is null ? "" : FolderHistory.Normalize(GitWorktrees.MainCheckoutOf(folder) ?? folder);

    public static string ProjectLabel(string? folder) => folder is null ? "Unknown folder" : Formats.FolderName(folder);

    internal static IReadOnlyList<LibraryEntry> ListLibrary(SessionLibrary library)
    {
        try
        {
            return library.List();
        }
        catch (Exception)
        {
            return [];
        }
    }


    internal static List<HistoryEntry> Merge(IReadOnlyList<SessionSummary> local, IReadOnlyList<LibraryEntry> stored, Dictionary<string, LibraryEntry> records,
        IReadOnlyDictionary<string, SessionSummary?> librarySummaries, EntryContext context)
    {
        var entries = local.Select(s => LocalEntry(s, records.GetValueOrDefault(s.SessionId), context)).ToList();
        var seen = local.Select(s => s.SessionId).ToHashSet();
        foreach (var libraryEntry in stored)
        {
            if (!libraryEntry.IsConflictCopy && seen.Contains(libraryEntry.Record.SessionId))
            {
                continue;
            }
            entries.Add(LibraryOnlyEntry(libraryEntry, librarySummaries.GetValueOrDefault(libraryEntry.TranscriptPath), context));
        }
        return entries.OrderByDescending(e => e.LastActivity).ToList();
    }

    /// <summary>What a load's entries are made with.</summary>
    internal sealed record EntryContext(string Machine, DateTimeOffset Now, HashSet<string> Open, Dictionary<string, SessionMark> Marks);

    /// <summary>The library's entries by session id, leaving out conflict copies.</summary>
    internal static Dictionary<string, LibraryEntry> Records(IReadOnlyList<LibraryEntry> stored) =>
        stored.Where(e => !e.IsConflictCopy).GroupBy(e => e.Record.SessionId).ToDictionary(g => g.Key, g => g.First());

    /// <summary>A session in Claude Code's storage on this machine, with its library entry if it has one.</summary>
    internal static HistoryEntry LocalEntry(SessionSummary summary, LibraryEntry? libraryEntry, EntryContext context)
    {
        var record = libraryEntry?.Record;
        var lastUsedElsewhere = record is not null && record.Machine != context.Machine && record.LastUsed > summary.LastActivity;
        return Entry(summary.SessionId, record?.Name ?? summary.Title, summary.FirstPrompt ?? record?.FirstPrompt, summary.Folder ?? record?.Folder,
            lastUsedElsewhere ? record!.Machine : context.Machine, lastUsedElsewhere ? record!.LastUsed : summary.LastActivity,
            summary.MessageCount, summary.GitBranch, summary.TranscriptPath, record, libraryEntry?.TranscriptPath, isConflict: false, label: null,
            context, continuedElsewhere: lastUsedElsewhere, prompts: summary.Prompts, librarySize: libraryEntry?.Size);
    }

    /// <summary>A session only in the library, from another machine (or older than Claude Code's own cleanup).</summary>
    internal static HistoryEntry LibraryOnlyEntry(LibraryEntry libraryEntry, SessionSummary? summary, EntryContext context)
    {
        var record = libraryEntry.Record;
        var title = record.Name ?? summary?.Title ?? summary?.FirstPrompt ?? "Untitled session";
        return Entry(record.SessionId, libraryEntry.IsConflictCopy ? $"{title} ({libraryEntry.ConflictLabel})" : title,
            summary?.FirstPrompt ?? record.FirstPrompt, record.Folder, record.Machine, record.LastUsed,
            summary?.MessageCount ?? 0, record.Project?.Branch, localTranscript: null, record, libraryEntry.TranscriptPath,
            libraryEntry.IsConflictCopy, libraryEntry.ConflictLabel, context, prompts: summary?.Prompts, librarySize: libraryEntry.Size);
    }

    /// <param name="librarySize">What the session takes in the session library, for one that's there (DESIGN.md §9, "History").</param>
    internal static HistoryEntry Entry(string id, string? title, string? prompt, string? folder, string lastMachine, DateTimeOffset last, int messages, string? branch,
        string? localTranscript, SessionRecord? record, string? libraryTranscript, bool isConflict, string? label, EntryContext context,
        bool continuedElsewhere = false, string? prompts = null, long? librarySize = null)
    {
        var who = lastMachine == context.Machine ? "This machine" : lastMachine;
        var details = new List<string> { who, Formats.Ago(context.Now - last) };
        if (messages > 0)
        {
            details.Add($"{messages} message{(messages == 1 ? "" : "s")}");
        }
        // A worktree Claude Code made is listed under its repository, so it says which; its own branch goes without saying.
        var worktree = folder is not null && GitWorktrees.MainCheckoutOf(folder) is not null ? Formats.FolderName(folder) : null;
        if (branch is not null && (worktree is null || branch != GitWorktrees.BranchPrefix + worktree))
        {
            details.Add(branch);
        }
        if (worktree is not null)
        {
            details.Add($"worktree {worktree}");
        }
        if (librarySize is { } size)
        {
            details.Add($"{Formats.Size(size)} in the library");
        }
        if (context.Open.Contains(id))
        {
            details.Add("open in a tab");
        }
        return new HistoryEntry
        {
            SessionId = id,
            Title = title is { Length: > 0 } ? title : prompt ?? "Untitled session",
            FirstPrompt = prompt,
            Prompts = prompts,
            Folder = folder,
            ProjectFolder = folder is null ? null : GitWorktrees.MainCheckoutOf(folder) ?? folder,
            ProjectKey = ProjectKey(folder),
            Machine = who,
            LastActivity = last,
            MessageCount = messages,
            Branch = branch,
            LocalTranscript = localTranscript,
            ContinuedElsewhere = continuedElsewhere,
            Record = record,
            LibraryTranscript = libraryTranscript,
            IsConflictCopy = isConflict,
            ConflictLabel = label,
            IsOpen = context.Open.Contains(id),
            MarkKey = TabMarks.ForSession(record, context.Marks.GetValueOrDefault(id)),
            Details = string.Join(" · ", details),
        };
    }
}
