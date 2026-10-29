using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Claudette.App.Controls;
using Claudette.App.Conversation;
using Claudette.App.Diffs;
using Claudette.App.Services;
using Claudette.App.ViewModels;
using Claudette.Core.Settings;

namespace Claudette.App.Views;

/// <summary>
/// One tab: the conversation, find and the prompts over it. The composer is <see cref="ComposerView"/>; their styles are <c>Themes/ConversationStyles.axaml</c>.
/// </summary>
public partial class TabView : UserControl
{
    /// <summary>
    /// One of the tab's lists: the conversation, or the work of a subagent the tab shows in its place (DESIGN.md §5,
    /// "Subagents"). Each follows new output and keeps its own place, so going back to the conversation finds it as it was.
    /// </summary>
    private sealed class ShownList(ScrollViewer scroll, ItemsControl items, ScrollMarks marks)
    {
        public ScrollViewer Scroll => scroll;

        public ItemsControl Items => items;

        public ScrollMarks Marks => marks;

        public bool StickToBottom { get; set; } = true;

        /// <summary>
        /// The reader is scrolling up (the wheel, a scroll bar, a key, a swipe): the next move up is theirs, even when the
        /// content grows in the same layout pass. Without it, a move up is the list correcting its estimate of the
        /// messages above, which keeps its place at the bottom.
        /// </summary>
        public bool ReaderScrolling { get; set; }

        /// <summary>The list's panel, once it has items: where each one is.</summary>
        public ConversationPanel? Panel { get; set; }
    }

    /// <summary>The most of the conversation's height the pinned to-do list's tasks take before they scroll.</summary>
    internal const double PinnedTodoShare = 0.4;

    private readonly ShownList _main;
    private readonly ShownList _agent;

    /// <summary>The list on screen: a subagent's work while the tab shows one, else the conversation.</summary>
    private ShownList Active => _tab?.IsViewingAgent == true ? _agent : _main;

    /// <summary>Where a drag of the side panel's edge started, and the width then; null when not resizing.</summary>

    public TabView()
    {
        InitializeComponent();
        // Tunnel, so the prompt's shortcuts are seen before the composer turns Enter into a new line.
        AddHandler(KeyDownEvent, OnPromptKeyDown, RoutingStrategies.Tunnel);
        // With tabs side by side, a click or the focus in a pane makes it the focused one (DESIGN.md §18, "Split panes").
        AddHandler(PointerPressedEvent, (_, _) => ViewModel?.FocusPane(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, (_, _) => ViewModel?.FocusPane(), RoutingStrategies.Bubble, handledEventsToo: true);
        _main = new ShownList(ConversationScroll, ConversationItems, ScrollMarks);
        _agent = new ShownList(AgentScroll, AgentItems, AgentMarks);
        ComposerPanel.Sent += () => _main.StickToBottom = true;
        // Claudette on the composer keeps clear of Jump to latest, and ducks while a thread's strip or a dialog sits over
        // the composer (DESIGN.md §5).
        ComposerPanel.KeepMascotClearOf([JumpToLatest, ThreadStrip, .. ConversationArea.Children.OfType<Border>().Where(b => b.Classes.Contains("overlay"))]);
        foreach (var list in new[] { _main, _agent })
        {
            var scroll = list.Scroll;
            scroll.ScrollChanged += (_, e) => OnConversationScrollChanged(list, e);
            // Seen even when the scroll viewer handles them.
            scroll.AddHandler(PointerWheelChangedEvent, (_, e) => list.ReaderScrolling |= e.Delta.Y > 0, RoutingStrategies.Tunnel, handledEventsToo: true);
            scroll.AddHandler(PointerPressedEvent, (_, e) => list.ReaderScrolling |= IsOnScrollBar(e.Source), RoutingStrategies.Tunnel, handledEventsToo: true);
            scroll.AddHandler(PointerMovedEvent, (_, e) => list.ReaderScrolling |= IsOnScrollBar(e.Source) && e.GetCurrentPoint(scroll).Properties.IsLeftButtonPressed,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            scroll.AddHandler(KeyDownEvent, (_, e) => list.ReaderScrolling |= e.Key is Key.Up or Key.PageUp or Key.Home, RoutingStrategies.Tunnel, handledEventsToo: true);
            scroll.AddHandler(InputElement.ScrollGestureEvent, (_, e) => list.ReaderScrolling |= e.Delta.Y < 0, RoutingStrategies.Tunnel, handledEventsToo: true);
            list.Items.ContainerPrepared += (_, e) => OnConversationContainerPrepared(list, e);
            // Opening or folding a chain's steps keeps what was clicked where it is (DESIGN.md §18). Before the button's
            // command runs, so the place is taken before anything grows.
            scroll.AddHandler(Button.ClickEvent, (_, e) => KeepInPlace(list, e.Source), RoutingStrategies.Bubble, handledEventsToo: true);
            list.Marks.MarkClicked += mark => ScrollItemToTop(list, mark.Index);
        }
        ActualThemeVariantChanged += (_, _) => UpdateMarks(Active);
        // Copy on a code block goes through the tab and says "Copied" (DESIGN.md §5, "Copy and times").
        CodeBlockCopy.Attach(this);
        // Add to scratch pad: a code block's button, and the menu of text selected in the conversation (DESIGN.md §18).
        ScratchPadAdd.Attach(this, ConversationLists);
        // An open to-do list takes at most two fifths of the conversation's height and scrolls past that (DESIGN.md §5, "Tasks").
        ConversationArea.SizeChanged += (_, e) => PinnedTodoTasks.MaxHeight = e.NewSize.Height * PinnedTodoShare;
    }

    private TabViewModel? ViewModel => DataContext as TabViewModel;

    /// <summary>The Perforce password prompt opened: type straight into it.</summary>
    private void OnPerforcePasswordAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox box)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => box.Focus());
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (ViewModel is not { } tab)
        {
            return;
        }
        if (Shortcuts.Matches(tab.Keyboard, KeyboardShortcuts.Stop, e.Key, e.KeyModifiers) && tab.StopCommand.CanExecute(null))
        {
            tab.StopCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && tab.IsViewingAgent)
        {
            // Back from a subagent's work to the conversation (DESIGN.md §5, "Subagents").
            tab.ShowConversationCommand.Execute(null);
            e.Handled = true;
        }
        else if (Shortcuts.Matches(tab.Keyboard, KeyboardShortcuts.Suffixes, e.Key, e.KeyModifiers) && !tab.IsViewingAgent)
        {
            ComposerPanel.ShowSuffixes();
            e.Handled = true;
        }
        else if (Shortcuts.Matches(tab.Keyboard, KeyboardShortcuts.Stash, e.Key, e.KeyModifiers))
        {
            // DESIGN.md §5, "Drafts and the stash".
            tab.StashCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Ctrl/Cmd+Enter answers "yes" to the waiting prompt, or sends an MCP server's form, and Ctrl/Cmd+Backspace "no"
    /// (DESIGN.md §7), or whatever Settings → Keyboard says. Not while typing in one of a prompt's own fields, and
    /// Backspace keeps deleting words in a field with text.
    /// </summary>
    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { HasKeyboardAnswer: true } tab)
        {
            return;
        }
        var allow = Shortcuts.Matches(tab.Keyboard, KeyboardShortcuts.AllowPrompt, e.Key, e.KeyModifiers);
        if (!allow && !Shortcuts.Matches(tab.Keyboard, KeyboardShortcuts.DenyPrompt, e.Key, e.KeyModifiers))
        {
            return;
        }
        var focusedBox = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as TextBox;
        if (focusedBox is not null && !ComposerPanel.HasFocus(focusedBox)
            || e.Key == Key.Back && !string.IsNullOrEmpty(focusedBox?.Text))
        {
            return;
        }
        // With Ctrl/Cmd+Enter as the send key (DESIGN.md §14), it sends what's typed in the composer; with nothing typed it
        // answers the prompt.
        if (allow && e.Key == Key.Enter && tab.Keyboard.SendKey == SendKey.PrimaryEnter && ComposerPanel.HasFocus(focusedBox) && tab.SendCommand.CanExecute(null))
        {
            return;
        }
        e.Handled = allow ? tab.AcceptWaitingPrompt() : tab.DeclineWaitingPrompt();
    }

    /// <summary>Follows new output unless the user has scrolled up; then offers "Jump to latest" (DESIGN.md §5).</summary>
    private void OnConversationScrollChanged(ShownList list, ScrollChangedEventArgs e)
    {
        var scroll = list.Scroll;
        var distanceFromBottom = scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y;
        (var stick, var scrollToEnd) = FollowOutput.Update(list.StickToBottom, list.ReaderScrolling, e.OffsetDelta.Y, e.ExtentDelta.Y, distanceFromBottom);
        list.StickToBottom = stick;
        list.ReaderScrolling = false;
        if (scrollToEnd)
        {
            ScrollToEnd(list);
        }
        if (ReferenceEquals(list, Active))
        {
            JumpToLatest.IsVisible = !list.StickToBottom && distanceFromBottom > FollowOutput.Threshold;
        }
        // The panel's arrange places the marks before the scroll viewer takes in its new height: they follow that too.
        if (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0)
        {
            UpdateMarks(list);
        }
    }

    /// <summary>
    /// A click that opens or folds a chain's steps (a reply's <i>7 steps · 18m 43s</i>, a step's line, a chain's own line):
    /// the reader is reading, so the list stops following new output, and once the steps are laid out the list scrolls by
    /// however far the button moved, so it stays where it was clicked rather than jumping up or down.
    /// </summary>
    private static void KeepInPlace(ShownList list, object? source)
    {
        if (source is not Visual visual || visual.FindAncestorOfType<Button>(includeSelf: true) is not { } button
            || !(button.Classes.Contains("stepspill") || button.Classes.Contains("steprow") || button.FindAncestorOfType<Card>() is { } card && card.Classes.Contains("steps")))
        {
            return;
        }
        var scroll = list.Scroll;
        if (button.TranslatePoint(default, scroll) is not { } before)
        {
            return;
        }
        list.StickToBottom = false;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (button.IsAttachedToVisualTree() && button.TranslatePoint(default, scroll) is { } after && Math.Abs(after.Y - before.Y) > 0.5)
            {
                scroll.Offset = new Vector(scroll.Offset.X, Math.Clamp(scroll.Offset.Y + after.Y - before.Y, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private static bool IsOnScrollBar(object? source) => source is Visual visual && visual.FindAncestorOfType<ScrollBar>(includeSelf: true) is not null;

    private void OnJumpToLatest(object? sender, RoutedEventArgs e)
    {
        var list = Active;
        list.StickToBottom = true;
        ScrollToEnd(list);
        JumpToLatest.IsVisible = false;
    }

    private static void ScrollToEnd(ShownList list)
    {
        var scroll = list.Scroll;
        scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
    }

    /// <summary>
    /// The tab shows a subagent's work or goes back to its conversation (DESIGN.md §5, "Subagents"): a subagent's work
    /// opens at its latest, following it as it goes; the conversation is where it was left.
    /// </summary>
    private void OnShownListChanged()
    {
        if (_tab?.IsViewingAgent == true)
        {
            _agent.StickToBottom = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrollToEnd(_agent), Avalonia.Threading.DispatcherPriority.Loaded);
        }
        var list = Active;
        var scroll = list.Scroll;
        JumpToLatest.IsVisible = !list.StickToBottom && scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y > FollowOutput.Threshold;
        MarkFindCurrent();
        UpdateMarks(list);
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.ViewedAgent))
        {
            OnShownListChanged();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_tab is not null)
        {
            _tab.ChangedFiles.DiffRequested -= OnDiffRequested;
            _tab.ToolWindowRequested -= OnToolWindowRequested;
            _tab.ScrollToRequested -= OnScrollToRequested;
            _tab.MessageJumpRequested -= OnMessageJumpRequested;
            _tab.AgentWindowRequested -= OnAgentWindowRequested;
            _tab.FindFocusRequested -= OnFindFocusRequested;
            _tab.Find.PropertyChanged -= OnFindPropertyChanged;
            _tab.PropertyChanged -= OnTabPropertyChanged;
        }
        _tab = ViewModel;
        if (_tab is not null)
        {
            _tab.ChangedFiles.DiffRequested += OnDiffRequested;
            _tab.ToolWindowRequested += OnToolWindowRequested;
            _tab.ScrollToRequested += OnScrollToRequested;
            _tab.MessageJumpRequested += OnMessageJumpRequested;
            _tab.AgentWindowRequested += OnAgentWindowRequested;
            _tab.FindFocusRequested += OnFindFocusRequested;
            _tab.Find.PropertyChanged += OnFindPropertyChanged;
            _tab.PropertyChanged += OnTabPropertyChanged;
            if (_tab.TakePendingScroll() is { } pending)
            {
                // Once the conversation's cards are laid out.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => OnScrollToRequested(pending), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
        MarkFindCurrent();
    }

    private TabViewModel? _tab;

    // ---- Find in the conversation (DESIGN.md §5, "Find") ---------------------------------------------------------

    /// <summary>The item marked as the current match, whose container has the <c>findcurrent</c> class.</summary>
    private ConversationItem? _findMarked;

    private void OnFindFocusRequested() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        FindBox.Focus();
        FindBox.SelectAll();
    });

    private void OnFindPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConversationSearch.Current) or nameof(ConversationSearch.Query))
        {
            MarkFindCurrent();
        }
    }

    /// <summary>
    /// Marks the current match's container, or the Tool calls card's when the match is one of its calls (whose tile is
    /// open); the conversation is virtualized, so a new container is marked as it's made.
    /// </summary>
    private void MarkFindCurrent()
    {
        foreach (var list in new[] { _main, _agent })
        {
            if (_findMarked is not null && list.Items.ContainerFromItem(_findMarked) is { } old)
            {
                old.Classes.Remove("findcurrent");
            }
        }
        _findMarked = _tab?.Find.Current is { } match ? _tab.TopLevelItemOf(match) ?? match : null;
        if (_findMarked is not null && Active.Items.ContainerFromItem(_findMarked) is { } current)
        {
            current.Classes.Add("findcurrent");
        }
        // The text itself, in that card: its container is looked up each time, since one scrolled away is recycled.
        var marked = _findMarked;
        var shown = Active;
        FindMarks.Show(marked is null ? null : () => shown.Items.ContainerFromItem(marked), _tab?.Find.Query.Trim() ?? "");
    }

    private void OnConversationContainerPrepared(ShownList list, ContainerPreparedEventArgs e)
    {
        e.Container.Classes.Set("findcurrent", _findMarked is not null && ReferenceEquals(list, Active) && ReferenceEquals(list.Items.ItemFromContainer(e.Container), _findMarked));
        if (list.Items.ItemsPanelRoot is ConversationPanel panel && !ReferenceEquals(panel, list.Panel))
        {
            if (list.Panel is not null)
            {
                list.Panel.Arranged -= OnPanelArranged;
            }
            list.Panel = panel;
            list.Panel.Arranged += OnPanelArranged;
        }
    }

    // ---- Marks in the scroll bar, and going from message to message (DESIGN.md §18, "Conversation cards") -------

    private void OnPanelArranged(object? sender, EventArgs e) => UpdateMarks(ReferenceEquals(sender, _agent.Panel) ? _agent : _main);

    /// <summary>Where the panel starts in the scroll viewer's content: below the list's margin.</summary>
    private static double PanelOrigin(ShownList list, ConversationPanel panel) =>
        list.Items.Margin.Top + (panel.TranslatePoint(default, list.Items)?.Y ?? 0);

    /// <summary>The items a list shows: the conversation's, or the subagent's the tab shows.</summary>
    private IReadOnlyList<ConversationItem> ItemsOf(ShownList list) =>
        ReferenceEquals(list, _agent) ? (IReadOnlyList<ConversationItem>?)_tab?.ViewedItems ?? [] : (IReadOnlyList<ConversationItem>?)_tab?.Items ?? [];

    /// <summary>The marks for the items worth finding again, where the panel last laid them out; none while all of it fits.</summary>
    private void UpdateMarks(ShownList list)
    {
        if (list.Panel is not { } panel || _tab is null)
        {
            return;
        }
        var items = ItemsOf(list);
        var scroll = list.Scroll;
        var extent = scroll.Extent.Height;
        List<ScrollMark> marks = [];
        if (extent > scroll.Viewport.Height + 1)
        {
            var origin = PanelOrigin(list, panel);
            var brushes = new Dictionary<LandmarkKind, IBrush>();
            for (var i = 0; i < items.Count; i++)
            {
                if (ConversationLandmarks.KindOf(items[i]) is not { } kind)
                {
                    continue;
                }
                if (!brushes.TryGetValue(kind, out var brush))
                {
                    brush = brushes[kind] = MarkBrush(kind);
                }
                marks.Add(new ScrollMark((origin + panel.TopOf(i)) / extent, brush, i, ConversationLandmarks.Describe(items[i])));
            }
        }
        if (!marks.SequenceEqual(list.Marks.Marks))
        {
            list.Marks.Marks = marks;
        }
    }

    private IBrush MarkBrush(LandmarkKind kind) =>
        this.FindResource(ActualThemeVariant, kind switch
        {
            LandmarkKind.Prompt => "CardYouStripeBrush",
            LandmarkKind.Waiting => "MeterWarningBrush",
            LandmarkKind.Error => "ErrorTextBrush",
            _ => "CardFilesStripeBrush",
        }) as IBrush ?? Brushes.Gray;

    /// <summary>F3, F4 and their Ctrl/Cmd forms: the message before or after the one at the top of the view, the first, the latest.</summary>
    private void OnMessageJumpRequested(MessageJump jump)
    {
        if (jump == MessageJump.Latest)
        {
            OnJumpToLatest(this, new RoutedEventArgs());
            return;
        }
        var list = Active;
        if (list.Panel is not { } panel || _tab is null)
        {
            return;
        }
        var items = ItemsOf(list);
        var target = ConversationLandmarks.First(items);
        if (jump != MessageJump.First)
        {
            var viewTop = list.Scroll.Offset.Y - PanelOrigin(list, panel);
            var at = panel.ItemAt(Math.Max(0, viewTop + 1));
            // A message the top of the view cuts off is the one before: going back starts with its own top.
            target = jump == MessageJump.Previous && at >= 0 && at < items.Count && panel.TopOf(at) < viewTop - 1 && ConversationLandmarks.IsMessage(items[at])
                ? at
                : ConversationLandmarks.Step(items, at, forward: jump == MessageJump.Next);
        }
        if (target >= 0)
        {
            ScrollItemToTop(list, target);
        }
    }

    /// <summary>
    /// Puts an item's top at the top of the view. The panel may only have estimated where it is, so once it's laid out
    /// the view lines it up again.
    /// </summary>
    private static void ScrollItemToTop(ShownList list, int index)
    {
        if (list.Panel is not { } panel)
        {
            return;
        }
        var scroll = list.Scroll;
        void Align(double top)
        {
            // The reader moved, so the conversation stops following new output (DESIGN.md §5).
            list.StickToBottom = false;
            list.ReaderScrolling = true;
            var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            scroll.Offset = new Vector(scroll.Offset.X, Math.Clamp(top - 8, 0, max));
        }
        Align(PanelOrigin(list, panel) + panel.TopOf(index));
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (list.Items.ContainerFromIndex(index) is { } container && container.TranslatePoint(default, list.Items) is { } at)
            {
                Align(list.Items.Margin.Top + at.Y);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Enter goes to the next match, Shift+Enter the one before, Esc closes the bar.</summary>
    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } tab)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.Shift:
                tab.Find.PreviousCommand.Execute(null);
                break;
            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                tab.Find.NextCommand.Execute(null);
                break;
            case Key.Escape when e.KeyModifiers == KeyModifiers.None:
                CloseFind(tab);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnCloseFind(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } tab)
        {
            CloseFind(tab);
        }
    }

    /// <summary>Closes the bar, and the focus goes back to the composer.</summary>
    private void CloseFind(TabViewModel tab)
    {
        tab.Find.CloseCommand.Execute(null);
        ComposerPanel.FocusComposer();
    }

    /// <summary>The built-in diff view, in its own window so it can stay open beside the conversation (DESIGN.md §8).</summary>
    private void OnDiffRequested(DiffSource source)
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var window = new DiffWindow { DataContext = new DiffWindowViewModel(source, dark) };
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    // The tool calls open in windows of their own: opening one again brings its window to the front.
    private readonly Dictionary<ToolUseItem, ToolCallWindow> _toolWindows = [];

    /// <summary>A tool call in a window of its own, which follows it while it runs (DESIGN.md §18, "Conversation cards").</summary>
    private void OnToolWindowRequested(ToolUseItem tool)
    {
        if (_toolWindows.TryGetValue(tool, out var open))
        {
            UnfocusedWindows.Activate(open);
            return;
        }
        if (_tab is not { } tab)
        {
            return;
        }
        var window = new ToolCallWindow { DataContext = tab.ToolWindowFor(tool, ActualThemeVariant == ThemeVariant.Dark) };
        _toolWindows[tool] = window;
        window.Closed += (_, _) => _toolWindows.Remove(tool);
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>
    /// Scrolls to a card, for example the Bash call that started a process, or a subagent's group from the agent map.
    /// A group nested in another only has a container once the groups around it have expanded and been laid out.
    /// </summary>
    private void OnScrollToRequested(ConversationItem item)
    {
        // In whichever list the tab shows: a subagent's work came up first if the item is in it (DESIGN.md §5, "Subagents").
        var list = Active;
        var items = list.Items;
        list.StickToBottom = false;
        // A subagent's card doesn't open in the conversation when its work shows under the tab.
        if (item is ToolUseItem tool && !(item is SubagentItem && ViewModel?.SubagentsInSidebar == true))
        {
            tool.IsExpanded = true;
        }
        if (items.ContainerFromItem(item) is { } container)
        {
            BringTopIntoView(container);
            return;
        }
        // The conversation is virtualized: bring the top-level item that holds it into view first, so it has controls.
        if (ViewModel?.TopLevelItemOf(item) is { } outer)
        {
            items.ScrollIntoView(outer);
            if (ReferenceEquals(outer, item) && items.ContainerFromItem(item) is { } realized)
            {
                BringTopIntoView(realized);
                return;
            }
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (items.ContainerFromItem(item) is { } top)
            {
                BringTopIntoView(top);
            }
            else if (items.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().FirstOrDefault(p => ReferenceEquals(p.Content, item)) is { } nested)
            {
                BringTopIntoView(nested);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>The top of a card, so a long expanded group starts at its header.</summary>
    private static void BringTopIntoView(Control container) =>
        container.BringIntoView(new Rect(0, 0, container.Bounds.Width, Math.Min(container.Bounds.Height, 80)));

    private AgentMapWindow? _agentWindow;

    /// <summary>The agent map in a window of its own, beside the conversation (DESIGN.md §18); one per tab.</summary>
    private void OnAgentWindowRequested()
    {
        if (_agentWindow is not null)
        {
            UnfocusedWindows.Activate(_agentWindow);
            return;
        }
        var window = _agentWindow = new AgentMapWindow { DataContext = DataContext };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_agentWindow, window))
            {
                _agentWindow = null;
            }
        };
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>The tab closed: its agent map window goes with it.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _agentWindow?.Close();
    }
}
