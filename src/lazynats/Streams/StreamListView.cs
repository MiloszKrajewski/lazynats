using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

internal sealed class StreamListView: DrillableListView<StreamInfo>
{
    private static readonly StreamNamePresenter PresenterInstance = new();

    public event Action? DescendRequested;

    public StreamListView(ObservableCollection<StreamInfo> items): base(items) =>
        // ListView already binds Enter -> Command.Accept by default; re-raised here rather than
        // StreamsTab subscribing to the inner ListView directly, which is private to the base.
        ListView.Accepted += (_, _) => DescendRequested?.Invoke();

    protected override IValuePresenter<StreamInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No streams — Ctrl+R to refresh";
    protected override string GetIdentity(StreamInfo item) => item.Config.Name!;

    public StreamInfo? SelectedStream => SelectedItem;
}
