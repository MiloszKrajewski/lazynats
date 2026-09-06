using System.Collections.ObjectModel;
using lazynats.Components;
using Terminal.Gui.Input;

namespace lazynats.Values;

internal sealed class KeyListView: DrillableListView<string>
{
    private static readonly KeyNamePresenter PresenterInstance = new();

    // Server-side pre-fetch filter (Ctrl+F) - not a shared DrillableListView<T> Enable* shape,
    // since no other current subclass has a server-side-filterable fetch to hang one off of. See
    // openspec/changes/add-kv-key-filter/design.md Decision 1. ValuesTab owns the actual dialog/
    // fetch-scoping in response to this event, the same way it does for CreateRequested/
    // DeleteRequested/EditRequested.
    public event Action? FilterRequested;

    public KeyListView(ObservableCollection<string> items): base(items)
    {
        EnableAscend();
        EnableCreate();
        EnableDelete();
        EnableEdit();
    }

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No keys — Ctrl+R to refresh";
    protected override string GetIdentity(string item) => item;

    public string? SelectedKey => SelectedItem;

    public override IEnumerable<ShortcutHint> TabOperations =>
        base.TabOperations.Append(new ShortcutHint(Key.F.WithCtrl, "Filter", () => FilterRequested?.Invoke()));
}
