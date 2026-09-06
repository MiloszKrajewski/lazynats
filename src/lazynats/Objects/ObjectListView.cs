using System.Collections.ObjectModel;
using lazynats.Components;
using Terminal.Gui.Input;

namespace lazynats.Objects;

internal sealed class ObjectListView: DrillableListView<string>
{
    private static readonly ObjectNamePresenter PresenterInstance = new();

    // Download has no shared DrillableListView<T> shape (unlike Create/Delete) - it's the only
    // subclass with a fourth action, so it's wired directly here per design.md Decision 2, the
    // same pattern the base class's own Shortcuts doc comment describes.
    public event Action? DownloadRequested;

    public ObjectListView(ObservableCollection<string> items): base(items)
    {
        EnableAscend();
        EnableCreate();
        EnableDelete();

        AddCommand(Command.Save, () => { DownloadRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.S.WithCtrl, Command.Save);
    }

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No objects — Ctrl+R to refresh";
    protected override string GetIdentity(string item) => item;

    public string? SelectedObject => SelectedItem;

    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts.Append(new ShortcutHint(Key.S.WithCtrl, "Download", () => DownloadRequested?.Invoke()));
}
