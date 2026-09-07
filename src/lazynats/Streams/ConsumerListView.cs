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
        EnableCreate();
        EnableDelete();
        EnableEdit();
        EnableFilter();
    }

    protected override IValuePresenter<ConsumerInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No consumers - N to add one, R to refresh";
    protected override string GetIdentity(ConsumerInfo item) => item.Name!;
    protected override string FilterDialogTitle => "Filter Consumers";

    public ConsumerInfo? SelectedConsumer => SelectedItem;
}
