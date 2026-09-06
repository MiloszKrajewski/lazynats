using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

internal sealed class StreamListView: DrillableListView<StreamInfo>
{
    private static readonly StreamNamePresenter PresenterInstance = new();

    public StreamListView(ObservableCollection<StreamInfo> items): base(items)
    {
        EnableCreate();
        EnableDelete();
        EnableEdit();
        EnableFilter();
    }

    protected override IValuePresenter<StreamInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No streams — R to refresh";
    protected override string GetIdentity(StreamInfo item) => item.Config.Name!;
    protected override string FilterDialogTitle => "Filter Streams";

    public StreamInfo? SelectedStream => SelectedItem;
}
