using System.Collections.ObjectModel;
using lazynats.Components;
using Terminal.Gui.Input;

namespace lazynats.Values;

internal sealed class KeyListView: DrillableListView<string>
{
    private static readonly KeyNamePresenter PresenterInstance = new();

    // Raised only when a key is highlighted - ValuesTab additionally gates on
    // KeyDetails.CurrentEntry being non-null (the detail panel's fetch having actually landed)
    // before opening the dialog. See openspec/changes/kv-value-peek-and-view/design.md Decision 5
    // and openspec/specs/nats-kv/spec.md's "Open KV Value Detail Dialog" requirement.
    public event Action? ViewValueRequested;

    public KeyListView(ObservableCollection<string> items): base(items)
    {
        EnableAscend();
        EnableCreate();
        EnableDelete();
        EnableEdit();
        // The shared Filter (Ctrl+F) wiring - ValuesTab additionally subscribes to FilterChanged
        // to scope its server-side fetch (the one list in the app that can), but the base class's
        // own in-memory narrowing already fully owns the dialog/compile/apply mechanics. See
        // openspec/changes/unify-list-filtering/design.md Decision 2.
        EnableFilter();

        // V -> ViewValueRequested. Bound directly here (like EnableAscend's Esc/Backspace) rather
        // than through the shared Ctrl+R/N/D/E/F TabOperations dispatch - this is a KeyListView-
        // specific action, not a shape every DrillableListView<T> subclass shares. Command.Expand
        // ("Expands a list or item") mirrors MessageDetailDialog/ValueDetailDialog's own V binding
        // - a different View instance, so reusing the same Command tag is harmless.
        AddCommand(Command.Expand, () => { if (SelectedKey is not null) ViewValueRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.V, Command.Expand);
    }

    protected override IValuePresenter<string> Presenter => PresenterInstance;
    protected override string EmptyHintText => "No keys - N to add one, R to refresh";
    protected override string GetIdentity(string item) => item;
    protected override string FilterDialogTitle => "Filter Keys";

    public string? SelectedKey => SelectedItem;

    // "View" rather than "Presentation" (PayloadDetailSection's own V hint, inside the dialog this
    // opens) - distinct wording avoids the picker/status bar showing two identically-labeled V
    // hints for genuinely different actions at different times.
    public override IEnumerable<ShortcutHint> Shortcuts =>
        base.Shortcuts.Append(new ShortcutHint(Key.V, "View", () => { if (SelectedKey is not null) ViewValueRequested?.Invoke(); }));
}
