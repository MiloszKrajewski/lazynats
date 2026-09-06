using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.KeyValueStore;

namespace lazynats.Values;

// Carries the derived bare bucket name alongside the raw NatsKVStatus, computed once per item at
// refresh time - see openspec/changes/filter-bucket-backed-streams/design.md's "KvBucketItem/
// ObjBucketItem wrappers" decision.
internal sealed record KvBucketItem(string Name, NatsKVStatus Status);

internal sealed class BucketListView: DrillableListView<KvBucketItem>
{
    private static readonly BucketNamePresenter PresenterInstance = new();

    public BucketListView(ObservableCollection<KvBucketItem> items): base(items)
    {
        EnableCreate();
        EnableDelete();
        EnableEdit();
        EnableFilter();
    }

    protected override IValuePresenter<KvBucketItem> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No buckets — Ctrl+R to refresh";
    protected override string GetIdentity(KvBucketItem item) => item.Name;
    protected override string FilterDialogTitle => "Filter Buckets";

    public KvBucketItem? SelectedBucket => SelectedItem;
}
