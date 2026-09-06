using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.KeyValueStore;

namespace lazynats.Kv;

internal sealed class BucketListView: DrillableListView<NatsKVStatus>
{
    private static readonly BucketNamePresenter PresenterInstance = new();

    public event Action? DescendRequested;

    public BucketListView(ObservableCollection<NatsKVStatus> items): base(items) =>
        // ListView already binds Enter -> Command.Accept by default; re-raised here rather than
        // KvTab subscribing to the inner ListView directly, which is private to the base.
        ListView.Accepted += (_, _) => DescendRequested?.Invoke();

    protected override IValuePresenter<NatsKVStatus> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No buckets — Ctrl+R to refresh";
    protected override string GetIdentity(NatsKVStatus item) => BucketName.From(item);

    public NatsKVStatus? SelectedBucket => SelectedItem;
}
