using Terminal.Gui.App;
using Terminal.Gui.ViewBase;

namespace lazynats.Components;

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

// Recomputes the available shortcut set on every app-wide focus change (and on demand via
// Refresh(), for state changes - like ListEditorView's post-modal re-sync after New/Edit - that
// don't move focus). Takes the owning IApplication instance (the same one views reach via `View.App`)
// rather than the obsolete static Application.Navigation. Wired into MainWindow's StatusBar.
internal sealed class ShortcutTracker: IDisposable
{
    private readonly IApplication _app;

    public event Action<IReadOnlyList<ShortcutHint>>? ShortcutsChanged;

    public ShortcutTracker(IApplication app)
    {
        _app = app;
        _app.Navigation!.FocusedChanged += OnFocusedChanged;
    }

    private void OnFocusedChanged(object? sender, EventArgs e) => Refresh();

    // Recomputes and re-raises ShortcutsChanged on demand, for callers whose available shortcuts
    // changed without keyboard focus moving anywhere (e.g. a view switching its own internal mode).
    //
    // Deliberately reads TopRunnableView.MostFocused (walks the live View.HasFocus tree) rather
    // than Navigation.GetFocused() (a separate cached pointer Terminal.Gui updates only at
    // specific points during a HasFocus transition). Application.Begin (run when a modal Dialog
    // opens) clears that cache via Navigation.SetFocused(null) but nothing re-populates it when
    // the modal's End() restores TopRunnable afterwards - so right after App.Run(dialog) returns,
    // GetFocused() can still read null/stale even though the underlying view (e.g. the list this
    // dialog was opened from) genuinely has HasFocus == true again.
    public void Refresh() =>
        ShortcutsChanged?.Invoke(ShortcutAggregator.Collect(_app.TopRunnableView?.MostFocused).ToList());

    public void Dispose() => _app.Navigation!.FocusedChanged -= OnFocusedChanged;
}
