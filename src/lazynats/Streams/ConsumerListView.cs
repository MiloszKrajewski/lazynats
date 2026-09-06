using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Input;

namespace lazynats.Streams;

internal sealed class ConsumerListView: DrillableListView<ConsumerInfo>
{
    private static readonly ConsumerNamePresenter PresenterInstance = new();

    public event Action? AscendRequested;
    public event Action? CreateRequested;
    public event Action? DeleteRequested;

    public ConsumerListView(ObservableCollection<ConsumerInfo> items): base(items)
    {
        // No default binding exists for "ascend" the way Enter already means Command.Accept on
        // ListView - both keys map to the same Command.Cancel action.
        AddCommand(Command.Cancel, () => { AscendRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.Esc, Command.Cancel);
        KeyBindings.Add(Key.Backspace, Command.Cancel);

        // The inner ListView's own DefaultKeyBindings alias Ctrl+N to Command.Down (Emacs-style
        // "next"), on top of the Down arrow key - same removal StreamListView already does for
        // its own Ctrl+N binding, for the same reason (it's the actual focus target, so it would
        // otherwise consume Ctrl+N before this component's own binding below ever sees it).
        ListView.KeyBindings.Remove(Key.N.WithCtrl);

        AddCommand(Command.New, () => { CreateRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.N.WithCtrl, Command.New);

        AddCommand(Command.DeleteAll, () => { DeleteRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);
    }

    protected override IValuePresenter<ConsumerInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No consumers — Ctrl+R to refresh";
    protected override string GetIdentity(ConsumerInfo item) => item.Name!;

    public ConsumerInfo? SelectedConsumer => SelectedItem;

    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts
            .Append(new ShortcutHint(Key.Esc, "Back", () => AscendRequested?.Invoke()))
            .Append(new ShortcutHint(Key.N.WithCtrl, "New", () => CreateRequested?.Invoke()))
            .Append(new ShortcutHint(Key.D.WithCtrl, "Delete", () => DeleteRequested?.Invoke()));
}
