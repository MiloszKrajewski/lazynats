using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Input;

namespace lazynats.Streams;

internal sealed class ConsumerListView: DrillableListView<ConsumerInfo>
{
    private static readonly ConsumerNamePresenter PresenterInstance = new();

    public event Action? AscendRequested;

    public ConsumerListView(ObservableCollection<ConsumerInfo> items): base(items)
    {
        // No default binding exists for "ascend" the way Enter already means Command.Accept on
        // ListView - both keys map to the same Command.Cancel action.
        AddCommand(Command.Cancel, () => { AscendRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.Esc, Command.Cancel);
        KeyBindings.Add(Key.Backspace, Command.Cancel);
    }

    protected override IValuePresenter<ConsumerInfo> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No consumers — Ctrl+R to refresh";
    protected override string GetIdentity(ConsumerInfo item) => item.Name!;

    public ConsumerInfo? SelectedConsumer => SelectedItem;

    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts.Append(new ShortcutHint(Key.Esc, "Back", () => AscendRequested?.Invoke()));
}
