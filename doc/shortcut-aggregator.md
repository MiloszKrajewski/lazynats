# Keyboard-shortcut discoverability — current state and pickup notes

Status: built and verified in isolation (`2026-07-23-add-list-editor-shortcuts`), **not wired into the
running app**. This doc exists so that work can be picked back up without re-deriving the design.

## What exists

- `IShortcutSource` / `ShortcutHint` (`src/lazynats/IShortcutSource.cs`): a `View` opts in by
  implementing `IShortcutSource.Shortcuts` with a small curated list of `(Key, Text, Action)`. Opt-in is
  deliberate — most of a view's `KeyBindings` (list navigation, etc.) are not meant to be advertised.
- `ShortcutAggregator.Collect(View? focused)` (`src/lazynats/ShortcutAggregator.cs`): walks
  `focused -> SuperView -> ... -> root`, collecting `Shortcuts` from every ancestor implementing
  `IShortcutSource` — the same chain Terminal.Gui's own key dispatch bubbles along.
- `ShortcutTracker` (same file): subscribes to `IApplication.Navigation!.FocusedChanged`, re-runs the
  aggregator on every focus change, and raises `ShortcutsChanged(IReadOnlyList<ShortcutHint>)`. Takes the
  owning `IApplication` in its constructor (not the static `Application.Navigation`, which is obsolete in
  Terminal.Gui 2.4.10).
- `ListEditorView<T>` (`src/lazynats/ListEditorView.cs`) implements `IShortcutSource`, advertising its own
  Ctrl+N/E/D as `virtual`, so a subclass can extend or replace the set. `SubscriptionsView` is the one
  real consumer today.

## What's missing: it isn't rendered anywhere

`MainWindow.cs` still builds its `StatusBar` by hand-wiring one `Shortcut` + one focus/status subscription
per widget:

```csharp
var clearShortcut = new Shortcut { Text = "Clear", Key = Key.C, Visible = false };
clearShortcut.Action = liveUpdates.Clear;
liveUpdates.HasFocusChanged += (_, _) => clearShortcut.Visible = liveUpdates.HasFocus;

var publishStatusShortcut = new Shortcut { Text = string.Empty, Visible = false };
publishTab.StatusChanged += message => { publishStatusShortcut.Text = message; publishStatusShortcut.Visible = true; };
```

`ShortcutTracker` was explicitly built to replace this pattern generically, but wiring it in was scoped
out of both changes so far. `publishStatusShortcut` is a different concern (transient send-result
messages, not key hints) and should stay as-is regardless.

## To pick this up

1. In `MainWindow`'s constructor, construct a `ShortcutTracker(App!)` (or however the owning
   `IApplication` is reached at that point — check what's available there now) and keep it alive for the
   window's lifetime (`Dispose` it alongside other resources).
2. Decide the rendering shape: today's `StatusBar` is built once from a fixed `Shortcut[]` passed to its
   constructor. `ShortcutTracker.ShortcutsChanged` fires with a *variable-length* list, so this either
   needs a fixed-size pool of `Shortcut`s that get repurposed (text/key/action swapped, visibility
   toggled) each time it fires, or confirming whether `StatusBar` (as a `View`) tolerates `Add`/`RemoveAll`
   of its `Shortcut` subviews at runtime — check the current `Terminal.Gui.Views.StatusBar` API surface
   before committing to either, since this wasn't verified during the prior changes.
3. Once wired, `clearShortcut` (`LiveUpdatesView`'s Ctrl+C-to-clear) becomes redundant and could be
   replaced by having `LiveUpdatesView` implement `IShortcutSource` instead — but that's an
   `LiveUpdatesView.cs` change, out of scope for both prior changes; do it deliberately, not as a
   drive-by.
4. Migrating `PublishTab`'s header editor (`_headerKeyField`/`_headerValueField`/`_headerListView`/
   `HeaderListDataSource`/`ClearHeaderInput`/`LoadSelectedForEditing`/`CommitHeaderInput`/
   `RemoveSelectedHeader`) onto `ListEditorView<HeaderPair>` is a related but separate piece of work —
   `HeaderColonPresenter` (`src/lazynats/HeaderColonPresenter.cs`, `"key: value"` parsing) already exists
   as the presenter for it, built and proven in isolation but not wired in, same as this. Doing that
   migration would make `PublishTab` a second real `IShortcutSource` consumer, which is useful signal
   before finalizing the `StatusBar` rendering shape in step 2 (a design meant for exactly one consumer
   risks being wrong for the second).

## Suggested order

Steps 4 then 1-3: getting `PublishTab` onto `ListEditorView<T>` first gives two real `IShortcutSource`
views to design the `StatusBar` rendering against, rather than guessing the right shape from
`SubscriptionsView` alone.
