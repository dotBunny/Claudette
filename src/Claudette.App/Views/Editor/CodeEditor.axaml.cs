using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using Claudette.App.Editor;
using Claudette.App.ViewModels;

namespace Claudette.App.Views.Editor;

/// <summary>
/// An open file's editor (DESIGN.md §18, "Project files", "The editor"): AvaloniaEdit showing the file's document, with
/// Claudette's TextMate coloring, the gutter's change bars and the peek, the current line, folding by indentation, Go to
/// line and find, and the cursor and scroll kept in the file's view model.
/// </summary>
public partial class CodeEditor : UserControl
{
    private readonly ChangeMargin _margin = new();
    private readonly ChangePeek _peekView = new();
    private readonly ChangePeekGenerator _peek;
    private readonly PeekCaretLayer _peekCaret;
    /// <summary>Tied to one document: installed once the file's is shown, and again for another.</summary>
    private FoldingManager? _folding;
    private ProjectFileViewModel? _file;
    private TextMateColoring? _coloring;
    private IBrush? _caretBrush;
    private bool _wordWrapOverridden;

    public CodeEditor()
    {
        InitializeComponent();
        SearchPanelButtons.Install(Editor);
        ActualThemeVariantChanged += (_, _) =>
        {
            _coloring?.UseDark(ActualThemeVariant == ThemeVariant.Dark);
            _margin.InvalidateVisual();
        };
        var area = Editor.TextArea;
        var view = area.TextView;
        // #614: with it off, dragging a selection to the last line freezes the editor.
        Editor.Options.AllowScrollBelowDocument = true;
        // Addresses in code aren't links: AvaloniaEdit would open them itself, in a fixed blue.
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        // Its own covers a peek under the line; this one is the text's height.
        Editor.Options.HighlightCurrentLine = false;
        view.BackgroundRenderers.Add(new CurrentLineHighlight(area, () => Token("EditorCurrentLineBrush")));
        // The bars go between the line numbers and the text.
        area.LeftMargins.Insert(Math.Min(1, area.LeftMargins.Count), _margin);
        _margin.Clicked += line => _file?.TogglePeekAt(line);
        _peek = new ChangePeekGenerator(view, _peekView);
        _peekCaret = new PeekCaretLayer(area, () => _peek.Line);
        view.InsertLayer(_peekCaret, KnownLayer.Caret, LayerInsertionPosition.Above);
        area.Caret.PositionChanged += OnCaretMoved;
        view.ScrollOffsetChanged += (_, _) =>
        {
            if (_file is not null && Editor.Document is not null)
            {
                _file.Scroll = view.ScrollOffset.Y;
            }
        };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        GoToLineText.KeyDown += OnGoToLineKeyDown;
        GoToLineText.LostFocus += (_, _) => GoToLineBox.IsVisible = false;
    }

    /// <summary>The editor itself, for the view that hosts it.</summary>
    internal TextEditor TextEditor => Editor;

    /// <summary>
    /// Scrolls to <paramref name="offset"/> pixels down the text. AvaloniaEdit 12's own <c>ScrollToVerticalOffset</c> does
    /// nothing (its body is commented out), so this sets its scroller's offset.
    /// </summary>
    internal void ScrollToVerticalOffset(double offset)
    {
        if (Editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } scroller)
        {
            scroller.Offset = scroller.Offset.WithY(Math.Max(0, offset));
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Unhook();
        _file = DataContext as ProjectFileViewModel;
        Hook();
    }

    private void Hook()
    {
        if (_file is not { } file)
        {
            return;
        }
        file.PropertyChanged += OnFileChanged;
        file.RevealRequested += Reveal;
        file.FindRequested += OpenFind;
        file.GoToLineRequested += OpenGoToLine;
        file.FocusRequested += FocusEditor;
        file.TextSettled += UpdateFoldings;
        file.PeekLine = () => _peek.Line;
        file.SelectionText = () => Editor.SelectedText;
        ShowDocument();
    }

    private void Unhook()
    {
        if (_file is not { } file)
        {
            return;
        }
        file.PropertyChanged -= OnFileChanged;
        file.RevealRequested -= Reveal;
        file.FindRequested -= OpenFind;
        file.GoToLineRequested -= OpenGoToLine;
        file.FocusRequested -= FocusEditor;
        file.TextSettled -= UpdateFoldings;
        file.PeekLine = null;
        file.SelectionText = null;
        _peek.Remove();
        _coloring?.Dispose();
        _coloring = null;
    }

    /// <summary>The file's document, once it's read: shown, colored, and where it was left.</summary>
    private void ShowDocument()
    {
        if (_file is not { Document: TextEditorDocument document } file)
        {
            return;
        }
        if (ReferenceEquals(Editor.Document, document.Document))
        {
            return;
        }
        UninstallFolding();
        Editor.Document = document.Document;
        _folding = FoldingManager.Install(Editor.TextArea);
        _coloring?.Dispose();
        _coloring = TextMateColoring.Install(Editor.TextArea.TextView, document.Document, file.Language.Scope, ActualThemeVariant == ThemeVariant.Dark);
        ApplyOptions();
        _margin.Markers = file.Markers;
        UpdateFoldings();
        ShowPeek();
        // Where it was left, or where it was asked to go before the editor was there.
        if (file.TakePendingReveal() is { } pending)
        {
            Later(() => Reveal(pending.Line, pending.Column, pending.EndLine, pending.EndColumn));
        }
        else
        {
            var line = Math.Clamp(file.CaretLine, 1, document.Document.LineCount);
            var column = file.CaretColumn;
            var scroll = file.Scroll;
            Later(() =>
            {
                Editor.TextArea.Caret.Position = new TextViewPosition(line, column);
                ScrollToVerticalOffset(scroll);
            });
        }
    }

    private void ApplyOptions()
    {
        if (_file is not { } file)
        {
            return;
        }
        Editor.IsReadOnly = file.IsReadOnly;
        Editor.Options.IndentationSize = file.Indent.Size;
        Editor.Options.ConvertTabsToSpaces = !file.Indent.UseTabs;
        if (!_wordWrapOverridden)
        {
            Editor.WordWrap = file.WordWrap;
        }
    }

    private void OnFileChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProjectFileViewModel.Document) or nameof(ProjectFileViewModel.IsLoaded):
                ShowDocument();
                break;
            case nameof(ProjectFileViewModel.Markers):
                _margin.Markers = _file?.Markers ?? Core.ProjectFiles.ChangeMarkers.None;
                break;
            case nameof(ProjectFileViewModel.Peek):
                ShowPeek();
                break;
            case nameof(ProjectFileViewModel.IsReadOnly) or nameof(ProjectFileViewModel.Indent) or nameof(ProjectFileViewModel.WordWrap):
                ApplyOptions();
                break;
            // Renamed with another extension: colored as that.
            case nameof(ProjectFileViewModel.Language) when _file is { } file && Editor.Document is { } shown:
                _coloring?.Dispose();
                _coloring = TextMateColoring.Install(Editor.TextArea.TextView, shown, file.Language.Scope, ActualThemeVariant == ThemeVariant.Dark);
                break;
            case nameof(ProjectFileViewModel.IsShowing) when _file?.IsShowing == true && _file.TakePendingReveal() is { } pending:
                Later(() => Reveal(pending.Line, pending.Column, pending.EndLine, pending.EndColumn));
                break;
        }
    }

    // ---- The peek ----------------------------------------------------------------------------------------------------

    /// <summary>The peek under its change: the change's last line, or for lines only removed, the line above where they were.</summary>
    private void ShowPeek()
    {
        if (_file is not { } file || Editor.Document is not { } document || file.Peek is not { } block)
        {
            _peek.Remove();
            RestoreCaret();
            return;
        }
        _peekView.DataContext = file;
        _peekView.Show(block, file, Editor.FontFamily, Editor.FontSize, Editor.TextArea.TextView.DefaultLineHeight);
        var line = block.IsRemovalOnly ? Math.Max(1, block.FirstLine - 1) : block.LastLine;
        _peek.Place(document, line);
        UpdateCaret();
    }

    private void OnCaretMoved(object? sender, EventArgs e)
    {
        if (_file is { } file)
        {
            file.CaretLine = Editor.TextArea.Caret.Line;
            file.CaretColumn = Editor.TextArea.Caret.Column;
        }
        UpdateCaret();
    }

    /// <summary>On the line with the peek under it, the caret is drawn the text's height instead of the row's.</summary>
    private void UpdateCaret()
    {
        if (_peekCaret.IsActive)
        {
            _caretBrush ??= Editor.TextArea.Caret.CaretBrush ?? Editor.Foreground;
            Editor.TextArea.Caret.CaretBrush = Brushes.Transparent;
        }
        else
        {
            RestoreCaret();
        }
        _peekCaret.InvalidateVisual();
    }

    private void RestoreCaret()
    {
        if (_caretBrush is not null)
        {
            Editor.TextArea.Caret.CaretBrush = _caretBrush;
            _caretBrush = null;
        }
    }

    // ---- What the file asks for ---------------------------------------------------------------------------------------

    /// <summary>
    /// Goes to a line and column, centered, and selects through <paramref name="endLine"/> when there's one, or up to
    /// <paramref name="endColumn"/> when there's that (a match Find in files found).
    /// </summary>
    private void Reveal(int line, int column, int? endLine, int? endColumn)
    {
        if (Editor.Document is not { } document)
        {
            return;
        }
        line = Math.Clamp(line, 1, document.LineCount);
        var start = document.GetLineByNumber(line);
        if (endColumn is { } stop)
        {
            var last = document.GetLineByNumber(Math.Clamp(endLine ?? line, line, document.LineCount));
            var from = start.Offset + Math.Clamp(column - 1, 0, start.Length);
            var to = last.Offset + Math.Clamp(stop - 1, 0, last.Length);
            Editor.Select(from, Math.Max(0, to - from));
        }
        else if (endLine is { } end && end >= line)
        {
            var last = document.GetLineByNumber(Math.Clamp(end, line, document.LineCount));
            Editor.Select(start.Offset, last.EndOffset - start.Offset);
        }
        else
        {
            Editor.TextArea.ClearSelection();
        }
        Editor.TextArea.Caret.Position = new TextViewPosition(line, Math.Max(1, column));
        Editor.ScrollTo(line, Math.Max(1, column));
        Editor.TextArea.Caret.BringCaretToView();
    }

    private void OpenFind(bool replace)
    {
        var search = Editor.SearchPanel;
        if (Editor.SelectedText is { Length: > 0 } selected && !selected.Contains('\n'))
        {
            search.SearchPattern = selected;
        }
        search.IsReplaceMode = replace && !Editor.IsReadOnly;
        search.Open();
        // The first time, its box is only there once the panel has laid out; until then Reactivate does nothing.
        Later(search.Reactivate);
    }

    private void OpenGoToLine()
    {
        if (Editor.Document is not { } document)
        {
            return;
        }
        GoToLineText.PlaceholderText = $"Go to line (1–{document.LineCount.ToString("N0", CultureInfo.CurrentCulture)})";
        GoToLineText.Text = "";
        GoToLineBox.IsVisible = true;
        GoToLineText.Focus();
    }

    private void OnGoToLineKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            GoToLineBox.IsVisible = false;
            FocusEditor();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            var parts = (GoToLineText.Text ?? "").Split(':', ',');
            if (int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var line))
            {
                var column = parts.Length > 1 && int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var c) ? c : 1;
                GoToLineBox.IsVisible = false;
                Reveal(line, column, null, null);
                FocusEditor();
            }
            e.Handled = true;
        }
    }

    private void FocusEditor() => Later(() => Editor.TextArea.Focus());

    /// <summary>
    /// The editor's own keys (DESIGN.md §18, "Project files", "Keyboard"): <c>Ctrl+G</c> Go to line, <c>Ctrl/Cmd+H</c>
    /// replace (<c>Cmd+Option+F</c> on macOS), <c>Alt+Z</c> word wrap for this file.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var primary = Services.Shortcuts.IsMac ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.G && e.KeyModifiers == KeyModifiers.Control)
        {
            OpenGoToLine();
            e.Handled = true;
        }
        else if (e.Key == Key.H && e.KeyModifiers == primary && !Services.Shortcuts.IsMac || e.Key == Key.F && e.KeyModifiers == (KeyModifiers.Meta | KeyModifiers.Alt))
        {
            OpenFind(replace: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Alt)
        {
            _wordWrapOverridden = true;
            Editor.WordWrap = !Editor.WordWrap;
            e.Handled = true;
        }
    }

    // ---- Folding and the theme ----------------------------------------------------------------------------------------

    /// <summary>The folds again, once typing has stopped for a moment.</summary>
    private void UpdateFoldings()
    {
        if (_folding is { } folding && Editor.Document is { } document && document.LineCount < Core.ProjectFiles.ChangeMarkers.MaxLines * 5)
        {
            folding.UpdateFoldings(IndentationFolding.Compute(document), -1);
        }
    }

    private void UninstallFolding()
    {
        if (_folding is { } folding)
        {
            FoldingManager.Uninstall(folding);
            _folding = null;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _coloring?.Dispose();
        _coloring = null;
        UninstallFolding();
        Editor.Document = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ShowDocument();
    }

    private IBrush? Token(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    /// <summary>Once what's happening now has laid out: the editor knows its lines then.</summary>
    private static void Later(Action action) => Avalonia.Threading.Dispatcher.UIThread.Post(action);
}
