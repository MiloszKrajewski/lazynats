using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

internal sealed class ConsumerListView: DrillableListView<ConsumerInfo>
{
    private static readonly ConsumerNamePresenter PresenterInstance = new();

    public ConsumerListView(ObservableCollection<ConsumerInfo> items): base(items)
    {
        EnableAscend();
        EnableCreateDelete();
    }

    protected override IValuePresenter<ConsumerInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No consumers — Ctrl+R to refresh";
    protected override string GetIdentity(ConsumerInfo item) => item.Name!;

    public ConsumerInfo? SelectedConsumer => SelectedItem;
}
