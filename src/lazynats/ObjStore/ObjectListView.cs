using System.Collections.ObjectModel;
using lazynats.Components;

namespace lazynats.ObjStore;

internal sealed class ObjectListView: DrillableListView<string>
{
    private static readonly ObjectNamePresenter PresenterInstance = new();

    public ObjectListView(ObservableCollection<string> items): base(items) => EnableAscend();

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No objects — Ctrl+R to refresh";
    protected override string GetIdentity(string item) => item;

    public string? SelectedObject => SelectedItem;
}
