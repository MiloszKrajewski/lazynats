using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.ObjStore;

internal sealed class BucketListView: DrillableListView<StreamInfo>
{
    private static readonly BucketNamePresenter PresenterInstance = new();

    public BucketListView(ObservableCollection<StreamInfo> items): base(items)
    {
        EnableDescend();
        EnableCreate();
        EnableDelete();
    }

    protected override IValuePresenter<StreamInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No buckets — Ctrl+R to refresh";
    protected override string GetIdentity(StreamInfo item) => BucketName.From(item);

    public StreamInfo? SelectedBucket => SelectedItem;
}
