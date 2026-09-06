## Context

`doc/UI.md` reserves the Streams tab (Alt+3) but nothing exists for it yet. `doc/stream-tab-UI.md`
records the fuller design conversation and slices the tab into independently shippable pieces;
this change implements slice 1 only — a flat, read-only stream list with a periodically-refreshed
detail panel. Consumer drill-down, delete, and create/edit are explicitly out of scope here and
land in later slices against the same doc.

`NATS.Client.JetStream` 2.8.2 is already referenced in `lazynats.csproj` but unused. Its relevant
surface, confirmed against the package's shipped XML docs:

- `INatsJSContext.ListStreamsAsync(string? subject, CancellationToken)` → `IAsyncEnumerable<INatsJSStream>`,
  each wrapping a `NATS.Client.JetStream.Models.StreamInfo` (`.Info`).
- `StreamInfo.Config` (`StreamConfig`: `Name`, `Subjects`, `Retention`, `MaxMsgs`, `MaxBytes`,
  `MaxAge`, `NumReplicas`, ...) and `StreamInfo.State` (`StreamState`: `Messages`, `Bytes`,
  `FirstSeq`, `LastSeq`, `ConsumerCount`, ...) — together, everything `StreamDetails` needs.
- There's no dedicated "list names only" call; `ListStreamsAsync` already returns full
  `StreamInfo` per stream, so `StreamListView` doesn't need a second call to get names — but (see
  Decisions) it's still only invoked on load and on manual refresh, not on a timer.
- `GetStreamAsync(stream, request, ct)` fetches a single named stream's `StreamInfo` — this is
  what `StreamDetails`' timer-driven poll uses, not `ListStreamsAsync`.

## Goals / Non-Goals

**Goals:**
- Read-only list of JetStream stream names on the LHS, loaded once on tab entry and refreshed
  only on demand (Ctrl+R) — not on a timer, to avoid a jumpy/reordering list and unnecessary
  server load.
- RHS panel showing the highlighted stream's config + live state, refreshing on a timer while
  displayed (not only on selection change), per `doc/UI.md`'s stated requirement — this is the
  only thing in the tab that polls.
- Tab wired into the Alt+3 slot with title `3:Streams`, following the existing
  `tab-content-structure` (self-contained `StreamsTab`, `MainWindow` only registers it) and
  `tab-navigation` conventions.
- AOT/trim-safe: JetStream's typed models are POCOs (no reflection-heavy (de)serialization path
  beyond what `NATS.Client.JetStream` itself already does, which is presumed AOT-safe since it
  ships as a first-party package the project already depends on for the same reasons as
  `NATS.Client.Core`).

**Non-Goals:**
- Consumer listing/drill-down, Esc/Backspace navigation, breadcrumb (slice 2).
- Delete (slice 3), create/edit (slice 4+).
- Message browsing / the `<Messages>` virtual node (deferred indefinitely per
  `doc/stream-tab-UI.md`).
- Any mutation of stream state.

## Decisions

**List is load-once + manual refresh; only the highlighted stream's detail polls on a timer.**
Polling `ListStreamsAsync` on a timer was the original plan, but that repopulates/reorders the
whole `StreamListView` out from under the user every tick — jumpy while scrolling or reading, and
wasteful against the server (a full list fetch every few seconds regardless of whether anything
changed). Instead: `StreamListView` is populated once when the Streams tab is first entered, and
otherwise only re-fetched on an explicit user action (Ctrl+R). `StreamDetails` independently
polls `GetStreamAsync(name, ...)` on a timer, but only for the single currently-highlighted
stream — that's the one place `doc/UI.md` actually asks for live-without-interaction behavior
("RHS info ... refreshes periodically while displayed ... so it reflects live state"), and it
doesn't touch list order/contents at all, so it can't cause the list to jump.

**Manual list refresh: Ctrl+R.** Matches the "list can be polled on request" call — a single
keybinding, no modal, re-runs `ListStreamsAsync` and replaces the list contents. If the
previously-highlighted stream is still present in the refreshed list, keep it highlighted;
otherwise fall back to the first item (same selection-recovery spirit as `ListEditorView`'s
existing convention, independently implemented since this isn't a `ListEditorView` subclass).
Advertised via `IShortcutSource` on `StreamListView` so it shows up in the status bar like the
app's other discoverable shortcuts (`ShortcutAggregator`/`ShortcutTracker`).

**Detail poll interval: 3 seconds.** `doc/UI.md` leaves this "TBD... a few seconds". 3s matches
the existing live-feed's general "cheap against the server" intent without being so slow that an
externally-driven state change feels stale for long. Now cheap per-tick by construction — it's a
single-stream `GetStreamAsync`, not a full list fetch.

**Poll (and the highlighted-stream target) only while the tab is selected.**
`ManagementTabs`/`Terminal.Gui.TabView` already tracks the active tab; `StreamsTab` starts its
detail poll timer on `Application.Iteration`-driven visibility (mirroring how
`SubscriptionRegistry`'s tasks run regardless, but here the poll is UI-driven, not a durable
subscription) — start on `SelectedTabChanged`/first layout, stop on tab switch away or dispose.
Avoids polling JetStream from a tab nobody's looking at.

**No new DI-registered service; `StreamsTab` takes `INatsJSContext` directly.** `Services.cs`
already registers `NatsConnection` as a singleton; add a singleton `INatsJSContext` (constructed
via `connection.CreateJetStreamContext()`, per the package's usual entry point) alongside it in
`Program.cs`, and resolve it into `StreamsTab` the same way `SubscribeTab`/`PublishTab` resolve
their dependencies today. A wrapping service isn't justified yet — there's exactly one consumer
and one call (`ListStreamsAsync`); revisit if slice 2's consumer listing needs shared plumbing.

**List/detail split mirrors `SubscriptionsView`+`SubscribeTab`, not `ListEditorView`.**
Per `doc/stream-tab-UI.md`, `StreamListView` is a plain `ListView`-backed component (no
New/Edit/Delete machinery) using the same `IValuePresenter<T>`/`PresenterListDataSource` pieces
`ListEditorView` already uses for row formatting, so a future slice-3 Delete can still reuse that
formatting contract without inheriting the modal create/edit contract that doesn't apply yet.

**Empty state.** If `ListStreamsAsync` yields no streams (no JetStream configured, or none
created), `StreamListView` shows a non-interactive hint line, consistent with `ListEditorView`'s
existing empty-state convention (dim/focused styling) even though this isn't a `ListEditorView`
subclass — same visual language, independently implemented.

**Error handling.** If a poll tick throws (server unreachable, JetStream disabled), `StreamsTab`
surfaces the error inline (e.g. in place of `StreamDetails`' content, or a status message) and
keeps the previous tick's list rather than clearing it — avoids flashing an empty list on a
transient hiccup. Exact presentation is an implementation detail, not a spec-level requirement
for this slice.

## Risks / Trade-offs

- **[Risk]** The list can go stale relative to the server (a stream created/deleted externally
  won't appear/disappear until Ctrl+R). → **Mitigation**: this is the deliberate trade-off
  requested — avoids a jumpy, reordering list and cuts server load — and `doc/UI.md`'s periodic-
  refresh requirement was always specifically about the RHS info panel, not the list.
- **[Risk]** `INatsJSContext` registration in `Program.cs` couples startup to JetStream being
  available even for users who only use core NATS pub/sub. → **Mitigation**: creating a
  `NatsJSContext` wrapper is a local, side-effect-free call (it doesn't itself contact the
  server); the tab's own fetches are what surface "JetStream unavailable", not startup.
- **[Risk]** Ctrl+R refresh can invalidate the current highlight (stream deleted externally).
  → **Mitigation**: fall back to first-item selection, same as the existing selection-recovery
  convention elsewhere in the app; `StreamDetails` shows no stream's details when nothing's
  highlighted (already a stated requirement for the empty-list case).

## Open Questions

- Exact error-state visual treatment (inline message vs. status bar) — left to implementation,
  not spec-worthy for this slice.
- Whether `INatsJSContext` registration belongs in `Services.cs` or inline in `Program.cs`,
  matching whatever precedent `NatsConnection`'s own registration sets there.
