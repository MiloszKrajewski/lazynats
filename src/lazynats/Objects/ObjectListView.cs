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

    // Post-fetch name filter (Ctrl+F) - not a shared DrillableListView<T> Enable* shape, mirroring
    // add-kv-key-filter's KeyListView.FilterRequested. ObjectsTab owns the actual dialog/
    // result-narrowing in response to this event. See
    // openspec/changes/add-obj-name-filter/design.md Decision 1.
    public event Action? FilterRequested;

    public ObjectListView(ObservableCollection<string> items): base(items)
    {
        EnableAscend();
        EnableCreate();
        EnableDelete();

        AddCommand(Command.Save, () => { DownloadRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.S.WithCtrl, Command.Save);

        // Command.Open is unused elsewhere on this view - repurposed here the same way
        // Command.Save is repurposed above for Ctrl+S download.
        AddCommand(Command.Open, () => { FilterRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.F.WithCtrl, Command.Open);
    }

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No objects — Ctrl+R to refresh";
    protected override string GetIdentity(string item) => item;

    public string? SelectedObject => SelectedItem;

    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts
            .Append(new ShortcutHint(Key.S.WithCtrl, "Download", () => DownloadRequested?.Invoke()))
            .Append(new ShortcutHint(Key.F.WithCtrl, "Filter", () => FilterRequested?.Invoke()));
}
