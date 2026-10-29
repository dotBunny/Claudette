using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Media;
using Claudette.Core;
using Claudette.Core.Accounts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>Tabs that share a working folder, shown together like browser tab groups (DESIGN.md §4, "Grouped by folder").</summary>
public sealed partial class TabGroupViewModel : ObservableObject
{
    /// <summary>The colors a new group takes in turn, and the swatches of the color picker.</summary>
    public static readonly IReadOnlyList<NamedColor> Palette =
    [
        new("Blue", Color.Parse("#4C8DFF")), new("Green", Color.Parse("#3DBE72")), new("Orange", Color.Parse("#F29B38")),
        new("Purple", Color.Parse("#A66BFF")), new("Pink", Color.Parse("#F25CA2")), new("Teal", Color.Parse("#25B8B8")),
        new("Yellow", Color.Parse("#D9B51A")), new("Red", Color.Parse("#F2555A")),
    ];

    public TabGroupViewModel(string folder, Color color, bool isCollapsed)
    {
        Folder = folder;
        Color = color;
        IsCollapsed = isCollapsed;
        FolderLabel = FolderName;
        Tabs.CollectionChanged += OnTabsChanged;
    }

    public string Folder { get; }

    public string FolderName => Formats.FolderName(Folder);

    /// <summary>The folder is in a git repository, so a tab can work in a worktree of it (DESIGN.md §4, "Worktree tabs").</summary>
    public bool CanMakeWorktrees => _canMakeWorktrees ??= Core.Git.GitInfo.TryGetBranch(Folder) is not null;

    private bool? _canMakeWorktrees;

    /// <summary>The folder name, or <c>parent/name</c> when two groups would otherwise look the same.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    public partial string FolderLabel { get; set; }

    /// <summary>A name the user gave the group, remembered for its folder; null for none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label), nameof(HasName))]
    public partial string? Name { get; set; }

    public bool HasName => Name is not null;

    /// <summary>The user's name for the group, else <see cref="FolderLabel"/>.</summary>
    public string Label => Name ?? FolderLabel;

    /// <summary>The account the group's new tabs use (DESIGN.md §4, "Accounts"); null for Default.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LabelAccountTag), nameof(HasLabelAccountTag))]
    public partial ClaudeAccount? Account { get; set; }

    /// <summary>The tag beside the label, for an account other than Default.</summary>
    public AccountTag? LabelAccountTag => AccountTag.For(Account, showDefault: false);

    public bool HasLabelAccountTag => LabelAccountTag is not null;

    /// <summary><b>Account ▸</b> in the group's menu, which the shell keeps current; empty while there's only Default.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccountMenu))]
    public partial IReadOnlyList<AccountMenuItem> AccountMenu { get; set; } = [];

    public bool HasAccountMenu => AccountMenu.Count > 0;

    /// <summary>The shell's handler for <see cref="ChooseAccountCommand"/>.</summary>
    internal Action<TabGroupViewModel, string?>? AccountChosen { get; set; }

    /// <summary>An account picked in <b>Account ▸</b>: the group's new tabs use it.</summary>
    [RelayCommand]
    private void ChooseAccount(string? accountId) => AccountChosen?.Invoke(this, accountId);

    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    /// <summary>The tab of the group used last, which the shell says as tabs are selected.</summary>
    private TabViewModel? _usedTab;

    /// <summary>
    /// The tab whose project tools the project's icon on the group's row shows and runs (DESIGN.md §18, "Project tools"):
    /// the one used last, so a job's busy dot and runs are the tab's that was being worked in; else the first. Null with
    /// only the group's Project Files tab open.
    /// </summary>
    public TabViewModel? ProjectTab => _usedTab is { } used && Tabs.Contains(used) ? used : Tabs.FirstOrDefault();

    /// <summary><paramref name="tab"/>, one of the group's, was selected: its project tools are the group's from now on.</summary>
    internal void Used(TabViewModel tab)
    {
        if (ReferenceEquals(_usedTab, tab) || !Tabs.Contains(tab))
        {
            return;
        }
        _usedTab = tab;
        OnPropertyChanged(nameof(ProjectTab));
    }

    /// <summary>
    /// The group's Project Files tab (DESIGN.md §18, "Project files"), its first row; null while it isn't open. It keeps
    /// the group in the sidebar with no session tab left.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles), nameof(HasNoFiles))]
    public partial ProjectFilesViewModel? Files { get; set; }

    public bool HasFiles => Files is not null;

    public bool HasNoFiles => Files is null;

    /// <summary>Neither a session tab nor its Project Files tab is open: the group leaves the sidebar.</summary>
    public bool IsEmpty => Tabs.Count == 0 && Files is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Brush), nameof(SoftBrush))]
    public partial Color Color { get; set; }

    public IBrush Brush => new SolidColorBrush(Color);

    public IBrush SoftBrush => new SolidColorBrush(Color, 0.18);

    partial void OnColorChanged(Color value)
    {
        foreach (var tab in Tabs)
        {
            tab.GroupColor = value;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpanded), nameof(CollapseMenuText))]
    public partial bool IsCollapsed { get; set; }

    public bool IsExpanded => !IsCollapsed;

    public string CollapseMenuText => IsCollapsed ? "Expand group" : "Collapse group";

    /// <summary>The most urgent status among the group's tabs, shown when it's collapsed.</summary>
    public string UrgentGlyph => RowUrgencies.Glyph(Tabs.Select(t => RowUrgencies.Of(t.Status)).DefaultIfEmpty().Max());

    public string Summary => $"{Tabs.Count} tab{(Tabs.Count == 1 ? "" : "s")} in {Folder}";

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var tab in e.NewItems?.OfType<TabViewModel>() ?? [])
        {
            tab.PropertyChanged += OnTabPropertyChanged;
            // Claudette on the composer's hair tie takes the group's colour (DESIGN.md §5).
            tab.GroupColor = Color;
        }
        foreach (var tab in e.OldItems?.OfType<TabViewModel>() ?? [])
        {
            tab.PropertyChanged -= OnTabPropertyChanged;
        }
        OnPropertyChanged(nameof(UrgentGlyph));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ProjectTab));
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.Status))
        {
            OnPropertyChanged(nameof(UrgentGlyph));
        }
    }

    /// <summary>A color as it's saved and typed: <c>#RRGGBB</c>. Group colors are opaque.</summary>
    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>
    /// Reads a color typed or saved as <c>#RRGGBB</c> or <c>#RGB</c>, with or without the <c>#</c>. Any transparency is
    /// dropped.
    /// </summary>
    public static bool TryParseHex(string? text, out Color color)
    {
        color = default;
        var hex = text?.Trim().TrimStart('#') ?? "";
        if (hex.Length is not (3 or 6) || !hex.All(char.IsAsciiHexDigit) || !Color.TryParse("#" + hex, out var parsed))
        {
            return false;
        }
        color = Color.FromRgb(parsed.R, parsed.G, parsed.B);
        return true;
    }
}

/// <summary>One of the group colors, with the name its swatch has for screen readers and in its tip.</summary>
public sealed record NamedColor(string Name, Color Color)
{
    /// <summary>Its name, as a list that picks one (Settings → Accounts) shows and reads it out.</summary>
    public override string ToString() => Name;
}
