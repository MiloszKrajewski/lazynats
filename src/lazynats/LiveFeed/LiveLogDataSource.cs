using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.LiveFeed;

// ListView's default data source (ListWrapper<T>, wired up by SetSource) recomputes
// MaxItemLength by rescanning the whole collection on every single mutation, making
// sustained appends O(n^2). Lines here can be arbitrarily long and screen width is
// the only real limit, so MaxItemLength is just reported as 0 (no horizontal scroll)
// instead of tracked, keeping every mutation O(1).
internal sealed class LiveLogDataSource: IListDataSource
{
    private readonly ObservableCollection<FeedEnvelope> _items;

    public LiveLogDataSource(ObservableCollection<FeedEnvelope> items)
    {
        _items = items;
        _items.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!SuspendCollectionChangedEvent) 
            CollectionChanged?.Invoke(this, e);
    }

    public int Count => _items.Count;
    public int MaxItemLength => 0;
    public bool SuspendCollectionChangedEvent { get; set; }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public bool IsMarked(int item) => false;
    public void SetMark(int item, bool value) { }
    public bool RenderMark(ListView listView, int item, int row, bool isMarked, bool markMultiple) => false;
    public IList ToList() => _items;

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX)
    {
        var segments = FeedRowFormatter.Format(_items[item]).Segments;
        var viewportEnd = viewportX + width;

        listView.Move(col, row);

        // Walk segments in order, tracking a running cursor (position in the row's full,
        // unclipped text) - each segment picks up exactly where the previous left off, so
        // [cursor, cursor + segment.Text.Length) is its text-space range. Intersect that with
        // the visible window and slice/draw only the intersected portion, under the segment's
        // own color (capture-and-restore, doc/multi-color-rendering.md idiom 2) when set, or the
        // attribute ListView already put in place for this row (normal or selected) when null -
        // see live-feed's "Row Subject/Header Text Is Colored" and "Row Payload Type Prefix Is
        // Colored" requirements.
        var cursor = 0;
        var written = 0;
        foreach (var segment in segments)
        {
            var segmentStart = cursor;
            var segmentEnd = cursor + segment.Text.Length;
            cursor = segmentEnd;

            var visibleStart = Math.Max(segmentStart, viewportX);
            var visibleEnd = Math.Min(segmentEnd, viewportEnd);
            if (visibleStart >= visibleEnd) continue;

            var slice = segment.Text.Substring(visibleStart - segmentStart, visibleEnd - visibleStart);

            if (segment.Color is { } color)
            {
                var prior = listView.GetCurrentAttribute();
                listView.SetAttribute(new Attribute(color, prior.Background));
                listView.AddStr(slice);
                listView.SetAttribute(prior);
            }
            else
            {
                listView.AddStr(slice);
            }

            written += slice.Length;
        }

        if (written < width) listView.AddStr(new string(' ', width - written));
    }

    public void Dispose() => _items.CollectionChanged -= OnCollectionChanged;
}
