using System.Collections.ObjectModel;
using lazynats.Components;
using Terminal.Gui.Input;

namespace lazynats.ObjStore;

internal sealed class ObjectListView: DrillableListView<string>
{
    private static readonly ObjectNamePresenter PresenterInstance = new();

    public event Action? AscendRequested;

    public ObjectListView(ObservableCollection<string> items): base(items)
    {
        // No default binding exists for "ascend" the way Enter already means Command.Accept on
        // ListView - both keys map to the same Command.Cancel action.
        AddCommand(Command.Cancel, () => { AscendRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.Esc, Command.Cancel);
        KeyBindings.Add(Key.Backspace, Command.Cancel);
    }

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No objects — Ctrl+R to refresh";
    protected override string GetIdentity(string item) => item;

    public string? SelectedObject => SelectedItem;

    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts.Append(new ShortcutHint(Key.Esc, "Back", () => AscendRequested?.Invoke()));
}
