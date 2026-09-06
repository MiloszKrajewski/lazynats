using Terminal.Gui.App;
using Terminal.Gui.ViewBase;

namespace lazynats;

// Walks the same ancestor chain Terminal.Gui's own key-event dispatch bubbles along (focused
// leaf -> SuperView -> ... -> root), collecting shortcuts from every IShortcutSource on the way.
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

// Recomputes the available shortcut set on every app-wide focus change. Takes the owning
// IApplication instance (the same one views reach via `View.App`) rather than the obsolete
// static Application.Navigation. Not wired into MainWindow's StatusBar in this change - see
// design.md's Non-Goals.
internal sealed class ShortcutTracker: IDisposable
{
    private readonly IApplication _app;

    public event Action<IReadOnlyList<ShortcutHint>>? ShortcutsChanged;

    public ShortcutTracker(IApplication app)
    {
        _app = app;
        _app.Navigation!.FocusedChanged += OnFocusedChanged;
    }

    private void OnFocusedChanged(object? sender, EventArgs e) =>
        ShortcutsChanged?.Invoke(ShortcutAggregator.Collect(_app.Navigation!.GetFocused()).ToList());

    public void Dispose() => _app.Navigation!.FocusedChanged -= OnFocusedChanged;
}
