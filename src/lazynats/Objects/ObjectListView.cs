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
        // The shared Filter (Ctrl+F) wiring, using the same grammar every other list's filter
        // uses - retires this view's own WildcardToRegex-based post-fetch filter. ObjectsTab
        // subscribes to FilterChanged to re-trigger its (always-full, never server-scoped) fetch,
        // since Object Store has no server-side name-wildcard fetch API to scope in the first
        // place. See openspec/changes/unify-list-filtering/design.md Decision 3.
        EnableFilter();

        // Command.Save is unused elsewhere on this view - repurposed here for bare-S download,
        // which stays list-bound (out of scope for tab-scoped-list-shortcuts, unlike Ctrl+F/bare F).
        AddCommand(Command.Save, () => { DownloadRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.S, Command.Save);
    }

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No objects - N to add one, R to refresh";
    protected override string GetIdentity(string item) => item;
    protected override string FilterDialogTitle => "Filter Objects";

    public string? SelectedObject => SelectedItem;

    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts.Append(new ShortcutHint(Key.S, "Download", () => DownloadRequested?.Invoke()));
}
