using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.ObjStore;

internal sealed class BucketListView: DrillableListView<StreamInfo>
{
    private static readonly BucketNamePresenter PresenterInstance = new();

    public event Action? DescendRequested;

    public BucketListView(ObservableCollection<StreamInfo> items): base(items) =>
        // ListView already binds Enter -> Command.Accept by default; re-raised here rather than
        // ObjTab subscribing to the inner ListView directly, which is private to the base.
        ListView.Accepted += (_, _) => DescendRequested?.Invoke();

    protected override IValuePresenter<StreamInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No buckets — Ctrl+R to refresh";
    protected override string GetIdentity(StreamInfo item) => BucketName.From(item);

    public StreamInfo? SelectedBucket => SelectedItem;
}
