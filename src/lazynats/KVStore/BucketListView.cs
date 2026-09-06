using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.KeyValueStore;
using Terminal.Gui.Input;

namespace lazynats.KVStore;

internal sealed class BucketListView: DrillableListView<NatsKVStatus>
{
    private static readonly BucketNamePresenter PresenterInstance = new();

    public event Action? DescendRequested;
    public event Action? CreateRequested;

    public BucketListView(ObservableCollection<NatsKVStatus> items): base(items)
    {
        // ListView already binds Enter -> Command.Accept by default; re-raised here rather than
        // KvTab subscribing to the inner ListView directly, which is private to the base.
        ListView.Accepted += (_, _) => DescendRequested?.Invoke();

        // The inner ListView's own DefaultKeyBindings alias Ctrl+N to Command.Down (Emacs-style
        // "next"), on top of the Down arrow key. It's the actual focus target, so left in place it
        // would consume Ctrl+N before this component's own binding below ever sees it. Down arrow
        // itself is untouched - only the redundant Ctrl+N alias for the same command is removed.
        // Mirrors Streams/StreamListView.cs's identical Ctrl+N layering.
        ListView.KeyBindings.Remove(Key.N.WithCtrl);

        // DrillableListView<T> only wires Ctrl+R (shared with the key level, which must stay
        // create-less per nats-kv) - Ctrl+N is layered on here, same as StreamListView.
        AddCommand(Command.New, () => { CreateRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.N.WithCtrl, Command.New);
    }

    protected override IValuePresenter<NatsKVStatus> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No buckets — Ctrl+R to refresh";
    protected override string GetIdentity(NatsKVStatus item) => BucketName.From(item);

    public NatsKVStatus? SelectedBucket => SelectedItem;

    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts.Append(new ShortcutHint(Key.N.WithCtrl, "New", () => CreateRequested?.Invoke()));
}
