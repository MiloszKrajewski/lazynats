## Context

Slice 1 (`add-streams-tab`, archived) shipped `StreamsTab` + `StreamListView` + `StreamDetails`:
a flat stream list (load-once + `Ctrl+R`) with a poll-refreshed detail panel, gated on tab focus
via `StreamsTab.OnHasFocusChanged` and a single `App.AddTimeout`-based timer. `doc/stream-tab-UI.md`
scoped slice 2 as "add `ConsumerListView` + `ConsumerDetails` + the Enter-to-descend/Esc-to-ascend
navigation and breadcrumb" and left its open questions unresolved. This document resolves them.

`System.Reactive` (v6.1.0) is already a `PackageReference` in `lazynats.csproj` and already has an
AOT probe (`src/lazynats.AotProbe/Probes/RxProbe.cs`, a bare `Subject<string>` round-trip) but no
real consumer in the app yet. This change is its first real usage.

## Goals / Non-Goals

**Goals:**
- Enter/Esc navigation between stream level and consumer level within `StreamsTab`.
- A poll-refreshed `ConsumerDetails` panel, matching `StreamDetails`' existing UX contract.
- A reusable, App-thread-safe async-polling pattern (`Core/AsyncExtensions.cs`) that eliminates the
  ad-hoc `App?.Invoke(...)` marshaling duplicated in `LiveUpdatesView`, `PublishTab`, `StreamsTab`.

**Non-Goals:**
- `<Messages>` virtual-node browsing, consumer-level `<Pending>` drill-down (deferred per
  `doc/stream-tab-UI.md`).
- Migrating the live feed pipeline (`SubscriptionRegistry`/`FeedReaderLoop`/`MessageDeduplicator`)
  onto `Core/AsyncExtensions.cs` — those are deliberately tuned (windowed dedup, `LiveLogDataSource`'s
  `MaxItemLength = 0` trick) and out of scope here; `AsyncExtensions` is built generic enough for
  that to be a later, separate change.
- Create/Edit/Delete at either level (still read-only, per `nats-streams`' existing requirement).

## Decisions

### 1. Duplicate `ConsumerListView`/`ConsumerDetails` rather than extract a shared base

`ConsumerListView`/`ConsumerDetails` are structurally identical to `StreamListView`/`StreamDetails`
(load-once-per-entry list + poll-refreshed detail), but are written as separate, near-mirror
sibling files rather than factored into a generic `DrillLevelView<T>`. Rationale: this is only the
2nd occurrence of the shape — extracting now would be a speculative abstraction designed against
two data points. Both pairs are kept deliberately similar (same method names/shapes) specifically
so a 3rd occurrence, if it ever arrives, makes extraction a mechanical diff rather than a redesign.

**Alternative considered**: generic `DrillLevelView<T>` parameterized over fetch-list/fetch-one/
presenter callbacks. Rejected for now as premature — no 3rd instance exists to validate the right
generalization boundary (e.g. how it'd need to thread "current stream name" for the consumer level
but not the stream level).

### 2. Each `*Details` pane owns its poll timer; `StreamsTab` doesn't centralize polling

`StreamDetails`/`ConsumerDetails` each own an always-running `Observable.Interval(3s)`-based
pipeline rather than `StreamsTab` owning one `App.AddTimeout` and branching on current level (as a
naive extension of slice 1's code would do). The timer is created once and never stopped/restarted;
an `_active` bool field (set via `SetActive(bool)`, called by `StreamsTab` whenever tab focus or
current level changes) gates whether the pipeline's `Where(_ => _active)` step lets a tick through
to the actual NATS call.

`StreamsTab` calls `SetActive(true)` on whichever pane is current, only while the tab itself has
focus, and `SetActive(false)` on the other pane / on tab focus loss. `_active` is a plain field
closed over by `Where`, not a `BehaviorSubject<bool>` — from `StreamsTab`'s side the call is
`SetActive(bool)` either way, so this is purely an internal implementation choice with no external
API difference.

**Alternative considered**: centralize in `StreamsTab` with a level-aware `PollTarget` union
branching between `GetStreamAsync`/`GetConsumerAsync`. Rejected because it doesn't extend the
"keep the two levels as identical siblings" decision above — polling logic would live in a 3rd
place (`StreamsTab`) rather than traveling with each pane.

**Alternative considered**: gate via `View.Visible`/`HasFocusChanged`. Rejected because both
`*Details` panes are deliberately `CanFocus = false` ("never focusable, never edits anything"),
so they can't observe focus-based visibility themselves, and toggling `Visible` between the two
panes only captures "which level is shown," not "is the Streams tab itself the selected tab" —
inferring the second condition from view state would need walking the `SuperView` chain, which is
more fragile than `StreamsTab` (which already computes both conditions) just telling the pane
directly.

### 3. Poll pipeline shape: `Interval` → `Where(active)` → `SelectAsync` → `Where(not null)` → `ObserveOnApp` → `Subscribe`

```
Observable.Interval(TimeSpan.FromSeconds(3))
    .Where(_ => _active)
    .SelectAsync(_ => FetchAsync(_target))   // catches internally, returns null on failure
    .Where(info => info is not null)
    .ObserveOnApp(app)
    .Subscribe(Show);
```

- `Where(_ => _active)` before the fetch means an inactive pane issues **zero** NATS calls — not
  "fetches and discards the result," an actual no-op. This directly satisfies "don't make API
  calls for something the user isn't looking at" without needing the timer itself to start/stop.
- The exception handling lives **inside** `FetchAsync` (try/catch around the NATS call, reporting
  and returning `null` on failure) rather than as a downstream `Catch` operator on the pipeline.
  A downstream `Observable.Catch` was tried first and rejected: `Catch` doesn't resubscribe to its
  source on error, it permanently switches the whole chain over to its handler observable — since
  the handler here (`Observable.Empty<Info>()`) completes immediately, that switch terminates the
  *entire* pipeline, including the `Interval` timer underneath, after the very first failure.
  Verified empirically (a minimal `SelectAsync`/`Catch` repro against `System.Reactive` 6.1.0):
  after one injected exception, no further ticks were ever delivered. Catching inside `FetchAsync`
  and filtering the resulting `null` with `Where` means only that one tick's result is dropped —
  the `Interval` timer and the rest of the chain are untouched, so polling resumes normally on the
  next tick. This is what makes even a bug in the `_active` gating (a stray call while hidden)
  harmless: worst case is a status-bar message for a pane the user isn't looking at, not a
  silently-dead poller.
- `SelectAsync` (new, `Core/AsyncExtensions.cs`) = `Select` into `Observable.FromAsync` + `Concat`,
  **not** `SelectMany`/`Merge`. `Concat` means at most one fetch is ever in flight per pipeline —
  if a tick fires while the previous fetch is still running, it queues rather than overlapping.
  This retires the manual `if (_pollTarget == name)` staleness guard slice 1 needed to defend
  against out-of-order concurrent responses (`StreamsTab.cs:110-112` today) — `Concat` makes
  that guard unnecessary by construction, since responses can never arrive out of submission order.

  **Alternative considered**: `SelectMany`/`Merge` (concurrent). Rejected — reintroduces the
  out-of-order-response problem `Concat` avoids for free.

  **Alternative considered**: `Select(...).Switch()` (cancel-previous, always-latest). Rejected for
  now — only meaningfully differs from `Concat` when fetches chronically exceed the 3s interval
  (unlikely for a JetStream `INFO` call), and would need `CancellationToken` plumbing through
  `SelectAsync` to actually cancel server-side work rather than just abandoning the subscription.
  Left as a possible future overload if `Concat`'s queuing-under-sustained-latency turns out to
  matter in practice.
- `ObserveOnApp` (new, `Core/AsyncExtensions.cs`) marshals each notification through
  `Application.Invoke(Action)` before it reaches the subscriber — Terminal.Gui has no
  `SynchronizationContext` to hook into `ObserveOn`, so this is a small custom operator wrapping
  the observer.

### 4. `Core/AsyncExtensions.cs` — new top-level folder, not `Components/`

`SelectAsync`/`ObserveOnApp` operate on `IObservable<T>`/`Application`, not UI views — they don't
belong in `Components/` (currently `EditFrame`, `ListEditorView<T>`, `IValuePresenter<T>`, all
UI-specific). A new `Core/` folder holds general-purpose, non-UI-specific extensions, deliberately
generic so the live feed pipeline can adopt them later without redesign (see Non-Goals).

### 5. Consumer list refetch policy: always fresh on descend, never on ascend

Every Enter-to-descend triggers a fresh `ListConsumersAsync` call — no per-stream caching, even for
re-descending into the same stream just left. Simpler than cache invalidation, and consumer list
fetches are cheap. Esc/Backspace-to-ascend does **not** trigger a stream-list refetch — it just
re-shows `StreamListView`'s already-loaded state (same instance, kept alive across the round-trip
since `EditFrame` wraps a fixed child and both list/detail pairs are toggled via `Visible` rather
than reconstructed). This preserves `nats-streams`' existing "Manual List Refresh" requirement
(list only changes via explicit `Ctrl+R`) for the top-level list — auto-refetching on ascend would
silently violate it.

### 6. Enter/Esc mechanism

- **Enter → descend**: `StreamListView` subscribes to its wrapped `ListView`'s `Accepted` event.
  Terminal.Gui's `ListView` already binds Enter → `Command.Accept` by default (confirmed via
  context7 docs), so no custom `KeyBindings` entry is needed — same idiom as any other
  `Accepting`/`Accepted` consumer in this codebase (per `doc/terminal-gui-howto.md`'s gotcha #2).
- **Esc/Backspace → ascend**: no default binding exists for this on `ListView`, so
  `ConsumerListView` registers a custom `AddCommand`/`KeyBindings.Add` for both keys, raised as an
  `AscendRequested` event — same shape as `StreamListView`'s existing `Ctrl+R` → `Command.Refresh`.

### 7. Two `EditFrame`-wrapped pairs, toggled via `Visible`

`EditFrame` wraps a single fixed child assigned at construction (no swap API) — so `StreamsTab`
holds two `EditFrame`s (one around `StreamListView`, one around `ConsumerListView`) at the same
`X`/`Y`/`Width`/`Height`, and two `*Details` panes, with only the current level's pair `Visible`.
Both list-view instances stay alive across descend/ascend, which is what makes decision 5's
"ascend never refetches" free (the old highlight/scroll position is just still there).

### 8. Breadcrumb

The existing `"Streams"`/`"Details"` `Label`s in `StreamsTab` become dynamic text on descend
(`"Consumers of <name>"` / `"Consumer Details"`) rather than adding new UI chrome — satisfies
`doc/UI.md`'s "current level should be obvious" requirement with no new layout.

### 9. `ConsumerDetails` fields

Mirrors `StreamDetails`' two-block layout (config, blank separator row, state):

```
Name              ConsumerInfo.Name
Filter Subject    union of ConsumerConfig.FilterSubjects and .FilterSubject, deduped, joined
Ack Policy        ConsumerConfig.AckPolicy
Deliver Policy    ConsumerConfig.DeliverPolicy
Max Deliver       ConsumerConfig.MaxDeliver
Max Ack Pending   ConsumerConfig.MaxAckPending

Delivered         ConsumerInfo.Delivered.StreamSeq
Ack Floor         ConsumerInfo.AckFloor.StreamSeq
Ack Pending       ConsumerInfo.NumAckPending
Redelivered       ConsumerInfo.NumRedelivered
Waiting           ConsumerInfo.NumWaiting
Pending           ConsumerInfo.NumPending
```

## Risks / Trade-offs

- **[Risk]** `Concat`-based `SelectAsync` queues rather than drops ticks under sustained latency
  (fetch consistently slower than 3s) → **Mitigation**: unlikely for a single JetStream `INFO` call;
  revisit with a `Switch`-based variant if it's ever observed in practice (see decision 3).
- **[Risk]** First real use of `System.Reactive` in the app — a new idiom for future contributors to
  learn, on top of the existing hand-rolled `Channel`-based feed pipeline → **Mitigation**: scoped
  tightly to `Core/AsyncExtensions.cs` + the two `*Details` panes; the feed pipeline is explicitly
  not touched by this change (see Non-Goals).
- **[Risk]** `_active` bool gating is only as correct as `StreamsTab` remembering to call
  `SetActive` on every relevant transition (tab focus change, level change) → **Mitigation**: even
  if missed, `Catch` prevents a crash; worst case is a stale/wasted poll, not app failure.

## Migration Plan

Additive change — no existing behavior removed, no data migration. `StreamDetails` is refactored
onto the new poll pipeline in place (its external `Show(StreamInfo?)` contract is unchanged).
Ships as one change; no flag or rollback mechanism needed beyond normal git revert.
