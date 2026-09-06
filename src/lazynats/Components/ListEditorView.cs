using System.Collections.ObjectModel;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Components;

internal abstract class ListEditorView: View
{
    protected static readonly Attribute InvalidInputAttribute = 
        new(ColorName16.Red, ColorName16.DarkGray);
}

// Generalizes the "text input + editable list" shape duplicated between SubscriptionsView and
// PublishView's header editor. Text<->T conversion is delegated to an injected presenter; Append/
// Edit/Delete/ClearInput are separately overridable so a subclass can change what an action does
// without needing to change how T is parsed/formatted.
internal class ListEditorView<T>: ListEditorView, IShortcutSource
{
    private readonly ObservableCollection<T> _items;
    private readonly IValuePresenter<T> _presenter;
    private readonly PresenterListDataSource<T> _dataSource;
    private readonly TextField _inputField;
    private readonly Terminal.Gui.Views.ListView _listView;
    private int? _editingIndex;

    public ListEditorView(ObservableCollection<T> items, IValuePresenter<T> presenter)
    {
        CanFocus = true;
        _items = items;
        _presenter = presenter;

        _inputField = new TextField { X = 0, Y = 0, Width = Dim.Fill() };
        _inputField.ValueChanged += (_, _) => UpdateInputValidity();
        _inputField.Accepted += (_, _) => Append(_inputField.Text);

        _dataSource = new PresenterListDataSource<T>(_items, _presenter);
        _listView = new Terminal.Gui.Views.ListView { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.Source = _dataSource;

        // Bound here (on the whole component), not on the input field or list, so Ctrl+N/E/D work
        // no matter which of those two currently has focus - same rationale as PublishView.
        AddCommand(Command.New, () => { ClearInput(); return true; });
        AddCommand(Command.Edit, () => { if (SelectedIndex is { } index) Edit(index); return true; });
        AddCommand(Command.DeleteAll, () => { if (SelectedIndex is { } index) Delete(index); return true; });
        KeyBindings.Add(Key.N.WithCtrl, Command.New);
        KeyBindings.Add(Key.E.WithCtrl, Command.Edit);
        KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);

        // Only offered a chance once the list itself has left Up unhandled (i.e. at/above its top
        // row) - otherwise (e.g. the input field already has focus) this declines, letting Up keep
        // bubbling past this component entirely.
        AddCommand(Command.Up, () => {
            if (!_listView.HasFocus) return false;
            _inputField.SetFocus();
            return true;
        });
        KeyBindings.Add(Key.CursorUp, Command.Up);

        Add(_inputField, _listView);

        UpdateInputValidity();
    }

    private int? SelectedIndex =>
        _listView.SelectedItem is { } index && index >= 0 && index < _items.Count ? index : null;

    // Read-only so a subclass whose commit path has side effects beyond direct collection mutation
    // (e.g. SubscriptionsView) can tell an edit-commit apart from a fresh add without duplicating
    // this bookkeeping itself.
    protected int? EditingIndex => _editingIndex;

    protected virtual void Append(string raw)
    {
        if (!_presenter.TryParse(raw, out var value, out var error)) {
            OnParseError(raw, error);
            return;
        }

        if (_editingIndex is { } index && index < _items.Count) _items[index] = value;
        else _items.Add(value);

        ClearInput();
    }

    protected virtual void Edit(int index)
    {
        _inputField.Text = _presenter.Format(_items[index]);
        _editingIndex = index;
        _inputField.SetFocus();
    }

    protected virtual void Delete(int index)
    {
        _items.RemoveAt(index);
        if (_editingIndex == index) ClearInput();
    }

    protected virtual void ClearInput()
    {
        _inputField.Text = string.Empty;
        _editingIndex = null;
        _inputField.SetFocus();
    }

    // Default no-op: live validation already flags *that* the input is invalid; this hook exists
    // for a subclass to surface *why* (e.g. relay `error` into a status bar).
    protected virtual void OnParseError(string raw, string? error) { }

    private void UpdateInputValidity()
    {
        var valid = _presenter.TryParse(_inputField.Text, out _, out _);
        _inputField.SetScheme(valid ? null : new Scheme(InvalidInputAttribute));
    }

    public virtual IEnumerable<ShortcutHint> Shortcuts =>
    [
        new ShortcutHint(Key.N.WithCtrl, "New", () => ClearInput()),
        new ShortcutHint(Key.E.WithCtrl, "Edit", () => { if (SelectedIndex is { } index) Edit(index); }),
        new ShortcutHint(Key.D.WithCtrl, "Delete", () => { if (SelectedIndex is { } index) Delete(index); }),
    ];

    protected override void Dispose(bool disposing)
    {
        if (disposing) _dataSource.Dispose();
        base.Dispose(disposing);
    }
}
