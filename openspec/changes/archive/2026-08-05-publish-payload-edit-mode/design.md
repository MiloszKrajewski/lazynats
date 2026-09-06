## Context

`PublishTab`'s Payload field (`_payloadView`, a `TextView`) is the only band in the tab that's a
live, always-editable multiline buffer — Subject is a single-line `TextField`, Headers is a
`ListEditorView<T>` that only ever edits via a modal (`PatternDialog`). That's why Payload is the
one place Tab/arrow keys get fully consumed instead of bubbling to focus navigation.

Two existing pieces this design builds on:
- `ListEditorView<T>` already implements `IShortcutSource` to advertise Ctrl+N/E/D, and
  `ShortcutAggregator.Collect` already walks the focused view's ancestor chain collecting from
  any `IShortcutSource` it finds — this is the mechanism Payload's new hints will reuse, not a
  new one.
- `ShortcutTracker` (`ShortcutAggregator.cs`) already recomputes the aggregated shortcut set, but
  only in response to `IApplication.Navigation.FocusedChanged`, and per its own comment was
  deliberately left unwired from `MainWindow`'s `StatusBar` in the change that introduced it.

A relevant constraint surfaced while tracing how `ShortcutTracker` would get built: every existing
`App!`/`App?` reference in this codebase (`MainWindow.cs`, `PublishTab.cs`,
`Subscriptions/*.cs`) appears inside a deferred callback (a lambda wired up during construction,
invoked later), never directly in a view's own constructor body. That's consistent with
`View.App` only being assigned once `Application.Run<T>()` has already constructed the instance —
not before. `ShortcutTracker` needs a non-null `IApplication` synchronously, at construction time,
to subscribe to `Navigation.FocusedChanged`, so it can't be built the way `MainWindow` builds its
`Shortcut` objects today.

## Goals / Non-Goals

**Goals:**
- Payload gains a Navigate/Edit gate: Ctrl+E or Enter enters Edit, Esc leaves it back to
  Navigate; while in Navigate, Up/Down and Tab/Shift-Tab move focus to Subject/Headers/Send.
- While in Edit, Payload's key handling (typing, arrows, Tab, everything) is bit-for-bit
  unchanged from today.
- `ShortcutTracker` is wired into `MainWindow`'s `StatusBar`, and gains a way to be refreshed
  without a focus change, since Navigate/Edit toggling never moves focus off `_payloadView`.
- Headers' existing Ctrl+N/E/D hints become visible in the `StatusBar` as a side effect of the
  wiring above (no changes to `ListEditorView<T>` itself).

**Non-Goals:**
- No new visual styling to distinguish Navigate vs. Edit — deferred to a separate, more general
  focus-hint design already being worked on independently.
- No change to Subject's or Headers' existing key handling or focus behavior.
- No change to mouse interaction with Payload — whatever Terminal.Gui does today for a mouse
  click is left as-is; this app is keyboard-first (see CLAUDE.md) and mouse isn't the design
  target here.
- Not building a generic reusable "gated text editor" component. This is scoped to `PublishTab`'s
  Payload field only, per how the problem was framed going in; if the same need shows up
  elsewhere later, generalize then.

## Decisions

**1. Interception mechanism: hook `_payloadView.KeyDown`, don't subclass or override commands.**
`TextView`'s own arrow/Tab handling is internal to the (obsolete but still-used) base class.
Overwriting it via `AddCommand` would replace its handling outright, with no supported way to
fall back to the original behavior while in Edit mode. A `KeyDown` handler in `PublishTab` that
inspects the current mode and either (a) performs focus navigation and marks the event handled
(Navigate mode, nav/entry keys), or (b) lets the event fall through untouched (Edit mode, or any
key that isn't part of the gate) keeps Edit-mode behavior byte-for-byte identical to today, with
no risk of drift from `TextView`'s real implementation.

**2. Key set while in Navigate: only Up/Down/Tab/Shift-Tab drive focus; everything else is
swallowed.** Up/Down mirror Shift-Tab/Tab (Left/Right stay inert, matching their current no-op
behavior everywhere else in the tab — no new semantics invented for them). Ctrl+E or Enter enters
Edit. All other keys, including plain typing, are consumed and dropped while in Navigate.
Considered letting unrecognized keys fall through to `TextView` unconditionally instead — rejected
because it would make the gate cosmetic: you could still edit payload text without ever pressing
Ctrl+E, which defeats the reason for this change.

**3. Mode lives as a private field on `PublishTab`, not a shared/reusable state machine type.**
Matches the "PublishTab-only, for now" scope from the proposal; building a generic component now
would be speculative given there's currently exactly one place that needs it.

**4. Discoverability: `PublishTab` implements `IShortcutSource`.** Exposes `Ctrl+E`/`Enter`:
"Edit" while in Navigate, `Esc`: "Stop Editing" while in Edit. `PublishTab` is already an ancestor
of whatever's focused inside it, so `ShortcutAggregator.Collect`'s existing ancestor walk picks
this up with no new plumbing — same mechanism `ListEditorView<T>` already uses for Ctrl+N/E/D.

**5. `ShortcutTracker` gains a public `Refresh()`.** Its only recompute trigger today is
`Navigation.FocusedChanged`, but toggling Navigate/Edit never moves focus — it stays on
`_payloadView` the whole time, only an internal flag changes. `Refresh()` re-runs the same
recompute-and-raise logic on demand; `PublishTab` calls it after every mode toggle. Considered
making the toggle blur-and-refocus `_payloadView` to piggyback on the existing
`FocusedChanged`-only path instead — rejected as a hack that risks a visible flicker and leans on
undocumented refocus semantics for something a three-line method solves directly.

**6. `IApplication` becomes a DI singleton; `Program.cs`'s `Application.Create()` moves before
`Services.Configure(services)`.** `MainWindow` then resolves `ShortcutTracker` via
`Services.Root.GetRequiredService<ShortcutTracker>()`, the same way it already resolves
`SubscriptionRegistry`/`NatsConnection`/etc. Considered instead deferring `ShortcutTracker`
construction to a post-construction lifecycle hook on `MainWindow` (something that fires once
`View.App` is actually set) — rejected as introducing a second, `MainWindow`-specific pattern for
"things that need `App`" alongside the DI-based one every other service already uses, for no real
benefit.

**7. `StatusBar` composition: today's fixed shortcuts stay exactly as they are; a dynamic segment
is appended/synced from `ShortcutTracker.ShortcutsChanged`.** Each `ShortcutHint` converts to a
`Shortcut` the same way `MainWindow` already builds the static ones (`Text`/`Key`/`Action`). The
existing bespoke visibility wiring (e.g. `clearShortcut` toggling on `liveUpdates.HasFocusChanged`)
is untouched.

## Risks / Trade-offs

- **Swallowing all keys in Navigate mode** → could feel unresponsive if the user forgets they're
  in Navigate and starts typing with no visible feedback. The only mitigation in this change is
  the `StatusBar` hint (Decision 4), since a dedicated visual state is explicitly deferred; may
  need revisiting once the general focus-hint frame design lands.
- **`KeyDown`-hook interception depends on `TextView` raising a cancelable `KeyDown` before its
  own internal command dispatch runs** → needs confirming empirically against the installed
  Terminal.Gui 2.4.x build during implementation; `doc/terminal-gui-howto.md` doesn't currently
  document this specific event-ordering guarantee for `TextView`.
- **Reordering `Program.cs`'s startup sequence** → low risk, since `ApplyColorTheme()` already
  documents `Application.Create()` as an early, load-bearing step — but worth double-checking
  nothing else implicitly relies on `Services.Configure` running before `Application.Create()`.
- **`StatusBar` becomes dynamic app-wide, not just in `PublishTab`** → any other `IShortcutSource`
  (currently just `ListEditorView<T>`, used by both Headers and `SubscribeTab`) starts actually
  appearing on screen for the first time. Worth a quick pass to confirm none of that existing
  hint text reads oddly now that it's actually visible.

## Migration Plan

No persisted state or data migration involved. Ships as a normal code change; no rollback concerns
beyond reverting the commit.

## Open Questions

- Exact Terminal.Gui v2 API for advancing focus to the next/previous focusable sibling from an
  arbitrary `KeyDown` handler (as opposed to letting `Tab` bubble naturally) needs confirming
  during implementation — not yet verified against the installed package version.
- Should Left/Right eventually gain their own navigation meaning, or stay permanently inert?
  Left inert for now (Decision 2); revisit only if this comes up again.
