using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Claudette.App.ViewModels;
using Claudette.Core.ProjectTools.EditorLogs;

namespace Claudette.App.Views;

/// <summary>
/// Keeps a read-only editor showing a log in the main area (DESIGN.md §18, "Project tools", "A run's log"; "Editor
/// logs"). The log keeps its lines; this mirrors those its filter shows into the editor's document as they come and go,
/// one to a line, follows the newest while the log stays current (<see cref="LogTabViewModel.StayCurrent"/>, which
/// scrolling up from the end turns off and scrolling back turns on), and comes back to where it was when the filter or
/// the wrapping changes. It draws the links (<see cref="RunLogLinkGenerator"/>), an editor's log's colors
/// (<see cref="LogLineColorizer"/>) and Unreal's columns (<see cref="LogColumnsGenerator"/>), and opens find.
/// <see cref="ProjectRunView"/> and <see cref="EditorLogView"/> each have one.
/// </summary>
internal sealed class LogMirror
{
    private readonly Control _view;
    private readonly TextEditor _editor;
    private LogTabViewModel? _log;
    private BatchedCollection<LogLine>? _output;

    /// <summary>The document's lines, one to a line: those of the log's lines its filter shows.</summary>
    private readonly List<LogLine> _shown = [];

    /// <summary>How many of the log's lines have been looked at for the document: all of them, between changes.</summary>
    private int _seen;

    /// <summary>A scroll of the mirror's own is on its way: changes until then go with it, and don't turn Stay current off.</summary>
    private bool _scrollPending;

    /// <summary>The filter changed: the document fills afresh when the UI thread gets to it, once for a burst of typing.</summary>
    private bool _refillPending;

    /// <summary>Stay current is being set from where the log is scrolled to, not by its button.</summary>
    private bool _settingFromScroll;

    public LogMirror(Control view, TextEditor editor)
    {
        _view = view;
        _editor = editor;
        Editor.SearchPanelButtons.Install(editor);
        view.AddHandler(InputElement.PointerPressedEvent, (_, _) => _log?.FocusPane(), RoutingStrategies.Tunnel, handledEventsToo: true);
        view.AddHandler(InputElement.GotFocusEvent, (_, _) => _log?.FocusPane(), RoutingStrategies.Bubble, handledEventsToo: true);
        // Its own links are drawn below; AvaloniaEdit's would open themselves, in a fixed blue.
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.HighlightCurrentLine = false;
        // #614: with it off, dragging a selection to the last line freezes the editor.
        editor.Options.AllowScrollBelowDocument = true;
        // Nothing is typed here, so there's no caret to show.
        editor.TextArea.Caret.CaretBrush = Brushes.Transparent;
        editor.Document = new TextDocument();
        var textView = editor.TextArea.TextView;
        textView.LineTransformers.Add(new LogLineColorizer(LineAt, Token));
        // The links first: one at a line's start wins over the indent of a line carrying an entry on.
        textView.ElementGenerators.Add(new RunLogLinkGenerator(() => _log, () => Token("AccentStatusBrush"), link => _log?.OpenLinkCommand.Execute(link)));
        textView.ElementGenerators.Add(new LogColumnsGenerator(LineAt, () => _log as EditorLogViewModel));
        textView.ScrollOffsetChanged += (_, _) => OnScrolled();
        view.ActualThemeVariantChanged += (_, _) => textView.Redraw();
    }

    /// <summary>The view's data context changed: it shows <paramref name="log"/> from now on.</summary>
    public void Use(LogTabViewModel? log)
    {
        Unwatch();
        _log = log;
        if (_view.IsAttachedToVisualTree())
        {
            Watch();
        }
    }

    /// <summary>The view is on the visual tree: the log's lines follow it from now on.</summary>
    public void Watch()
    {
        if (_log is null || _output is not null)
        {
            return;
        }
        _output = _log.Output;
        _output.CollectionChanged += OnOutputChanged;
        _log.LinksFound += OnLinksFound;
        _log.FindRequested += OnFindRequested;
        _log.FilterChanged += OnFilterChanged;
        _log.PropertyChanged += OnLogPropertyChanged;
        Fill();
    }

    public void Unwatch()
    {
        if (_output is not null)
        {
            _output.CollectionChanged -= OnOutputChanged;
            _output = null;
        }
        if (_log is not null)
        {
            _log.LinksFound -= OnLinksFound;
            _log.FindRequested -= OnFindRequested;
            _log.FilterChanged -= OnFilterChanged;
            _log.PropertyChanged -= OnLogPropertyChanged;
        }
    }

    /// <summary>The document's line <paramref name="number"/>, counting from 1, as the log has it.</summary>
    private LogLine? LineAt(int number) => number >= 1 && number <= _shown.Count ? _shown[number - 1] : null;

    /// <summary>The lines its filter shows afresh, then back to where it was.</summary>
    private void Fill()
    {
        _shown.Clear();
        if (_output is not null && _log is { } log)
        {
            _shown.AddRange(_output.Where(log.Shows));
        }
        _seen = _output?.Count ?? 0;
        // Changing the whole document moves the scroll; where it ends up is ComeBack's.
        _scrollPending = true;
        _editor.Document.Text = string.Join("\n", _shown.Select(l => OneLine(l.Text)));
        UpdateIndentation();
        ComeBack();
    }

    /// <summary>
    /// A batch of lines is one Add, after a Remove when the oldest were dropped, or a Reset when it replaced them all
    /// (<see cref="BatchedCollection{T}.AddAndTrim"/>).
    /// </summary>
    private void OnOutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_log is not { } log)
        {
            return;
        }
        var document = _editor.Document;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is { Count: > 0 } added && e.NewStartingIndex == _seen:
                _seen += added.Count;
                var shown = added.Cast<LogLine>().Where(log.Shows).ToArray();
                if (shown.Length == 0)
                {
                    return;
                }
                var first = _shown.Count == 0;
                _shown.AddRange(shown);
                document.Insert(document.TextLength, (first ? "" : "\n") + string.Join("\n", shown.Select(l => OneLine(l.Text))));
                break;
            case NotifyCollectionChangedAction.Remove when e.OldStartingIndex == 0 && e.OldItems is { Count: > 0 } removed && removed.Count <= _seen:
                _seen -= removed.Count;
                // The document's first lines are those of the dropped ones it shows.
                var gone = 0;
                foreach (LogLine line in removed)
                {
                    if (gone < _shown.Count && ReferenceEquals(_shown[gone], line))
                    {
                        gone++;
                    }
                }
                if (gone == _shown.Count)
                {
                    Fill();
                }
                else if (gone > 0)
                {
                    _shown.RemoveRange(0, gone);
                    document.Remove(0, document.GetLineByNumber(gone + 1).Offset);
                }
                return;
            default:
                Fill();
                return;
        }
        if (log.StayCurrent && !_scrollPending)
        {
            ComeBack();
        }
    }

    /// <summary>
    /// Once the lines are laid out: to the newest while the log stays current, the newest at the bottom rather than at
    /// the top with room below; otherwise back to the line that was at the top, or the next one shown.
    /// </summary>
    private void ComeBack()
    {
        _scrollPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // The editor's own ScrollToVerticalOffset doesn't move it.
                if (_log is not { } log || _editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroller)
                {
                    return;
                }
                var textView = _editor.TextArea.TextView;
                if (log.StayCurrent)
                {
                    scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, textView.DocumentHeight - scroller.Viewport.Height));
                }
                else if (log.TopLine is { } top && NumberOf(top) is { } number)
                {
                    scroller.Offset = new Vector(scroller.Offset.X, textView.GetVisualTopByDocumentLine(number));
                }
            }
            finally
            {
                _scrollPending = false;
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>The document's line for <paramref name="top"/>, or for the first shown after it; null when none is.</summary>
    private int? NumberOf(LogLine top)
    {
        if (_output is null || _log is not { } log || _shown.Count == 0)
        {
            return null;
        }
        var at = _output.IndexOf(top);
        if (at < 0)
        {
            // Dropped with the oldest lines: the start is the nearest.
            return 1;
        }
        for (var i = at; i < _output.Count; i++)
        {
            if (log.Shows(_output[i]) && _shown.IndexOf(_output[i]) is >= 0 and var shown)
            {
                return shown + 1;
            }
        }
        return null;
    }

    /// <summary>Scrolled by the user: Stay current says whether it's at the end, and the line at the top is remembered.</summary>
    private void OnScrolled()
    {
        if (_scrollPending || !_editor.IsEffectivelyVisible || _log is not { } log)
        {
            return;
        }
        var atEnd = IsAtEnd();
        if (!atEnd)
        {
            log.TopLine = FirstVisible();
        }
        if (log.StayCurrent != atEnd)
        {
            _settingFromScroll = true;
            try
            {
                log.StayCurrent = atEnd;
            }
            finally
            {
                _settingFromScroll = false;
            }
        }
    }

    private LogLine? FirstVisible()
    {
        var textView = _editor.TextArea.TextView;
        return _shown.Count == 0 ? null : LineAt(textView.GetDocumentLineByVisualTop(textView.ScrollOffset.Y).LineNumber);
    }

    /// <summary>A line of the log as one line of the document, so the two keep step.</summary>
    private static string OneLine(string line) => line.Contains('\n') || line.Contains('\r') ? line.ReplaceLineEndings(" ") : line;

    /// <summary>Scrolled to the newest line, within a few pixels.</summary>
    private bool IsAtEnd() => _editor.VerticalOffset + _editor.ViewportHeight >= _editor.TextArea.TextView.DocumentHeight - 4;

    /// <summary>Its filter changed: the lines shown afresh, once for a burst of changes.</summary>
    private void OnFilterChanged()
    {
        if (_refillPending)
        {
            return;
        }
        _refillPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refillPending = false;
            if (_output is not null)
            {
                Fill();
            }
        }, DispatcherPriority.Background);
    }

    private void OnLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LogTabViewModel.IsShown) when _log is { IsShown: true }:
                // Coming on screen: a log that was staying current picks up where it's got to.
                ComeBack();
                break;
            case nameof(LogTabViewModel.StayCurrent) when !_settingFromScroll && _log is { } log:
                // Its button: on goes to the newest line; off stays where it is.
                if (log.StayCurrent)
                {
                    ComeBack();
                }
                else
                {
                    log.TopLine = FirstVisible();
                }
                break;
            case nameof(LogTabViewModel.WordWrap):
                // Wrapped or not, the lines are other heights: back to the same place.
                ComeBack();
                break;
            case nameof(EditorLogViewModel.CategoryColumn) or nameof(EditorLogViewModel.HasTimeColumn):
                UpdateIndentation();
                _editor.TextArea.TextView.Redraw();
                break;
        }
    }

    /// <summary>A wrapped line's rows after the first start under its message, in Unreal's columns.</summary>
    private void UpdateIndentation() =>
        _editor.Options.WordWrapIndentation = _log is EditorLogViewModel { HasCategories: true } log
            ? _editor.TextArea.TextView.WideSpaceWidth * LogColumnsGenerator.MessageColumn(log)
            : 0;

    private void OnLinksFound() => _editor.TextArea.TextView.Redraw();

    /// <summary><c>Ctrl/Cmd+F</c>: the editor's find, with a selection of one line in its box.</summary>
    private void OnFindRequested()
    {
        var search = _editor.SearchPanel;
        if (_editor.SelectedText is { Length: > 0 } selected && !selected.Contains('\n'))
        {
            search.SearchPattern = selected;
        }
        search.Open();
        // The first time, its box is only there once the panel has laid out; until then Reactivate does nothing.
        Dispatcher.UIThread.Post(search.Reactivate);
    }

    private IBrush? Token(string key) => _view.TryFindResource(key, _view.ActualThemeVariant, out var value) ? value as IBrush : null;
}

/// <summary>
/// Colors an editor's log (DESIGN.md §18, "Editor logs", "Its lines"): an error's lines in the error color and a
/// warning's in the warning color, stack traces and verbose lines muted, and in Unreal's columns the time muted, the
/// category in the accent and the verbosity in its level's color. A run's lines have nothing to color.
/// </summary>
internal sealed class LogLineColorizer(Func<int, LogLine?> lineAt, Func<string, IBrush?> token) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length == 0 || lineAt(line.LineNumber) is not { } logLine)
        {
            return;
        }
        var columns = logLine.Columns is { IsCarriedOn: false } own ? own : null;
        var message = Math.Min(line.Length, columns?.MessageStart ?? 0);
        if (MessageBrush(logLine) is { } brush && message < line.Length)
        {
            Color(line.Offset + message, line.EndOffset, brush);
        }
        if (columns is null)
        {
            return;
        }
        if (columns.PrefixLength > 0 && token("MutedTextBrush") is { } muted)
        {
            Color(line.Offset, line.Offset + columns.PrefixLength, muted);
        }
        if (columns.CategoryLength > 0 && token("AccentStatusBrush") is { } accent)
        {
            Color(line.Offset + columns.PrefixLength, line.Offset + columns.PrefixLength + columns.CategoryLength, accent);
        }
        if (columns.TagStart >= 0 && TagBrush(logLine.Verbosity) is { } tag)
        {
            Color(line.Offset + columns.TagStart, line.Offset + columns.TagStart + columns.TagLength, tag);
        }
    }

    private void Color(int start, int end, IBrush brush) => ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));

    private IBrush? MessageBrush(LogLine line) =>
        line.IsTrace || line.Verbosity == LogVerbosity.Verbose ? token("MutedTextBrush") : TagBrush(line.Verbosity);

    private IBrush? TagBrush(LogVerbosity verbosity) => verbosity switch
    {
        LogVerbosity.Error => token("ErrorTextBrush"),
        LogVerbosity.Warning => token("WarningTextBrush"),
        LogVerbosity.Verbose => token("MutedTextBrush"),
        _ => null,
    };
}

/// <summary>
/// Shows an Unreal log's lines in columns (DESIGN.md §18, "Editor logs", "Its lines"): in place of
/// <c>[2026.10.20-19.01.22:123][  0]</c> the time in the machine's, then the category, padded to the longest's width,
/// then the verbosity, padded to its own, then the message. Only what's drawn changes: the document keeps the file's
/// text, which is what's selected, copied and found. A line carrying an entry on is indented to its message.
/// </summary>
internal sealed class LogColumnsGenerator(Func<int, LogLine?> lineAt, Func<EditorLogViewModel?> log) : VisualLineElementGenerator
{
    /// <summary>The time column's width in characters: <c>15:01:22.123</c>.</summary>
    public const int TimeColumn = 12;

    /// <summary>The verbosity column's: <c>Warning</c>, <c>Display</c>, <c>Verbose</c>.</summary>
    public const int TagColumn = 7;

    /// <summary>The space between two columns.</summary>
    public const int Gap = 2;

    /// <summary>What's drawn in place of the line's parts: where each starts in the line, how much of it, and the text.</summary>
    private readonly List<(int Start, int Length, string Text)> _parts = [];

    /// <summary>Where the message starts, in characters.</summary>
    public static int MessageColumn(EditorLogViewModel log) => TimeWidth(log) + log.CategoryColumn + Gap + TagColumn + Gap;

    private static int TimeWidth(EditorLogViewModel log) => log.HasTimeColumn ? TimeColumn + Gap : 0;

    public override void StartGeneration(ITextRunConstructionContext context)
    {
        base.StartGeneration(context);
        _parts.Clear();
        if (log() is not { HasCategories: true } editorLog || lineAt(context.VisualLine.FirstDocumentLine.LineNumber)?.Columns is not { } columns)
        {
            return;
        }
        if (columns.IsCarriedOn)
        {
            _parts.Add((0, 0, Spaces(MessageColumn(editorLog))));
            return;
        }
        var category = editorLog.CategoryColumn;
        var hasCategory = columns.CategoryLength > 0;
        var hasTag = columns.TagStart >= 0;
        // The columns the line has nothing for are blank.
        var blank = (hasCategory ? "" : Spaces(category + Gap)) + (hasCategory || hasTag ? "" : Spaces(TagColumn + Gap));
        if (columns.PrefixLength > 0)
        {
            _parts.Add((0, columns.PrefixLength, (columns.Time ?? "").PadRight(TimeColumn) + Spaces(Gap) + blank));
        }
        else if (Spaces(TimeWidth(editorLog)) + blank is { Length: > 0 } before)
        {
            _parts.Add((0, 0, before));
        }
        if (hasCategory)
        {
            // In place of the ": " after it.
            var after = Spaces(Math.Max(1, category - columns.CategoryLength + Gap)) + (hasTag ? "" : Spaces(TagColumn + Gap));
            _parts.Add((columns.PrefixLength + columns.CategoryLength, 2, after));
        }
        if (hasTag)
        {
            _parts.Add((columns.TagStart + columns.TagLength, 2, Spaces(Math.Max(1, TagColumn - columns.TagLength + Gap))));
        }
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var lineStart = CurrentContext.VisualLine.FirstDocumentLine.Offset;
        foreach (var part in _parts)
        {
            if (lineStart + part.Start >= startOffset)
            {
                return lineStart + part.Start;
            }
        }
        return -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var start = offset - CurrentContext.VisualLine.FirstDocumentLine.Offset;
        foreach (var part in _parts)
        {
            if (part.Start == start)
            {
                return new FormattedTextElement(part.Text, part.Length);
            }
        }
        return null;
    }

    private static string Spaces(int count) => new(' ', count);
}
