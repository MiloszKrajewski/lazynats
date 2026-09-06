using System.Collections.ObjectModel;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace lazynats.Components;

// Generalizes the "presenter-formatted list with New/Edit/Delete" shape duplicated between
// SubscriptionsView and PublishView's header editor. Row formatting is delegated to an injected
// presenter; New/Edit are delegated to abstract TryCreate/TryEdit callbacks so a subclass can run
// whatever modal its item type needs (a single field today, potentially several later) without
// this base class needing to know what that modal looks like. Obtaining a value (TryCreate/TryEdit)
// is kept separate from committing it (Add/Replace/Delete, all overridable with a default that
// mutates the item collection directly) so a subclass whose true source of truth lives elsewhere
// (e.g. SubscriptionsView redirecting into a NATS subscription registry) can commit there instead,
// without the base class also mutating the item collection on its behalf.
internal abstract class ListEditorView<T>: View, IShortcutSource
{
    private readonly ObservableCollection<T> _items;
    private readonly PresenterListDataSource<T> _dataSource;
    private readonly Terminal.Gui.Views.ListView _listView;

    public ListEditorView(ObservableCollection<T> items, IValuePresenter<T> presenter)
    {
        CanFocus = true;
        _items = items;

        _dataSource = new PresenterListDataSource<T>(_items, presenter);
        _listView = new Terminal.Gui.Views.ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.Source = _dataSource;

        // Bound here (on the whole component), not on the list, so Ctrl+N/E/D work no matter
        // which child currently has focus - same rationale as PublishView.
        AddCommand(Command.New, () => { TryCreateItem(); return true; });
        AddCommand(Command.Edit, () => { if (SelectedIndex is { } index) TryEditItem(index); return true; });
        AddCommand(Command.DeleteAll, () => { if (SelectedIndex is { } index) Delete(index); return true; });
        KeyBindings.Add(Key.N.WithCtrl, Command.New);
        KeyBindings.Add(Key.E.WithCtrl, Command.Edit);
        KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);

        Add(_listView);
    }

    private int? SelectedIndex =>
        _listView.SelectedItem is { } index && index >= 0 && index < _items.Count ? index : null;

    // Run a modal appropriate to T and report whether the user committed a new value.
    protected abstract bool TryCreate(out T result);

    // Run a modal (seeded from `original`) appropriate to T and report whether the user committed
    // an edited value.
    protected abstract bool TryEdit(T original, out T result);

    protected virtual void Add(T value) => _items.Add(value);
    protected virtual void Replace(int index, T value) => _items[index] = value;
    protected virtual void Delete(int index) => _items.RemoveAt(index);

    private void TryCreateItem()
    {
        if (TryCreate(out var result)) Add(result);
    }

    private void TryEditItem(int index)
    {
        if (TryEdit(_items[index], out var result)) Replace(index, result);
    }

    public virtual IEnumerable<ShortcutHint> Shortcuts =>
    [
        new ShortcutHint(Key.N.WithCtrl, "New", TryCreateItem),
        new ShortcutHint(Key.E.WithCtrl, "Edit", () => { if (SelectedIndex is { } index) TryEditItem(index); }),
        new ShortcutHint(Key.D.WithCtrl, "Delete", () => { if (SelectedIndex is { } index) Delete(index); }),
    ];

    protected override void Dispose(bool disposing)
    {
        if (disposing) _dataSource.Dispose();
        base.Dispose(disposing);
    }
}
