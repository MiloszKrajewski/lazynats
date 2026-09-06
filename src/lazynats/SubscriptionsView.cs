using System.Collections.ObjectModel;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats;

internal sealed class SubscriptionsView: View
{
    private readonly SubscriptionRegistry _registry;
    private readonly ObservableCollection<string> _patterns = [];
    private readonly List<Guid> _ids = [];
    private readonly ListView _listView;
    private readonly TextField _patternField;

    public SubscriptionsView(SubscriptionRegistry registry)
    {
        CanFocus = true;
        _registry = registry;

        _patternField = new TextField { X = 0, Y = 0, Width = Dim.Fill(12) };
        _patternField.Accepted += (_, _) => AddFromField();

        var addButton = new Button { Text = "_Add", X = Pos.AnchorEnd(10), Y = 0, Width = 10 };
        addButton.Accepting += (_, _) => AddFromField();

        _listView = new ListView { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.SetSource(_patterns);

        AddCommand(Command.DeleteAll, () => { RemoveSelected(); return true; });
        KeyBindings.Add(Key.Delete, Command.DeleteAll);

        Add(_patternField, addButton, _listView);

        _registry.Changed += RefreshFromRegistry;
        RefreshFromRegistry();
    }

    private void AddFromField()
    {
        var pattern = _patternField.Text.Trim();
        if (pattern.Length == 0) return;
        _registry.Add(pattern);
        _patternField.Text = string.Empty;
    }

    private void RemoveSelected()
    {
        if (_listView.SelectedItem is not { } index || index < 0 || index >= _ids.Count) return;
        _registry.Remove(_ids[index]);
    }

    private void RefreshFromRegistry()
    {
        _ids.Clear();
        _patterns.Clear();
        foreach (var subscription in _registry.Active) {
            _ids.Add(subscription.Id);
            _patterns.Add(subscription.Pattern);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _registry.Changed -= RefreshFromRegistry;
        base.Dispose(disposing);
    }
}
