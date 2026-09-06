using System.Collections.ObjectModel;
using lazynats.Components;

namespace lazynats.Values;

internal sealed class KeyListView: DrillableListView<string>
{
    private static readonly KeyNamePresenter PresenterInstance = new();

    public KeyListView(ObservableCollection<string> items): base(items)
    {
        EnableAscend();
        EnableCreate();
        EnableDelete();
        EnableEdit();
        // The shared Filter (Ctrl+F) wiring - ValuesTab additionally subscribes to FilterChanged
        // to scope its server-side fetch (the one list in the app that can), but the base class's
        // own in-memory narrowing already fully owns the dialog/compile/apply mechanics. See
        // openspec/changes/unify-list-filtering/design.md Decision 2.
        EnableFilter();
    }

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No keys — Ctrl+R to refresh";
    protected override string GetIdentity(string item) => item;
    protected override string FilterDialogTitle => "Filter Keys";

    public string? SelectedKey => SelectedItem;
}
