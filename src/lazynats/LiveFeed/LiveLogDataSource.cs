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
        var (text, subjectStart, subjectLength) = FeedRowFormatter.Format(_items[item]);
        var visible = viewportX < text.Length ? text[viewportX..] : string.Empty;
        if (visible.Length > width) visible = visible[..width];

        listView.Move(col, row);

        // Intersect the subject's text-space range with the visible window (already sliced
        // above) to split `visible` into up to three segments - only the middle one, if
        // non-empty, gets the subject color. Coordinates below are relative to `visible`.
        var subjectVisibleStart = Math.Max(subjectStart, viewportX) - viewportX;
        var subjectVisibleEnd = Math.Min(subjectStart + subjectLength, viewportX + visible.Length) - viewportX;

        if (subjectVisibleStart < subjectVisibleEnd) {
            listView.AddStr(visible[..subjectVisibleStart]);

            // Live feed's "Row Subject Text Is Colored" requirement: color only the subject
            // segment, composed with whatever attribute (normal or selected) ListView already
            // set for this row, so the highlight survives selection instead of assuming Normal.
            var prior = listView.GetCurrentAttribute();
            listView.SetAttribute(new Attribute(Theme.SubjectColor, prior.Background));
            listView.AddStr(visible[subjectVisibleStart..subjectVisibleEnd]);
            listView.SetAttribute(prior);

            listView.AddStr(visible[subjectVisibleEnd..].PadRight(width - subjectVisibleEnd));
        } else {
            listView.AddStr(visible.PadRight(width));
        }
    }

    public void Dispose() => _items.CollectionChanged -= OnCollectionChanged;
}
