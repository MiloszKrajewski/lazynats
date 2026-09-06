using Terminal.Gui.ViewBase;

namespace lazynats.Components;

// Walks the same ancestor chain Terminal.Gui's own key-event dispatch bubbles along (focused
// leaf -> SuperView -> ... -> root), collecting shortcuts from every IShortcutSource on the way.
// Called on demand (e.g. by ShortcutPickerDialog's caller, at the moment it opens) rather than
// continuously tracked - see openspec/changes/add-shortcut-picker/design.md.
internal static class ShortcutAggregator
{
    public static IEnumerable<ShortcutHint> Collect(View? focused)
    {
        for (var view = focused; view is not null; view = view.SuperView)
            if (view is IShortcutSource source)
                foreach (var hint in source.Shortcuts)
                    yield return hint;
    }
}
