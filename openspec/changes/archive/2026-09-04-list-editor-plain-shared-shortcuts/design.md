## Context

`ListEditorView<T>` has exactly two subclasses: `SubscriptionsView` (`bindSharedKeys: false`,
tab-hosted, dispatched via `SubscribeTab`) and `HeaderEditorView` (`bindSharedKeys: true`,
standalone, used inside `PublishDialog` and `TemplateDialog`). The standalone path binds
`Ctrl+N`/`Ctrl+E`/`Ctrl+D` directly on the component (`ListEditorView.cs:82-95`) so they fire
"regardless of which child has focus" within it; the tab-hosted path leaves them unbound on the
view itself and exposes bare `N`/`E`/`D` via `TabOperations` for the owning tab to dispatch
(`ListEditorView.cs:345-357`).

`HeaderEditorView` has no `FilterBox` and no in-place text-editing descendant (headers are edited
via a separate modal `HeaderDialog`), so nothing within its own focus subtree would ever consume a
bare `N`/`E`/`D`/`F` keystroke. The Ctrl modifier was solving a collision that doesn't exist for
its one actual consumer. Separately, the standalone path's `Ctrl+F` binding
(`ListEditorView.cs:93-94`) sits outside the `if (_bindSharedKeys && _filterEnabled)` guard that
already gates its *hint* (`ListEditorView.cs:339`) — so the key itself is always live even when
`_filterEnabled` is false, which is exactly `HeaderEditorView`'s case: pressing `Ctrl+F` there
today silently opens a filter dialog nobody was told exists.

## Goals / Non-Goals

**Goals:**
- Standalone (`bindSharedKeys: true`) New/Edit/Delete bind bare `N`/`E`/`D`, identical to the
  tab-hosted path's key chords — the two paths now differ only in *where* the binding is
  registered (directly on the component vs. hoisted to an owning tab), not in which key.
- Filter's key binding (bare `F`) is registered if and only if the subclass has called
  `EnableFilter()`, in both usage modes — closing the standalone-path gap where the key binding
  and its hint disagreed.
- Update every user-visible string referencing the old `Ctrl+N` (the base class's default
  `EmptyHint` and `HeaderEditorView`'s override) to plain `N`.

**Non-Goals:**
- No change to `bindSharedKeys`'s architectural role (component-owned binding vs. tab-hoisted
  dispatch) — it still exists and still matters for *where* a key is wired up, just not for
  *which* key.
- No change to `SubscriptionsView`/tab-hosted behavior — already bare letters, already correctly
  gates Filter on `_filterEnabled` via `TabOperations` (`ListEditorView.cs:354`).
- No change to `HeaderEditorView`'s own behavior beyond the key/hint text — it still never calls
  `EnableFilter()`, so it now has no Filter binding at all (previously an unadvertised `Ctrl+F`;
  now nothing), matching the proposal's rationale that filtering 1-2 headers was never useful.

## Decisions

**Standalone New/Edit/Delete drop `.WithCtrl`; Filter's binding moves inside the existing
`_filterEnabled` guard.**

```csharp
if (_bindSharedKeys) {
    KeyBindings.Add(Key.N, Command.New);
    KeyBindings.Add(Key.E, Command.Edit);
    KeyBindings.Add(Key.D, Command.DeleteAll);
}
if (_bindSharedKeys && _filterEnabled) {
    AddCommand(Command.FindNext, () => { OpenFilterDialog(); return true; });
    KeyBindings.Add(Key.F, Command.FindNext);
}
```

`AddCommand(Command.FindNext, ...)` moves alongside its `KeyBindings.Add` into the
`_filterEnabled`-gated block, rather than staying unconditional — nothing else calls
`Command.FindNext` on this view, so registering the command handler at all when filtering isn't
enabled was dead setup work, not just a harmless extra binding.

**The `Shortcuts` property's standalone branch drops `.WithCtrl` to match, with no other
structural change** — it already correctly gates the Filter hint behind
`_bindSharedKeys && _filterEnabled` (`ListEditorView.cs:339`); only the key chords in the New/
Edit/Delete/Filter hints change from `Key.X.WithCtrl` to `Key.X`.

**Not merging `Shortcuts` and `TabOperations` into one shared implementation, despite now
returning identical key chords for New/Edit/Delete/Filter.** They still differ in exactly one
place that matters: `Shortcuts` conditionally includes New/Edit/Delete/Filter only when
`_bindSharedKeys` (the tab-hosted case returns none, deferring entirely to `TabOperations` per the
existing comment at `ListEditorView.cs:323-327`), and unconditionally includes the `/`-Search
hint regardless of mode, which `TabOperations` never does. Collapsing two four-line list literals
that already read clearly into a shared helper for the sake of "they're identical now" would trade
today's readability for a saving that doesn't outweigh it (YAGNI) — revisit only if a third
distinct hint set shows up.

**Base `ListEditorView.EmptyHint` default and `HeaderEditorView`'s override both change
`"Ctrl+N"` → `"N"` in their hint text.** `HeaderEditorView` is the only subclass relying on the
base default going forward that also uses `bindSharedKeys: true` — but the base default itself
must change too, since it's `virtual` and any future standalone subclass that doesn't override it
would otherwise show a now-incorrect `Ctrl+N` hint.

## Risks / Trade-offs

- **Removing the Filter key entirely for `HeaderEditorView`** (rather than rebinding it to bare
  `F`) **is a capability removal, not just a rebind** → intentional per the proposal's rationale
  (a header list realistically holds 1-2 entries) and confirmed by the fact that `_filterEnabled`
  was already `false` for it — no user-visible "Filter" hint has ever appeared for headers, so
  nothing observable changes except the previously-undocumented `Ctrl+F` no longer working.
- **If a future standalone (`bindSharedKeys: true`) subclass is added that *does* share its modal
  with a free-text field expecting bare letters, the Ctrl-avoidance rationale this change removes
  would need to be re-added for that specific case** → acceptable: `bindSharedKeys` still exists
  as the extension point: a future subclass with a genuine collision can pass `bindSharedKeys:
  false` and dispatch through an owning tab, or (if truly modal-only with a real collision) that
  specific case can reintroduce a modifier locally rather than every standalone consumer paying
  for a collision only one of them might ever have.

## Migration Plan

Pure key-binding and hint-text change, no persisted state. Verify via `tmux`: open `PublishDialog`
(Alt+P), focus the header list, confirm bare `N`/`E`/`D` create/edit/delete headers and that
`Ctrl+N` no longer does anything; confirm `Ctrl+F`/`F` no longer opens a filter dialog for headers;
confirm the empty-state hint reads "No headers — N to add one"; re-verify `SubscriptionsView`
(Subscribe tab) is unaffected (already bare letters, unchanged).
