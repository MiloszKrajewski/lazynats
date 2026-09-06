using Terminal.Gui.Input;

namespace lazynats.Components;

internal readonly record struct ShortcutHint(Key Key, string Text, Action Action);

// Opt-in: a View implements this to curate which of its KeyBindings should be surfaced to the
// user (e.g. in a status bar), separate from bindings that exist for internal/navigational
// reasons and would just be noise if advertised automatically.
internal interface IShortcutSource
{
    IEnumerable<ShortcutHint> Shortcuts { get; }
}

// Opt-in: a tab-hosted list curates which of its operations (Refresh/New/Delete/Edit/Filter) an
// owning tab should bind Ctrl+R/N/D/E/F to and dispatch to, separate from IShortcutSource.Shortcuts
// (which only covers what remains genuinely list-owned once tab-scoped-list-shortcuts moves the
// rest up) - see openspec/specs/tab-scoped-list-shortcuts/spec.md.
internal interface ITabOperationsSource
{
    IEnumerable<ShortcutHint> TabOperations { get; }
}
