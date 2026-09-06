## Context

Tab-hosted lists (`DrillableListView<T>`, `ListEditorView<T>` in tab-hosted mode) currently bind
Ctrl+N/E/D/R/F (plus `ObjectListView`'s Ctrl+S and `TemplatesTab`'s Ctrl+X/Ctrl+O) for their
operations, dispatched by the owning tab per `tab-scoped-list-shortcuts`. These collide with
terminal-level control codes on some setups (confirmed dead: Ctrl+S/Ctrl+Q as XON/XOFF flow
control; classically also Ctrl+H/Ctrl+M/Ctrl+I aliasing Backspace/Enter/Tab).

Every tab surface that hosts these lists has exactly one thing that accepts free-typed text: the
quick-search field (`FilterBox`), reached via `/`. Every `TextField`/`TextView` besides it lives
inside a modal `Dialog<T>` — a separate `Toplevel`, isolated from tab-level key dispatch entirely.
`ListView.KeystrokeNavigator` is already nulled out on every list, so the list itself never
consumes a bare letter either. This makes bare-letter shortcuts safe everywhere *except* while
`FilterBox` itself holds focus — which is exactly why this change also has to tighten how that
field claims and releases focus, not just rename keys.

There is one already-shipped precedent for a bare-letter binding: `LiveUpdatesView` binds `Key.C`
(no Ctrl) for Clear, active only while it holds focus.

## Goals / Non-Goals

**Goals:**
- Replace Ctrl+N/E/D/R/F/S/X/O with bare N/E/D/R/F/S/X/O on every tab-hosted list.
- Make `/` the only way to focus the quick-search field — no Up-arrow-at-list-top entry, no
  Tab/Shift+Tab entry, no mouse-click entry (`CanFocus` gates all three at once).
- Make leaving the field (however it happens) reliably return focus to the list and make the field
  unfocusable again, via a single mechanism rather than per-exit-key bookkeeping.
- Give quick-search real cancel/undo semantics on Esc: revert to the query that was active when
  the field was activated, not just blank it.
- Fix the pre-existing bug where Esc on an already-empty field, at a level with no ascend wiring,
  leaves focus stranded in the field.

**Non-Goals:**
- Standalone/modal list-editor usage (`HeaderEditorView` inside `PublishDialog`,
  `bindSharedKeys: true`) is unchanged — it shares a modal with genuine free-text fields (Subject,
  Payload) that bare letters would collide with, so it keeps Ctrl+N/E/D/F.
- No change to the sticky pattern filter's own modal (`PatternDialog`, Ctrl+F today → bare F) beyond
  the key rename — its dialog-based apply/cancel model is untouched.
- No change to top-level Alt+digit/Alt+letter shortcuts (`MainWindow`'s `topLevelShortcuts`) — those
  already avoid Ctrl entirely and are out of scope.
- No change to the `* ? >` filter-expression grammar or fuzzy-subsequence matching algorithms
  themselves.

## Decisions

### Bare-letter mapping is a direct 1:1 rename
Ctrl+N→N, Ctrl+E→E, Ctrl+D→D, Ctrl+R→R, Ctrl+F→F, Ctrl+S→S (`ObjectListView` download), Ctrl+X→X
(`TemplatesTab` export), Ctrl+O→O (`TemplatesTab` import). No letter needs to move to avoid a
collision — the only existing bare-letter binding in the app (`C` on `LiveUpdatesView`) doesn't
overlap any of these, and the entire mapping lives in `tab-scoped-list-shortcuts`-dispatched code
paths, which stay scoped to whichever list/tab is active per that capability's existing rules.

### `FilterBox.CanFocus` toggles around activation, not a permanently-focusable field
Terminal.Gui v2's own focus model (confirmed via its docs) requires every ancestor of a focused
view to itself be `CanFocus == true`, and setting `CanFocus = false` on a currently-focused view
auto-clears its focus. Toggling `FilterBox.CanFocus` (the container, not the inner `TextField`)
is therefore sufficient by itself to block every entry path at once — Tab-order, arrow-key
navigation, and mouse click — not just the specific one being removed. `/` (bound on the list) is
the only place `CanFocus` gets set `true`, immediately before `SetFocus()`.

**Alternative considered:** leave `CanFocus` permanently `true` and instead remove/guard each entry
key binding individually (Up-arrow, Tab/Shift+Tab toggle). Rejected — it leaves mouse-click entry
open (contradicts "not focusable until `/`"), and it means "is search active" has to be tracked
separately from "is search focused," inviting drift between the two.

### One generic focus-lost hook closes every exit path
```csharp
protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
{
    base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
    if (!newHasFocus) CanFocus = false;
}
```
Overridden on `FilterBox` itself — the same pattern `ListEditorView`/`DrillableListView` already
use for their empty-hint focus styling, where the container's `OnHasFocusChanged` reliably fires
when its focused inner child loses focus. This is what makes mouse-click-away work correctly with
zero new code: there is no explicit "user clicked elsewhere" handler anywhere in the app today, and
none is needed — the framework's own focus-change notification covers it.

Enter and Esc still need their own `KeyDown` handling (to decide *where* focus goes and, for Esc,
to revert text first) but neither needs to set `CanFocus = false` itself; calling `FocusList()`
(which moves focus away) is sufficient to trigger the hook above as a side effect.

**Alternative considered:** set `CanFocus = false` explicitly inside each of the Enter/Esc/Tab exit
handlers. Rejected as the naive approach discussed early in exploration — it's correct but
duplicated three-plus times and silently misses mouse-click-away, which none of today's handlers
address at all.

### Esc snapshots on activation and reverts, rather than clearing
```
"/"           → snapshot := current field text (may be non-empty from a prior session)
typing        → live narrowing, unchanged from today
Esc           → field.Text := snapshot; ApplyFilter(snapshot); FocusList()
Enter/Tab/
arrow/click   → FocusList() (or the framework's own focus move) — text left exactly as typed
```
Because quick-search is live (`ApplyFilter` runs on every keystroke, there's no staged/unapplied
draft), Enter/Tab/arrow/click-away need no explicit "apply" step — whatever's currently typed is
already in effect. Esc is the only exit that needs new state (one string field on `FilterBox`,
captured at activation) and new behavior (restore text, re-apply, then hand focus to the list).

This changes the "Esc clears non-empty search text" requirement into "Esc reverts to the
pre-activation snapshot" — clearing is now just what reverting looks like the first time you
activate search with nothing previously set, not the rule itself. A user editing a previously-set
query across multiple `/` sessions can Esc out of an in-progress edit without losing the query they
had before.

### Esc-on-empty: ascend takes priority, then fall back to plain cancel
`HandleEmptySearchEscape()` keeps its existing "ascend if the list has ascend wiring" behavior
(`ConsumerListView`/`KeyListView`/`ObjectListView`) — Esc-to-ascend is real, useful drill-up
navigation, not something this change touches. Where ascend isn't wired (every top-level list:
`StreamListView`, the bucket lists, `TemplateListView`), the empty-field case now falls through to
the same cancel/defocus path non-empty Esc uses (revert to snapshot — which for an empty field
degenerates to "stay empty" — then `FocusList()`). Today it does neither, leaving focus stranded;
that's the bug this folds in a fix for.

### `ManagementTabs.AdvanceWithinPage`: delete the entry branch, keep the exit branch
```csharp
private bool? AdvanceWithinPage()
{
    for (var view = App?.Navigation?.GetFocused(); view is not null; view = view.SuperView)
    {
        if (view is FilterBox { Target: { } target })
        {
            target.FocusList();          // KEEP — Tab while inside search still exits safely
            return true;
        }

        // DELETE — Tab from the list must no longer enter FilterBox
        // if (view is IFilterable { AttachedFilterBox: { } box }) { box.Focus(); return true; }
    }

    App?.Navigation?.AdvanceFocus(NavigationDirection.Forward, TabBehavior.TabStop);
    return true;
}
```
Tab/Shift+Tab route to this method unconditionally (Terminal.Gui delivers them to the nearest
enclosing `TabGroup`, never to the focused leaf's own `KeyBindings` — see the existing comment in
`ManagementTabs`), so this is the only place a Tab pressed *while already inside* `FilterBox` can
be given a safe meaning. Deleting it entirely (instead of narrowing it to exit-only) would let Tab
fall through to the generic `AdvanceFocus` fallback while still inside the field — exactly the kind
of tab-page focus leak `ManagementTabs` was hand-rolled to prevent for arrow keys and Tab alike.
Keeping the exit branch means Tab (and Shift+Tab) behave identically to Enter while inside the
field: exit to the list, field's `CanFocus` flips false via the generic hook.

Once the entry branch is gone, Tab pressed while the *list* has focus (with nothing else
focusable on that page) falls through to the generic `AdvanceFocus` fallback — which is not new,
untested behavior: `SubscribeTab` has no `FilterBox` attached at all today and already exercises
this exact fallback path unremarked. Streams/Values/Objects/Templates simply start behaving the
way Subscribe already does.

### `AttachFilterBox`'s Up-arrow-at-list-top binding is deleted outright
No snapshot/revert semantics apply to it — it was a pure entry shortcut
(`AddCommand(Command.Up, () => { box.Focus(); return true; })` in both `DrillableListView<T>` and
`ListEditorView<T>`). Removing it means Up at the top of the list is unhandled by the list, and per
`tab-navigation`'s existing "Up Climbs From Content to the Current Tab's Header" requirement,
bubbles straight to the tab header in one press instead of the current two (list → field → header).
`tab-navigation`'s own scenario text needs a small delta since it currently describes climbing
"through" the field as an intermediate stop.

### Alt+K → `?` for the Shortcuts picker, on the same depth-first-focus-first guarantee
`MainWindow` subscribes to its own `KeyDown` C# event to dispatch `topLevelShortcuts` (Alt+1..5,
Alt+P, Alt+Q, Alt+K). Terminal.Gui's key-dispatch model (confirmed via its own docs) is strictly
depth-first: `NewKeyDownEvent` recurses into the currently-focused subview *before* the current
view's own `OnKeyDown`/`KeyDown`/`KeyBindings` get a turn, and only continues unwinding upward if
the deeper call left the key unhandled. A focused `TextField` (e.g. `FilterBox`, or any dialog
field) always consumes a plain character keystroke as text input first, marking it handled — so
`MainWindow`'s own `KeyDown` subscriber never sees that keystroke at all. This is the exact
mechanism that already makes bare-letter list shortcuts safe next to typed text, and it applies
identically to a bare `?` bound at the `MainWindow` level: it only reaches `MainWindow` when
nothing more specific (a focused text field) has already claimed it. Modal dialogs (`PatternDialog`
included, where `?` is a meaningful wildcard character in the filter-expression grammar) are
separate `Toplevel`s outside `MainWindow`'s dispatch chain entirely, so they're unaffected either
way. `?` replaces Alt+K specifically (not some other punctuation) because it echoes `/`'s existing
role as a punctuation-key global shortcut, and reads naturally as "help."

**Alternative considered:** leave Alt+K as-is. Rejected per explicit user request — no functional
problem with Alt+K was raised, this is a legibility/mnemonic preference (`?` is a more conventional
"show me the shortcuts" key than an arbitrary letter).

## Risks / Trade-offs

- **[Risk]** `CanFocus` toggling on a container while framework focus-change machinery is mid-flight
  (e.g. setting it inside `OnHasFocusChanged` itself) is a pattern worth confirming empirically,
  not just from docs. → **Mitigation**: drive it via `tmux` (per this project's established
  UI-verification approach) before considering the change done — activate search, exit via each of
  Enter/Esc/Tab/arrow/mouse, and confirm focus lands correctly and `CanFocus` state doesn't leave
  the field in a stuck-focused-but-flagged-unfocusable state.
- **[Risk]** Removing the Tab-entry branch changes what Tab does on every tab with a `FilterBox`
  attached (previously: toggle into search; now: generic `AdvanceFocus`, same as `SubscribeTab`
  today). → **Mitigation**: this is a deliberate, user-confirmed behavior change (see proposal's
  **BREAKING** note), and the fallback path is already shipped and unremarked on `SubscribeTab`, so
  the risk is calibration (does it feel right on every tab), not correctness.
- **[Trade-off]** Esc-as-revert is slightly more complex than Esc-as-clear (one extra field, one
  extra re-apply call) for a real usability win (undo an in-progress search edit without losing a
  previously-set query) — accepted per explicit user confirmation during exploration.

## Migration Plan

Single-PR, no data migration or rollback concerns — this is pure UI key-binding and focus-behavior
code with no persisted state. Verify interactively via `tmux` per tab (Streams, Values, Objects,
Templates, Subscribe) before considering the change complete, since this class of change (focus
routing, key dispatch) is exactly what automated build/type checks won't catch.

## Open Questions

None outstanding — all prior open questions (search-indicator styling, Tab-as-exit vs. Tab-as-leak,
Esc-as-clear vs. Esc-as-revert) were resolved during exploration prior to this proposal.
