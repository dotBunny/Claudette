using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
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
/// had a × and arrows that did nothing. Each calls the panel itself instead.
/// </summary>
internal static class SearchPanelButtons
{
    private static readonly ConditionalWeakTable<SearchPanel, object> Installed = [];

    /// <summary>
    /// For <paramref name="editor"/>'s find box, which it makes as its own template is applied: its buttons call it from
    /// then on, whenever its template is applied, the first time it opens and with a new theme.
    /// </summary>
    public static void Install(TextEditor editor)
    {
        Attach(editor.SearchPanel);
        // The panel is made after the template is applied, so it's looked for once that's done.
        editor.TemplateApplied += (_, _) => Dispatcher.UIThread.Post(() => Attach(editor.SearchPanel));
    }

    private static void Attach(SearchPanel? search)
    {
        if (search is null || !Installed.TryAdd(search, search))
        {
            return;
        }
        search.TemplateApplied += (_, _) => CallPanel(search);
        CallPanel(search);
    }

    private static void CallPanel(SearchPanel search)
    {
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
            if (direct is not null)
            {
                button.Command = direct;
            }
        }
    }
}
