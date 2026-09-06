using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Terminal.Gui.Views;

namespace lazynats;

// Renders each item via the same presenter ListEditorView<T> uses for text conversion, so the
// list shows exactly what Ctrl+E would load back into the input. MaxItemLength is 0 to avoid an
// O(n^2) rescan on append, same rationale as LiveLogDataSource.
internal sealed class PresenterListDataSource<T>: IListDataSource
{
    private readonly ObservableCollection<T> _items;
    private readonly IValuePresenter<T> _presenter;
    private NotifyCollectionChangedEventHandler? _collectionChanged;

    public PresenterListDataSource(ObservableCollection<T> items, IValuePresenter<T> presenter)
    {
        _items = items;
        _presenter = presenter;
        _items.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _collectionChanged?.Invoke(this, e);

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
        var text = _presenter.Format(_items[item]);
        var visible = viewportX < text.Length ? text[viewportX..] : string.Empty;
        if (visible.Length > width) visible = visible[..width];
        listView.Move(col, row);
        listView.AddStr(visible.PadRight(width));
    }

    public void Dispose() => _items.CollectionChanged -= OnCollectionChanged;
}
