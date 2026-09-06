using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.Objects;

// Carries the derived bare bucket name alongside the raw StreamInfo, computed once per item at
// refresh time - see openspec/changes/filter-bucket-backed-streams/design.md's "KvBucketItem/
// ObjBucketItem wrappers" decision.
internal sealed record ObjBucketItem(string Name, StreamInfo Info);

internal sealed class BucketListView: DrillableListView<ObjBucketItem>
{
    private static readonly BucketNamePresenter PresenterInstance = new();

    public BucketListView(ObservableCollection<ObjBucketItem> items): base(items)
    {
        EnableDescend();
        EnableCreate();
        EnableDelete();
        EnableEdit();
        EnableFilter();
    }

    protected override IValuePresenter<ObjBucketItem> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No buckets — Ctrl+R to refresh";
    protected override string GetIdentity(ObjBucketItem item) => item.Name;
    protected override string FilterDialogTitle => "Filter Buckets";

    public ObjBucketItem? SelectedBucket => SelectedItem;
}
