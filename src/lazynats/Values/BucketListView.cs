using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.KeyValueStore;

namespace lazynats.Values;

internal sealed class BucketListView: DrillableListView<NatsKVStatus>
{
    private static readonly BucketNamePresenter PresenterInstance = new();

    public BucketListView(ObservableCollection<NatsKVStatus> items): base(items)
    {
        EnableDescend();
        EnableCreate();
        EnableDelete();
        EnableEdit();
    }

    protected override IValuePresenter<NatsKVStatus> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No buckets — Ctrl+R to refresh";
    protected override string GetIdentity(NatsKVStatus item) => BucketName.From(item);

    public NatsKVStatus? SelectedBucket => SelectedItem;
}
