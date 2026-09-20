using Terminal.Gui.Input;

namespace lazynats.Components;

// Group is an opaque identity (not a display string - no group name is ever shown) used only to
// keep a source's hints clustered together when presented; see ShortcutAggregator.Collect and
// ShortcutPickerDialog's ordering logic. Priority optionally reorders a hint earlier within its
// own group only - see openspec/changes/shortcut-picker-groups/design.md.
internal readonly record struct ShortcutHint(Key Key, string Text, Action Action, object? Group = null, int? Priority = null);

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
