using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Terminal.Gui.Views;

namespace lazynats;

// ListView's default data source (ListWrapper<T>, wired up by SetSource) recomputes
// MaxItemLength by rescanning the whole collection on every single mutation, making
// sustained appends O(n^2). Lines here can be arbitrarily long and screen width is
// the only real limit, so MaxItemLength is just reported as 0 (no horizontal scroll)
// instead of tracked, keeping every mutation O(1).
internal sealed class LiveLogDataSource: IListDataSource
{
    private readonly ObservableCollection<FeedEnvelope> _items;
    private NotifyCollectionChangedEventHandler? _collectionChanged;

    public LiveLogDataSource(ObservableCollection<FeedEnvelope> items)
    {
        _items = items;
        _items.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!SuspendCollectionChangedEvent) _collectionChanged?.Invoke(this, e);
    }

    public int Count => _items.Count;
    public int MaxItemLength => 0;
    public bool SuspendCollectionChangedEvent { get; set; }

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add => _collectionChanged += value;
        remove => _collectionChanged -= value;
    }

    public bool IsMarked(int item) => false;
    public void SetMark(int item, bool value) { }
    public bool RenderMark(ListView listView, int item, int row, bool isMarked, bool markMultiple) => false;
    public IList ToList() => _items;

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX)
    {
        var text = FeedRowFormatter.Format(_items[item]);
        var visible = viewportX < text.Length ? text[viewportX..] : string.Empty;
        if (visible.Length > width) visible = visible[..width];
        listView.Move(col, row);
        listView.AddStr(visible.PadRight(width));
    }

    public void Dispose() => _items.CollectionChanged -= OnCollectionChanged;
}
