using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats;

internal readonly record struct HeaderPair(string Key, string Value);

// Alternates row background (even/odd) to visually separate header pairs without a
// FrameView border, per the flat styling used throughout the Publish tab.
internal sealed class HeaderListDataSource: IListDataSource
{
    private static readonly Attribute EvenRow = new(ColorName16.White, ColorName16.DarkGray);
    private static readonly Attribute OddRow = new(ColorName16.White, ColorName16.Black);

    private readonly ObservableCollection<HeaderPair> _items;
    private NotifyCollectionChangedEventHandler? _collectionChanged;

    public HeaderListDataSource(ObservableCollection<HeaderPair> items)
    {
        _items = items;
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
        var pair = _items[item];
        var text = $"{pair.Key}   {pair.Value}";
        if (!selected) listView.SetAttribute(item % 2 == 0 ? EvenRow : OddRow);

        var visible = viewportX < text.Length ? text[viewportX..] : string.Empty;
        if (visible.Length > width) visible = visible[..width];
        listView.Move(col, row);
        listView.AddStr(visible.PadRight(width));
    }

    public void Dispose() => _items.CollectionChanged -= OnCollectionChanged;
}
