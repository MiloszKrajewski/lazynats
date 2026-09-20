using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

internal sealed class StreamListView: DrillableListView<StreamInfo>
{
    private static readonly StreamNamePresenter PresenterInstance = new();

    public StreamListView(ObservableCollection<StreamInfo> items): base(items)
    {
        EnableCreate("Add new Stream");
        EnableDelete("Delete Stream");
        EnableEdit("Edit Stream");
        EnableFilter("Filter Streams");
    }

    protected override IValuePresenter<StreamInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No streams - N to add one, R to refresh";
    protected override string GetIdentity(StreamInfo item) => item.Config.Name!;

    public StreamInfo? SelectedStream => SelectedItem;
}
