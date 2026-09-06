using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Input;

namespace lazynats.Streams;

internal sealed class StreamListView: DrillableListView<StreamInfo>
{
    private static readonly StreamNamePresenter PresenterInstance = new();

    public event Action? DescendRequested;
    public event Action? CreateRequested;

    public StreamListView(ObservableCollection<StreamInfo> items): base(items)
    {
        // ListView already binds Enter -> Command.Accept by default; re-raised here rather than
        // StreamsTab subscribing to the inner ListView directly, which is private to the base.
        ListView.Accepted += (_, _) => DescendRequested?.Invoke();

        // The inner ListView's own DefaultKeyBindings alias Ctrl+N to Command.Down (Emacs-style
        // "next"), on top of the Down arrow key. It's the actual focus target, so left in place it
        // would consume Ctrl+N before this component's own binding below ever sees it. Down arrow
        // itself is untouched - only the redundant Ctrl+N alias for the same command is removed.
        ListView.KeyBindings.Remove(Key.N.WithCtrl);

        // DrillableListView<T> only wires Ctrl+R (shared with the consumer level, which must stay
        // create-less per nats-streams) - Ctrl+N is layered on here the same way ConsumerListView
        // layers its own Esc/Backspace.
        AddCommand(Command.New, () => { CreateRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.N.WithCtrl, Command.New);
    }

    protected override IValuePresenter<StreamInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No streams — Ctrl+R to refresh";
    protected override string GetIdentity(StreamInfo item) => item.Config.Name!;

    public StreamInfo? SelectedStream => SelectedItem;

    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts.Append(new ShortcutHint(Key.N.WithCtrl, "New", () => CreateRequested?.Invoke()));
}
