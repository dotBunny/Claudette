using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Claudette.App.Services;
using Claudette.App.ViewModels.Settings;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Claudette.App.ViewModels;

/// <summary>A setting or section found by the Settings search box (DESIGN.md §14).</summary>
public sealed record SettingsSearchResult(string Category, string Label)
{
    /// <summary>The sidebar group the category is in, for the tab's project pages: "NightOwl".</summary>
    public string? Group { get; init; }

    /// <summary>The section of the page it's in: "Look".</summary>
    public string? Section { get; init; }

    /// <summary>The row or section the window scrolls to and picks out.</summary>
    public string? Key { get; init; }

    /// <summary>Its one line, under its name in the results.</summary>
    public string? Description { get; init; }

    /// <summary>The words it's found by: its name, its line, what's behind More, and other words for it.</summary>
    public IReadOnlyList<string> Words { get; init; } = [];

    /// <summary>Where it is, as the results list says: "Appearance → Look", or "NightOwl → Links".</summary>
    public string Where => Group is not null ? $"{Group} → {Category}" : Section is not null && Section != Label ? $"{Category} → {Section}" : Category;
}

/// <summary>A choice in one of Settings' dropdowns, with its label.</summary>
public sealed record SettingChoice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// What the Settings window follows of the main window (DESIGN.md §14): the selected tab, whose project has pages of its
/// own, the tabs that have their own settings, and the page it showed last.
/// </summary>
public interface ISettingsHost
{
    /// <summary>The selected tab's project pages, made fresh; null with no tab open.</summary>
    ProjectSettingsViewModel? SelectedTabProject();

    /// <summary>Raised when another tab is selected, or the last one closes.</summary>
    event Action? SelectedTabChanged;

    /// <summary>The open tabs whose Tab settings… override a setting, by its override key.</summary>
    IReadOnlyList<string> TabsWithOwn(string overrideKey);

    /// <summary>The page Settings showed when it last closed, which it opens at next.</summary>
    string? LastSettingsPage { get; set; }
}

/// <summary>
/// The Settings window (DESIGN.md §14): the sidebar's pages in their groups, the search box, and the page being shown.
/// Each category is a page of its own (<see cref="SettingsPage"/>), with its sections and settings; below the categories,
/// the selected tab's project has its pages too (<see cref="Project"/>), which follow the selected tab while the window
/// is open. Changes apply immediately; there's no Save button. Dispose it when the window closes.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    /// <summary>How long a setting that search or a link brought into view stays picked out.</summary>
    internal static readonly TimeSpan HighlightFor = TimeSpan.FromSeconds(2);

    private readonly AppServices _services;
    private readonly SettingsContext _context;
    private readonly ISettingsHost? _host;
    private readonly IReadOnlyList<(string Heading, IReadOnlyList<string> Categories)> _groups;
    private readonly List<SettingsPage> _categoryPages;
    private UiTimeout HighlightWait => field ??= new(_services.Time, _services.Dispatcher);

    /// <param name="opening">
    /// Where the window opens, and the project pages of the tab that was selected (DESIGN.md §14). Without it, it opens
    /// where it was last left, or at General, with the host's project pages or none.
    /// </param>
    /// <param name="host">The main window, whose selected tab the project pages follow; null in some tests.</param>
    public SettingsViewModel(AppServices services, string? accountText, ClaudeUpdateViewModel? updates = null, SettingsOpening? opening = null, ISettingsHost? host = null)
    {
        _services = services;
        _host = host;
        _context = new SettingsContext(services, host is null ? null : host.TabsWithOwn);
        General = new GeneralPage(_context);
        Appearance = new AppearancePage(_context);
        Conversation = new ConversationPage(_context);
        Notifications = new NotificationsPage(_context);
        Keyboard = new KeyboardPage(_context);
        Tabs = new TabsPage(_context);
        ClaudeCode = new ClaudeCodePage(_context, accountText, updates, Tabs);
        Accounts = new AccountsPage(_context);
        Usage = new UsagePage(_context);
        Sessions = new SessionsPage(_context);
        Files = new FilesPage(_context);
        AllTools = new AllToolsPage(_context);
        var types = services.ProjectTools.Detector.Types;
        ProjectTypes = [.. types.Select(type => new ProjectTypePage(_context, type))];
        Git = new GitPage(_context);
        Perforce = new PerforcePage(_context);
        Advanced = new AdvancedPage(_context, ClaudeCode);
        _groups = SettingsSidebar.Groups(types);
        Categories = [.. _groups.SelectMany(g => g.Categories)];
        SettingsPage[] pages = [General, Appearance, Conversation, Notifications, Keyboard, ClaudeCode, Accounts, Tabs, Usage, Advanced, Sessions, Files, AllTools, .. ProjectTypes, Git, Perforce];
        _categoryPages = [.. Categories.Select(category => pages.Single(p => p.Title == category))];
        foreach (var page in _categoryPages)
        {
            page.RefreshItems();
        }
        UseProject(opening?.Project ?? host?.SelectedTabProject());
        _context.SettingsSaved += OnSettingsChanged;
        _context.AllPagesChanged += OnSettingsChanged;
        if (host is not null)
        {
            host.SelectedTabChanged += OnSelectedTabChanged;
        }

        var category = opening?.Category ?? host?.LastSettingsPage;
        if (category is not null && Pages.Any(p => p.Title == category))
        {
            SelectedCategory = category;
        }
        if (opening?.Key is { } key)
        {
            Highlight(key);
        }
        if (opening?.StartNew == true)
        {
            if (CurrentPage is ProjectLinksPage)
            {
                Project?.StartNewLink();
            }
            else if (CurrentPage is ProjectActionsPage)
            {
                Project?.StartNewAction();
            }
        }
    }

    public void Dispose()
    {
        if (_host is not null)
        {
            _host.SelectedTabChanged -= OnSelectedTabChanged;
            _host.LastSettingsPage = SelectedCategory;
        }
        HighlightWait.Dispose();
        General.Dispose();
        ClaudeCode.Dispose();
        Accounts.Dispose();
        Git.Dispose();
        Project?.Dispose();
        _context.Dispose();
    }

    // ---- The pages ----------------------------------------------------------------------------------------------------

    public GeneralPage General { get; }

    public AppearancePage Appearance { get; }

    public ConversationPage Conversation { get; }

    public NotificationsPage Notifications { get; }

    public KeyboardPage Keyboard { get; }


    public ClaudeCodePage ClaudeCode { get; }

    public AccountsPage Accounts { get; }

    public TabsPage Tabs { get; }

    public UsagePage Usage { get; }

    public SessionsPage Sessions { get; }

    public FilesPage Files { get; }

    public AllToolsPage AllTools { get; }

    /// <summary>A page for each project type Claudette knows, in its order, after All tools (DESIGN.md §18, "Project types").</summary>
    public IReadOnlyList<ProjectTypePage> ProjectTypes { get; }

    /// <summary>The project type's page for projects of <paramref name="kind"/>, such as <c>unreal</c>.</summary>
    public ProjectTypePage ProjectType(string kind) => ProjectTypes.Single(p => p.Kind == kind);

    /// <summary>Source control's first page (DESIGN.md §14).</summary>
    public GitPage Git { get; }

    public PerforcePage Perforce { get; }

    public AdvancedPage Advanced { get; }

    /// <summary>The categories, in the sidebar's order, with a page for each project type Claudette knows.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>
    /// Every page, in the sidebar's order: the categories' (<see cref="Categories"/>), then the selected tab's project
    /// pages (<see cref="ProjectPages"/>), which there are none of with no tab open.
    /// </summary>
    public IReadOnlyList<SettingsPage> Pages { get; private set; } = [];

    /// <summary>The sidebar's list: each group's heading and its pages.</summary>
    public ObservableCollection<SettingsNavEntry> NavEntries { get; } = [];

    /// <summary>
    /// The selected tab's project's pages, in a list of their own at the foot of the sidebar, above the version, so they
    /// stay in view however far the categories scroll (DESIGN.md §14, "The project's pages"); empty with no tab open.
    /// </summary>
    public ObservableCollection<SettingsNavEntry> ProjectNavEntries { get; } = [];

    /// <summary>The account in Claude Code, with <b>Sign in</b> and <b>Sign out</b> wired to the header's (DESIGN.md §11). Null in some tests.</summary>
    public AccountViewModel? Account
    {
        get => ClaudeCode.Account;
        init => ClaudeCode.Account = value;
    }

    /// <summary>Claudette's version, update status and actions in General, the same as the sidebar's badge. Null in some tests.</summary>
    public AppUpdateViewModel? AppUpdates
    {
        get => General.AppUpdates;
        init => General.AppUpdates = value;
    }

    /// <summary>A source build's new commits upstream in General, the same as the sidebar's badge. Null for an installed Claudette.</summary>
    public SourceUpdateViewModel? SourceUpdates
    {
        get => General.SourceUpdates;
        init => General.SourceUpdates = value;
    }

    /// <summary>The window takes the Claude style's shapes (DESIGN.md §3, "Visual style"), as the main window does.</summary>
    public bool IsClaudeStyle => _services.Settings.Appearance.Style == AppStyle.Claude;

    private void OnSettingsChanged()
    {
        OnPropertyChanged(nameof(IsClaudeStyle));
        RefreshNavTags();
    }

    /// <summary>Each page's entry says what its page's <see cref="SettingsPage.NavTag"/> says now, such as <i>Off</i>.</summary>
    private void RefreshNavTags()
    {
        foreach (var entry in NavEntries.Where(e => !e.IsHeading))
        {
            entry.Tag = Pages.FirstOrDefault(p => p.Title == entry.Category)?.NavTag;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage), nameof(SelectedNavEntry), nameof(SelectedProjectNavEntry))]
    public partial string SelectedCategory { get; set; } = SettingsCategory.General;

    /// <summary>The page the window shows: the selected category's, or one of the project's. Null for a page there isn't.</summary>
    public SettingsPage? CurrentPage => Pages.FirstOrDefault(p => p.Title == SelectedCategory);

    partial void OnSelectedCategoryChanged(string value) => CurrentPage?.OnShown();

    /// <summary>
    /// The categories' selection: the current page's entry, or none while one of the project's pages shows. A heading
    /// isn't a choice, and leaves the page as it is.
    /// </summary>
    public SettingsNavEntry? SelectedNavEntry
    {
        get => NavEntries.FirstOrDefault(e => !e.IsHeading && e.Category == SelectedCategory);
        set => Select(value);
    }

    /// <summary>The project's pages' selection: the current page's entry, or none while a category shows.</summary>
    public SettingsNavEntry? SelectedProjectNavEntry
    {
        get => ProjectNavEntries.FirstOrDefault(e => e.Category == SelectedCategory);
        set => Select(value);
    }

    /// <summary>Shows a page picked in either list; anything else (a heading, or a list emptied as it's filled again) leaves it as it is.</summary>
    private void Select(SettingsNavEntry? value, [CallerMemberName] string? list = null)
    {
        if (value is { IsHeading: false } entry)
        {
            SelectedCategory = entry.Category;
        }
        else
        {
            OnPropertyChanged(list);
        }
    }

    private void BuildNav()
    {
        NavEntries.Clear();
        foreach (var (heading, categories) in _groups)
        {
            NavEntries.Add(SettingsNavEntry.Heading(heading));
            foreach (var page in categories.Select(category => _categoryPages.First(p => p.Title == category)))
            {
                NavEntries.Add(SettingsNavEntry.Page(page.Title, page.IconKey, page.NavTag));
            }
        }
        ProjectNavEntries.Clear();
        foreach (var page in Pages.OfType<ProjectPage>())
        {
            ProjectNavEntries.Add(SettingsNavEntry.Page(page.Title, page.IconKey));
        }
        OnPropertyChanged(nameof(SelectedNavEntry));
        OnPropertyChanged(nameof(SelectedProjectNavEntry));
    }

    // ---- Going to a setting (DESIGN.md §14: search and links bring one into view) --------------------------------------

    /// <summary>The row or section the window should scroll to: set as it's asked for, cleared once it's there.</summary>
    [ObservableProperty]
    public partial string? ScrollToKey { get; set; }

    /// <summary>Shows <paramref name="category"/>, and brings the setting or section with <paramref name="key"/> into view, picked out for a moment.</summary>
    public void Show(string category, string? key = null)
    {
        if (Pages.Any(p => p.Title == category))
        {
            SelectedCategory = category;
        }
        if (key is not null)
        {
            Highlight(key);
        }
    }

    /// <summary>Where <see cref="Show"/> or a new window's opening says, on the page showing.</summary>
    private void Highlight(string key)
    {
        if (CurrentPage is not { } page)
        {
            return;
        }
        ClearHighlight();
        if (page.FindSetting(key) is { } setting)
        {
            setting.IsHighlighted = true;
        }
        else if (page.FindGroup(key) is { } group)
        {
            group.IsHighlighted = true;
        }
        else
        {
            return;
        }
        ScrollToKey = key;
        HighlightWait.Restart(HighlightFor, ClearHighlight);
    }

    private void ClearHighlight()
    {
        foreach (var page in Pages)
        {
            foreach (var setting in page.Items.Where(s => s.IsHighlighted))
            {
                setting.IsHighlighted = false;
            }
            foreach (var group in page.Groups.Where(g => g.IsHighlighted))
            {
                group.IsHighlighted = false;
            }
        }
    }

    /// <summary>Another window asked for Settings while it's open: it comes to <paramref name="opening"/>'s page and setting.</summary>
    public void Open(SettingsOpening opening)
    {
        if (opening.Project is { } project)
        {
            UseProject(project);
        }
        if (opening.Category is { } category)
        {
            Show(category, opening.Key);
        }
        if (opening.StartNew)
        {
            if (CurrentPage is ProjectLinksPage)
            {
                Project?.StartNewLink();
            }
            else if (CurrentPage is ProjectActionsPage)
            {
                Project?.StartNewAction();
            }
        }
    }

    // ---- Search (DESIGN.md §14: "A search box filters settings by name") -------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearching))]
    public partial string SearchText { get; set; } = "";

    public bool IsSearching => SearchText.Trim().Length > 0;

    public ObservableCollection<SettingsSearchResult> SearchResults { get; } = [];

    /// <summary>
    /// Each page's sections and settings whose words have every word typed, the ones named by them first, then the ones
    /// with the words together, in the sidebar's order; then the project pages', named with the project's group ("NightOwl → Links"). The page showing
    /// follows the first, unless it has one of its own.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return;
        }
        bool Has(string text, string word) => text.Contains(word, StringComparison.OrdinalIgnoreCase);
        bool Finds(SettingsSearchResult r) => words.All(w => r.Words.Any(text => Has(text, w)) || Has(r.Category, w) || r.Section is { } s && Has(s, w) || r.Group is { } g && Has(g, w));
        bool Named(SettingsSearchResult r) => words.All(w => Has(r.Label, w));
        var phrase = string.Join(' ', words);
        bool Phrase(SettingsSearchResult r) => r.Words.Any(text => Has(text, phrase));
        var found = Pages.SelectMany(p => p.SearchEntries).Select(WithGroupWords).Where(Finds).ToList();
        foreach (var result in found.OrderBy(r => Named(r) ? 0 : Phrase(r) ? 1 : 2))
        {
            SearchResults.Add(result);
        }
        if (SearchResults.Count > 0 && SearchResults.All(r => r.Category != SelectedCategory))
        {
            SelectedCategory = SearchResults[0].Category;
        }
    }

    /// <summary>An entry with the words its page's group is found by, such as "version control" for Source control's (<see cref="SettingsSidebar.GroupWords"/>).</summary>
    private SettingsSearchResult WithGroupWords(SettingsSearchResult entry)
    {
        var more = _groups.Where(g => g.Categories.Contains(entry.Category)).SelectMany(g => SettingsSidebar.GroupWords(g.Heading)).ToList();
        return more.Count == 0 ? entry : entry with { Words = [.. entry.Words, .. more] };
    }

    /// <summary>Picking a result shows its page, and brings it into view there.</summary>
    [ObservableProperty]
    public partial SettingsSearchResult? SelectedSearchResult { get; set; }

    partial void OnSelectedSearchResultChanged(SettingsSearchResult? value)
    {
        if (value is not null)
        {
            Show(value.Category, value.Key);
        }
    }

    // ---- Recording a shortcut (Keyboard, Quick suffixes) ----------------------------------------------------------------

    /// <summary>A shortcut is being recorded: the window passes the next key press to <see cref="RecordShortcut"/>.</summary>
    public bool IsRecordingShortcut => _context.Shortcuts.IsRecording;

    public void CancelRecording() => _context.Shortcuts.Cancel();

    /// <inheritdoc cref="ShortcutRecorder.Record"/>
    public void RecordShortcut(KeyChord chord) => _context.Shortcuts.Record(chord);
}
