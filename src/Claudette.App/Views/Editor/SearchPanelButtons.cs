using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Search;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.Views.Editor;

/// <summary>
/// The buttons of AvaloniaEdit's find box (its search panel): × and the arrows, and Replace and Replace all. Theirs are
/// routed commands, run against whatever last had the keyboard anywhere in the app, and asked only as the panel opens
/// whether they can run, so a find box opened, or used, with the keyboard elsewhere (the explorer, a banner's button)
/// had a × and arrows that did nothing. Each calls the panel itself instead. And one of Claudette's own before ×:
/// <b>Find in files</b>, which takes what the box has to Find in files (DESIGN.md §18, "Finding in files").
/// </summary>
internal static class SearchPanelButtons
{
    /// <summary>The name of the <b>Find in files</b> button, so it's added once to each of the panel's templates.</summary>
    internal const string FindInFilesName = "FindInFilesButton";

    private static readonly ConditionalWeakTable<SearchPanel, object> Installed = [];

    /// <summary>
    /// For <paramref name="editor"/>'s find box, which it makes as its own template is applied: its buttons call it from
    /// then on, whenever its template is applied, the first time it opens and with a new theme; and an editor's, not a
    /// log's, has <paramref name="findInFiles"/>.
    /// </summary>
    public static void Install(TextEditor editor, FindInFilesButton? findInFiles = null)
    {
        Attach(editor.SearchPanel, findInFiles);
        // The panel is made after the template is applied, so it's looked for once that's done.
        editor.TemplateApplied += (_, _) => Dispatcher.UIThread.Post(() => Attach(editor.SearchPanel, findInFiles));
    }

    private static void Attach(SearchPanel? search, FindInFilesButton? findInFiles)
    {
        if (search is null || !Installed.TryAdd(search, search))
        {
            return;
        }
        search.TemplateApplied += (_, _) => CallPanel(search, findInFiles);
        CallPanel(search, findInFiles);
    }

    private static void CallPanel(SearchPanel search, FindInFilesButton? findInFiles)
    {
        Button? close = null;
        Button? next = null;
        foreach (var button in search.GetVisualDescendants().OfType<Button>())
        {
            ICommand? direct = button.Command switch
            {
                var command when ReferenceEquals(command, SearchCommands.CloseSearchPanel) => new RelayCommand(search.Close),
                var command when ReferenceEquals(command, SearchCommands.FindNext) => new RelayCommand(() => search.FindNext()),
                var command when ReferenceEquals(command, SearchCommands.FindPrevious) => new RelayCommand(search.FindPrevious),
                var command when ReferenceEquals(command, SearchCommands.ReplaceNext) => new RelayCommand(search.ReplaceNext),
                var command when ReferenceEquals(command, SearchCommands.ReplaceAll) => new RelayCommand(search.ReplaceAll),
                _ => null,
            };
            if (ReferenceEquals(button.Command, SearchCommands.CloseSearchPanel))
            {
                close = button;
            }
            else if (ReferenceEquals(button.Command, SearchCommands.FindNext))
            {
                next = button;
            }
            if (direct is not null)
            {
                button.Command = direct;
            }
        }
        if (findInFiles is not null && close is not null && next is not null)
        {
            AddFindInFiles(search, close, next, findInFiles);
        }
    }

    /// <summary><b>Find in files</b>, before × in the box's row, the size the arrows beside it are.</summary>
    private static void AddFindInFiles(SearchPanel search, Button close, Button like, FindInFilesButton findInFiles)
    {
        if (close.GetVisualParent() is not Panel row || row.Children.OfType<Button>().Any(b => b.Name == FindInFilesName))
        {
            return;
        }
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Data = search.TryFindResource("IconFindInFiles", out var data) ? data as Geometry : null,
            StrokeThickness = 1.2,
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
        };
        icon.Bind(Shape.StrokeProperty, search.GetResourceObservable("MutedTextBrush"));
        var button = new Button
        {
            Name = FindInFilesName,
            Content = icon,
            Width = like.Width,
            Height = like.Height,
            Margin = like.Margin,
            Padding = like.Padding,
            MinWidth = like.MinWidth,
            MinHeight = like.MinHeight,
            Background = like.Background,
            BorderThickness = like.BorderThickness,
            CornerRadius = like.CornerRadius,
            VerticalAlignment = like.VerticalAlignment,
            HorizontalContentAlignment = like.HorizontalContentAlignment,
            VerticalContentAlignment = like.VerticalContentAlignment,
            Command = new RelayCommand(() => findInFiles.Run(search)),
        };
        AutomationProperties.SetName(button, "Find in files");
        ToolTip.SetTip(button, findInFiles.Tip());
        // Its shortcut as it's bound now.
        button.PointerEntered += (_, _) => ToolTip.SetTip(button, findInFiles.Tip());
        row.Children.Insert(row.Children.IndexOf(close), button);
    }
}

/// <summary>The find box's <b>Find in files</b>: what a click does, and its tip, asked as the pointer comes.</summary>
internal sealed record FindInFilesButton(Action<SearchPanel> Run, Func<string> Tip);
