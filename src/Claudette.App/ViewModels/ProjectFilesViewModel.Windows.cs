using Claudette.Core.Development;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// The open files in windows of their own (DESIGN.md §18, "Project files", "A file in a window of its own"): popping one
/// out, putting it back, and bringing its window forward. Their tabs stay in the row; the tab shows only the others.
/// </summary>
public sealed partial class ProjectFilesViewModel
{
    /// <summary><b>Open in a window</b> on a file tab's menu, or <b>Put back</b> while it's in one.</summary>
    [RelayCommand]
    private void ToggleFileWindow(ProjectFileViewModel? file)
    {
        if (file is null)
        {
            return;
        }
        if (file.IsInWindow)
        {
            PutBack(file);
        }
        else
        {
            PopOut(file);
        }
    }

    /// <summary>The button at the right of the file tabs' row: the file showing, in a window.</summary>
    [RelayCommand]
    private void OpenShowingInWindow()
    {
        if (Showing is { } showing)
        {
            PopOut(showing);
        }
    }

    public string OpenInWindowTip => Showing is { } showing ? $"Open {showing.Name} in a window" : "Open in a window";

    /// <summary>
    /// Opens <paramref name="file"/> in a window of its own, at <paramref name="place"/> when it was dragged there, or else
    /// where the view puts a new one; one already in a window comes to the front. The tab shows the next file that isn't
    /// in one, and beside another tab with its explorer hidden, with none left, its pane closes.
    /// </summary>
    internal void PopOut(ProjectFileViewModel file, WindowPlacement? place = null)
    {
        if (!OpenFiles.Contains(file))
        {
            return;
        }
        if (file.IsInWindow)
        {
            ShowWindow(file);
            return;
        }
        file.IsPreview = false;
        file.WindowPlace = place;
        // The tab's view of it goes before the window's is made: one editor takes its requests at a time.
        file.IsInWindow = true;
        if (ReferenceEquals(Showing, file))
        {
            Showing = NearestInTab(OpenFiles.IndexOf(file));
        }
        if (!_started)
        {
            Start();
        }
        else if (!file.IsLoaded)
        {
            _ = file.LoadAsync();
        }
        _shell.ShowFileWindow?.Invoke(file);
        OnWindowsChanged();
        if (Showing is null && ClosesWithLastFile)
        {
            _shell.ClosePaneCommand.Execute(this);
        }
    }

    /// <summary>
    /// <b>Put back</b>, its window's ×, or <b>Close tab</b> in it: the window closes, and the file shows in its tab again,
    /// with its edits, cursor, scroll and mode.
    /// </summary>
    internal void PutBack(ProjectFileViewModel file)
    {
        if (!file.IsInWindow)
        {
            return;
        }
        // The window lets go of it first, then the tab's view takes it.
        file.IsInWindow = false;
        file.WindowPlace = null;
        if (OpenFiles.Contains(file))
        {
            Showing = file;
        }
        OnWindowsChanged();
    }

    /// <summary>Its window to the front, with the keyboard in it: a click on its tab, or opening it again from elsewhere.</summary>
    internal void ShowWindow(ProjectFileViewModel file) => _shell.ShowFileWindow?.Invoke(file);

    /// <summary>
    /// Find in files' or Replace in files' shortcut in a file's window: the popup over Claudette's window, with what the
    /// editor has selected, or its find box has.
    /// </summary>
    internal void FindInFilesFrom(ProjectFileViewModel file, bool replace) =>
        _shell.ShowFindInFiles(replace, group: Group, start: file.FindInFilesStart?.Invoke());

    /// <summary>Whether <paramref name="path"/> is open here in a window of its own.</summary>
    internal bool IsOpenInWindow(string path) => OpenFiles.Any(f => f.IsInWindow && PathComparer.Equals(f.Path, path));

    /// <summary>
    /// The file to show in the tab in place of one that was at <paramref name="index"/>: the first from there on that isn't
    /// in a window, or else the last before it.
    /// </summary>
    private ProjectFileViewModel? NearestInTab(int index) =>
        OpenFiles.Skip(index).FirstOrDefault(f => !f.IsInWindow) ?? OpenFiles.Take(index).LastOrDefault(f => !f.IsInWindow);

    /// <summary>Shows <paramref name="file"/>: in its tab, or its window, brought forward.</summary>
    private void Show(ProjectFileViewModel file)
    {
        if (file.IsInWindow)
        {
            ShowWindow(file);
        }
        else
        {
            Showing = file;
        }
    }

    /// <summary>
    /// What the editor's place says with nothing to show: <i>No files open</i>, or that they're in windows of their own.
    /// </summary>
    public string NoFileText => OpenFiles.Where(f => f.IsInWindow).ToArray() switch
    {
        [] => "No files open",
        [var one] => $"{one.Name} is in a window of its own",
        var many => $"{many.Length} files are in windows of their own",
    };

    /// <summary>Whether a file is in a window isn't kept: it's back in its tab next time, as any open file.</summary>
    private void OnWindowsChanged()
    {
        OnPropertyChanged(nameof(NoFileText));
        OnPropertyChanged(nameof(RowDetail));
        OnFileStateChanged();
    }
}
