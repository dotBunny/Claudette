using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Claudette.App.Diffs;
using Claudette.App.ViewModels;
using Claudette.Core.ProjectFiles;

namespace Claudette.App.Views;

/// <summary>
/// One open file in a Project Files tab: the chip fades while the cursor's line is under it, and a Markdown file's
/// editor and preview share the width by the mode and scroll together (DESIGN.md §18, "Markdown preview").
/// </summary>
public partial class ProjectFileView : UserControl
{
    /// <summary>The least either side of Edit and preview narrows to.</summary>
    internal const double MinPaneWidth = 240;

    private ProjectFileViewModel? _file;
    private FileViewMode _mode;

    /// <summary>An offset set here, whose scroll isn't the user's.</summary>
    private double? _settingEditorOffset;

    public ProjectFileView()
    {
        InitializeComponent();
        var view = CodeEditor.TextEditor.TextArea.TextView;
        CodeEditor.TextEditor.TextArea.Caret.PositionChanged += (_, _) => FadeChip();
        view.ScrollOffsetChanged += (_, _) =>
        {
            FadeChip();
            OnEditorScrolled();
        };
        Preview.Scrolled += OnPreviewScrolled;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_file is not null)
        {
            _file.PropertyChanged -= OnFileChanged;
        }
        _file = DataContext as ProjectFileViewModel;
        if (_file is not null)
        {
            _file.PropertyChanged += OnFileChanged;
            _mode = _file.Mode;
        }
        LayOutPanes();
    }

    private void OnFileChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProjectFileViewModel.Mode):
                KeepPlace();
                LayOutPanes();
                break;
            case nameof(ProjectFileViewModel.ShowsPreview) or nameof(ProjectFileViewModel.ShowsEditor):
                LayOutPanes();
                break;
        }
    }

    /// <summary>The chip fades while the cursor's line is under it, so it never hides what's being typed.</summary>
    private void FadeChip()
    {
        var area = CodeEditor.TextEditor.TextArea;
        if (!Chip.IsEffectivelyVisible || area.TextView is not { VisualLinesValid: true } view)
        {
            return;
        }
        var caret = area.Caret.CalculateCaretRectangle();
        var top = caret.Y - view.ScrollOffset.Y;
        var origin = view.TranslatePoint(new Point(0, top), Chip);
        Chip.Classes.Set("faded", origin is { } at && at.Y + view.DefaultLineHeight > 0 && at.Y < Chip.Bounds.Height);
    }

    // ---- Edit and preview (DESIGN.md §18, "Markdown preview") ----------------------------------------------------------

    /// <summary>
    /// The editor, the preview, or both, half the width each, as the mode says. Both start even each time the mode is
    /// picked, and the line between them gives either side more down to <see cref="MinPaneWidth"/>.
    /// </summary>
    private void LayOutPanes()
    {
        var columns = TextPanes.ColumnDefinitions;
        var both = _file is { ShowsEditor: true, ShowsPreview: true };
        var previewOnly = _file is { ShowsPreview: true } && !both;
        columns[0].Width = previewOnly ? new GridLength(0) : GridLength.Star;
        columns[0].MinWidth = both ? MinPaneWidth : 0;
        columns[2].Width = both || previewOnly ? GridLength.Star : new GridLength(0);
        columns[2].MinWidth = both ? MinPaneWidth : 0;
    }

    private void OnPanesEdgeDoubleTapped(object? sender, TappedEventArgs e)
    {
        LayOutPanes();
        e.Handled = true;
    }

    /// <summary>
    /// Switching between the editor and the preview keeps the place: the side coming into view goes to the line at the
    /// top of the one that was showing.
    /// </summary>
    private void KeepPlace()
    {
        if (_file is not { } file)
        {
            return;
        }
        var before = _mode;
        _mode = file.Mode;
        var showedEditor = before is FileViewMode.Edit or FileViewMode.EditAndPreview;
        var showedPreview = before is FileViewMode.Preview or FileViewMode.EditAndPreview;
        if (file.ShowsPreview && !showedPreview)
        {
            Preview.ShowLine(EditorTopLine());
        }
        else if (file.ShowsEditor && !showedEditor && showedPreview)
        {
            var line = Preview.TopLine;
            // Once the editor has laid out its lines again.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrollEditorTo(line), Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnEditorScrolled()
    {
        var offset = CodeEditor.TextEditor.TextArea.TextView.ScrollOffset.Y;
        if (_settingEditorOffset is { } expected)
        {
            _settingEditorOffset = null;
            if (Math.Abs(offset - expected) < 1)
            {
                return;
            }
        }
        if (_file is { IsEditAndPreviewMode: true, ShowsPreview: true })
        {
            Preview.ScrollToLine(EditorTopLine());
        }
    }

    private void OnPreviewScrolled(double line)
    {
        if (_file is { IsEditAndPreviewMode: true })
        {
            ScrollEditorTo(line);
        }
    }

    /// <summary>The line at the top of the editor, and how far down it the top is.</summary>
    private double EditorTopLine()
    {
        var view = CodeEditor.TextEditor.TextArea.TextView;
        if (view.Document is not { LineCount: > 0 } document)
        {
            return 1;
        }
        var y = view.ScrollOffset.Y;
        var line = view.GetDocumentLineByVisualTop(y);
        if (line is null)
        {
            return 1;
        }
        var top = view.GetVisualTopByDocumentLine(line.LineNumber);
        var next = line.LineNumber < document.LineCount ? view.GetVisualTopByDocumentLine(line.LineNumber + 1) : top + view.DefaultLineHeight;
        return line.LineNumber + (next > top ? Math.Clamp((y - top) / (next - top), 0, 1) : 0);
    }

    /// <summary>Scrolls the editor so <paramref name="line"/> is at its top, without the preview following back.</summary>
    private void ScrollEditorTo(double line)
    {
        var view = CodeEditor.TextEditor.TextArea.TextView;
        if (view.Document is not { LineCount: > 0 } document)
        {
            return;
        }
        var number = Math.Clamp((int)Math.Floor(line), 1, document.LineCount);
        var top = view.GetVisualTopByDocumentLine(number);
        var next = number < document.LineCount ? view.GetVisualTopByDocumentLine(number + 1) : top + view.DefaultLineHeight;
        var y = top + Math.Clamp(line - number, 0, 1) * (next - top);
        if (Math.Abs(view.ScrollOffset.Y - y) < 0.5)
        {
            return;
        }
        _settingEditorOffset = y;
        CodeEditor.ScrollToVerticalOffset(y);
    }

    // ---- The notes' buttons and the diff ------------------------------------------------------------------------------

    // In its tab or its window of its own, the file's Files tab has the commands.
    private void OnOpenInSystem(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectFileViewModel file)
        {
            file.Files.OpenFileInSystemCommand.Execute(file);
        }
    }

    private void OnOpenInViewer(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectFileViewModel file)
        {
            file.Files.OpenFileInViewerCommand.Execute(file);
        }
    }

    /// <summary>A double-click on a line of the diff, not on its Revert: <b>Edit</b>, at that line.</summary>
    private void OnDiffDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ProjectFileViewModel file && e.Source is StyledElement { DataContext: InlineDiffRow or SideBySideDiffRow } source
            && (source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is null)
        {
            file.EditAt(source.DataContext);
            e.Handled = true;
        }
    }
}
