using System.Collections.ObjectModel;
using lazynats.Components;

namespace lazynats.KVStore;

internal sealed class KeyListView: DrillableListView<string>
{
    private static readonly KeyNamePresenter PresenterInstance = new();

    public KeyListView(ObservableCollection<string> items): base(items) => EnableAscend();

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No keys — Ctrl+R to refresh";
    protected override string GetIdentity(string item) => item;

    public string? SelectedKey => SelectedItem;
}
