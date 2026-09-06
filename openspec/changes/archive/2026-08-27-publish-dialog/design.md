## Context

`PublishTab` (`src/lazynats/Publish/PublishTab.cs`) is a permanent management tab with three
bands (Subject, Headers, Payload) and a Send button. Payload is a `TextView`, and because
`TextView` normally consumes Tab (inserts a literal tab) and arrow keys (moves the text cursor),
the tab added a Navigate/Edit mode gate around it: Navigate is the default and lets Up/Down/Tab
bubble up as focus movement, Ctrl+E or Enter switches to Edit (where TextView's own key handling
takes over unchanged), and Esc switches back. This added an `IShortcutSource` implementation, a
`_payloadEditing` bool, a `KeyDown` interceptor, and a `HasFocusChanged` reset-on-refocus rule —
all just to keep Tab usable for focus movement.

`CreateKeyDialog` (`src/lazynats/KVStore/CreateKeyDialog.cs`) has an equivalent multi-line Value
`TextView` and none of this: it sets `TabKeyAddsTab = false`, which stops `TextView` from
intercepting Tab at all, so Tab reaches normal focus-advance handling with no mode concept
needed. (Up/Down inside a multi-line `TextView` legitimately move the text cursor rather than
focus, which is standard `TextView` behavior and unrelated to the Tab problem the gate was built
for.) This design carries that fix over to Publish and uses the removal of the extra mode as the
opportunity to also move Publish out of the tab strip into a dialog, since composing/sending one
message is a one-off action, not a standing view.

## Goals / Non-Goals

**Goals:**
- Replace `PublishTab` with a modal `PublishDialog`, opened from anywhere via Alt+P, containing
  Subject, Headers, Payload, and Cancel/Send.
- Drop the Payload Navigate/Edit gate; Payload is always directly editable, using
  `TabKeyAddsTab = false` the same way `CreateKeyDialog`'s Value field already does.
- Keep the dialog open after Send (success or failure) with fields intact, so the same message
  can be tweaked and resent, matching the current tab's retention behavior for the span of one
  dialog session.
- Renumber the remaining management tabs (Streams/KV/OBJ) and their Alt+digit shortcuts to close
  the gap left by removing Publish.
- Reuse `HeaderEditorView`/`HeaderDialog`/`HeaderColonPresenter` unchanged.

**Non-Goals:**
- Message templates (already a stretch goal in `doc/UI.md`, untouched here).
- Binary/hex payload editing.
- Persisting Subject/Headers/Payload across separate dialog opens (Cancel/Esc, then a later
  Alt+P, starts empty) — only retention within one continuously-open dialog session is in scope.

## Decisions

**`PublishDialog` is a plain `Dialog`, not a `Dialog<T>`.** Every existing `Dialog<T>` in this
codebase (`CreateKeyDialog`, `CreateBucketDialog`, `HeaderDialog`, ...) commits once and closes,
handing a result back to its caller who then acts on it. Publish is different: Send is meant to
be pressed repeatedly without closing the dialog (the "edited and resent" requirement), and
nothing outside the dialog needs the composed message as a value — the dialog performs the
`PublishAsync` call itself, exactly like `PublishTab` already does with its injected
`NatsConnection`. Modeling Send as a `Dialog<T>` commit-and-close would force reopening the
dialog (and losing the inline status) for every resend.

**Payload uses `TabKeyAddsTab = false`, no mode gate.** This removes `_payloadEditing`, the
`KeyDown` interceptor, the `HasFocusChanged` reset rule, and the `IShortcutSource`
implementation entirely — `PublishDialog` has no dynamic shortcut hint to publish, so it doesn't
implement `IShortcutSource` at all. Up/Down inside Payload keep `TextView`'s normal cursor-move
behavior (unlike the old Navigate mode, which repurposed them as focus-move); Tab/Shift-Tab move
focus to/from Subject, Headers, and Send, same as every other band.

**Send feedback is an inline label in the dialog, not the main status bar.** The status bar's
dynamic tail (`ShortcutTracker`) is driven by focus and changes as the user moves between
Subject/Headers/Payload, so a status message posted there would be transient and easy to miss.
`PublishDialog` instead carries its own status `Label` above the button row, updated after each
Send attempt and cleared implicitly when the dialog closes. `MainWindow` drops
`publishStatusShortcut` and the `StatusChanged` event it wired up; `PublishDialog` needs no
public event for this.

**Alt+P is a global `Shortcut` in `MainWindow`, matching Alt+Q.** Its `Action` constructs a new
`PublishDialog(connection)` and runs it with `App!.Run(dialog)` — the same blocking-modal pattern
already used for `HeaderDialog`/`PatternDialog`. `PublishDialog` itself opens `HeaderDialog` via
`App!.Run(dialog)` from inside `HeaderEditorView` when the user edits a header, i.e. Publish's
own dialog is itself the parent of another modal dialog. Terminal.Gui supports stacking modal
`Application.Run` calls (this is how e.g. `KvTab` already opens `CreateKeyDialog` while other
dialogs could in principle be open), so this is confirmed safe.

**Opening the dialog from the global Alt+P `Shortcut` is deferred via `AddTimeout(TimeSpan.Zero,
...)`, not called directly from the `Action`.** `publishShortcut.Action` runs synchronously from
inside the very Alt+P key dispatch that triggered it. Calling `App!.Run(new PublishDialog(...))`
directly from there pumps a nested modal loop *while that same keypress is still being
processed*; empirically, that nested loop re-observes the same in-flight Alt+P as an unhandled
key, which bubbles back up to this same global binding and recursively opens another
`PublishDialog` on top - repeating for as many iterations as it takes for the input to be fully
consumed (observed stacking ~10+ deep from a single keypress). The stacked, still-open dialogs
looked indistinguishable from a single dialog that "won't close" (Cancel/Esc only ever closed the
topmost of several) and "clears its own fields" (each newly-stacked instance was fresh and
empty) - a misleading symptom that cost significant debugging time before the re-entrancy was
identified via `KeyDown` logging showing over a dozen `Alt+P` events fire from one keypress.
Wrapping the `Run` call in `AddTimeout(TimeSpan.Zero, ...)` defers it to the next main-loop
iteration, after the triggering key dispatch has fully unwound, which eliminates the re-entrancy.
This risk is specific to a *global, `BindKeyToApplication = true`* shortcut whose action opens a
modal dialog - the existing tab-switching shortcuts in `MainWindow` are safe because their
actions are synchronous, non-blocking state changes (`tabs.Value = ...`) that never pump a nested
loop.

**Each Alt+P press starts a fresh, empty dialog.** `MainWindow` doesn't hold a persistent
`PublishDialog` instance; Alt+P always `new`s one up, mirroring how every other create/edit
dialog in the app (`CreateKeyDialog`, `CreateBucketDialog`) is constructed fresh per-open. Only
retention *within* one open dialog (across repeated Sends) is required.

**Field layout mirrors `CreateBucketDialog`/`CreateKeyDialog`, but wider.** Fixed-width
`EditFrame`-wrapped fields at explicit `Y` offsets, `Padding.Thickness` top value `1` per the
project's dialog-spacing convention, Cancel added before Send so Send is the last-added,
Enter-activated default button, and `OnAccepting` overridden to return `true` so Enter on
Subject (or the Headers list, when it doesn't itself consume Enter) doesn't fall through to
`Dialog`'s default "unhandled Accept closes the dialog" behavior. The field width itself departs
from the 43 columns those other dialogs use: Publish's Subject/Headers/Payload routinely carry
long NATS subjects and JSON payloads, so `PublishDialog` uses 76 columns instead, giving those
fields the extra room.

## Risks / Trade-offs

- [Nested modal `Application.Run` (PublishDialog → HeaderDialog) is a new combination for this
  codebase — existing modal-from-modal calls are all single-level] → Verify manually
  (tmux-driven) during implementation: open Publish, add/edit a header, confirm the header dialog
  closes back into a still-functional Publish dialog and Send still works afterward.
- [Dropping `IShortcutSource` from Publish removes the Ctrl+E discoverability hint from the
  status bar] → No mitigation needed: the hint existed only to advertise the mode gate being
  removed here, so there's nothing left to discover.
- [Alt+P must not collide with an existing global binding] → Checked: `MainWindow`'s current
  global shortcuts are Alt+Q and Alt+1..5; Alt+P is free. `_Send`'s own mnemonic is Alt+S, which
  also doesn't collide.
