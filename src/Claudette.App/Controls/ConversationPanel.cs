using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Claudette.App.Conversation;

namespace Claudette.App.Controls;

/// <summary>
/// The conversation's items one above the other, with controls only for those in view and <see cref="CacheLength"/>
/// screens either side (DESIGN.md §5). It remembers each item's height once it has measured it, and places the items
/// by those heights, so an item that changes height (a reply streaming in, a card opening) moves only the items after
/// it. While the UI is otherwise idle it builds the items it hasn't measured at this width, half a screen's worth at a
/// time, to learn their heights, so where each item is, and the marks in the scroll bar, stop moving as the user
/// scrolls.
/// Until then they're taken to be the average height of those it has measured.
/// </summary>
/// <remarks>
/// Avalonia's <see cref="VirtualizingStackPanel"/> loses its place whenever one of its built items changes height, and
/// finds it again from the items' average height. A conversation has one-line rows beside replies a page long, so it
/// found the wrong place: while a reply streamed in, it rebuilt the messages around it on every frame, and each rebuilt
/// reply blanked until its Markdown was parsed again.
/// </remarks>
public sealed class ConversationPanel : VirtualizingPanel
{
    public static readonly StyledProperty<double> CacheLengthProperty =
        AvaloniaProperty.Register<ConversationPanel, double>(nameof(CacheLength));

    /// <summary>The pool a container goes back to; <see cref="OwnContainer"/> for an item that is a control itself.</summary>
    private static readonly AttachedProperty<object?> RecycleKeyProperty =
        AvaloniaProperty.RegisterAttached<ConversationPanel, Control, object?>("RecycleKey");

    private static readonly object OwnContainer = new();

    /// <summary>The height an item is taken to have before any has been measured.</summary>
    private const double FirstEstimate = 40;

    /// <summary>Each item's height when it was last measured; NaN for one not measured yet.</summary>
    private readonly List<double> _heights = [];

    /// <summary>The width each item was last measured at; NaN for one not measured yet.</summary>
    private readonly List<double> _widths = [];

    private double _measuredTotal;
    private int _measuredCount;

    /// <summary>
    /// The height of an item not measured yet: the average of the measured ones, worked out again only when another item
    /// is measured for the first time. A reply growing as it streams in doesn't move the items above it.
    /// </summary>
    private double _estimate = FirstEstimate;

    /// <summary>Where each item starts, from <see cref="_heights"/>; after the last item, where it ends.</summary>
    private double[] _tops = new double[16];

    /// <summary>The controls of the items that have them, from <see cref="_firstIndex"/>; null for an item added among them since.</summary>
    private List<Control?> _realized = [];
    private int _firstIndex;

    /// <summary>While measuring, the controls from the measure before that haven't been taken yet.</summary>
    private List<Control?> _previous = [];
    private int _previousFirst;

    private readonly Dictionary<object, Stack<Control>> _recyclePool = [];

    /// <summary>What's in view, in this panel's coordinates; empty until the layout says.</summary>
    private Rect _viewport;

    /// <summary>The part of the panel the last measure built controls for, and the panel's height then.</summary>
    private double _builtTop, _builtBottom, _extent;

    private double _lastWidth = double.NaN;
    private IScrollAnchorProvider? _anchors;
    private bool _isInLayout;

    /// <summary>A container being prepared: the ItemsControl's ContainerPrepared handlers can already ask for its index.</summary>
    private Control? _preparing;
    private int _preparingIndex = -1;

    /// <summary>A control <see cref="ScrollIntoView"/> made outside the built range, until a measure takes it in.</summary>
    private Control? _scrollTo;
    private int _scrollToIndex = -1;

    static ConversationPanel() => AffectsMeasure<ConversationPanel>(CacheLengthProperty);

    public ConversationPanel() => EffectiveViewportChanged += OnEffectiveViewportChanged;

    /// <summary>How many screens of items above and below the view keep their controls.</summary>
    public double CacheLength
    {
        get => GetValue(CacheLengthProperty);
        set => SetValue(CacheLengthProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _anchors = this.FindAncestorOfType<IScrollAnchorProvider>();
    }

    /// <summary>Such as a subagent group's panel when the group's row is recycled: the scroll viewer forgets its items.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var element in _realized)
        {
            if (element is not null)
            {
                _anchors?.UnregisterAnchorCandidate(element);
            }
        }
        _anchors = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Items;
        if (_heights.Count != items.Count)
        {
            ResetItems(items.Count);
        }
        if (items.Count == 0)
        {
            (_builtTop, _builtBottom, _extent) = (0, 0, 0);
            return default;
        }

        _isInLayout = true;
        try
        {
            _lastWidth = availableSize.Width;
            UpdateTops();
            var (top, bottom) = Window();
            var first = IndexAt(top);

            (_previous, _realized) = (_realized, _previous);
            (_previousFirst, _firstIndex) = (_firstIndex, first);
            _realized.Clear();
            // The controls above the window go first, so the items coming into it can have them.
            for (var k = 0; k < _previous.Count && _previousFirst + k < first; k++)
            {
                if (_previous[k] is { } above)
                {
                    Recycle(above);
                    _previous[k] = null;
                }
            }

            // From the first item in the window down to its bottom, by the heights the items measure now.
            var width = 0.0;
            var index = first;
            var y = _tops[first];
            do
            {
                var element = GetOrCreate(items, index);
                _realized.Add(element);
                // A step folded into its chain's line is built but not shown, so it measures nothing.
                element.SetCurrentValue(IsVisibleProperty, items[index] is not ConversationItem { IsFolded: true });
                element.Measure(availableSize);
                SetHeight(index, element.DesiredSize.Height);
                width = Math.Max(width, element.DesiredSize.Width);
                y += element.DesiredSize.Height;
                index++;
            }
            while (index < items.Count && y < bottom);

            foreach (var below in _previous)
            {
                if (below is not null)
                {
                    Recycle(below);
                }
            }
            _previous.Clear();

            UpdateTops();
            (_builtTop, _builtBottom, _extent) = (_tops[first], _tops[index], _tops[items.Count]);
            QueueSurvey();
            return new Size(width, _extent);
        }
        finally
        {
            _isInLayout = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _isInLayout = true;
        try
        {
            for (var k = 0; k < _realized.Count; k++)
            {
                if (_realized[k] is not { } element)
                {
                    continue;
                }
                var rect = new Rect(0, _tops[_firstIndex + k], finalSize.Width, element.DesiredSize.Height);
                element.Arrange(rect);
                // The scroll viewer keeps the item nearest the top of the view where it is when items above it change
                // height. Every built item is offered, and it picks from those in view as the view is now: _viewport
                // hears of a jump only after the layout, so offering only the items in it left none to keep still on
                // the pass after the conversation first went to its end, and what was in view slid down.
                if (element.IsVisible)
                {
                    _anchors?.RegisterAnchorCandidate(element);
                }
            }
            _scrollTo?.Arrange(new Rect(0, _tops[_scrollToIndex], finalSize.Width, _scrollTo.DesiredSize.Height));
            return finalSize;
        }
        finally
        {
            _isInLayout = false;
            Arranged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The items have their places again: the scroll bar's marks follow them (DESIGN.md §18, "Conversation cards").</summary>
    public event EventHandler? Arranged;

    /// <summary>
    /// Where item <paramref name="index"/> starts, in this panel: where it was laid out, or, for an item not measured
    /// yet, where the heights it's taken to have put it. The end of the last item for the item count.
    /// </summary>
    public double TopOf(int index) => index >= 0 && index <= _heights.Count && index < _tops.Length ? _tops[index] : ItemsHeight;

    /// <summary>The items' height in all, as last laid out.</summary>
    public double ItemsHeight => _heights.Count > 0 && _heights.Count < _tops.Length ? _tops[_heights.Count] : 0;

    /// <summary>The item at <paramref name="y"/> in this panel: the last one starting at or above it; -1 with none.</summary>
    public int ItemAt(double y) => _heights.Count == 0 ? -1 : IndexAt(y);

    /// <summary>The view and <see cref="CacheLength"/> screens either side; only the start until the view is known.</summary>
    private (double Top, double Bottom) Window()
    {
        if (_viewport.Height <= 0)
        {
            return (0, 0);
        }
        var cache = _viewport.Height * CacheLength;
        return (Math.Max(0, _viewport.Top - cache), _viewport.Bottom + cache);
    }

    /// <summary>A new measure when the view, with half its cache either side, reaches past what's built.</summary>
    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var viewport = e.EffectiveViewport;
        var resized = viewport.Size != _viewport.Size;
        _viewport = viewport;
        var margin = viewport.Height * CacheLength / 2;
        if (resized
            || Math.Max(0, viewport.Top - margin) < _builtTop
            || Math.Min(_extent, viewport.Bottom + margin) > _builtBottom)
        {
            InvalidateMeasure();
        }
    }

    // ---- Heights and places ---------------------------------------------------------------------------------------

    /// <param name="estimate">Whether an item measured for the first time changes <see cref="_estimate"/> now.</param>
    private void SetHeight(int index, double height, bool estimate = true)
    {
        var old = _heights[index];
        _heights[index] = height;
        _widths[index] = _lastWidth;
        if (double.IsNaN(old))
        {
            _measuredTotal += height;
            _measuredCount++;
            if (estimate)
            {
                _estimate = Average();
            }
        }
        else
        {
            _measuredTotal += height - old;
        }
    }

    /// <summary>
    /// The measured items' average height, to the screen's pixel, as their own heights are, so every item starts on a
    /// pixel. Starting between them, an item was placed a pixel off by the scroll viewer keeping the view still and then
    /// put back by the conversation following its end: the newest message shook as each pass measured more.
    /// </summary>
    private double Average() =>
        LayoutHelper.RoundLayoutValue(_measuredTotal / Math.Max(1, _measuredCount), LayoutHelper.GetLayoutScale(this));

    private void UpdateTops()
    {
        var count = _heights.Count;
        if (_tops.Length < count + 1)
        {
            Array.Resize(ref _tops, Math.Max(count + 1, _tops.Length * 2));
        }
        var items = Items;
        var y = 0.0;
        for (var i = 0; i < count; i++)
        {
            _tops[i] = y;
            var height = _heights[i];
            if (i < items.Count && items[i] is ConversationItem item)
            {
                // A step folded into its chain's line takes no room; one just unfolded is measured again.
                if (item.IsFolded != item.FoldSeen)
                {
                    item.FoldSeen = item.IsFolded;
                    _widths[i] = double.NaN;
                }
                if (item.IsFolded)
                {
                    height = 0;
                }
                else if (height == 0)
                {
                    height = double.NaN;
                }
            }
            y += double.IsNaN(height) ? _estimate : height;
        }
        _tops[count] = y;
    }

    /// <summary>The item at <paramref name="y"/>: the last one starting at or above it.</summary>
    private int IndexAt(double y)
    {
        var (low, high) = (0, _heights.Count - 1);
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_tops[middle] <= y)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        return low;
    }

    // ---- Learning the heights of the items out of view -------------------------------------------------------------

    /// <summary>A pass of <see cref="Survey"/> is waiting for the UI to be idle.</summary>
    private bool _surveyQueued;

    /// <summary>Every item has been measured at the width the panel was last measured at.</summary>
    internal bool KnowsEveryHeight => NextToSurvey(0) < 0;

    /// <summary>After a measure, while there's an item it hasn't measured at this width: a pass when the UI is idle.</summary>
    private void QueueSurvey()
    {
        if (!_surveyQueued && _viewport.Height > 0 && !KnowsEveryHeight)
        {
            _surveyQueued = true;
            Dispatcher.UIThread.Post(Survey, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Builds the next half screen's worth of items not measured at this width, from the top, measures them and puts
    /// their controls back. The measure that follows places the items by their heights, and queues the next pass.
    /// </summary>
    /// <remarks>
    /// The items above the view changing height move it: the scroll viewer keeps what's in view where it is, by the
    /// items built around it. So the measure that follows builds around where the view will be, not where it was,
    /// keeping those items built however far the pass moved them. An item measured in a pass leaves the height taken
    /// for the items not measured yet as it was, so the pass moves only the items it measured, and those after them.
    /// </remarks>
    private void Survey()
    {
        _surveyQueued = false;
        var items = Items;
        if (_heights.Count != items.Count || _viewport.Height <= 0 || double.IsNaN(_lastWidth)
            || !IsEffectivelyVisible || TopLevel.GetTopLevel(this) is null)
        {
            // Out of sight, such as a tab in the background: the measure when it shows again carries on.
            return;
        }
        var size = new Size(_lastWidth, double.PositiveInfinity);
        var (measured, moved) = (0.0, 0.0);
        for (var index = NextToSurvey(0); index >= 0 && measured < _viewport.Height / 2; index = NextToSurvey(index + 1))
        {
            var old = double.IsNaN(_heights[index]) ? _estimate : _heights[index];
            var element = GetOrCreate(items, index);
            element.SetCurrentValue(IsVisibleProperty, items[index] is not ConversationItem { IsFolded: true });
            element.Measure(size);
            var height = element.DesiredSize.Height;
            SetHeight(index, height, estimate: false);
            Recycle(element);
            measured += height;
            if (index < _firstIndex)
            {
                moved += height - old;
            }
        }
        _viewport = _viewport.Translate(new Vector(0, moved));
        if (KnowsEveryHeight)
        {
            // The last of them: an item added later, not measured yet, is taken to be the average of all of them.
            _estimate = Average();
        }
        InvalidateMeasure();
    }

    /// <summary>The first item from <paramref name="from"/> not measured at this width, leaving out those the measure builds; -1 for none.</summary>
    private int NextToSurvey(int from)
    {
        var builtEnd = _firstIndex + _realized.Count;
        for (var i = from; i < _widths.Count; i++)
        {
            // NaN for an item not measured yet, which never equals a width.
            if (_widths[i] != _lastWidth && (i < _firstIndex || i >= builtEnd) && i != _scrollToIndex)
            {
                return i;
            }
        }
        return -1;
    }

    // ---- The items changing ---------------------------------------------------------------------------------------

    protected override void OnItemsControlChanged(ItemsControl? oldValue)
    {
        base.OnItemsControlChanged(oldValue);
        // The ItemsControl clears the children; the controls kept for later were its.
        _realized.Clear();
        _previous.Clear();
        _recyclePool.Clear();
        _heights.Clear();
        _widths.Clear();
        (_measuredTotal, _measuredCount, _firstIndex) = (0, 0, 0);
        (_scrollTo, _scrollToIndex) = (null, -1);
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items, e);
        InvalidateMeasure();
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewStartingIndex >= 0:
                Insert(e.NewStartingIndex, e.NewItems!.Count);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldStartingIndex >= 0:
                Remove(e.OldStartingIndex, e.OldItems!.Count);
                break;
            case NotifyCollectionChangedAction.Replace when e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0:
            case NotifyCollectionChangedAction.Move when e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldItems!.Count == 1:
                Remove(e.OldStartingIndex, e.OldItems!.Count);
                Insert(e.NewStartingIndex, e.NewItems!.Count);
                break;
            default:
                ResetItems(items.Count);
                break;
        }
    }

    private void Insert(int index, int count)
    {
        _heights.InsertRange(index, Enumerable.Repeat(double.NaN, count));
        _widths.InsertRange(index, Enumerable.Repeat(double.NaN, count));
        if (index <= _firstIndex)
        {
            for (var k = 0; k < _realized.Count; k++)
            {
                IndexChanged(_realized[k], _firstIndex + k, _firstIndex + k + count);
            }
            _firstIndex += count;
        }
        else if (index < _firstIndex + _realized.Count)
        {
            var at = index - _firstIndex;
            for (var k = at; k < _realized.Count; k++)
            {
                IndexChanged(_realized[k], _firstIndex + k, _firstIndex + k + count);
            }
            _realized.InsertRange(at, Enumerable.Repeat<Control?>(null, count));
        }
        if (_scrollTo is not null && index <= _scrollToIndex)
        {
            _scrollToIndex += count;
        }
    }

    private void Remove(int index, int count)
    {
        for (var i = index; i < index + count; i++)
        {
            if (!double.IsNaN(_heights[i]))
            {
                _measuredTotal -= _heights[i];
                _measuredCount--;
            }
        }
        _heights.RemoveRange(index, count);
        _widths.RemoveRange(index, count);

        var removeFrom = Math.Clamp(index - _firstIndex, 0, _realized.Count);
        var removeTo = Math.Clamp(index + count - _firstIndex, 0, _realized.Count);
        for (var k = removeFrom; k < removeTo; k++)
        {
            if (_realized[k] is { } removed)
            {
                RecycleRemoved(removed);
            }
        }
        _realized.RemoveRange(removeFrom, removeTo - removeFrom);
        var first = index < _firstIndex ? Math.Max(index, _firstIndex - count) : _firstIndex;
        for (var k = removeFrom; k < _realized.Count; k++)
        {
            IndexChanged(_realized[k], _firstIndex + k + (removeTo - removeFrom), first + k);
        }
        _firstIndex = first;

        if (_scrollTo is { } scrollTo && index <= _scrollToIndex)
        {
            if (_scrollToIndex < index + count)
            {
                RecycleRemoved(scrollTo);
                (_scrollTo, _scrollToIndex) = (null, -1);
            }
            else
            {
                _scrollToIndex -= count;
            }
        }
    }

    private void ResetItems(int count)
    {
        foreach (var element in _realized)
        {
            if (element is not null)
            {
                RecycleRemoved(element);
            }
        }
        _realized.Clear();
        if (_scrollTo is { } scrollTo)
        {
            RecycleRemoved(scrollTo);
            (_scrollTo, _scrollToIndex) = (null, -1);
        }
        _heights.Clear();
        _heights.AddRange(Enumerable.Repeat(double.NaN, count));
        _widths.Clear();
        _widths.AddRange(Enumerable.Repeat(double.NaN, count));
        (_measuredTotal, _measuredCount, _firstIndex) = (0, 0, 0);
    }

    private void IndexChanged(Control? element, int oldIndex, int newIndex)
    {
        if (element is not null && oldIndex != newIndex)
        {
            ItemContainerGenerator!.ItemContainerIndexChanged(element, oldIndex, newIndex);
        }
    }

    // ---- Containers -----------------------------------------------------------------------------------------------

    private Control GetOrCreate(IReadOnlyList<object?> items, int index)
    {
        var k = index - _previousFirst;
        if (k >= 0 && k < _previous.Count && _previous[k] is { } kept)
        {
            _previous[k] = null;
            return kept;
        }
        if (index == _scrollToIndex && _scrollTo is { } scrollTo)
        {
            (_scrollTo, _scrollToIndex) = (null, -1);
            return scrollTo;
        }

        var item = items[index];
        var generator = ItemContainerGenerator!;
        if (!generator.NeedsContainer(item, index, out var recycleKey))
        {
            return ItemAsOwnContainer((Control)item!, index);
        }
        _preparingIndex = index;
        try
        {
            Control container;
            if (recycleKey is not null && _recyclePool.TryGetValue(recycleKey, out var pool) && pool.Count > 0)
            {
                container = _preparing = pool.Pop();
                container.SetCurrentValue(IsVisibleProperty, true);
            }
            else
            {
                container = _preparing = generator.CreateContainer(item, index, recycleKey);
                container.SetValue(RecycleKeyProperty, recycleKey);
            }
            generator.PrepareItemContainer(container, item, index);
            AddInternalChild(container);
            generator.ItemContainerPrepared(container, item, index);
            return container;
        }
        finally
        {
            (_preparing, _preparingIndex) = (null, -1);
        }
    }

    private Control ItemAsOwnContainer(Control item, int index)
    {
        if (!item.IsSet(RecycleKeyProperty))
        {
            var generator = ItemContainerGenerator!;
            generator.PrepareItemContainer(item, item, index);
            AddInternalChild(item);
            item.SetValue(RecycleKeyProperty, OwnContainer);
            generator.ItemContainerPrepared(item, item, index);
        }
        item.SetCurrentValue(IsVisibleProperty, true);
        return item;
    }

    /// <summary>An item left the window: its container goes back to the pool.</summary>
    private void Recycle(Control element)
    {
        _anchors?.UnregisterAnchorCandidate(element);
        var recycleKey = element.GetValue(RecycleKeyProperty);
        if (recycleKey == OwnContainer)
        {
            element.SetCurrentValue(IsVisibleProperty, false);
        }
        else
        {
            Release(element, recycleKey);
        }
    }

    /// <summary>An item left the collection.</summary>
    private void RecycleRemoved(Control element)
    {
        _anchors?.UnregisterAnchorCandidate(element);
        var recycleKey = element.GetValue(RecycleKeyProperty);
        if (recycleKey == OwnContainer)
        {
            RemoveInternalChild(element);
        }
        else
        {
            Release(element, recycleKey);
        }
    }

    private void Release(Control element, object? recycleKey)
    {
        ItemContainerGenerator!.ClearItemContainer(element);
        if (recycleKey is not null)
        {
            if (!_recyclePool.TryGetValue(recycleKey, out var pool))
            {
                _recyclePool[recycleKey] = pool = new Stack<Control>();
            }
            pool.Push(element);
            element.SetCurrentValue(IsVisibleProperty, false);
        }
        RemoveInternalChild(element);
    }

    // ---- What the ItemsControl asks -------------------------------------------------------------------------------

    protected override Control? ContainerFromIndex(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return null;
        }
        if (index == _scrollToIndex)
        {
            return _scrollTo;
        }
        if (index == _preparingIndex)
        {
            return _preparing;
        }
        if ((Slot(_realized, _firstIndex, index) ?? Slot(_previous, _previousFirst, index)) is { } built)
        {
            return built;
        }
        return Items[index] is Control control && control.GetValue(RecycleKeyProperty) == OwnContainer ? control : null;
    }

    private static Control? Slot(List<Control?> controls, int first, int index) =>
        index - first is var k && k >= 0 && k < controls.Count ? controls[k] : null;

    protected override int IndexFromContainer(Control container)
    {
        if (ReferenceEquals(container, _scrollTo))
        {
            return _scrollToIndex;
        }
        if (ReferenceEquals(container, _preparing))
        {
            return _preparingIndex;
        }
        var k = _realized.IndexOf(container);
        if (k >= 0)
        {
            return _firstIndex + k;
        }
        k = _previous.IndexOf(container);
        return k >= 0 ? _previousFirst + k : -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers() => _realized.OfType<Control>();

    /// <summary>
    /// Builds the item's control, if it hasn't one, where the item goes, and has the scroll viewers bring it into view;
    /// the measure that follows takes it in with the items around it.
    /// </summary>
    protected override Control? ScrollIntoView(int index)
    {
        var items = Items;
        if (_isInLayout || index < 0 || index >= items.Count || !IsEffectivelyVisible || TopLevel.GetTopLevel(this) is null)
        {
            return null;
        }
        if (Slot(_realized, _firstIndex, index) is { } built)
        {
            built.BringIntoView();
            return built;
        }

        var element = GetOrCreate(items, index);
        (_scrollTo, _scrollToIndex) = (element, index);
        element.Measure(new Size(double.IsNaN(_lastWidth) ? Bounds.Width : _lastWidth, double.PositiveInfinity));
        SetHeight(index, element.DesiredSize.Height);
        UpdateTops();
        element.Arrange(new Rect(0, _tops[index], Bounds.Width, element.DesiredSize.Height));
        // The panel's height takes in the item's, so the scroll viewer can reach it.
        InvalidateMeasure();
        UpdateLayout();
        element.BringIntoView();
        UpdateLayout();
        // The items built above it can have moved it.
        element.BringIntoView();

        if (ReferenceEquals(_scrollTo, element))
        {
            // Nothing took it in, such as when the panel has no room: it isn't left behind unlisted.
            (_scrollTo, _scrollToIndex) = (null, -1);
            Recycle(element);
            return null;
        }
        return element;
    }

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        var count = Items.Count;
        var fromControl = from as Control;
        if (count == 0 || fromControl is null && direction is not NavigationDirection.First and not NavigationDirection.Last)
        {
            return null;
        }
        var fromIndex = fromControl is null ? -1 : IndexFromContainer(fromControl);
        var toIndex = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => count - 1,
            NavigationDirection.Next or NavigationDirection.Down => fromIndex + 1,
            NavigationDirection.Previous or NavigationDirection.Up => fromIndex - 1,
            NavigationDirection.Left or NavigationDirection.Right => fromIndex,
            _ => -2,
        };
        if (toIndex == -2)
        {
            return null;
        }
        if (toIndex == fromIndex)
        {
            return from;
        }
        if (wrap)
        {
            toIndex = toIndex < 0 ? count - 1 : toIndex >= count ? 0 : toIndex;
        }
        return ScrollIntoView(toIndex);
    }
}
