using Claudette.Core.Settings;

namespace Claudette.App.ViewModels.Settings;

/// <summary>Settings → Conversation → Show subagents' work's choices (DESIGN.md §5, "Subagents").</summary>
public sealed record SubagentDisplayChoice(SubagentDisplay Display)
{
    public override string ToString() => Display == SubagentDisplay.InSidebar ? "Under the tab in the sidebar" : "In the conversation";
}

/// <summary>
/// Settings → Conversation (DESIGN.md §14): what the conversation shows of Claude's steps, the working line, messages
/// sent while Claude works, the message box, and what each tab's row in the sidebar shows.
/// </summary>
public sealed class ConversationPage : SettingsPage
{
    public ConversationPage(SettingsContext context) : base(context, SettingsCategory.Conversation)
    {
        Section("steps", "Steps and replies",
        [
            Setting("appearance.quietSteps", "Quieter in-between steps", s => s.Appearance.QuietSteps, (s, v) => s.Appearance.QuietSteps = v,
                "So your prompts, Claude's answers and the files it changed stand out.",
                "Tool calls, thinking, hooks, notes and the replies Claude carried on after are drawn without their card's tint and outline. Pointing at one brings it back.",
                ["cards", "tint", "outline", "quiet"]),
            Setting("appearance.indentYourMessages", "Indent your messages", s => s.Appearance.IndentYourMessages, (s, v) => s.Appearance.IndentYourMessages = v,
                "Your messages stand far in from the left, Claude's replies at the left, and its thinking and tool calls a little in, so you can tell whose turn it is at a glance.",
                keywords: ["cards", "you", "margin", "offset", "indent"]),
            Setting("appearance.collapseToolCalls", "Show tool calls collapsed", s => s.Appearance.CollapseToolCalls, (s, v) => s.Appearance.CollapseToolCalls = v,
                "Each Tool calls card starts as its header line, rather than with its tiles open.",
                "The chevron before a card's icon opens it. Right-click a card for Collapse all tool calls or Expand all tool calls, which changes every card in that tab and the ones to come, as thinking's menu does.",
                ["tools", "tiles", "fold", "expand", "collapse"]),
            Setting("appearance.expandThinking", "Show thinking expanded", s => s.Appearance.ExpandThinking, (s, v) => s.Appearance.ExpandThinking = v,
                "Claude's thinking opens by itself, rather than as a line to click.", keywords: ["reasoning"]),
            Setting("appearance.showSubagents", "Show subagents' work", s => s.Appearance.ShowSubagents, (s, v) => s.Appearance.ShowSubagents = v,
                "Where each subagent's work shows.",
                "Under the tab: each has a row of its own under its tab in the sidebar, and selecting it shows its work in the tab, read-only: Claude Code doesn't let you write to a subagent. In the conversation, each is a card that opens to show what it did. The Agents page lists them either way.",
                ["agents", "sidebar"]),
            Setting("claudeCode.showAllHookRuns", "Show every hook run", s => s.ClaudeCode.ShowAllHookRuns, (s, v) => s.ClaudeCode.ShowAllHookRuns = v,
                "Off, only hooks that fail, or print something while they run, get a row.", keywords: ["hooks"]),
        ]);
        Section("working", "While Claude works",
        [
            Setting("appearance.funWorkingWords", "Show fun words", s => s.Appearance.FunWorkingWords, (s, v) => s.Appearance.FunWorkingWords = v,
                "A twinkling line above the message box, such as “Noodling… 42s · 3.1k tokens”.",
                "Off, it says “Working…”. Verbs added to spinnerVerbs in Claude Code's settings show here too.",
                ["working line", "spinner", "verbs"]),
            Setting("appearance.showToolInWorkingLine", "Show what Claude is doing", s => s.Appearance.ShowToolInWorkingLine, (s, v) => s.Appearance.ShowToolInWorkingLine = v,
                "While a tool runs, the line says what it does, such as “Running dotnet test…”.", keywords: ["working line", "tool"]),
            Setting("general.messagesWhileWorking", "Messages sent while Claude works", s => s.General.MessagesWhileWorking, (s, v) => s.General.MessagesWhileWorking = v,
                more: "A queued message has Send now, to send it into the turn after all, and Cancel. Stop takes queued messages back to the message box.",
                keywords: ["queue", "steer", "interrupt"]),
        ]);
        Section("messageBox", "Message box",
        [
            Setting("appearance.showPromptCacheExpiry", "Show when the prompt cache expires", s => s.Appearance.ShowPromptCacheExpiry, (s, v) => s.Appearance.ShowPromptCacheExpiry = v,
                "A count in the message box's bar, such as “Cache 52m”.",
                "How long your next message can read the conversation from the prompt cache, for much less than reading it all again. It turns amber in the last 3 minutes.",
                ["cache", "cost"]),
            Setting("claudeCode.promptSuggestions", "Suggest my next prompt", s => s.ClaudeCode.PromptSuggestions, (s, v) => s.ClaudeCode.PromptSuggestions = v,
                "After Claude replies, the empty message box shows a guess at what you'll send next.",
                "Tab or → puts it in the box. Each guess is a small extra model call, which counts toward your usage. Prompt suggestions turned off in Claude Code's /config stay off. Applies to tabs started after the change.",
                ["autocomplete", "suggestion"]),
        ]);
        Section("tabRows", "Tab rows",
        [
            Setting("appearance.showContextOnTabs", "Show context", s => s.Appearance.ShowContextOnTabs, (s, v) => s.Appearance.ShowContextOnTabs = v,
                "A ring on each tab that fills with its context window.",
                "It turns amber when Claude Code is about to compact the conversation, and red when it's nearly full.", ["context window", "sidebar", "ring"]),
            Setting("appearance.showTaskProgressOnTabs", "Show task progress", s => s.Appearance.ShowTaskProgressOnTabs, (s, v) => s.Appearance.ShowTaskProgressOnTabs = v,
                "How many of Claude's tasks are done, such as “3/7”.",
                "At the right of the row's second line, and the task Claude is working on in place of the model while it works.", ["tasks", "to-do", "sidebar"]),
        ], description: "What each tab's row in the sidebar shows besides its name. The git branch is on Git.");
    }

    public override string IconKey => "IconSettingsConversation";

    public override string Description => "What the conversation and the sidebar show while Claude works.";

    // ---- Steps and replies ----------------------------------------------------------------------------------------

    /// <summary>Steps on the way are drawn quieter than prompts, answers and changes (DESIGN.md §18, "Conversation cards").</summary>
    public bool QuietSteps
    {
        get => Settings.Appearance.QuietSteps;
        set => Set(value, v => Settings.Appearance.QuietSteps = v);
    }

    /// <summary>Your messages' cards start further in than Claude's (DESIGN.md §18, "Conversation cards").</summary>
    public bool IndentYourMessages
    {
        get => Settings.Appearance.IndentYourMessages;
        set => Set(value, v => Settings.Appearance.IndentYourMessages = v);
    }

    /// <summary>Tool calls cards start folded to their header line (DESIGN.md §18, "Conversation cards").</summary>
    public bool CollapseToolCalls
    {
        get => Settings.Appearance.CollapseToolCalls;
        set => Set(value, v => Settings.Appearance.CollapseToolCalls = v);
    }

    public bool ExpandThinking
    {
        get => Settings.Appearance.ExpandThinking;
        set => Set(value, v => Settings.Appearance.ExpandThinking = v);
    }

    public IReadOnlyList<SubagentDisplayChoice> SubagentDisplayChoices { get; } =
        [new(SubagentDisplay.InSidebar), new(SubagentDisplay.InConversation)];

    /// <summary>Where a subagent's work shows: its row under the tab, or its card in the conversation (DESIGN.md §5, "Subagents").</summary>
    public SubagentDisplayChoice ShowSubagents
    {
        get => SubagentDisplayChoices.FirstOrDefault(c => c.Display == Settings.Appearance.ShowSubagents) ?? SubagentDisplayChoices[0];
        set => Set(value?.Display ?? SubagentDisplay.InSidebar, v => Settings.Appearance.ShowSubagents = v);
    }

    /// <summary><b>Show every hook run</b> in the conversation.</summary>
    public bool ShowAllHookRuns
    {
        get => Settings.ClaudeCode.ShowAllHookRuns;
        set => Set(value, v => Settings.ClaudeCode.ShowAllHookRuns = v);
    }

    // ---- While Claude works (DESIGN.md §5, "Working line", "Queued messages") ----------------------------------

    /// <summary>The working line's twinkling glyph and fun verbs (DESIGN.md §5, "Working line").</summary>
    public bool FunWorkingWords
    {
        get => Settings.Appearance.FunWorkingWords;
        set => Set(value, v => Settings.Appearance.FunWorkingWords = v);
    }

    /// <summary>The working line says what the running tool is doing (DESIGN.md §5, "Working line").</summary>
    public bool ShowToolInWorkingLine
    {
        get => Settings.Appearance.ShowToolInWorkingLine;
        set => Set(value, v => Settings.Appearance.ShowToolInWorkingLine = v);
    }

    /// <summary>Sent at once, for Claude to read at its next step.</summary>
    public bool SteerWhileWorking
    {
        get => Settings.General.MessagesWhileWorking == WhileWorking.Steer;
        set
        {
            if (value)
            {
                SetWhileWorking(WhileWorking.Steer);
            }
        }
    }

    /// <summary>Held until the turn ends, then sent as the next turn.</summary>
    public bool QueueWhileWorking
    {
        get => Settings.General.MessagesWhileWorking == WhileWorking.Queue;
        set
        {
            if (value)
            {
                SetWhileWorking(WhileWorking.Queue);
            }
        }
    }

    private void SetWhileWorking(WhileWorking choice)
    {
        if (Settings.General.MessagesWhileWorking == choice)
        {
            return;
        }
        Settings.General.MessagesWhileWorking = choice;
        OnPropertyChanged(nameof(SteerWhileWorking));
        OnPropertyChanged(nameof(QueueWhileWorking));
        Save();
    }

    // ---- The message box ------------------------------------------------------------------------------------------

    /// <summary>The composer's chip with how long the prompt cache has left (DESIGN.md §6, "Prompt cache").</summary>
    public bool ShowPromptCacheExpiry
    {
        get => Settings.Appearance.ShowPromptCacheExpiry;
        set => Set(value, v => Settings.Appearance.ShowPromptCacheExpiry = v);
    }

    /// <summary><b>Suggest my next prompt</b> (DESIGN.md §5, "Prompt suggestions").</summary>
    public bool PromptSuggestions
    {
        get => Settings.ClaudeCode.PromptSuggestions;
        set => Set(value, v => Settings.ClaudeCode.PromptSuggestions = v);
    }

    // ---- Tab rows (DESIGN.md §4, "Sidebar") ---------------------------------------------------------------------------

    /// <summary>The context ring on each tab's row (DESIGN.md §4, "Sidebar").</summary>
    public bool ShowContextOnTabs
    {
        get => Settings.Appearance.ShowContextOnTabs;
        set => Set(value, v => Settings.Appearance.ShowContextOnTabs = v);
    }

    /// <summary>How far Claude's tasks have got on each tab's row (DESIGN.md §5, "Tasks").</summary>
    public bool ShowTaskProgressOnTabs
    {
        get => Settings.Appearance.ShowTaskProgressOnTabs;
        set => Set(value, v => Settings.Appearance.ShowTaskProgressOnTabs = v);
    }
}
