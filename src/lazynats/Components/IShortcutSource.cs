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
