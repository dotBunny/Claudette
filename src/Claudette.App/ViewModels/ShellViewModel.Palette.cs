using Claudette.App.Games;
using Claudette.App.Services;
using Claudette.Core;
using Claudette.Core.Accessibility;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// Moving around by keyboard (DESIGN.md §4, "Command palette"): the command palette, and going to the next tab
/// waiting for you.
/// </summary>
public sealed partial class ShellViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaletteOpen))]
    public partial CommandPaletteViewModel? Palette { get; set; }

    public bool IsPaletteOpen => Palette is not null;

    /// <summary>Ctrl/Cmd+Shift+P: everything the palette can do, as things are now.</summary>
    [RelayCommand]
    private void OpenPalette()
    {
        Picker = null;
        History = null;
        Palette = new CommandPaletteViewModel(PaletteEntries(), () => Palette = null);
    }

    public void ClosePalette() => Palette = null;

    /// <summary>
    /// Ctrl/Cmd+J: the next tab, in sidebar order after the selected one, that's waiting for you (a permission prompt,
    /// a question or a plan). Nothing happens when no other tab is.
    /// </summary>
    [RelayCommand]
    private void SelectNextNeedingInput()
    {
        var tabs = AllTabs.ToList();
        var start = SelectedTab is null ? -1 : tabs.IndexOf(SelectedTab);
        for (var step = 1; step <= tabs.Count; step++)
        {
            var tab = tabs[(start + step + tabs.Count) % tabs.Count];
            if (tab.NeedsInput && !ReferenceEquals(tab, SelectedTab))
            {
                SelectedTab = tab;
                return;
            }
        }
    }

    /// <summary>
    /// Commands first (the selected tab's among them), then the open tabs, recent and favorite folders, Settings'
    /// categories, and the help. A command shows its shortcut, so the palette teaches them.
    /// </summary>
    internal List<PaletteEntry> PaletteEntries()
    {
        var entries = new List<PaletteEntry>();
        void Command(string label, Action run, string? shortcutId = null, string? detail = null) =>
            entries.Add(new PaletteEntry(label, "Command", () =>
            {
                run();
                return Task.CompletedTask;
            }, shortcutId is null ? null : Tips.Text(shortcutId), detail));
        void AsyncCommand(string label, Func<Task> run, string? shortcutId = null) =>
            entries.Add(new PaletteEntry(label, "Command", run, shortcutId is null ? null : Tips.Text(shortcutId)));

        Command("New tab", NewTab, KeyboardShortcuts.NewTab);
        Command("Open from History…", OpenHistory, KeyboardShortcuts.History);
        AsyncCommand("Settings", OpenSettingsAsync, KeyboardShortcuts.Settings);
        Command(Layout.IsSidebarCollapsed ? "Expand the sidebar" : "Collapse the sidebar", () => Layout.ToggleSidebarCommand.Execute(null), KeyboardShortcuts.ToggleSidebar);
        var zoom = _services.Settings.Appearance.Zoom;
        Command("Zoom in", ZoomIn, KeyboardShortcuts.ZoomIn, $"{zoom}%");
        Command("Zoom out", ZoomOut, KeyboardShortcuts.ZoomOut, $"{zoom}%");
        if (zoom != Zoom.Default)
        {
            Command("Reset zoom", ResetZoom, KeyboardShortcuts.ResetZoom, $"{zoom}%");
        }
        if (AllTabs.Any(t => t.NeedsInput && !ReferenceEquals(t, SelectedTab)))
        {
            Command("Go to the next tab waiting for you", SelectNextNeedingInput, KeyboardShortcuts.NextTabNeedingInput);
        }
        // DESIGN.md §18, "Project files".
        if (Groups.Count > 0)
        {
            Command("Go to file…", GoToFile, KeyboardShortcuts.GoToFile);
            // DESIGN.md §18, "Finding in files".
            Command("Find in files…", OpenFindInFiles, KeyboardShortcuts.FindInFiles);
            Command("Replace in files…", OpenReplaceInFiles, KeyboardShortcuts.ReplaceInFiles);
        }
        if (SelectedFiles is { } files)
        {
            if (files.Showing is { IsModified: true })
            {
                AsyncCommand("Save the file", files.SaveAsync);
            }
            if (files.HasUnsaved)
            {
                AsyncCommand("Save all files", files.SaveAllAsync);
            }
            if (files.Showing is { } showing)
            {
                AsyncCommand("Close the file", () => files.CloseShowingOrTabAsync(), KeyboardShortcuts.CloseTab);
                Command("Go to line…", showing.RequestGoToLine);
                // DESIGN.md §18, "Markdown preview": the modes that aren't showing.
                if (showing.IsMarkdown)
                {
                    if (!showing.IsPreviewMode)
                    {
                        Command("Preview the file", () => showing.IsPreviewMode = true);
                    }
                    if (!showing.IsEditAndPreviewMode)
                    {
                        Command("Edit and preview the file", () => showing.IsEditAndPreviewMode = true);
                    }
                    if (!showing.IsEditMode)
                    {
                        Command("Edit the file", () => showing.IsEditMode = true);
                    }
                }
            }
            Command(files.ShowOnlyChanged ? "Show every file in the explorer" : "Show only changed files", () => files.ShowOnlyChanged = !files.ShowOnlyChanged);
            Command(files.ToggleExplorerText, () => files.ToggleExplorerCommand.Execute(null));
            if (IsSplit)
            {
                Command("Close this pane", () => ClosePane(files));
                Command("Close the other panes", CloseOtherPanes);
            }
            AsyncCommand("Close project files", () => CloseProjectFilesAsync(files));
        }
        if (SelectedTab is { } tab)
        {
            Command("Find in the conversation", () => tab.OpenFindCommand.Execute(null), KeyboardShortcuts.Find);
            // DESIGN.md §18, "Conversation cards": the messages, by the keys or from here.
            Command("Go to the first message", () => tab.JumpToMessage(MessageJump.First), KeyboardShortcuts.FirstMessage);
            Command("Go to the latest message", () => tab.JumpToMessage(MessageJump.Latest), KeyboardShortcuts.LatestMessage);
            // DESIGN.md §18, "Split panes".
            if (IsSplit)
            {
                Command("Close this pane", () => ClosePane(tab));
                Command("Close the other panes", CloseOtherPanes);
            }
            if (tab.HasSubThreads || tab.ThreadHead is { HasSubThreads: true })
            {
                Command("Show the thread with its sub-threads", () => ShowThreadSideBySide(tab));
            }
            if (tab.StopCommand.CanExecute(null))
            {
                Command("Stop Claude", () => tab.StopCommand.Execute(null), KeyboardShortcuts.Stop);
            }
            if (tab.ProjectTools.MainAction is { } main)
            {
                Command(main.Label, () => tab.ProjectTools.RunMainActionCommand.Execute(null), KeyboardShortcuts.RunProjectAction, tab.ProjectTools.Project?.Name);
            }
            // DESIGN.md §5, "Drafts and the stash".
            if (tab.Draft is { IsEmpty: false })
            {
                Command("Stash the message", () => tab.StashCommand.Execute(null), KeyboardShortcuts.Stash);
            }
            // The tool windows that have something to show (DESIGN.md §3, "Tool windows").
            entries.AddRange(ToolWindowEntries());
            if (Groups.FirstOrDefault(g => g.Tabs.Contains(tab)) is { } group)
            {
                Command("Open project files", () => OpenProjectFiles(group));
            }
            // DESIGN.md §18, "Games with Claudette".
            if (_services.Games.IsEnabled)
            {
                Command("Play tic-tac-toe with Claudette", () => tab.PlayGame(GameKind.TicTacToe));
                Command("Play Connect Four with Claudette", () => tab.PlayGame(GameKind.ConnectFour));
                Command("Play hangman with Claudette", () => tab.PlayGame(GameKind.Hangman));
            }
            Command("Tab settings…", () => OpenTabSettingsCommand.Execute(tab));
            // DESIGN.md §4, "Sidebar": what's under its row.
            if (tab.HasRowsUnder)
            {
                Command(tab.CollapseMenuText, () => tab.ToggleRowsCollapsedCommand.Execute(null));
            }
            AsyncCommand("Duplicate tab", () => DuplicateTabAsync(tab));
            if (CanOpenWorktreeTab(tab))
            {
                AsyncCommand("New tab in a worktree", () => OpenWorktreeTabAsync(tab.GroupFolder));
            }
            AsyncCommand("Export conversation…", () => tab.ExportConversationCommand.ExecuteAsync(null));
            // DESIGN.md §18, "Threads".
            if (tab.CanMakeThread)
            {
                Command("Make this tab a thread", () => MakeThread(tab));
            }
            if (tab.IsThread)
            {
                AsyncCommand("New sub-thread", () => NewSubThreadAsync(tab));
                if (CanOpenWorktreeTab(tab))
                {
                    AsyncCommand("New sub-thread in a worktree", () => NewSubThreadInWorktreeAsync(tab));
                }
                Command(tab.AskBeforeSendingToSubThreads ? "Stop asking before sending to sub-threads" : "Ask before sending to sub-threads",
                    () => tab.ToggleAskBeforeSendingToSubThreadsCommand.Execute(null));
                Command("Stop being a thread", () => StopBeingThread(tab));
            }
            foreach (var thread in ThreadsFor(tab))
            {
                Command($"Assign to thread: {thread.DisplayName}", () => AssignToThread(tab, thread));
            }
            if (tab.IsSubThread)
            {
                Command("Go to thread", () => GoToThread(tab));
                Command("Detach from thread", () => DetachFromThread(tab));
            }
            if (tab.RestartCommand.CanExecute(null))
            {
                AsyncCommand("Restart Claude Code", () => tab.RestartCommand.ExecuteAsync(null));
            }
            AsyncCommand("Close tab", () => CloseTabAsync(tab), KeyboardShortcuts.CloseTab);
        }

        foreach (var open in AllTabs)
        {
            entries.Add(new PaletteEntry(open.DisplayName, "Tab", () =>
            {
                SelectedTab = open;
                return Task.CompletedTask;
            }, Detail: open.FolderName));
        }
        foreach (var openFiles in Groups.Select(g => g.Files).OfType<ProjectFilesViewModel>())
        {
            entries.Add(new PaletteEntry(openFiles.DisplayName, "Tab", () =>
            {
                Selected = openFiles;
                return Task.CompletedTask;
            }, Detail: openFiles.Group.Label));
        }

        var state = _services.State;
        foreach (var folder in state.FavoriteFolders.Concat(state.RecentFolders.Select(r => r.Path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(new PaletteEntry(Formats.FolderName(folder),
                "Folder", () => OpenFolderAsync(folder), Detail: folder));
        }

        foreach (var category in SettingsSidebar.Categories(_services.ProjectTools.Detector.Types))
        {
            entries.Add(new PaletteEntry(category, "Settings", () => OpenSettingsAtAsync(category)));
        }

        // DESIGN.md §14, "Version": the help at the foot of Settings' sidebar.
        entries.Add(new PaletteEntry("User Guide", "Help", () => HelpLinks.OpenGuideAsync(_services)));
        entries.Add(new PaletteEntry("Ask on Discord", "Help", () => HelpLinks.OpenDiscordAsync(_services)));
        entries.Add(new PaletteEntry("Report an issue", "Help", () => HelpLinks.ReportIssueAsync(_services)));
        entries.Add(new PaletteEntry("About Claudette", "Help", OpenAboutAsync));
        return entries;
    }
}
