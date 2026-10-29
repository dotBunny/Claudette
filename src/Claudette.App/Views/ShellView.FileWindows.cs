using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Claudette.App.Services;
using Claudette.App.ViewModels;
using Claudette.Core.Development;

namespace Claudette.App.Views;

/// <summary>
/// The Project Files tabs' files in windows of their own (DESIGN.md §18, "Project files", "A file in a window of its
/// own"): one window a file, opened over where the file was or centered on Claudette's window, and brought forward when
/// asked again. Find in files, opened from one, brings Claudette's window forward for its popup.
/// </summary>
public partial class ShellView
{
    /// <summary>A new window's size when the Files tab isn't on screen to take it from.</summary>
    private static readonly Size FileWindowSize = new(900, 700);

    private readonly Dictionary<ProjectFileViewModel, ProjectFileWindow> _fileWindows = [];

    /// <summary>The files' windows open now, for tests.</summary>
    internal IReadOnlyDictionary<ProjectFileViewModel, ProjectFileWindow> FileWindows => _fileWindows;

    /// <summary>Opens <paramref name="file"/>'s window, or brings the one it has forward.</summary>
    private void ShowFileWindow(ProjectFileViewModel file)
    {
        if (_fileWindows.TryGetValue(file, out var open))
        {
            open.BringForward();
            return;
        }
        var window = ProjectFileWindow.Open(file, file.WindowPlace ?? NewFileWindowPlace(file));
        _fileWindows[file] = window;
        window.Closed += (_, _) => _fileWindows.Remove(file);
    }

    /// <summary>
    /// Where a file popped out from a menu, a button or the palette opens: over the editor's place in its Files tab, a
    /// little down and to the right, at its size; with the Files tab off screen, centered on Claudette's window.
    /// </summary>
    private WindowPlacement NewFileWindowPlace(ProjectFileViewModel file)
    {
        if (this.GetVisualDescendants().OfType<ProjectFilesView>().FirstOrDefault(v => ReferenceEquals(v.DataContext, file.Files) && v.IsEffectivelyVisible)
            ?.NewWindowPlace(offset: 24) is { } over)
        {
            return over;
        }
        if (TopLevel.GetTopLevel(this) is not Window main)
        {
            return new WindowPlacement(0, 0, FileWindowSize.Width, FileWindowSize.Height, false);
        }
        var scaling = main.RenderScaling;
        var x = main.Position.X + (int)((main.Bounds.Width - FileWindowSize.Width) * scaling / 2);
        var y = main.Position.Y + (int)((main.Bounds.Height - FileWindowSize.Height) * scaling / 2);
        return new WindowPlacement(x, y, FileWindowSize.Width, FileWindowSize.Height, false);
    }

    /// <summary>Find in files' popup is over Claudette's window: opened from a file's window, that comes forward.</summary>
    private void BringForwardForFindInFiles()
    {
        if (_shell?.FindInFiles is not null && TopLevel.GetTopLevel(this) is Window { IsActive: false } main && _fileWindows.Values.Any(w => w.IsActive))
        {
            UnfocusedWindows.Activate(main);
        }
    }
}
