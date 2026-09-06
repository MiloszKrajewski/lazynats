# UI Design

This is the design reference for `lazynats`'s terminal UI: what exists today, what's planned,
and what's still undecided. For the authoritative behavior spec of a shipped feature, see
`openspec/specs/<capability>/spec.md` — this document is the map; specs are the contract.

## Layout

```
┌─────────────────────────────────────────────┐
│ Management area (tabs)                 ~75% │
│ [Subscribe] [Publish] ...                   │
│                                             │
├─────────────────────────────────────────────┤
│ Live Feed                              ~25% │
│                                             │
├─────────────────────────────────────────────┤
│ Status bar (shortcuts, transient messages)  │
└─────────────────────────────────────────────┘
```

Two stacked areas: a tabbed management area on top, a single global live feed below, with a
status bar along the bottom. `MainWindow` currently splits them 75/25 (`Dim.Percent(75)` for the
tabs, the feed filling the rest above the status bar) — a fixed layout, not yet user-resizable.

## Management tabs

| Tab | Title | Shortcut | Status |
|---|---|---|---|
| Subscribe (live NATS monitoring) | `Su[b]scribe` | Alt+B | Built |
| Publish (compose/send a message) | `[P]ublish` | Alt+P | Built |
| Streams (durable streams) | `[S]treams` | Alt+S | Planned |
| Consumers (durable consumers on streams) | `[C]onsumers` | Alt+C | Planned |
| KV stores (key/value stores) | `[K]V` | Alt+K | Planned |
| OBJ stores (object stores) | `[O]BJ` | Alt+O | Planned |

Alt+letter shortcuts work from anywhere in the app, including from inside another tab's content
(`tab-navigation` spec). Subscribe/Publish is a matched verb pair (NATS's own `nats sub`/
`nats pub` vocabulary); Subscribe uses Alt+B rather than Alt+S so it won't collide with the
future Streams tab. Shortcuts for unbuilt tabs are reserved above so later additions don't need
to renumber anything already shipped.

**Open question:** do Consumers get their own tab, or live inside the Streams tab (e.g. as a
drill-down from a selected stream)?

### Tab keyboard model

Tab content and each tab's own header are separate focus targets (`tab-navigation` spec):

- **Up**, unhandled by whatever's focused inside a tab's content, climbs to that same tab's
  header — never to a different tab's header, and never as a side effect of Up bubbling further.
- **Down** on a focused header re-enters that tab's content.
- **Left/Right** change the selected tab only while a header is focused; inside content they're
  left for the content itself (e.g. a list editor's own navigation) and never leak into a tab
  switch.

`ManagementTabs` (a `Tabs` subclass) implements this by absorbing all four arrow-key commands
itself rather than relying on `Tabs`'s stock arrow-key behavior, which conflates "content
declined this key" with "cycle to another tab."

## Live feed

A single global, deduplicated feed merging messages from every active subscription — not
scoped to whichever subscription is currently selected in the Subscribe tab (`live-feed` spec).

**Pipeline:** `SubscriptionRegistry` runs one background reader task per active pattern
subscription → each wraps a received `NatsMsg<byte[]>` in a `FeedEnvelope` (receipt timestamp +
originating subscription id) → all envelopes land in one shared `Channel<FeedEnvelope>` →
`FeedReaderLoop` drains available messages in a batch and dispatches them to the UI thread in a
single call (so UI-thread marshaling doesn't scale with per-message throughput) →
`MessageDeduplicator` collapses envelopes whose `hash(subject + headers + payload)` was already
seen within a short trailing window, so a message matching two overlapping subscription patterns
(e.g. `invoices.>` and `invoices.get.*`) shows once, not twice → `LiveLogDataSource` /
`FeedRowFormatter` render surviving envelopes into `LiveUpdatesView`'s list, one row per message.

**Row content today** (`FeedRowFormatter`): `HH:mm:ss.fff`, subject, headers rendered as
space-joined `key=value`, payload decoded as UTF-8 text — all on one line. The feed retains the
most recent 128 messages, evicting the oldest as new ones arrive; a "follow" behavior keeps the
view scrolled to the newest row as long as the user hadn't scrolled away from the bottom.

**Not yet built:**
- **JSON-aware rendering.** `doc/UI.md`'s original intent — pretty-print payloads that parse as
  JSON, render UTF-8 text as text — isn't implemented; payload is always raw-decoded UTF-8 today.
- **Drill-down ("go in").** Selecting a row currently pops a bare `MessageBox` with just the
  subject. A real detail view (full headers, full payload, JSON-formatted where applicable) is
  still to design.
- **In-view header suppression** is already correct: `LiveUpdatesView` renders no heading of its
  own, relying on the hosting `FrameView`'s `" Live Feed "` title/border.

Press **C** (only while the feed has focus) to clear it.

## Publish tab

Compose and send one NATS message — subject, headers, payload — entirely by keyboard
(`nats-publish` spec). Not a modal: a persistent tab, inserted right after Subscribe.

- **Subject** — single-line field. Empty subject disables Send and paints the field in an
  invalid style (red-on-dark-gray today); no other field blocks sending.
- **Headers** — an editable key/value list, keyboard-only, no per-row buttons:
  - **Ctrl+N** clears the input row and any in-progress edit.
  - Typing a key/value and pressing **Enter** appends a new pair (or, if a pair was loaded via
    Ctrl+E, updates that pair in place instead of appending).
  - **Ctrl+E** loads the selected pair back into the input row for editing.
  - **Ctrl+D** removes the selected pair. Plain **Delete** is left alone for in-field text
    editing, not bound to row removal.
  - These bindings are attached to the whole tab, not the individual header fields/list, so they
    fire regardless of which of the three currently has focus.
- **Payload** — a multi-line text area (`TextView`; `Editor`'s multi-caret/folding/highlighting
  isn't needed for a "just text or JSON" box, so the simpler, obsolete-but-AOT-clean control is
  used deliberately — see `PublishView.cs`).
- **Send** publishes via the connected `NatsConnection`, UTF-8-encoding the payload text, and
  reports success/failure through the status bar without clearing the form — so the same message
  can be tweaked and resent.

Payload is text-only for now (covers JSON and UTF-8 text). Loading a binary file, and a hex
editor for binary payloads, are both explicitly deferred — hex editing depends on how much work
a AOT-clean control turns out to need.

## Subscribe tab

Manages the set of active subject-pattern subscriptions that feed the live feed
(`nats-subscriptions` spec):

- Lists active patterns (e.g. `invoices.>`).
- **Ctrl+N** adds a subscription: opens `PatternDialog`, and on commit starts a real
  `NatsConnection.SubscribeAsync<byte[]>` for that pattern under a fresh `Guid` identity.
- **Ctrl+D** removes the selected subscription, cancelling its background reader and NATS
  subscription.
- **Ctrl+E** is a convenience for "change the pattern": it opens the same dialog pre-filled, and
  on commit removes the old subscription and adds a new one — a NATS subscription can't be
  altered in place, so the replacement always gets a new identity. The same result is reachable
  without Ctrl+E via separate delete + add. `PatternDialog` has no buttons: Enter commits (a
  no-op while the pattern is empty), Esc cancels.

## Shared component: the list editor

Subscribe's pattern list and Publish's header list are the same shape — a presenter-formatted,
selectable list with New/Edit/Delete — so that shape lives once, in
`Components/ListEditorView<T>` (`list-editor` spec), rather than being duplicated:

- Renders only the list; row text comes from an injected `IValuePresenter<T>`, so different item
  types (a subscription pattern, a header pair) reuse the same editor without subclassing for
  display alone.
- **Ctrl+N**/**Ctrl+E** are abstract: every subclass must supply how "New"/"Edit" obtain a value
  (typically a modal appropriate to `T`). What happens *after* a value is obtained — `Add`,
  `Replace`, `Delete` — defaults to mutating the item collection directly, but each is
  independently overridable, so a subclass whose real source of truth lives elsewhere (e.g.
  `SubscriptionsView` redirecting into `SubscriptionRegistry`, which updates the item collection
  asynchronously on its own) can commit there instead without double-adding.
- **Ctrl+D** deletes the selection.
- All three bindings are attached to the editor as a whole, so they fire regardless of which
  child view currently has focus.

`PublishView`'s header editor predates this extraction and still hand-rolls the same New/Edit/
Delete mechanics inline rather than going through `ListEditorView<T>` — worth revisiting if the
duplication becomes a maintenance cost.

## Shortcut discovery

`IShortcutSource` lets a view opt in to advertising a curated subset of its key bindings (e.g.
`ListEditorView<T>`'s Ctrl+N/E/D) separately from bindings that exist purely for internal
navigation and would just be noise if surfaced (`keyboard-shortcut-discovery` spec).
`ShortcutAggregator` walks from the currently focused view up through its ancestors, collecting
every `IShortcutSource` it passes through, and `ShortcutTracker` recomputes that set on every
app-wide focus change.

**Not yet wired up:** the mechanism exists and is exercised by `ListEditorView<T>`, but
`MainWindow`'s `StatusBar` doesn't yet consume `ShortcutTracker`'s output — today's status bar
shortcuts (`Quit`, `Subscribe`, `Publish`, `Clear`, the Publish status message) are hand-assembled
in `MainWindow`, not driven by focus-based aggregation.

## Message templates (stretch goal, unbuilt)

Keep named, reusable message templates for the Publish tab, loaded from a JSON file and grouped
under arbitrary category names (e.g. `invoicing`, `bookings`):

```json
{
    "invoicing": {
        "get-invoice-by-id": {
            "subject": "invoices.get",
            "headers": { "tenant": "name" },
            "type": "json|text|hex|base64",
            "payload": { "id": -1 }
        }
    }
}
```

`type` governs how `payload` is interpreted before sending:

| `type` | `payload` is... | Sent as |
|---|---|---|
| `json` | a JSON value | UTF-8 rendering of that JSON value |
| `text` | a string | UTF-8 encoding of the string |
| `base64` | a base64 string | the decoded byte array |
| `hex` | a hex string | the decoded byte array |

No design work has started on where templates live in the UI (a picker inside Publish? a
separate tab?) or how loading interacts with the existing Subject/Headers/Payload fields.

## Padded edit-field framing (idea, unbuilt)

Wrap edit components (text fields, text areas) in a thin flat frame — not a full bordered box —
colored the same as the edit control's own background, so it reads as breathing-room padding
around the field rather than a separate bordered container competing with the app's real
bordered panels (`Window`/`FrameView`/`Dialog`, which use the sharper full box-drawing style, see
"UI conventions" in `CLAUDE.md`):

```
╷▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄
┃
╵▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀
```

Top rule uses lower-half-block glyphs (`▄`), bottom rule uses upper-half-block glyphs (`▀`) — each
renders as a thin sliver hugging the content row rather than a full blank padding row, so the
"frame" adds only a hairline of separation instead of a full extra row of chrome. No design work
has started on which components adopt this (just `PublishView`'s Subject/Payload fields? every
`TextField`/`TextView` app-wide?) or how it interacts with focus/invalid styling (e.g. Publish's
red-on-dark-gray invalid Subject field).

## Streams / Consumers / KV / OBJ

Reserved tabs (see table above); no design has started beyond the tab shortcuts being carved out
so they don't collide with what's already shipped.
