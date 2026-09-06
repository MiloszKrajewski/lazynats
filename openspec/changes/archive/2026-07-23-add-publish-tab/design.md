## Context

`MainWindow` (`src/lazynats/MainWindow.cs`) currently hosts a fixed two-pane layout: a
`FrameView` wrapping `SubscriptionsView` on top (`Dim.Percent(75)`), and a `FrameView` wrapping
`LiveUpdatesView` below. Neither tabs nor any `ColorScheme`/`Attribute` customization exist
anywhere in the codebase today — every view uses the default Terminal.Gui v2 theme and
`FrameView`'s bordered chrome. `SubscriptionsView` establishes the codebase's existing
add/remove-list idiom: a `TextField` + `Accepted` handler to add, a `ListView` bound to an
`ObservableCollection<string>`, and `Key.Delete` bound via `AddCommand`/`KeyBindings` to remove
the selected item.

This change introduces the app's first use of `Terminal.Gui.Views.Tabs` and its first deliberate
departure from default `FrameView` chrome (flat, alternating-background styling), while otherwise
reusing established patterns rather than inventing new ones where avoidable.

Note: an earlier draft of this design assumed a `TabView`/`Tab` API (consistent with older
Terminal.Gui v2 docs/memory). The installed package (Terminal.Gui 2.4.10) has no such types —
confirmed via reflection and its XML docs, which state "In earlier versions of Terminal.Gui,
`TabView` provided similar functionality." The actual type is `Tabs`: a `View` where any added
SubView becomes a tab automatically, using that SubView's own `Title` as the tab caption, and
where the focused SubView is the selected (front-most) tab. `doc/terminal-gui-howto.md` has been
updated with this correction.

## Goals / Non-Goals

**Goals:**
- Add a Publish tab, positioned after Subscriptions, for composing and sending a single NATS
  message (subject, headers, text payload).
- Introduce `TabView` in the management area without disturbing the live feed pane's layout or
  behavior.
- Keep the headers editor keyboard-only, following the same interaction shape already
  established by `SubscriptionsView` (input row + `Accepted` to add, `Key.Delete` to remove).
- Gate Send on Subject non-emptiness only; reflect invalid state visually, not via popup.

**Non-Goals:**
- Binary/hex/base64/JSON-typed payload editing (`doc/UI.md` scopes this as a later stretch goal;
  payload is plain text for now).
- Message templates (explicitly a stretch goal in `doc/UI.md`, not part of this change).
- Per-row mouse buttons for headers (explicitly rejected during design exploration in favor of
  keyboard-only).
- Any change to `nats-subscriptions` requirement-level behavior — only its container changes.
- Prefilling Subject from the selected subscription pattern, and duplicate-header-key handling
  (allow multi-value vs overwrite) — left open, not decided in this change (see Open Questions).

## Decisions

### Tabs replace the fixed Subscriptions frame
`MainWindow` gets a `Tabs` in place of `subscriptionsFrame`. `SubscriptionsView` (unchanged,
`Title = "Subscriptions"`) and the new `PublishView` (`Title = "Publish"`) are added to it in
that order via `Add`, and `Value` is set to `SubscriptionsView` so it's selected by default. The
live feed `FrameView` stays exactly as-is below it, unaffected by tab switching. This generalizes
the container so future tabs (Streams, Consumers, KV, OBJ per `doc/UI.md`) slot in the same way
— just add another `Title`-bearing `View`.
- Alternative considered: keep Publish as a modal, per the original `doc/UI.md` wording. Rejected
  per explicit user direction — a tab keeps the compose form visible/persistent across sends,
  which suits the "keep form filled in, resend" workflow better than a transient dialog.

### Headers editor: single ListView + Ctrl+N/Ctrl+D/Ctrl+E commands, no buttons
A key `TextField` and a value `TextField` (the "input row") sit above a `ListView` of
`"key   value"` formatted strings backed by an `ObservableCollection<HeaderPair>`-like list. The
input row also holds an implicit `editingIndex` (null when composing a new pair):
- **Ctrl+N** clears the input row and discards `editingIndex` (starts a fresh entry).
- **Ctrl+E** loads the selected list row's key/value into the input row and sets `editingIndex`
  to that row's position, for in-place editing.
- **Ctrl+D** removes the selected list row directly (independent of the input row); if it was
  the row currently loaded for editing, the input row is cleared too.
- **Enter** (via `Accepted` on the value field) commits the input row: appends a new pair when
  `editingIndex` is null, or overwrites the pair at `editingIndex` otherwise, then clears the
  input row.
- `Key.Delete` is deliberately **not** bound to row removal — it's left free for ordinary text
  editing within the key/value fields.
- Alternative considered: `SubscriptionsView`'s original Enter-to-add/Delete-to-remove-only
  scheme (no dedicated edit command). Superseded — the user wanted in-place editing and wanted
  Delete reserved for text editing, so Ctrl+N/Ctrl+D/Ctrl+E replaces it.
- Alternative considered: real per-row `[+]`/`[-]` `Button` views for mouse support. Rejected —
  explicitly called "stupid" as decoration and unwanted as extra real views; keyboard-only is
  simpler and consistent with the one interaction model already in the codebase.
- Alternative considered: inline-editable 2-column `TableView`. Rejected in favor of reusing the
  proven, lower-risk list pattern rather than introducing `TableView` editing (unused elsewhere
  in this codebase) for a first pass.

### Flat visual styling via alternating background, scoped to Publish tab
Section groupings (Subject / Headers / Payload) and alternating header rows use background-color
banding (distinct `Terminal.Gui.Drawing.Attribute`/`Scheme` per band) instead of `FrameView`
borders, to satisfy the "as flat as possible" requirement. This is new to the codebase — no
`Scheme`/`Attribute` customization exists elsewhere — so it's scoped to this tab only; retrofitting
Subscriptions/Feed frames to match is explicitly out of scope here.
- Risk: exact rendering of banding and of `Tabs`'s own header border (its default `TabLineStyle`
  draws a bordered tab header) is unverified against the actual running renderer and must be
  checked by running the app, not assumed from doc reading alone. `Tabs.TabLineStyle =
  LineStyle.None` is available if the default header chrome doesn't fit "as flat as possible."

### Send validation: Subject-only gate, no popups
Send `Button.Enabled` toggles on whether Subject is non-empty (checked on every keystroke of the
Subject field). When disabled, Subject's label/field is rendered in a distinct (e.g. red)
`Attribute` to flag why. No other field blocks Send — payload may be empty (valid in NATS), and
a partially-filled header input row is treated as an unsubmitted draft, not an error.
- Alternative considered: also flag an orphaned header input (one of key/value filled, other
  blank). Rejected — user chose Subject-only scope to keep validation minimal for this change.

### Send behavior: status bar feedback, form retained
On Send, publish via the existing `NatsConnection` (already available through `Services.Root`,
same as `SubscriptionRegistry`'s usage). Report the outcome (subject published to, or error
message) via the `StatusBar` already present in `MainWindow`, matching the transient-hint style
used for the `Clear` shortcut. Subject/Headers/Payload are left populated after a successful
send so the same message can be tweaked and resent.
- Alternative considered: `MessageBox` confirmation + clear form. Rejected — interrupts a rapid
  resend loop, which is the primary expected usage (repeated test publishes).

## Risks / Trade-offs

- [New `Tabs` usage is unverified in this codebase] → Verified live: both tabs render in the
  right order, clicking a tab header switches the front-most/focused content correctly, and the
  live feed pane below is unaffected.
- [Alternating-background styling is unverified — `ColorScheme`/`Attribute` APIs untested here]
  → Verified live (captured raw ANSI colors): Subject/Payload bands render at RGB (118,118,118),
  the Headers band at (128,128,128), and the invalid-Subject indicator renders red-on-black,
  reverting to the inherited band color once Subject is non-empty.
- [Ctrl+N/Ctrl+D/Ctrl+E must work regardless of which header subview has focus — key field,
  value field, or the list] → Verified live: `KeyBindings` added on `PublishView` itself
  (not the individual fields/list) do bubble up correctly from whichever child has focus, same
  mechanism `SubscriptionsView` already relies on for `Key.Delete`. However, `TextField`'s own
  built-in default bindings claim `Ctrl+D` ("delete char forward") and `Ctrl+E` ("move cursor to
  end"), and `ListView`'s own claim `Ctrl+N` ("move down") — these are consumed by the focused
  child *before* bubbling, so in practice: Ctrl+N clears the input row only while focus is in a
  key/value field (not the list, where it moves selection instead), and Ctrl+E/Ctrl+D only edit
  or delete the selected row while focus is on the list (not a text field, where they move the
  cursor / delete a character instead). This isn't literally "any focus" as first scoped, but it
  matches the natural workflow (type in the fields, then move to the list to edit/delete a row),
  so no further change was made.
- [Plain `View` containers used to band Subject/Headers/Payload sections might not properly host
  focusable descendants] → Confirmed as a real issue during live testing, not just theoretical:
  the three band wrapper `View`s originally had no explicit `CanFocus`, and although each
  contained genuinely focusable children (`TextField`/`ListView` with `CanFocus` true), focus
  could enter the Publish tab (visually, via `Tabs.Value`) but a subsequent `Tab` press bounced
  straight back out to the Subscriptions tab instead of landing on any Publish control — pressing
  Tab found nothing focusable to descend into. Setting `CanFocus = true` on each band `View`
  fixed it; `SubscriptionsView`/`FrameView` didn't need this because their focusable controls are
  direct children with no intermediate band wrapper.
- [Scoping flat styling to only the Publish tab creates visual inconsistency with
  Subscriptions/Feed panes] → Accepted for now; a follow-up change can retrofit the rest of the
  app once the pattern is validated here.
- [`Terminal.Gui.Views.TextView`, used for the Payload field, is obsolete in the installed
  package version — superseded by `EditorView` from a separate `tui-cs/Editor` package] →
  Accepted for now: `TextView` still compiles and functions (warning only), and this project's
  established policy (`lazynats.AotProbe`) is to validate a new package's AOT/trim behavior
  before relying on it, which hasn't been done for `Editor`/`EditorView`. Revisit as a follow-up
  once that package has been probed.

## Migration Plan

Purely additive UI change; no data migration. Rollout is a normal code change — no feature flag,
since there's no persisted state to migrate and the existing Subscriptions behavior is
unaffected (just re-parented under a tab).

## Open Questions

- Should Subject prefill from the currently-selected subscription pattern? Left undecided;
  default to blank Subject for this change.
- Duplicate header keys: allow as multi-value (NATS permits it) or overwrite the existing row?
  Left undecided; implementer should pick the simpler option (append, allow duplicates) unless
  it proves confusing in practice, and can revisit later.
- Exact `TabView` header rendering (line under tabs, active-tab indicator) and how well it can be
  made to look "flat" — to be confirmed empirically during implementation, not decided here.
