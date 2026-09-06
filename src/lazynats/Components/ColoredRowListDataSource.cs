using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Terminal.Gui.Views;

namespace lazynats.Components;

// Static/bound ObservableCollection<ColoredRow> source, modeled on PresenterListDataSource<T> -
// same shape, delegating Render to the shared ColoredRowRenderer instead of a presenter. Used by
// ShortcutPickerDialog to hand ListView pre-built colored rows, the same way LiveLogDataSource
// does for FeedEnvelopes. MaxItemLength is 0, same rationale as PresenterListDataSource/
// LiveLogDataSource (avoid an O(n^2) rescan on append/mutation).
internal sealed class ColoredRowListDataSource: IListDataSource
{
    private readonly ObservableCollection<ColoredRow> _items;

    public ColoredRowListDataSource(ObservableCollection<ColoredRow> items)
    {
        _items = items;
        _items.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        CollectionChanged?.Invoke(this, e);

    public int Count => _items.Count;
    public int MaxItemLength => 0;
    public bool SuspendCollectionChangedEvent { get; set; }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public bool IsMarked(int item) => false;
    public void SetMark(int item, bool value) { }
    public bool RenderMark(ListView listView, int item, int row, bool isMarked, bool markMultiple) => false;
    public IList ToList() => _items;

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX) =>
        ColoredRowRenderer.Render(listView, _items[item], col, row, width, viewportX);

    public void Dispose() => _items.CollectionChanged -= OnCollectionChanged;
}
