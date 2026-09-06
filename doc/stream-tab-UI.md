# Streams tab — design overview and slice plan

Status: exploration only, nothing implemented yet. `doc/UI.md` (lines 28-45) has the authoritative
baseline requirements (LHS list / RHS info, Enter drills stream → consumers, Esc/Backspace climbs
back up, RHS polls periodically, baseline ops are info + delete at both levels). This doc exists to
record the design conversation that refined that baseline into a slice plan, so slices 2..N don't
need to re-derive it from scratch.

## The metaphor

LHS list / RHS detail, in the spirit of Norton/Midnight Commander — but single-panel-with-preview
(closer to `ranger`/`nnn`'s preview pane) rather than NC's dual mirrored panels. RHS never becomes a
list itself; it's always "info about whatever's highlighted on the left."

## Streams have two child types, and that's friction

A stream's children aren't homogeneous the way a directory's files are: **consumers** (durable/
ephemeral cursors with delivery/ack state) and **messages** (the actual payloads, addressable by
seqno) are siblings, not parent/child of each other. A consumer doesn't own a subset of messages —
it's a cursor/ack-state object pointing into the stream's message log.

```
                    ┌─────────┐
                    │ Stream  │
                    └────┬────┘
              ┌──────────┴──────────┐
              ▼                     ▼
        ┌───────────┐        ┌────────────┐
        │ Consumers │        │  Messages  │
        │  (state)  │        │ (by seqno) │
        └───────────┘        └────────────┘
```

Resolution agreed on: a pinned virtual `<Messages>` entry sits in the same LHS list alongside real
consumers when drilled into a stream (same trick k9s uses for pseudo-resources). **Deferred**,
though — message browsing by seqno is its own big subject (windowing/paging a potentially
huge collection, no natural page-size, jump-to-seq UX). Not part of the slice plan below. When it
gets picked up, revisit this doc for the virtual-node idea before redesigning from scratch.

Consumers stay leaves for now (no drill-down from a consumer). A `<Pending>` virtual child under a
consumer, using the same pinned-virtual-entry trick, is a plausible future symmetric extension but
explicitly not scoped now.

## Component shape

Learned from `ListEditorView<T>` (presenter-driven formatting, keybinding-through-focus-chain,
empty-state hint — see `openspec/specs/list-editor/spec.md`) but **not reusing it directly**: its
whole shape is New/Edit/Delete via modal callbacks on a flat, homogeneous collection, and it has no
notion of Enter-to-descend/Esc-to-ascend. Streams needs specialist components that borrow the same
conventions rather than inherit the create/edit machinery.

```
StreamsTab                         ← registered with ManagementTabs (Tab-suffix per
 │                                    openspec/specs/tab-content-structure/spec.md)
 ├─ owns current level + breadcrumb/title (doc/UI.md requires the level be obvious,
 │  since both levels' LHS lists look the same shape)
 ├─ StreamListView   ─┐  same LHS slot,          load-once + manual refresh (Ctrl+R)
 │                     │  swapped based on level  — NOT polled on a timer
 └─ ConsumerListView  ─┘                          (same rule applies here)
        │ highlight drives, on the RHS:
        ├─ StreamDetails     (poll-refreshed, only for the highlighted stream)
        └─ ConsumerDetails   (poll-refreshed, only for the highlighted consumer)
```

`StreamListView`/`ConsumerListView` are not `Tab`-suffixed (inner pieces, per the naming
convention).

**Lists are load-once + Ctrl+R, never polled.** Originally the plan was to poll the whole list on
a timer so RHS detail could piggyback on the same fetch. Rejected: repopulating/reordering the
list every few seconds is jumpy while scrolling or reading, and wasteful against the server when
nothing changed. Instead each list view fetches once on entry and otherwise only on an explicit
Ctrl+R (re-fetch, preserve the current highlight if it's still present, else select the first
item). This applies to `ConsumerListView` too when slice 2 builds it, not just `StreamListView`.

**Only the RHS detail panel polls, and only for the single highlighted item** (`GetStreamAsync`/
the consumer equivalent for one name, not a full list fetch) — the thing `doc/UI.md` actually
asks to stay live "without the user having to re-select" is the detail panel, not the list.
Interval settled at 3 seconds (slice 1's `openspec/changes/add-streams-tab/design.md`). Detail
polling only runs while the Streams tab is the selected tab, and re-targets whenever the
highlight changes.

## Slice plan

Smallest-possible-steps, each independently shippable:

- **Slice 1** (in progress): `StreamsTab` + `StreamListView` + `StreamDetails` only. Flat list of
  streams (load-once + Ctrl+R), RHS shows info for the highlighted stream (polls every 3s). **No
  consumers, no drill-down, no Esc/Backspace handling** — nothing to navigate back from yet.
  Smallest thing that earns the tab its place in the Alt+3 rotation.
- **Slice 2**: add `ConsumerListView` + `ConsumerDetails` + the Enter-to-descend/Esc-to-ascend
  navigation and breadcrumb. This is where the two-level state machine actually shows up — the state
  machine sketch above (streams ⟷ consumers-of-X) applies here.
- **Slice 3**: Delete, both levels. Single keybinding + confirmation, no dialog needed.
- **Slice 4+**: New/Edit, once stream-config and consumer-config editing dialogs are designed
  (stream config has real surface — subjects, retention policy, max age/bytes/msgs, replicas — that's
  its own design conversation, not a quick add).
- **Not scoped / deferred indefinitely until picked up deliberately**: message browsing (the
  `<Messages>` virtual node), consumer-level `<Pending>` drill-down.

## Open questions not yet resolved

- What fields `StreamDetails`/`ConsumerDetails` actually render (`StreamDetails`'s fields are
  pinned down in slice 1's design.md; `ConsumerDetails`' aren't yet — slice 2).
- Whether `<Messages>`/`<Pending>` virtual-node navigation, when eventually built, is windowed by
  PgUp/PgDn, a jump-to-seq prompt, or pinned-at-newest by default.
